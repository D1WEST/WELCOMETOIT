using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Modules.NPC
{
    public class WorkerTooltipUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        private VisualElement _tooltip;
        private VisualElement _barSleep, _barAnger, _barRestless;
        private Label _nameLabel;

        public static WorkerTooltipUI Instance { get; private set; }
        private WorkerPhysical _currentTarget;
        private Camera _mainCam;

        private void Awake() => Instance = this;

        private void OnEnable()
        {
            var root = uiDocument.rootVisualElement;
            _tooltip = root.Q<VisualElement>("worker-tooltip");
            _barSleep = root.Q<VisualElement>("bar-sleep");
            _barAnger = root.Q<VisualElement>("bar-anger");
            _barRestless = root.Q<VisualElement>("bar-restless");
            _nameLabel = root.Q<Label>("name-label");
            _mainCam = Camera.main;
        }

        public void Show(WorkerPhysical target)
        {
            _currentTarget = target;
            _tooltip.style.display = DisplayStyle.Flex;
            _nameLabel.text = target.Data.name;
        }

        public void Hide()
        {
            _currentTarget = null;
            _tooltip.style.display = DisplayStyle.None;
        }

        private void LateUpdate()
        {
            if (_currentTarget == null || _tooltip == null) return;

            _barSleep.style.width = Length.Percent(_currentTarget.Data.currentSleepiness);
            _barAnger.style.width = Length.Percent(_currentTarget.Data.currentAnger);
            _barRestless.style.width = Length.Percent(_currentTarget.Data.currentRestlessness);

            Vector3 worldPos = _currentTarget.transform.position + Vector3.up * 1.0f;
            Vector2 screenPos = RuntimePanelUtils.CameraTransformWorldToPanel(_tooltip.panel, worldPos, _mainCam);

            if (float.IsNaN(screenPos.x)) return;

            _tooltip.style.left = screenPos.x - (_tooltip.layout.width / 2);
            _tooltip.style.top = screenPos.y + 20f;
        }
    }
}