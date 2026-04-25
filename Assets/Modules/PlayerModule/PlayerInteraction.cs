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
    public static PlayerInteraction Instance { get; private set; }
    private MonitorPhysical _carriedMonitor;

    [Header("Equipment Carrying")]
    [SerializeField] private Transform handSlot; // Пустой объект перед камерой игрока

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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

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

    public void PickUpMonitor(MonitorPhysical monitor)
    {
        // ЗАПРЕТ: Если в руках уже есть монитор - ничего не делаем
        if (_carriedMonitor != null) return;

        _carriedMonitor = monitor;

        // Привязываем к рукам визуально
        _carriedMonitor.transform.SetParent(handSlot);
        _carriedMonitor.transform.localPosition = Vector3.zero;
        _carriedMonitor.transform.localRotation = Quaternion.identity;

        // Выключаем физику и коллайдер, чтобы он не мешал ходить
        _carriedMonitor.SetPhysics(false);
        if (_carriedMonitor.TryGetComponent<Collider>(out var col)) col.enabled = false;

        Debug.Log($"Подобрали монитор {monitor.targetWorkplaceId}");
    }

    private void LateUpdate() => UpdateUIPosition();

    private void PerformInteraction()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableLayer))
        {
            // 1. Если несем монитор и смотрим на НУЖНЫЙ стол
            if (_carriedMonitor != null && hit.collider.TryGetComponent<WorkplaceInteractable>(out var desk))
            {
                if (desk.workplaceId == _carriedMonitor.targetWorkplaceId && !desk.hasMonitor)
                {
                    desk.InstallMonitor(_carriedMonitor);
                    _carriedMonitor.gameObject.SetActive(true);
                    _carriedMonitor = null;
                    return;
                }
            }

            // 2. Обычный подбор монитора или шлепок
            if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
            {
                interactable.Interact(this.gameObject);
            }
        }
    }

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
        if (_focusedInteractable == null) { ResetHold(); return; }

        if (interactAction.action.WasPressedThisFrame())
        {
            // Если несем монитор
            if (_carriedMonitor != null)
            {
                if (_focusedInteractable is WorkplaceInteractable desk)
                {
                    // Проверяем: это тот самый стол?
                    if (desk.workplaceId == _carriedMonitor.targetWorkplaceId)
                    {
                        if (!desk.hasMonitor)
                        {
                            if (_carriedMonitor.TryGetComponent<Collider>(out var col)) col.enabled = true;
                            desk.InstallMonitor(_carriedMonitor);
                            _carriedMonitor = null;
                            Debug.Log("Монитор успешно установлен!");
                            return;
                        }
                    }
                    else
                    {
                        Debug.Log($"Этот монитор от стола {_carriedMonitor.targetWorkplaceId}, а не от {desk.workplaceId}!");
                    }
                }
                return; // Блокируем всё остальное, пока в руках монитор
            }

            // Обычный клик
            _focusedInteractable.Interact(gameObject);
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
        if (_carriedMonitor != null && interactable is WorkplaceInteractable deskInt)
        {
            if (deskInt.workplaceId == _carriedMonitor.targetWorkplaceId)
                _promptLabel.text = "Установить монитор [E]";
        }
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