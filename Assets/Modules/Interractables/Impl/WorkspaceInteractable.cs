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

        public void AssignWorker(WorkerInstance worker)
        {
            var previousDesk = FindDeskByWorker(worker);
            if (previousDesk != null && previousDesk != this)
            {
                previousDesk.AssignWorker(null);
            }

            if (_currentWorker != null) _currentWorker.isAssigned = false;
            if (_spawnedNpcVisual != null) Destroy(_spawnedNpcVisual);

            _currentWorker = worker;

            if (_currentWorker != null)
            {
                _currentWorker.isAssigned = true;

                GameObject prefab = GameDataManager.Instance.GetWorkerPrefab(_currentWorker.templateId);
                if (prefab != null && npcSpawnPoint != null)
                    _spawnedNpcVisual = Instantiate(prefab, npcSpawnPoint.position, npcSpawnPoint.rotation, transform);

                StartWorkLoop().Forget();
            }
            else
            {
                StopWork();
            }
        }

        private async UniTaskVoid StartWorkLoop()
        {
            StopWork(); // На всякий случай
            _workCts = new CancellationTokenSource();

            try
            {
                while (_currentWorker != null && !_workCts.IsCancellationRequested)
                {
                    if (_roomManager == null)
                    {
                        _roomManager = GetComponentInParent<RoomManager>();
                    }

                    // Если комната выключена или рабочий отдыхает — ждем и ничего не делаем
                    if (_roomManager != null && (!_roomManager.isRoomActive || _currentWorker.isResting))
                    {
                        await UniTask.Yield(); // Просто ждем следующий кадр
                        continue;
                    }

                    int intervalMs = Mathf.Max(1000, 5000 - (_currentWorker.patience * 300));

                    await UniTask.Delay(intervalMs, cancellationToken: _workCts.Token);

                    // Эффективность работы (сколько денег приносит за один раз)
                    int profit = (int)(_currentWorker.workPower * 2.5f);

                    // Добавляем деньги через наш менеджер
                    GameDataManager.Instance.ChangeMoney(profit);
                }
            }
            catch (System.OperationCanceledException) { }
        }

        private void StopWork()
        {
            _workCts?.Cancel();
            _workCts?.Dispose();
            _workCts = null;
        }

    }
}