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
    public bool IsCarryingItem => _carriedMonitor != null;

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
    [SerializeField] private InputActionReference dropAction;

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

        dropAction.action.Enable();
        dropAction.action.performed += _ => DropItem();
    }

    private void OnDisable() => interactAction.action.Disable();

    private void DropItem()
    {
        if (_carriedMonitor == null) return;


        _carriedMonitor.transform.SetParent(null);
        _carriedMonitor.SetPhysics(true);

        if (_carriedMonitor.TryGetComponent<Collider>(out var col)) col.enabled = true;

        if (_carriedMonitor.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.AddForce(playerCamera.transform.forward * 3f + Vector3.up * 2f, ForceMode.Impulse);
        }

        _carriedMonitor = null;
    }

    private void Update()
    {
        if (_focusedInteractable is MonoBehaviour mb1 && mb1 == null) _focusedInteractable = null;
        if (_nearestInteractable is MonoBehaviour mb2 && mb2 == null) _nearestInteractable = null;

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
            if (hit.collider.TryGetComponent<AnimalAI>(out var fox))
            {
                // ПРОВЕРКА: Если лиса уже "мертва" (в процессе удаления), игнорируем её
                if (fox != null)
                {
                    _focusedInteractable = fox;
                    _nearestInteractable = fox;
                    ShowUI(fox);

                    if (_lastLookedWorker != null) { WorkerTooltipUI.Instance.Hide(); _lastLookedWorker = null; }
                    return;
                }
            }
            // Приоритет 1: Рабочий (WorkerPhysical)
            else if (hit.collider.TryGetComponent<WorkerPhysical>(out var worker))
            {
                if (_lastLookedWorker != worker)
                {
                    _lastLookedWorker = worker;
                    WorkerTooltipUI.Instance.Show(_lastLookedWorker);
                }
                rayHitInteractable = worker;
            }
            // Приоритет 2: Монитор (MonitorPhysical)
            else if (hit.collider.TryGetComponent<MonitorPhysical>(out var mon))
            {
                rayHitInteractable = mon;
                if (_lastLookedWorker != null) { WorkerTooltipUI.Instance.Hide(); _lastLookedWorker = null; }
            }
            // Приоритет 3: Стол или что-то еще (IInteractable на родителе)
            else
            {
                rayHitInteractable = hit.collider.GetComponentInParent<IInteractable>();
                if (_lastLookedWorker != null) { WorkerTooltipUI.Instance.Hide(); _lastLookedWorker = null; }
            }
        }
        else
        {
            if (_lastLookedWorker != null) { WorkerTooltipUI.Instance.Hide(); _lastLookedWorker = null; }
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
        if (_carriedMonitor != null && dropAction.action.WasPressedThisFrame())
        {
            DropItem();
            return;
        }

        if (_focusedInteractable == null)
        {
            ResetHold();
            return;
        }

        if (_carriedMonitor != null)
        {
            if (interactAction.action.WasPressedThisFrame())
            {
                if (_focusedInteractable is WorkplaceInteractable desk)
                {
                    if (desk.workplaceId == _carriedMonitor.targetWorkplaceId && !desk.hasMonitor)
                    {
                        if (_carriedMonitor.TryGetComponent<Collider>(out var col)) col.enabled = true;
                        desk.InstallMonitor(_carriedMonitor);
                        _carriedMonitor = null;
                        _focusedInteractable = null;
                        return;
                    }
                }
            }
            return;
        }
        if (_focusedInteractable.InteractionType == InteractionType.Click)
        {
            if (interactAction.action.WasPressedThisFrame())
            {
                _focusedInteractable.Interact(gameObject);
            }
        }
        else if (_focusedInteractable.InteractionType == InteractionType.Hold)
        {
            if (interactAction.action.IsPressed())
            {
                _isHolding = true;
                _holdTimer += Time.deltaTime;

                float progress = Mathf.Clamp01(_holdTimer / _focusedInteractable.HoldDuration);
                if (_progressFill != null)
                {
                    _progressFill.style.width = Length.Percent(progress * 100);
                }

                if (_holdTimer >= _focusedInteractable.HoldDuration)
                {
                    _focusedInteractable.Interact(gameObject);
                    ResetHold();
                }
            }
            else
            {
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
        string finalPrompt = interactable.InteractionPrompt;

        if (_carriedMonitor != null)
        {
            if (interactable is WorkplaceInteractable desk)
            {
                if (desk.workplaceId == _carriedMonitor.targetWorkplaceId)
                {
                    finalPrompt = "Установить монитор [E]";
                }
                else
                {
                    finalPrompt = $"Это не тот стол! Отнесите к {_carriedMonitor.targetWorkplaceId}";
                }
            }
            else if (interactable is AnimalAI || interactable is WorkerPhysical)
            {
                finalPrompt = "Руки заняты!";
            }
        }

        // Применяем текст к UI
        _promptLabel.text = finalPrompt;

        // Остальная логика (кнопки, прогресс)
        _keyLabel.text = $"[{interactAction.action.GetBindingDisplayString()}]";
        _progressBg.style.display = interactable.InteractionType == InteractionType.Hold ? DisplayStyle.Flex : DisplayStyle.None;
        _promptRoot.style.display = DisplayStyle.Flex;
    }

    private void HideUI()
    {
        _nearestInteractable = null;
        if (_promptRoot != null) _promptRoot.style.display = DisplayStyle.None;
        ResetHold();
    }

    private void UpdateUIPosition()
    {
        // 2. Если объект удален, скрываем UI и выходим
        if (_nearestInteractable == null || _nearestInteractable is MonoBehaviour mb && mb == null)
        {
            HideUI();
            return;
        }

        if (_promptRoot.style.display == DisplayStyle.None) return;

        Transform pivot = _nearestInteractable.InteractionPivot;
        if (pivot == null) return;

        Vector3 worldPos = pivot.position;
        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(_promptRoot.panel, worldPos, playerCamera);

        _promptRoot.style.left = panelPos.x - (_promptRoot.layout.width / 2);
        _promptRoot.style.top = panelPos.y - (_promptRoot.layout.height / 2);
    }
}