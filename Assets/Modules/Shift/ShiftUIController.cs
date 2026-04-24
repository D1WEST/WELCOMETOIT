using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Modules.Shift
{
    public class ShiftUIController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private Label _dayLabel;
        private Label _timeLabel;
        private Label _progressLabel;
        private VisualElement _progressBarFill;

        private void OnEnable()
        {
            if (uiDocument == null) return;

            var root = uiDocument.rootVisualElement;

            // Поиск элементов по именам из UXML
            _dayLabel = root.Q<Label>("day-label");
            _timeLabel = root.Q<Label>("time-label");
            _progressLabel = root.Q<Label>("progress-text");
            _progressBarFill = root.Q<VisualElement>("progress-bar-fill");

            // Подписка на события менеджера смены
            if (ShiftManager.Instance != null)
            {
                ShiftManager.Instance.OnProgressChanged += UpdateProgressUI;
                ShiftManager.Instance.OnTimeChanged += UpdateTimeUI;
            }
        }

        private void UpdateProgressUI(float current, float target)
        {
            if (_dayLabel != null)
                _dayLabel.text = $"ДЕНЬ {ShiftManager.Instance.currentDay}";

            if (_progressLabel != null)
                _progressLabel.text = $"{Mathf.FloorToInt(current)} / {target}";

            if (_progressBarFill != null)
            {
                float pct = (target > 0) ? Mathf.Clamp01(current / target) : 0;
                _progressBarFill.style.width = Length.Percent(pct * 100);
            }
        }

        private void UpdateTimeUI(string timeStr)
        {
            if (_timeLabel != null)
                _timeLabel.text = timeStr;
        }
    }
}