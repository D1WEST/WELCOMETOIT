using System.Threading;
using Assets.Modules.Save;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Modules.Money
{
    public class MoneyHUDController : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private Label _moneyTotalLabel;
        private Label _moneyDeltaLabel;
        private CancellationTokenSource _cts;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            var root = _uiDocument.rootVisualElement;

            // Ждем кадра, чтобы UI успел отрисоваться
            InitializeUI(root).Forget();

            GameDataManager.OnMoneyChanged += HandleMoneyChanged;
        }

        private async UniTaskVoid InitializeUI(VisualElement root)
        {
            await UniTask.Yield(); // Даем время на сборку UI
            _moneyTotalLabel = root.Q<Label>("money-total");
            _moneyDeltaLabel = root.Q<Label>("money-delta");

            if (GameDataManager.Instance != null && _moneyTotalLabel != null)
                _moneyTotalLabel.text = $"$ {GameDataManager.Instance.playerMoney}";
        }

        private void OnDisable()
        {
            GameDataManager.OnMoneyChanged -= HandleMoneyChanged;
            _cts?.Cancel();
        }

        private void HandleMoneyChanged(int oldVal, int newVal, int delta)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            AnimateMoney(oldVal, newVal, delta, _cts.Token).Forget();
        }

        private async UniTaskVoid AnimateMoney(int start, int end, int delta, CancellationToken token)
        {
            if (_moneyTotalLabel == null || _moneyDeltaLabel == null) return;

            // 1. Анимация дельты
            _moneyDeltaLabel.text = delta > 0 ? $"+ ${delta}" : $"- ${Mathf.Abs(delta)}";
            _moneyDeltaLabel.style.color = delta > 0 ? Color.green : Color.red;
            _moneyDeltaLabel.style.opacity = 1;
            _moneyDeltaLabel.style.top = 0;

            // 2. Тиканье цифр
            float duration = 0.6f;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                int current = (int)Mathf.Lerp(start, end, t);
                _moneyTotalLabel.text = $"$ {current}";

                // Плавный подъем дельты
                _moneyDeltaLabel.style.top = Mathf.Lerp(0, -30, t);
                _moneyDeltaLabel.style.opacity = Mathf.Lerp(1, 0, t);

                await UniTask.Yield(token);
            }

            _moneyTotalLabel.text = $"$ {end}";
            _moneyDeltaLabel.style.opacity = 0;
        }
    }
}