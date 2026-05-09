using Assets.Modules.Interractables;
using Assets.Modules.NPC;
using Assets.Modules.Save;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Assets.Modules.Interractables.Impl
{
    public class WorkplaceInteractable : MonoBehaviour, IInteractable
    {
        public static List<WorkplaceInteractable> AllDesks = new List<WorkplaceInteractable>();

        [Header("Equipment")]
        [SerializeField] private Transform monitorMountPoint;
        [SerializeField] private MonitorPhysical monitorPrefab;
        private MonitorPhysical _activeMonitor;
        public bool hasMonitor { get; private set; }

        [Header("Save Settings")]
        [SerializeField] public string workplaceId;

        [Header("Settings")]
        [SerializeField] private string promptEmpty = "Назначить работника";
        [SerializeField] private string promptOccupied = "Управление рабочим местом";
        [SerializeField] private Transform pivot;
        [SerializeField] private Transform npcSpawnPoint;

        [Header("References")]
        [SerializeField] private WorkplaceUIController uiController;

        private WorkerInstance _currentWorker;
        private GameObject _spawnedNpcVisual;
        private CancellationTokenSource _workCts;

        // Реализация интерфейса
        public string InteractionPrompt => _currentWorker == null ? promptEmpty : $"{promptOccupied} ({_currentWorker.name})";
        public Transform InteractionPivot => pivot;
        public InteractionType InteractionType => InteractionType.Click;
        public float HoldDuration => 0;
        public bool HasWorker => _currentWorker != null;
        public WorkerInstance Worker => _currentWorker;

        private RoomManager _roomManager;

        private Outline _outline;
        private bool _isQuestTarget = false;


        private void Awake() => AllDesks.Add(this);


        public void OnHoverEnter()
        {
            if (_isQuestTarget) return; // Если мы цель, не меняем настройки
            if (_outline != null) _outline.enabled = true;
        }

        public void OnHoverExit()
        {
            if (_isQuestTarget) return; // Если мы цель, не выключаем!
            if (_outline != null) _outline.enabled = false;
        }

        public void SetQuestHighlight(bool state)
        {
            if (_outline == null) return;

            _isQuestTarget = state;

            if (state)
            {
                _outline.enabled = true;
                _outline.OutlineMode = Outline.Mode.OutlineAll; // ВИДНО СКВОЗЬ СТЕНЫ
                _outline.OutlineColor = Color.red;
                _outline.OutlineWidth = 5f; // Жирная линия, чтобы было видно издалека
            }
            else
            {
                // Сбрасываем в обычный режим
                _outline.OutlineMode = Outline.Mode.OutlineVisible;
                _outline.OutlineColor = Color.white;
                _outline.OutlineWidth = 5f;
                _outline.enabled = false;
            }
        }

        private async void Start()
        {
            await UniTask.Delay(200);

            // Кэшируем компонент один раз при старте
            _outline = GetComponent<Outline>();

            // На всякий случай гарантируем, что он выключен
            if (_outline != null) _outline.enabled = false;

            if (_activeMonitor == null)
            {
                SpawnInitialMonitor();
            }

            RestoreAssignedWorker();
        }

        public void OnMonitorManualPickUp()
        {
            hasMonitor = false;
            _activeMonitor = null;
        }

        public void SetTargetHighlight(bool state)
        {
            if (_outline == null) _outline = GetComponent<Outline>();
            if (_outline == null) return;

            if (state)
            {
                _outline.enabled = true;
                _outline.OutlineMode = Outline.Mode.OutlineAll;
                _outline.OutlineColor = Color.red;
                _outline.OutlineWidth = 5f;
            }
            else
            {
                _outline.OutlineMode = Outline.Mode.OutlineVisible;
                _outline.OutlineColor = Color.yellow;
                _outline.OutlineWidth = 3f;
                _outline.enabled = false;
            }
        }

        private void SpawnInitialMonitor()
        {
            // Если монитор уже стоит (например, мы его установили руками или он выжил после перезагрузки)
            if (_activeMonitor != null) return;

            if (monitorPrefab != null && monitorMountPoint != null)
            {
                // Создаем монитор
                GameObject obj = Instantiate(monitorPrefab.gameObject, monitorMountPoint.position, monitorMountPoint.rotation, monitorMountPoint);
                _activeMonitor = obj.GetComponent<MonitorPhysical>();

                if (_activeMonitor != null)
                {
                    _activeMonitor.Initialize(this.workplaceId, this);
                    _activeMonitor.SetPhysics(false); // Выключаем физику на старте
                    hasMonitor = true;
                }
            }
            else
            {
                Debug.LogError($"[Workplace {workplaceId}] Не назначен префаб монитора или точка крепления!");
            }
        }

        public void KickMonitor()
        {
            if (!hasMonitor || _activeMonitor == null) return;

            hasMonitor = false;
            _activeMonitor.GetKicked();

            GameDataManager.Instance.SaveGame(ShiftManager.Instance.currentDay);
        }

        public void InstallMonitor(MonitorPhysical monitor)
        {
            _activeMonitor = monitor;
            _activeMonitor.transform.SetParent(monitorMountPoint);
            _activeMonitor.transform.localPosition = Vector3.zero;
            _activeMonitor.transform.localRotation = Quaternion.identity;

            _activeMonitor.Initialize(this.workplaceId, this);
            hasMonitor = true;
        }

        private void RestoreAssignedWorker()
        {
            if (GameDataManager.Instance == null) return;

            // Ищем в списке моих рабочих того, у кого assignedWorkplaceId совпадает с ID этого стола
            var savedWorker = GameDataManager.Instance.myWorkers.Find(w => w.assignedWorkplaceId == this.workplaceId);

            if (savedWorker != null)
            {
                // Назначаем его БЕЗ сохранения (чтобы не зациклить), просто визуально
                ApplyWorkerVisuals(savedWorker);
            }
        }

        public void AssignWorker(WorkerInstance worker)
        {
            // 1. Если мы назначаем реального человека (не null)
            if (worker != null)
            {
                // Ищем по всем столам: если этот работник где-то сидит — выгоняем его оттуда
                // Используем instanceId для уникальной идентификации
                foreach (var desk in AllDesks)
                {
                    if (desk != this && desk.Worker != null && desk.Worker.instanceId == worker.instanceId)
                    {
                        desk.ClearDeskInternal(); // Очищаем старый стол
                    }
                }
            }

            // 2. Очищаем ТЕКУЩИЙ стол перед тем как посадить нового
            ClearDeskInternal();

            // 3. Назначаем нового рабочего
            _currentWorker = worker;

            if (_currentWorker != null)
            {
                _currentWorker.isAssigned = true;
                _currentWorker.assignedWorkplaceId = this.workplaceId;

                ApplyWorkerVisuals(_currentWorker);
            }

            // Сохраняем один раз в конце всей операции
            if (GameDataManager.Instance != null)
                GameDataManager.Instance.SaveGame(ShiftManager.Instance.currentDay);
        }

        // Вспомогательный метод для ПОЛНОЙ очистки стола (логика + визуал)
        private void ClearDeskInternal()
        {
            if (_currentWorker != null)
            {
                _currentWorker.isAssigned = false;
                _currentWorker.assignedWorkplaceId = null;
            }

            _currentWorker = null;

            // Убиваем 3D модель
            if (_spawnedNpcVisual != null)
            {
                Destroy(_spawnedNpcVisual);
                _spawnedNpcVisual = null;
            }

            StopWork();
        }

        // Вынес визуальную часть в отдельный метод, чтобы вызывать его и при загрузке
        private void ApplyWorkerVisuals(WorkerInstance worker)
        {
            _currentWorker = worker;

            GameObject prefab = GameDataManager.Instance.GetWorkerPrefab(worker.templateId);
            if (prefab != null && npcSpawnPoint != null)
            {
                _spawnedNpcVisual = Instantiate(prefab, npcSpawnPoint.position, npcSpawnPoint.rotation, transform);

                if (_spawnedNpcVisual.TryGetComponent<WorkerPhysical>(out var physical))
                {
                    physical.Init(_currentWorker);
                }
            }

            StartWorkLoop().Forget();
        }

        private void OnDestroy()
        {
            StopWork();
            AllDesks.Remove(this);
        }

        public void Interact(GameObject interactor)
        {
            uiController.Open(this, interactor);
        }

        public static WorkplaceInteractable FindDeskByWorker(WorkerInstance worker)
        {
            if (worker == null) return null;
            return AllDesks.Find(d => d.Worker != null && d.Worker.instanceId == worker.instanceId);
        }

        private async UniTaskVoid StartWorkLoop()
        {
            StopWork();
            _workCts = new CancellationTokenSource();

            // Кэшируем комнату один раз при старте цикла
            var room = GetComponentInParent<RoomManager>();

            while (_currentWorker != null && !_workCts.IsCancellationRequested)
            {
                // 1. ПРОВЕРКА: Есть ли менеджеры?
                if (ShiftManager.Instance == null || room == null)
                {
                    if (room == null)
                        Debug.LogWarning($"[Workplace] {gameObject.name} не находится внутри объекта с RoomManager!");
                    await UniTask.Delay(500, cancellationToken: _workCts.Token);
                    continue;
                }

                // 2. ПРОВЕРКА: Активна ли смена, комната и не отдыхает ли рабочий?
                if (!ShiftManager.Instance.IsShiftActive || !room.isRoomActive || _currentWorker.isResting)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, _workCts.Token);
                    continue;
                }

                // 3. ЛОГИКА СОСТОЯНИЙ (Сон, Гнев, Скука)
                // Сон
                if (_currentWorker.currentSleepiness >= 100)
                {
                    _currentWorker.status = WorkerStatus.Sleeping;
                    _currentWorker.currentSleepiness -= 5 * Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, _workCts.Token);
                    continue;
                }

                // Гнев
                if (_currentWorker.currentAnger >= 100)
                {
                    _currentWorker.status = WorkerStatus.Angry;
                    await UniTask.Delay(2000, cancellationToken: _workCts.Token);
                    _currentWorker.currentAnger -= 20;
                    continue;
                }

                if (!room.isOpened || !room.isRoomActive || !hasMonitor)
                {
                    if (!hasMonitor && _currentWorker != null) _currentWorker.status = WorkerStatus.NoEquipment;
                    await UniTask.Yield(PlayerLoopTiming.Update, _workCts.Token);
                    continue;
                }

                // Если всё в порядке - работаем
                _currentWorker.status = WorkerStatus.Working;

                // Увеличиваем усталость со временем
                _currentWorker.currentSleepiness += 1f * Time.deltaTime;
                _currentWorker.currentRestlessness += 0.8f * Time.deltaTime;

                // ИНТЕРВАЛ РАБОТЫ
                int intervalMs = Mathf.Max(1000, 5000 - (_currentWorker.patience * 300));

                try
                {
                    await UniTask.Delay(intervalMs, cancellationToken: _workCts.Token);
                }
                catch (System.OperationCanceledException)
                {
                    break;
                }

                if (_currentWorker == null || _currentWorker.status != WorkerStatus.Working) continue;

                // 4. НАЧИСЛЕНИЕ (Деньги и Прогресс)
                int profit = Mathf.RoundToInt(_currentWorker.workPower * 2.5f);

                // 1. Начисляем деньги
                if (GameDataManager.Instance != null)
                    GameDataManager.Instance.ChangeMoney(profit);

                if (ShiftManager.Instance != null)
                {
                    int goldMineBonus = 30 * GameDataManager.Instance.playerPerks.goldMineLevel;
                    ShiftManager.Instance.AddProgress(profit + goldMineBonus, transform.position);
                }
            }
        }

        private void StopWork()
        {
            _workCts?.Cancel();
            _workCts?.Dispose();
            _workCts = null;
        }

    }
}