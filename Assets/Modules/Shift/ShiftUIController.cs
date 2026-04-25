using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks; // Используем UniTask для безопасного ожидания

public class ShiftUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;

    // Ссылки на элементы UI
    private Label _dayLabel;
    private Label _timeLabel;
    private Label _progressLabel;
    private VisualElement _progressBarFill;

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        var root = uiDocument.rootVisualElement;
        _dayLabel = root.Q<Label>("day-label");
        _timeLabel = root.Q<Label>("time-label");
        _progressLabel = root.Q<Label>("progress-text");
        _progressBarFill = root.Q<VisualElement>("progress-bar-fill");

        if (ShiftManager.Instance != null)
        {
            SubscribeToEvents();
        }
        else
        {
            WaitForManagerAndSubscribe().Forget();
        }
    }

    private async UniTaskVoid WaitForManagerAndSubscribe()
    {
        await UniTask.WaitUntil(() => ShiftManager.Instance != null);
        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        ShiftManager.Instance.OnProgressChanged -= UpdateProgressUI;
        ShiftManager.Instance.OnTimeChanged -= UpdateTimeUI;

        ShiftManager.Instance.OnProgressChanged += UpdateProgressUI;
        ShiftManager.Instance.OnTimeChanged += UpdateTimeUI;

        UpdateProgressUI(ShiftManager.Instance.currentProgress, ShiftManager.Instance.targetGoal);
    }

    private void OnDisable()
    {
        if (ShiftManager.Instance != null)
        {
            ShiftManager.Instance.OnProgressChanged -= UpdateProgressUI;
            ShiftManager.Instance.OnTimeChanged -= UpdateTimeUI;
        }
    }

    // Метод обновления шкалы и текста прогресса
    private void UpdateProgressUI(float current, float target)
    {
        if (_dayLabel != null)
        {
            _dayLabel.text = $"ДЕНЬ {ShiftManager.Instance.currentDay}";
        }

        if (_progressLabel != null)
        {
            _progressLabel.text = $"{Mathf.FloorToInt(current)} / {target}";
        }

        if (_progressBarFill != null)
        {
            float pct = (target > 0) ? Mathf.Clamp01(current / target) : 0;

            _progressBarFill.style.width = Length.Percent(pct * 100);

            _progressBarFill.style.backgroundColor = pct >= 1f ? Color.cyan : new Color(0.3f, 0.68f, 0.31f);
        }
    }

    // Метод обновления часов
    private void UpdateTimeUI(string timeStr)
    {
        if (_timeLabel != null)
        {
            _timeLabel.text = timeStr;
        }
    }
}