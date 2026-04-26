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
            if (_container == null || _isSpeaking) return; // Не перебиваем, если уже говорит
            _isSpeaking = true;

            _container.style.display = DisplayStyle.Flex;
            _messageLabel.text = "";

            BossPhysical.Instance.SetState(2); // Talking
            var soundQuery = AudioQuery.ByKey("Boss").ByIndex(4).Cycle().At(BossPhysical.Instance.transform);
            AudioManager.Instance.PlayAudio(soundQuery).Forget();

            foreach (char c in message)
            {
                _messageLabel.text += c;
                await UniTask.Delay(40);
            }

            AudioManager.Instance.StopAudio(soundQuery);
            BossPhysical.Instance.SetState(0); // Idle

            await UniTask.Delay(3500); // Даем время дочитать
            _container.style.display = DisplayStyle.None;
            _isSpeaking = false;
        }
    }
}