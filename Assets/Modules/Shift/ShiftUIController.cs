using UnityEngine;
using UnityEngine.UIElements;

public class ShiftUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    private Label _dayLabel, _timeLabel, _progressLabel;
    private VisualElement _progressBar;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _dayLabel = root.Q<Label>("day-label");
        _timeLabel = root.Q<Label>("time-label");
        _progressLabel = root.Q<Label>("progress-text");
        _progressBar = root.Q<VisualElement>("progress-bar-fill");

        ShiftManager.Instance.OnProgressChanged += UpdateUI;
        ShiftManager.Instance.OnTimeChanged += (t) => _timeLabel.text = t;
    }

    private void UpdateUI(float current, float target)
    {
        _dayLabel.text = $"ДЕНЬ {ShiftManager.Instance.currentDay}";
        _progressLabel.text = $"{Mathf.FloorToInt(current)} / {target}";

        float pct = target > 0 ? Mathf.Clamp01(current / target) : 0;
        _progressBar.style.width = Length.Percent(pct * 100);
    }
}