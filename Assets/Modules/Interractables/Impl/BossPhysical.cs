using System.Threading;
using Assets.Modules.Perks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.Modules.Interractables.Impl
{
    public class BossPhysical : MonoBehaviour, IInteractable
    {
        public static BossPhysical Instance { get; private set; }

        [SerializeField] private PerkShopController shopController;
        private Animator _animator;
        private CancellationTokenSource _ambientCts;

        public string InteractionPrompt => "Поговорить с Боссом [E]";
        public Transform InteractionPivot => transform;
        public InteractionType InteractionType => InteractionType.Click;
        public float HoldDuration => 0;

        private void Awake()
        {
            Instance = this;
            _animator = GetComponentInChildren<Animator>();
            _ambientCts = new CancellationTokenSource();
        }

        private void Start()
        {
            StartAmbientLogic(_ambientCts.Token).Forget();
        }

        // Случайный бубнёж и пение (Индексы 0-2)
        private async UniTaskVoid StartAmbientLogic(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // Ждем от 15 до 30 секунд между звуками
                await UniTask.Delay(Random.Range(15000, 30000), cancellationToken: token);

                if (!ShiftManager.Instance.IsShiftActive) continue;

                // Выбираем случайный звук из 0, 1, 2 (Мужик поет)
                int randomTrack = Random.Range(0, 3);
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("Boss").ByIndex(randomTrack).WithVolume(0.3f).At(this.transform)
                ).Forget();
            }
        }

        public void SetState(int state)
        {
            if (_animator != null) _animator.SetInteger("State", state);
        }

        public void Interact(GameObject interactor)
        {
            // Звук клика (Индекс 6)
            AudioManager.Instance.PlayAudio(AudioQuery.ByKey("Boss").ByIndex(6).RandomSound()).Forget();
            shopController.Open(interactor);
        }

        private void OnDestroy() => _ambientCts?.Cancel();
    }
}