using Assets.Modules.Perks;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

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
        private Outline _outline;

        private void Awake()
        {
            Instance = this;
            _animator = GetComponentInChildren<Animator>();
            _ambientCts = new CancellationTokenSource();
        }



        public void OnHoverEnter()
        {
            if (_outline != null) _outline.enabled = true;
        }

        public void OnHoverExit()
        {
            if (_outline != null) _outline.enabled = false;
        }
        private void Start()
        {
            // Кэшируем компонент один раз при старте
            _outline = GetComponent<Outline>();

            // На всякий случай гарантируем, что он выключен
            if (_outline != null) _outline.enabled = false;

            StartAmbientLogic(_ambientCts.Token).Forget();
        }

        // Случайный бубнёж и пение (Индексы 0-2)
        private async UniTaskVoid StartAmbientLogic(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(Random.Range(15000, 30000), cancellationToken: token);

                if (!ShiftManager.Instance.IsShiftActive) continue;

                int randomTrack = Random.Range(0, 3);

                // --- ГЛАВНЫЙ ФИКС ---
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("Boss")
                        .ByIndex(randomTrack)
                        .RandomSound() // ВАЖНО: Это пометит звук как SFX и не тронет музыку
                        .At(this.transform) // ВАЖНО: Позиция в 3D
                        .WithVolume(0.4f)
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