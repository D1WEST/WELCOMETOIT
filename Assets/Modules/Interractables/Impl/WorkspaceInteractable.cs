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
        [Header("Save Settings")]
        [SerializeField] private string workplaceId;

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
            await UniTask.Delay(100);
            RestoreAssignedWorker();
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

            while (_currentWorker != null && !_workCts.IsCancellationRequested)
            {
                if (ShiftManager.Instance == null)
                {
                    await UniTask.Delay(500, cancellationToken: _workCts.Token);
                    continue;
                }

                if (!ShiftManager.Instance.IsShiftActive || _currentWorker.isResting)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, _workCts.Token);
                    continue;
                }

                int intervalMs = Mathf.Max(1000, 5000 - (_currentWorker.patience * 300));

                try
                {
                    await UniTask.Delay(intervalMs, cancellationToken: _workCts.Token);
                }
                catch (System.OperationCanceledException) { break; }

                if (_currentWorker == null) break;

                int power = _currentWorker.workPower;

                GameDataManager.Instance.ChangeMoney(power * 2);
                ShiftManager.Instance.AddProgress(power, transform.position);
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