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

        private void Awake() => AllDesks.Add(this);

        private async void Start()
        {
            await UniTask.Delay(200);

            if (_activeMonitor == null)
            {
                SpawnInitialMonitor();
            }

            RestoreAssignedWorker();
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
                    _activeMonitor.Initialize(this.workplaceId);
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

            _activeMonitor.Initialize(this.workplaceId);
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
            // 1. Очистка старого рабочего
            if (_currentWorker != null)
            {
                _currentWorker.isAssigned = false;
                _currentWorker.assignedWorkplaceId = null; // Очищаем ID стола у рабочего
            }

            if (_spawnedNpcVisual != null) Destroy(_spawnedNpcVisual);

            _currentWorker = worker;

            // 2. Назначение нового
            if (_currentWorker != null)
            {
                _currentWorker.isAssigned = true;
                _currentWorker.assignedWorkplaceId = this.workplaceId; // ПРИВЯЗЫВАЕМ ID СТОЛА

                ApplyWorkerVisuals(_currentWorker);

                // Сохраняем игру, так как данные рабочего изменились
                GameDataManager.Instance.SaveGame(ShiftManager.Instance.currentDay);
            }
            else
            {
                StopWork();
                GameDataManager.Instance.SaveGame(ShiftManager.Instance.currentDay);
            }
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
                    if (room == null) Debug.LogWarning($"[Workplace] {gameObject.name} не находится внутри объекта с RoomManager!");
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
                catch (System.OperationCanceledException) { break; }

                if (_currentWorker == null || _currentWorker.status != WorkerStatus.Working) continue;

                // 4. НАЧИСЛЕНИЕ (Деньги и Прогресс)
                int profit = Mathf.RoundToInt(_currentWorker.workPower * 2.5f);

                if (GameDataManager.Instance != null)
                    GameDataManager.Instance.ChangeMoney(profit);

                if (ShiftManager.Instance != null)
                    ShiftManager.Instance.AddProgress(profit, transform.position);
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