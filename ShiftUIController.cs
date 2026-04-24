using Assets.Modules.Shift;
using UnityEngine;
using UnityEngine.UIElements;

public class ShiftUiController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    private Label _dayLabel, _timeLabel, _progressLabel;
    private VisualElement _progressBar;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        _dayLabel = root.Q<Label>("day-text");
        _timeLabel = root.Q<Label>("time-text");
        _progressLabel = root.Q<Label>("progress-text");
        _progressBar = root.Q<VisualElement>("progress-bar");

        ShiftManager.Instance.OnProgressChanged += UpdateProgress;
        ShiftManager.Instance.OnTimeChanged += (t) => _timeLabel.text = t;
    }

    private void UpdateProgress(float current, float target)
    {
        _dayLabel.text = $"ДЕНЬ {ShiftManager.Instance.currentDay}";
        _progressLabel.text = $"{Mathf.FloorToInt(current)} / {target}";
        float pct = Mathf.Clamp01(current / target);
        _progressBar.style.width = Length.Percent(pct * 100);
    }
}