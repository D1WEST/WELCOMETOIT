using Assets.Modules.Interractables;
using Assets.Modules.Interractables.Impl;
using Assets.Modules.NPC;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float detectionRadius = 5f; // Дистанция появления надписи
    [SerializeField] private float interactionDistance = 3f; // Дистанция рейкаста для клика
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

    private IInteractable _nearestInteractable; // Тот, кто просто рядом
    private IInteractable _focusedInteractable; // Тот, на кого смотрим рейкастом
    private float _holdTimer = 0f;
    private bool _isHolding = false;
    private WorkerPhysical _lastLookedWorker;

    private void OnEnable()
    {
        var root = promptTemplate.Instantiate();
        _promptRoot = root.Q<VisualElement>("prompt-container");
        _promptRoot.style.display = DisplayStyle.None;
        uiDocument.rootVisualElement.Add(_promptRoot);

        _keyLabel = _promptRoot.Q<Label>("key-label");
        _promptLabel = _promptRoot.Q<Label>("prompt-label");
        _progressBg = _promptRoot.Q<VisualElement>("progress-bg");
        _progressFill = _promptRoot.Q<VisualElement>("progress-fill");

        interactAction.action.Enable();
        interactAction.action.started += OnActionStarted;
        interactAction.action.canceled += _ => ResetHold();
    }

    private void OnDisable() => interactAction.action.Disable();

    private void Update()
    {
        FindInteractables();
        HandleInteractionLogic();
    }

    private void LateUpdate() => UpdateUIPosition();

    private void FindInteractables()
    {
        // 1. Сначала пускаем РЕЙКАСТ (это наш точный фокус)
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        IInteractable rayHitInteractable = null;

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableLayer))
        {
            // Проверяем, не рабочий ли это
            if (hit.collider.TryGetComponent<WorkerPhysical>(out var worker))
            {
                if (_lastLookedWorker != worker)
                {
                    _lastLookedWorker = worker;
                    WorkerTooltipUI.Instance.Show(_lastLookedWorker);
                }
                rayHitInteractable = worker;
            }
            else
            {
                // Если не рабочий, ищем обычный интерактив
                rayHitInteractable = hit.collider.GetComponentInParent<IInteractable>();
            }
        }

        _focusedInteractable = rayHitInteractable;

        // 2. Если рейкаст никого не нашел, скрываем тултип рабочего
        if (rayHitInteractable == null && _lastLookedWorker != null)
        {
            WorkerTooltipUI.Instance.Hide();
            _lastLookedWorker = null;
        }

        // 3. ЛОГИКА ОТОБРАЖЕНИЯ ПОДСКАЗКИ [E]
        // Если мы смотрим на объект и у него есть текст (например, "Разбудить")
        if (_focusedInteractable != null && !string.IsNullOrEmpty(_focusedInteractable.InteractionPrompt))
        {
            if (_nearestInteractable != _focusedInteractable)
            {
                _nearestInteractable = _focusedInteractable;
                ShowUI(_nearestInteractable);
            }
        }
        else
        {
            // Если под прицелом никого с текстом нет, ищем ближайшего через сферу (как раньше)
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, interactableLayer);
            IInteractable bestCandidate = null;
            float minDistance = float.MaxValue;

            foreach (var col in colliders)
            {
                var interactable = col.GetComponentInParent<IInteractable>();
                if (interactable == null) continue;
                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < minDistance) { minDistance = dist; bestCandidate = interactable; }
            }

            if (bestCandidate != _nearestInteractable)
            {
                _nearestInteractable = bestCandidate;
                if (_nearestInteractable != null) ShowUI(_nearestInteractable);
                else HideUI();
            }
        }

        // 4. Визуальный фидбек прозрачности
        if (_promptRoot != null && _promptRoot.style.display == DisplayStyle.Flex)
        {
            bool isTargeting = (_focusedInteractable != null && _focusedInteractable == _nearestInteractable);
            _promptRoot.style.opacity = isTargeting ? 1.0f : 0.5f;
            _keyLabel.style.display = isTargeting ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void HandleInteractionLogic()
    {
        if (_focusedInteractable == null)
        {
            ResetHold();
            return;
        }

        // Логика CLICK
        if (_focusedInteractable.InteractionType == InteractionType.Click)
        {
            if (interactAction.action.WasPressedThisFrame()) // Мгновенный отклик
            {
                _focusedInteractable.Interact(gameObject);
            }
        }
        // Логика HOLD
        else if (_isHolding)
        {
            _holdTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_holdTimer / _focusedInteractable.HoldDuration);
            _progressFill.style.width = Length.Percent(progress * 100);

            if (_holdTimer >= _focusedInteractable.HoldDuration)
            {
                _focusedInteractable.Interact(gameObject);
                ResetHold();
            }
        }
    }

    private void OnActionStarted(InputAction.CallbackContext context)
    {
        if (_focusedInteractable != null && _focusedInteractable.InteractionType == InteractionType.Hold)
        {
            _isHolding = true;
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
        _progressBg.style.display = interactable.InteractionType == InteractionType.Hold ? DisplayStyle.Flex : DisplayStyle.None;
        _promptRoot.style.display = DisplayStyle.Flex;
    }

    private void HideUI()
    {
        if (_promptRoot != null) _promptRoot.style.display = DisplayStyle.None;
        ResetHold();
    }

    private void UpdateUIPosition()
    {
        if (_nearestInteractable == null || _promptRoot.style.display == DisplayStyle.None) return;

        Vector3 worldPos = _nearestInteractable.InteractionPivot != null
            ? _nearestInteractable.InteractionPivot.position
            : (_nearestInteractable as MonoBehaviour).transform.position;

        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(_promptRoot.panel, worldPos, playerCamera);

        _promptRoot.style.left = panelPos.x - (_promptRoot.layout.width / 2);
        _promptRoot.style.top = panelPos.y - (_promptRoot.layout.height / 2);
    }
}