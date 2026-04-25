using Assets.Modules.PlayerModule;
using Assets.Modules.Save;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public class PaycheckUIController : MonoBehaviour
{
    public static PaycheckUIController Instance { get; private set; }
    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root, _checkFrame, _amountBox;
    private Label _amountLabel, _currencyLabel, _descriptionLabel, _bankName, _signature;
    private Button _actionBtn;

    private int _pendingMoney;
    private bool _isWinState;
    private PlayerInput _cachedPlayerInput;

    private void Awake() => Instance = this;

    private void Start()
    {
        _root = uiDocument.rootVisualElement;
        _root.style.display = DisplayStyle.None;

        _checkFrame = _root.Q<VisualElement>("check-frame");
        _amountBox = _root.Q<VisualElement>("amount-box");
        _amountLabel = _root.Q<Label>("amount-text");
        _currencyLabel = _root.Q<Label>("currency-symbol");
        _descriptionLabel = _root.Q<Label>("description-text");
        _bankName = _root.Q<Label>("bank-name");
        _signature = _root.Q<Label>("label-signature");
        _actionBtn = _root.Q<Button>("action-btn");

        _actionBtn.clicked += OnActionClicked;
    }

    public void ShowResult(bool isWin, int amount, GameObject player)
    {
        _isWinState = isWin;
        _pendingMoney = amount;
        _root.style.display = DisplayStyle.Flex;

        // Блокировка игрока
        if (player != null)
        {
            _cachedPlayerInput = player.GetComponent<PlayerInput>();
            if (_cachedPlayerInput != null) _cachedPlayerInput.enabled = false;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (isWin) SetupWinView(amount);
        else SetupLoseView();
    }

    private void SetupWinView(int amount)
    {
        Color winBlue = new Color(0, 0.53f, 0.8f); // #0088CC
        Color lightBlue = new Color(0.35f, 0.76f, 1f); // #59C2FF

        ApplyTheme(winBlue, lightBlue, "Cash Out");
        _amountLabel.text = amount.ToString("F2");
        _currencyLabel.style.display = DisplayStyle.Flex;
        _descriptionLabel.text = "Playing Overtask!";
    }

    private void SetupLoseView()
    {
        Color failRed = new Color(0.8f, 0.1f, 0.1f); // Красный
        Color lightRed = new Color(1f, 0.4f, 0.4f); // Светло-красный

        ApplyTheme(failRed, lightRed, "Retry");
        _amountLabel.text = "FIRED";
        _currencyLabel.style.display = DisplayStyle.None; // Прячем знак $
        _descriptionLabel.text = "Couldn't handle the load!";
    }

    private void ApplyTheme(Color main, Color secondary, string btnText)
    {
        _checkFrame.style.borderLeftColor = secondary; 
        _checkFrame.style.borderRightColor = secondary;
        _checkFrame.style.borderTopColor = secondary;
        _checkFrame.style.borderBottomColor = secondary;
        _amountBox.style.borderLeftColor = secondary;
        _amountBox.style.borderRightColor = secondary;
        _amountBox.style.borderTopColor = secondary;
        _amountBox.style.borderBottomColor = secondary;
        _bankName.style.color = main;
        _signature.style.color = main;
        _actionBtn.style.backgroundColor = main;
        _actionBtn.text = btnText;

        _root.Query<Label>().ForEach(l => {
            if (l.name.StartsWith("label-")) l.style.color = secondary;
        });
    }

    private void OnActionClicked()
    {
        if (_isWinState)
        {
            GameDataManager.Instance.ChangeMoney(_pendingMoney);
            ShiftManager.Instance.currentDay++;
            GameDataManager.Instance.SaveGame(ShiftManager.Instance.currentDay);
        }
        else
        {
            GameDataManager.Instance.RestoreCheckpoint();
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}