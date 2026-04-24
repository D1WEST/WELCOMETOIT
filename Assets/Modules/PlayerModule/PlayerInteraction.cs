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
    [SerializeField] private UIDocument uiDocument; // Сюда вешаем UIDocument (Source Asset должен быть пустым!)
    [SerializeField] private VisualTreeAsset promptTemplate;

    private VisualElement _promptRoot;
    private Label _keyLabel;
    private Label _promptLabel;
    private IInteractable _currentInteractable;

    private void OnEnable()
    {
        if (uiDocument == null || promptTemplate == null) return;

        // Создаем элемент и добавляем его в корень документа
        _promptRoot = promptTemplate.Instantiate();
        _promptRoot.style.display = DisplayStyle.None;
        // Важно: делаем позиционирование абсолютным
        _promptRoot.style.position = Position.Absolute;
        uiDocument.rootVisualElement.Add(_promptRoot);

        _keyLabel = _promptRoot.Q<Label>("key-label");
        _promptLabel = _promptRoot.Q<Label>("prompt-label");

        interactAction.action.Enable();
        // Подписываемся на событие нажатия
        interactAction.action.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        interactAction.action.performed -= OnInteractPerformed;
        interactAction.action.Disable();
    }

    private void Update()
    {
        CheckForInteractable();
    }

    private void LateUpdate() // Позиционирование лучше делать в LateUpdate
    {
        UpdateUIPosition();
    }

    private void CheckForInteractable()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)); // Луч из центра экрана
        RaycastHit hit;

        // Визуализация луча в эдиторе
        Debug.DrawRay(ray.origin, ray.direction * interactionDistance, Color.green);

        if (Physics.Raycast(ray, out hit, interactionDistance, interactableLayer))
        {
            // Пытаемся найти IInteractable в объекте или его родителях
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

        // Если луч никуда не попал
        if (_currentInteractable != null)
        {
            _currentInteractable = null;
            HideUI();
        }
    }

    private void ShowUI(IInteractable interactable)
    {
        _promptLabel.text = interactable.InteractionPrompt;
        _keyLabel.text = $"[{interactAction.action.GetBindingDisplayString()}]";
        _promptRoot.style.display = DisplayStyle.Flex;
    }

    private void HideUI()
    {
        if (_promptRoot != null)
            _promptRoot.style.display = DisplayStyle.None;
    }

    private void UpdateUIPosition()
    {
        if (_currentInteractable == null || _promptRoot.style.display == DisplayStyle.None) return;

        // Определяем мировую точку (Pivot или центр объекта)
        Vector3 worldPos = _currentInteractable.InteractionPivot != null
            ? _currentInteractable.InteractionPivot.position
            : (_currentInteractable as MonoBehaviour).transform.position;

        // Магия перевода координат из World Space в UI Toolkit Space
        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
            _promptRoot.panel, worldPos, playerCamera);

        // Смещаем подсказку, чтобы она была по центру точки
        _promptRoot.style.left = panelPos.x - (_promptRoot.layout.width / 2);
        _promptRoot.style.top = panelPos.y - (_promptRoot.layout.height / 2);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (_currentInteractable != null)
        {
            Debug.Log("Логика сработала! Взаимодействие с: " + (_currentInteractable as MonoBehaviour).name);
            _currentInteractable.Interact(gameObject);
        }
    }
}