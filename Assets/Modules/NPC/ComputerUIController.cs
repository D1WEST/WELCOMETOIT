using Assets.Modules.NPC;
using Assets.Modules.PlayerModule;
using Assets.Modules.Save;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using Position = Assets.Modules.NPC.Position;

public class ComputerUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset cardTemplate;

    private VisualElement _root;
    private PlayerInput _cachedPlayerInput;

    private ScrollView _listContainer;
    private bool _isMarketTab = true;
    private PlayerInputActions _inputActions;


    private void OnEnable()
    {
        _root = uiDocument.rootVisualElement;
        _root.style.display = DisplayStyle.None;

        _listContainer = _root.Q<ScrollView>("worker-list");

        _root.Q<Button>("tab-market").clicked += () => { _isMarketTab = true; RefreshUI(); };
        _root.Q<Button>("tab-my-workers").clicked += () => { _isMarketTab = false; RefreshUI(); };
        _root.Q<Button>("btn-refresh").clicked += () => { GameDataManager.Instance.RefreshMarket(); RefreshUI(); };
        _root.Q<Button>("btn-close").clicked += CloseMenu;
    }

    public void Open(GameObject player)
    {
        _root.style.display = DisplayStyle.Flex;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _cachedPlayerInput = player.GetComponent<PlayerInput>();
        if (_cachedPlayerInput != null)
        {
            _cachedPlayerInput.enabled = false;
        }

        RefreshUI();
    }

    public void CloseMenu()
    {
        _root.style.display = DisplayStyle.None;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_cachedPlayerInput != null)
        {
            _cachedPlayerInput.enabled = true;
            _cachedPlayerInput = null;
        }
    }

    private void RefreshUI()
    {
        _listContainer.Clear();
        var list = _isMarketTab ? GameDataManager.Instance.marketWorkers : GameDataManager.Instance.myWorkers;

        foreach (var worker in list)
        {
            var cardInstance = cardTemplate.Instantiate();
            // Добавляем стиль, чтобы карточка не сжималась
            var card = cardInstance.Q<VisualElement>(className: "worker-card");
            FillCardData(card, worker);
            _listContainer.Add(card);
        }
    }

    private void FillCardData(VisualElement card, WorkerInstance data)
    {
        // Заполняем все поля из модели
        card.Q<Label>("name-label").text = data.name;
        card.Q<Label>("id-label").text = $"ID: {data.templateId}";
        card.Q<Label>("pos-label").text = data.currentPosition.ToString();

        card.Q<Label>("stat-patience").text = data.patience.ToString();
        card.Q<Label>("stat-power").text = data.workPower.ToString();
        card.Q<Label>("stat-sleep").text = data.sleepiness.ToString();
        card.Q<Label>("stat-anger").text = data.angriness.ToString();

        if (data.avatar != null)
            card.Q<VisualElement>("avatar").style.backgroundImage = new StyleBackground(data.avatar);

        var container = card.Q<VisualElement>("action-container");
        var btn = card.Q<Button>("action-btn");

        if (_isMarketTab)
        {
            btn.text = $"Buy ${data.buyPrice}";
            btn.clicked += () => { GameDataManager.Instance.HireWorker(data); RefreshUI(); };
        }
        else
        {
            ShowManageMenu(card, data); // Сразу вызываем отрисовку кнопок управления
        }
    }

    private void ShowManageMenu(VisualElement card, WorkerInstance data)
    {
        var container = card.Q<VisualElement>("action-container");
        container.Clear();

        // Кнопка отдыха
        var restBtn = CreateMenuButton(data.isResting ? "Wake" : "Rest", "#2196F3");
        restBtn.clicked += () => { data.isResting = !data.isResting; RefreshUI(); };

        // Кнопка Улучшения с ценой
        int upCost = GameDataManager.Instance.GetUpgradeCost(data);
        string upText = data.currentPosition == Position.Eng_LEGEND ? "MAX" : $"Up ${upCost}";

        var upBtn = CreateMenuButton(upText, "#FF9800");
        if (data.currentPosition != Position.Eng_LEGEND)
        {
            upBtn.clicked += () => {
                GameDataManager.Instance.PromoteWorker(data);
                RefreshUI();
            };

            // Если денег мало — делаем кнопку полупрозрачной (визуальный фидбек)
            if (GameDataManager.Instance.playerMoney < upCost)
                upBtn.style.opacity = 0.5f;
        }

        // Кнопка Продажи
        var sellBtn = CreateMenuButton($"Sell ${data.sellPrice}", "#F44336");
        sellBtn.clicked += () => { GameDataManager.Instance.SellWorker(data); RefreshUI(); };

        container.Add(restBtn);
        container.Add(upBtn);
        container.Add(sellBtn);
    }

    private Button CreateMenuButton(string text, string hexColor)
    {
        var btn = new Button { text = text };

        // Прямая установка цвета фона
        if (ColorUtility.TryParseHtmlString(hexColor, out var color))
        {
            btn.style.backgroundColor = color;
        }

        btn.style.color = Color.white;
        btn.style.fontSize = 12;
        btn.style.marginTop = 2;
        btn.style.marginBottom = 2;
        btn.style.paddingLeft = 5;
        btn.style.paddingRight = 5;
        btn.style.borderTopWidth = 0;
        btn.style.borderBottomWidth = 0;
        btn.style.borderLeftWidth = 0;
        btn.style.borderRightWidth = 0;
        btn.style.borderBottomLeftRadius = 3;
        btn.style.borderBottomRightRadius = 3;
        btn.style.borderTopLeftRadius = 3;
        btn.style.borderTopRightRadius = 3;

        return btn;
    }
}