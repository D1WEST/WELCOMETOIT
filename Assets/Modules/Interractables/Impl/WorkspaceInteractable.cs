using Assets.Modules.Interractables;
using Assets.Modules.NPC;
using Assets.Modules.Save;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Assets.Modules.Interractables.Impl
{
    public class WorkplaceInteractable : MonoBehaviour, IInteractable
    {
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

        public void Interact(GameObject interactor)
        {
            // Открываем меню выбора, передавая этот стол как цель
            uiController.Open(this, interactor);
        }

        public void AssignWorker(WorkerInstance worker)
        {
            // 1. Убираем старого рабочего и его модель
            if (_currentWorker != null)
            {
                _currentWorker.isAssigned = false;
            }

            if (_spawnedNpcVisual != null)
            {
                Destroy(_spawnedNpcVisual);
            }

            _currentWorker = worker;

            // 2. Если назначили нового
            if (_currentWorker != null)
            {
                _currentWorker.isAssigned = true;

                // СПАВН 3D МОДЕЛИ
                GameObject prefab = GameDataManager.Instance.GetWorkerPrefab(_currentWorker.templateId);
                if (prefab != null && npcSpawnPoint != null)
                {
                    _spawnedNpcVisual = Instantiate(prefab, npcSpawnPoint.position, npcSpawnPoint.rotation, transform);
                }

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

            Debug.Log($"[Workplace] {_currentWorker.name} приступил к работе.");

            try
            {
                while (_currentWorker != null && !_workCts.IsCancellationRequested)
                {
                    // Время тика работы зависит от концентрации (patience)
                    // Чем выше концентрация, тем чаще он выдает результат
                    int intervalMs = Mathf.Max(1000, 5000 - (_currentWorker.patience * 300));

                    await UniTask.Delay(intervalMs, cancellationToken: _workCts.Token);

                    // Эффективность работы (сколько денег приносит за один раз)
                    int profit = (int)(_currentWorker.workPower * 2.5f);

                    // Добавляем деньги через наш менеджер
                    GameDataManager.Instance.ChangeMoney(profit);

                    Debug.Log($"[Workplace] {_currentWorker.name} заработал ${profit}");
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

        private void OnDestroy() => StopWork();
    }
}