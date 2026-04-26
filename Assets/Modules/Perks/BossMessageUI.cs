using Assets.Modules.Interractables.Impl;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Modules.Perks
{
    public class BossMessageUI : MonoBehaviour
    {
        public static BossMessageUI Instance { get; private set; }

        private bool _isSpeaking = false;

        private Label _messageLabel;
        private VisualElement _container;

        private void Awake() => Instance = this;

        private void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _messageLabel = root.Q<Label>("boss-message");
            _container = root.Q<VisualElement>("boss-message-container");
            if (_container != null) _container.style.display = DisplayStyle.None;
        }

        public void ForceHide()
        {
            _isSpeaking = false;
            if (_container != null) _container.style.display = DisplayStyle.None;
            // Останавливаем звук бубнежа
            AudioManager.Instance.StopAudio(AudioQuery.ByKey("Boss").ByIndex(4));
            BossPhysical.Instance.SetState(0);
        }

        public async UniTask ShowHint(string message)
        {
            if (_container == null || _isSpeaking) return;
            _isSpeaking = true;

            _container.style.display = DisplayStyle.Flex;
            _messageLabel.text = "";

            BossPhysical.Instance.SetState(2); // Анимация Talking

            // --- ГЛАВНЫЙ ФИКС ЗВУКА ---
            // Используем .RandomSound(), чтобы AudioManager понял, что это НЕ музыка.
            // Используем .Cycle(), чтобы звук повторялся, пока мы печатаем.
            var talkQuery = AudioQuery.ByKey("Boss")
                .ByIndex(4)
                .RandomSound() // Это пометит звук как SFX, и он не тронет BGM
                .Cycle()
                .At(BossPhysical.Instance.transform)
                .WithVolume(0.5f);

            AudioManager.Instance.PlayAudio(talkQuery).Forget();

            foreach (char c in message)
            {
                _messageLabel.text += c;
                await UniTask.Delay(40);
            }

            // ОСТАНОВКА
            AudioManager.Instance.StopAudio(talkQuery);
            BossPhysical.Instance.SetState(0);

            await UniTask.Delay(3000);
            _container.style.display = DisplayStyle.None;
            _isSpeaking = false;
        }
    }
}