using System.Threading;
using Assets.Modules.Save;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Modules.Money
{
    public class MoneyUIController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private Label _moneyTotalLabel;
        private Label _moneyDeltaLabel;
        private CancellationTokenSource _cts;

        private void OnEnable()
        {
            // Проверяем, назначен ли UIDocument в инспекторе
            if (uiDocument == null)
            {
                Debug.LogError("UIDocument не назначен в MoneyUIController!");
                return;
            }

            var root = uiDocument.rootVisualElement;
            if (root == null) return;

            // Ищем элементы. ПРОВЕРЬ, ЧТОБЫ ИМЕНА В UI BUILDER БЫЛИ ТАКИМИ ЖЕ!
            _moneyTotalLabel = root.Q<Label>("money-total");
            _moneyDeltaLabel = root.Q<Label>("money-delta");

            // Если элементы не найдены, выводим ошибку, чтобы знать наверняка
            if (_moneyTotalLabel == null) Debug.LogError("Не найден элемент 'money-total' в UXML!");
            if (_moneyDeltaLabel == null) Debug.LogError("Не найден элемент 'money-delta' в UXML!");

            GameDataManager.OnMoneyChanged += HandleMoneyChanged;
        }

        private void Start()
        {
            // Устанавливаем начальное значение в Start, так как Instance уже точно будет готов
            if (GameDataManager.Instance != null && _moneyTotalLabel != null)
            {
                _moneyTotalLabel.text = $"$ {GameDataManager.Instance.playerMoney}";
            }
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
            AnimateMoneyChange(oldVal, newVal, delta, _cts.Token).Forget();
        }

        private async UniTaskVoid AnimateMoneyChange(int startVal, int endVal, int delta, CancellationToken token)
        {
            _moneyDeltaLabel.text = delta > 0 ? $"+ ${delta}" : $"- ${Mathf.Abs(delta)}";
            _moneyDeltaLabel.style.color = delta > 0 ? Color.green : Color.red;

            await AnimateDeltaPopup(token);

            float duration = 0.5f;
            float elapsed = 0;

            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                int currentDisplayVal = (int)Mathf.Lerp(startVal, endVal, t);
                _moneyTotalLabel.text = $"$ {currentDisplayVal}";

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            _moneyTotalLabel.text = $"$ {endVal}";

            _moneyDeltaLabel.style.opacity = 0;
        }

        private async UniTask AnimateDeltaPopup(CancellationToken token)
        {
            _moneyDeltaLabel.style.opacity = 1;
            float startTop = 0;
            float endTop = -20;

            for (int i = 0; i < 20; i++)
            {
                token.ThrowIfCancellationRequested();
                _moneyDeltaLabel.style.top = Mathf.Lerp(startTop, endTop, i / 20f);
                await UniTask.Delay(10, cancellationToken: token);
            }
        }
    }
}
