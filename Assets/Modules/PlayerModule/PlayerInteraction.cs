using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Assets.Modules.Interractables;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private Camera playerCamera;

    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    [Header("UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset promptTemplate;

    private VisualElement _promptRoot;
    private Label _keyLabel;
    private Label _promptLabel;
    private VisualElement _progressBg;
    private VisualElement _progressFill;

    private IInteractable _currentInteractable;
    private float _holdTimer = 0f;
    private bool _isHolding = false;

    private void OnEnable()
    {
        var root = promptTemplate.Instantiate();
        // Берем первый дочерний элемент, чтобы управлять именно контейнером
        _promptRoot = root.Q<VisualElement>("prompt-container");
        _promptRoot.style.display = DisplayStyle.None; // СРАЗУ СКРЫВАЕМ

        uiDocument.rootVisualElement.Add(_promptRoot);

        _keyLabel = _promptRoot.Q<Label>("key-label");
        _promptLabel = _promptRoot.Q<Label>("prompt-label");
        _progressBg = _promptRoot.Q<VisualElement>("progress-bg");
        _progressFill = _promptRoot.Q<VisualElement>("progress-fill");

        interactAction.action.Enable();

        // Подписки для Hold логики
        interactAction.action.started += _ => _isHolding = true;
        interactAction.action.canceled += _ => ResetHold();
    }

    private void OnDisable() => interactAction.action.Disable();

    private void Update()
    {
        CheckForInteractable();
        HandleHoldLogic();
    }

    private void LateUpdate() => UpdateUIPosition();

    private void CheckForInteractable()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                if (_currentInteractable != interactable)
                {
                    _currentInteractable = interactable;
                    ShowUI(interactable);
                }
                return;
            }
        }

        if (_currentInteractable != null)
        {
            _currentInteractable = null;
            ResetHold();
            HideUI();
        }
    }

    private void HandleHoldLogic()
    {
        if (_currentInteractable == null) return;

        // Если это обычный клик
        if (_currentInteractable.InteractionType == InteractionType.Click)
        {
            if (interactAction.action.WasPerformedThisFrame())
            {
                _currentInteractable.Interact(gameObject);
            }
            return;
        }

        // Если это Hold (Удержание)
        if (_isHolding)
        {
            _holdTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_holdTimer / _currentInteractable.HoldDuration);
            _progressFill.style.width = Length.Percent(progress * 100);

            if (_holdTimer >= _currentInteractable.HoldDuration)
            {
                _currentInteractable.Interact(gameObject);
                ResetHold();
            }
        }
    }

    private void ResetHold()
    {
        _isHolding = false;
        _holdTimer = 0f;
        if (_progressFill != null) _progressFill.style.width = 0;
    }

    private void ShowUI(IInteractable interactable)
    {
        _promptLabel.text = interactable.InteractionPrompt;
        _keyLabel.text = $"[{interactAction.action.GetBindingDisplayString()}]";

        // Показываем полоску только если тип - Hold
        _progressBg.style.display = interactable.InteractionType == InteractionType.Hold
            ? DisplayStyle.Flex : DisplayStyle.None;

        _promptRoot.style.display = DisplayStyle.Flex;
    }

    private void HideUI()
    {
        _promptRoot.style.display = DisplayStyle.None;
    }

    private void UpdateUIPosition()
    {
        if (_currentInteractable == null || _promptRoot.style.display == DisplayStyle.None) return;

        Vector3 worldPos = _currentInteractable.InteractionPivot != null
            ? _currentInteractable.InteractionPivot.position
            : (_currentInteractable as MonoBehaviour).transform.position;

        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
            _promptRoot.panel, worldPos, playerCamera);

        _promptRoot.style.left = panelPos.x - (_promptRoot.layout.width / 2);
        _promptRoot.style.top = panelPos.y - (_promptRoot.layout.height / 2);
    }
}