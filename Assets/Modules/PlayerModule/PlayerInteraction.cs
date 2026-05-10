using Assets.Modules.Interractables;
using Assets.Modules.Interractables.Impl;
using Assets.Modules.NPC;
using Cysharp.Threading.Tasks;
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
    private IInteractable _lastHighlighted;
    public bool IsLocked = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        // Инициализируем UI строго ОДИН РАЗ
        if (_promptRoot == null)
        {
            var root = promptTemplate.Instantiate();
            _promptRoot = root.Q<VisualElement>("prompt-container");
            _promptRoot.style.display = DisplayStyle.None;
            uiDocument.rootVisualElement.Add(_promptRoot);

            _keyLabel = _promptRoot.Q<Label>("key-label");
            _promptLabel = _promptRoot.Q<Label>("prompt-label");
            _progressBg = _promptRoot.Q<VisualElement>("progress-bg");
            _progressFill = _promptRoot.Q<VisualElement>("progress-fill");
        }
    }

    private void OnEnable()
    {
        // ОСТАВЛЯЕМ ТОЛЬКО ВКЛЮЧЕНИЕ
        interactAction.action.Enable();
        dropAction.action.Enable();

        // Подписки на события для HOLD (удержания)
        interactAction.action.started += OnActionStarted;
        interactAction.action.canceled += _ => ResetHold();

        // УБРАЛИ ПОДПИСКУ НА DropItem() ТУТ, ТАК КАК МЫ ОБРАБАТЫВАЕМ ЕЁ В UPDATE
    }

    private void OnDisable() => interactAction.action.Disable();

    private void DropItem()
    {
        // ЗАЩИТА: Если монитора нет, выходим
        if (_carriedMonitor == null) return;


        // 1. Отключаем "квестовую" подсветку стола, прежде чем выбросить
        ToggleTargetDeskHighlight(_carriedMonitor.targetWorkplaceId, false);

        // 2. Включаем физику и отцепляем от рук
        _carriedMonitor.transform.SetParent(null);
        _carriedMonitor.SetPhysics(true);

        if (_carriedMonitor.TryGetComponent<Collider>(out var col)) col.enabled = true;

        if (_carriedMonitor.TryGetComponent<Rigidbody>(out var rb))
        {
            // Импульс вперед и чуть вверх
            rb.AddForce(playerCamera.transform.forward * 4f + Vector3.up * 2f, ForceMode.Impulse);
        }

        // 3. Зануляем ссылку в конце
        _carriedMonitor = null;
    }

    private void Update()
    {
        // Проверка на уничтоженные объекты
        if (_focusedInteractable is MonoBehaviour mb1 && mb1 == null) _focusedInteractable = null;
        if (_nearestInteractable is MonoBehaviour mb2 && mb2 == null) _nearestInteractable = null;

        // Если заблокированы — ничего не делаем
        if (IsLocked)
        {
            if (_promptRoot.style.display == DisplayStyle.Flex) HideUI();
            return;
        }

        FindInteractables();
        HandleInteractionLogic();
    }

    public async UniTaskVoid UnlockWithDelay()
    {
        await UniTask.NextFrame(); // Ждем конец текущего кадра, где нажали "E"
        IsLocked = false;
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

        ToggleTargetDeskHighlight(monitor.targetWorkplaceId, true);

    }

    private void LateUpdate() => UpdateUIPosition();

    private void FindInteractables()
    {
        // 1. Сначала пускаем РЕЙКАСТ (это наш точный фокус)
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        IInteractable rayHitInteractable = null;

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableLayer))
        {
            if (hit.collider.TryGetComponent<AnimalAI>(out var fox))
            {
                if (fox != null)
                {
                    rayHitInteractable = fox; // Устанавливаем, чтобы зафиксировать для подсветки
                    _focusedInteractable = fox;
                    _nearestInteractable = fox;
                    ShowUI(fox);

                    if (_lastLookedWorker != null) { WorkerTooltipUI.Instance.Hide(); _lastLookedWorker = null; }

                    // Раньше тут был return, мы его убираем, чтобы код дошел до блока подсветки ниже
                }
            }
            else if (hit.collider.TryGetComponent<WorkerPhysical>(out var worker))
            {
                if (_lastLookedWorker != worker)
                {
                    _lastLookedWorker = worker;
                    WorkerTooltipUI.Instance.Show(_lastLookedWorker);
                }
                rayHitInteractable = worker;
            }
            else if (hit.collider.TryGetComponent<MonitorPhysical>(out var mon))
            {
                rayHitInteractable = mon;
                if (_lastLookedWorker != null) { WorkerTooltipUI.Instance.Hide(); _lastLookedWorker = null; }
            }
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

        // ==========================================
        // HIGHLIGHT LOGIC
        // ==========================================
        if (_focusedInteractable != _lastHighlighted)
        {
            // Выключаем старую обводку (безопасная проверка через MonoBehaviour)
            if (_lastHighlighted is MonoBehaviour mbOld && mbOld != null)
                _lastHighlighted.OnHoverExit();

            _lastHighlighted = _focusedInteractable;

            // Включаем новую обводку
            if (_lastHighlighted is MonoBehaviour mbNew && mbNew != null)
                _lastHighlighted.OnHoverEnter();
        }
        // ==========================================

        // Если мы нашли лису через первый блок if, нам нужно прервать выполнение 
        // логики "ближайшего" объекта, чтобы подсказка [E] не перепрыгнула на стол
        if (_focusedInteractable is AnimalAI) return;

        // 2. Если рейкаст никого не нашел, скрываем тултип рабочего
        if (rayHitInteractable == null && _lastLookedWorker != null)
        {
            WorkerTooltipUI.Instance.Hide();
            _lastLookedWorker = null;
        }

        // 3. ЛОГИКА ОТОБРАЖЕНИЯ ПОДСКАЗКИ [E]
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
            // Поиск ближайшего через сферу (без изменений)
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

        // 4. Визуальный фидбек прозрачности (без изменений)
        if (_promptRoot != null && _promptRoot.style.display == DisplayStyle.Flex)
        {
            bool isTargeting = (_focusedInteractable != null && _focusedInteractable == _nearestInteractable);
            _promptRoot.style.opacity = isTargeting ? 1.0f : 0.5f;
            _keyLabel.style.display = isTargeting ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void HandleInteractionLogic()
    {
        // --- 1. ПРИОРИТЕТ: ВЫБРОС (G) ---
        // Это должно быть самым первым, чтобы работало всегда
        if (_carriedMonitor != null && dropAction.action.WasPressedThisFrame())
        {
            DropItem();
            return;
        }

        // Если ничего не под прицелом — сброс
        if (_focusedInteractable == null)
        {
            ResetHold();
            return;
        }

        // --- 2. ЛОГИКА С МОНИТОРОМ В РУКАХ (УСТАНОВКА) ---
        if (_carriedMonitor != null)
        {
            if (interactAction.action.WasPressedThisFrame())
            {
                if (_focusedInteractable is WorkplaceInteractable desk)
                {
                    // Ставим только если ID совпадает и стол пустой
                    if (desk.workplaceId == _carriedMonitor.targetWorkplaceId && !desk.hasMonitor)
                    {
                        desk.SetQuestHighlight(false);
                        if (_carriedMonitor.TryGetComponent<Collider>(out var col)) col.enabled = true;
                        desk.InstallMonitor(_carriedMonitor);
                        _carriedMonitor = null;
                        _focusedInteractable = null;
                        return;
                    }
                }
            }
            // Если в руках монитор — блокируем другие нажатия E (шлепки, меню)
            return;
        }

        // --- 3. ОБЫЧНОЕ ВЗАИМОДЕЙСТВИЕ (E) ---
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
                if (_progressFill != null) _progressFill.style.width = Length.Percent(progress * 100);

                if (_holdTimer >= _focusedInteractable.HoldDuration)
                {
                    _focusedInteractable.Interact(gameObject);
                    ResetHold();
                }
            }
            else { ResetHold(); }
        }
    }

    private void ToggleTargetDeskHighlight(string id, bool state)
    {
        // Используем наш статический список всех столов
        var target = WorkplaceInteractable.AllDesks.Find(d => d.workplaceId == id);
        if (target != null)
        {
            target.SetQuestHighlight(state);
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