using Assets.Modules.Save;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Cursor = UnityEngine.Cursor;

public class PaycheckUIController : MonoBehaviour
{
    public static PaycheckUIController Instance { get; private set; }
    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root;
    private Label _amountLabel;
    private int _pendingMoney;
    private PlayerInput _cachedPlayerInput;

    private void Awake() => Instance = this;

    private void Start()
    {
        _root = uiDocument.rootVisualElement;
        _root.style.display = DisplayStyle.None; // Скрыт
        _amountLabel = _root.Q<Label>("amount-text");

        _root.Q<Button>("cash-out-btn").clicked += CashOut;
    }

    public void ShowPaycheck(int amount, GameObject player)
    {
        _pendingMoney = amount;
        _amountLabel.text = amount.ToString("F2");
        _root.style.display = DisplayStyle.Flex;

        // БЛОКИРОВКА ИГРОКА (как в WorkplaceUIController)
        if (player != null)
        {
            _cachedPlayerInput = player.GetComponent<PlayerInput>();
            if (_cachedPlayerInput != null) _cachedPlayerInput.enabled = false;
        }

        // ОСВОБОЖДЕНИЕ КУРСОРA
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CashOut()
    {
        GameDataManager.Instance.ChangeMoney(_pendingMoney);

        ShiftManager.Instance.currentDay++;

        GameDataManager.Instance.SaveGame(ShiftManager.Instance.currentDay);

        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}