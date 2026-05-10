using Assets.Modules.Audio;
using Assets.Modules.NPC;
using Assets.Modules.PlayerModule;
using Assets.Modules.Save;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using PlayerInput = Assets.Modules.PlayerModule.PlayerInput;
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

    private void UpdateHintVisibility()
    {
        var hintContainer = _root.Q<VisualElement>("footer-hint-container");
        if (hintContainer != null)
        {
            // Показываем только если Босс НЕ заткнут
            hintContainer.style.display = GameDataManager.Instance.bossHintsEnabled
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }

    private void OnEnable()
    {
        _root = uiDocument.rootVisualElement;
        _root.style.display = DisplayStyle.None;

        _listContainer = _root.Q<ScrollView>("worker-list");

        // --- ЗВУКИ ВКЛАДОК И ОБНОВЛЕНИЯ ---
        _root.Q<Button>("tab-market").clicked += () => {
            _isMarketTab = true;
            AudioManager.Instance.PlayAudio(AudioQuery.ByKey("AdminPC").ByIndex(0).RandomSound()).Forget();
            RefreshUI();
        };

        _root.Q<Button>("tab-my-workers").clicked += () => {
            _isMarketTab = false;
            AudioManager.Instance.PlayAudio(AudioQuery.ByKey("AdminPC").ByIndex(0).RandomSound()).Forget();
            RefreshUI();
        };

        _root.Q<Button>("btn-refresh").clicked += () => {
            GameDataManager.Instance.RefreshMarket();
            AudioManager.Instance.PlayAudio(AudioQuery.ByKey("AdminPC").ByIndex(1).RandomSound()).Forget();
            RefreshUI();
        };

        _root.Q<Button>("btn-close").clicked += CloseMenu;
    }

    private async UniTaskVoid ListenForExit()
    {
        // Небольшая задержка перед началом прослушивания
        await UniTask.Delay(100);

        while (_root.style.display == DisplayStyle.Flex)
        {
            // Проверяем нажатие E
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                CloseMenu();
                break;
            }
            await UniTask.Yield();
        }
    }

    public void Open(GameObject player)
    {
        if (_root.style.display == DisplayStyle.Flex) return; // Защита от дубля

        _root.style.display = DisplayStyle.Flex;

        // Блокируем игрока
        GameDataManager.SetPlayerInteractivity(false);

        // Запускаем слушатель выхода
        ListenForExit().Forget();

        AudioManager.Instance.PlayAudio(AudioQuery.ByKey("AdminPC").ByIndex(4).RandomSound()).Forget();
        UpdateHintVisibility();
        GameDataManager.Instance.RefreshMarket();
        RefreshUI();
    }

    public void CloseMenu()
    {
        if (_root.style.display == DisplayStyle.None) return;
        _root.style.display = DisplayStyle.None;

        // Разблокируем игрока (он подождет 1 кадр внутри метода)
        GameDataManager.SetPlayerInteractivity(true);

        AudioManager.Instance.PlayAudio(AudioQuery.ByKey("AdminPC").ByIndex(5).RandomSound()).Forget();
    }

    private void RefreshUI()
    {
        _listContainer.Clear();
        var list = _isMarketTab ? GameDataManager.Instance.marketWorkers : GameDataManager.Instance.myWorkers;

        foreach (var worker in list)
        {
            var cardInstance = cardTemplate.Instantiate();
            var card = cardInstance.Q<VisualElement>(className: "worker-card");
            FillCardData(card, worker);
            _listContainer.Add(card);
        }
    }

    private void FillCardData(VisualElement card, WorkerInstance data)
    {
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
            btn.clicked += () => {
                GameDataManager.Instance.HireWorker(data);
                // --- ЗВУК ПОКУПКИ (Индекс 2) ---
                AudioManager.Instance.PlayAudio(AudioQuery.ByKey("AdminPC").ByIndex(2).RandomSound()).Forget();
                RefreshUI();
            };
        }
        else
        {
            ShowManageMenu(card, data);
        }
    }

    private void ShowManageMenu(VisualElement card, WorkerInstance data)
    {
        var container = card.Q<VisualElement>("action-container");
        container.Clear();

        var restBtn = CreateMenuButton(data.isResting ? "Wake" : "Rest", "#2196F3");
        restBtn.clicked += () => {
            data.isResting = !data.isResting;
            // Клик по кнопке внутри управления (можно использовать звук вкладки или отдельный)
            AudioManager.Instance.PlayAudio(AudioQuery.ByKey("AdminPC").ByIndex(0).RandomSound()).Forget();
            RefreshUI();
        };

        int upCost = GameDataManager.Instance.GetUpgradeCost(data);
        string upText = data.currentPosition == Position.Eng_LEGEND ? "MAX" : $"Up ${upCost}";

        var upBtn = CreateMenuButton(upText, "#FF9800");
        if (data.currentPosition != Position.Eng_LEGEND)
        {
            upBtn.clicked += () => {
                GameDataManager.Instance.PromoteWorker(data);
                // Покупка апгрейда — тоже звук покупки (Индекс 2)
                AudioManager.Instance.PlayAudio(AudioQuery.ByKey("AdminPC").ByIndex(2).RandomSound()).Forget();
                RefreshUI();
            };

            if (GameDataManager.Instance.playerMoney < upCost)
                upBtn.style.opacity = 0.5f;
        }

        var sellBtn = CreateMenuButton($"Sell ${data.sellPrice}", "#F44336");
        sellBtn.clicked += () => {
            GameDataManager.Instance.SellWorker(data);
            // --- ЗВУК ПРОДАЖИ (Индекс 3) ---
            AudioManager.Instance.PlayAudio(AudioQuery.ByKey("AdminPC").ByIndex(3).RandomSound()).Forget();
            RefreshUI();
        };

        container.Add(restBtn);
        container.Add(upBtn);
        container.Add(sellBtn);
    }

    private Button CreateMenuButton(string text, string hexColor)
    {
        var btn = new Button { text = text };
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