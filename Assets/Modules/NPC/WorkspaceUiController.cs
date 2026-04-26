using Assets.Modules.Interractables.Impl;
using Assets.Modules.NPC;
using Assets.Modules.PlayerModule;
using Assets.Modules.Save;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public class WorkplaceUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset cardTemplate;

    private VisualElement _root;
    private ScrollView _listContainer;
    private WorkplaceInteractable _targetDesk;
    private PlayerInput _cachedPlayerInput;

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

        // Кнопка закрытия
        var closeBtn = _root.Q<Button>("btn-close");
        if (closeBtn != null) closeBtn.clicked += Close;

        // Кнопка "Освободить место"
        var unassignBtn = _root.Q<Button>("btn-unassign");
        if (unassignBtn != null) unassignBtn.clicked += Unassign;
    }

    public void Open(WorkplaceInteractable desk, GameObject player)
    {
        // Если на столе нет монитора - меню просто не откроется
        if (!desk.hasMonitor)
        {
            return;
        }

        _targetDesk = desk;
        _root.style.display = DisplayStyle.Flex;

        // Управление видимостью кнопки программно:
        var unassignBtn = _root.Q<Button>("btn-unassign");
        if (unassignBtn != null)
        {
            // Показываем кнопку только если на столе ЕСТЬ рабочий
            unassignBtn.style.display = desk.HasWorker ? DisplayStyle.Flex : DisplayStyle.None;
        }

        _cachedPlayerInput = player.GetComponent<PlayerInput>();
        if (_cachedPlayerInput != null) _cachedPlayerInput.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdateHintVisibility();
        RefreshUI();
    }

    private void RefreshUI()
    {
        _listContainer.Clear();
        var myWorkers = GameDataManager.Instance.myWorkers;

        foreach (var worker in myWorkers)
        {
            // 1. Создаем экземпляр шаблона
            var cardInstance = cardTemplate.Instantiate();

            // 2. ГЛАВНЫЙ МОМЕНТ: Берем самый первый элемент внутри префаба.
            // Это надежнее, чем искать по классу "worker-card"
            var card = cardInstance.ElementAt(0);

            if (card != null)
            {
                // 3. Заполняем данными
                FillCardData(card, worker);

                // 4. Добавляем в список
                _listContainer.Add(card);
            }
            else
            {
                Debug.LogError("Ошибка: Не удалось получить корневой элемент из cardTemplate!");
            }
        }
    }

    private void FillCardData(VisualElement card, WorkerInstance data)
    {
        if (data == null) return;

        // 1. Ищем элементы (используем твои имена из UXML)
        var nameLabel = card.Q<Label>("name-label");
        var idLabel = card.Q<Label>("id-label");
        var posLabel = card.Q<Label>("pos-label");
        var patienceLabel = card.Q<Label>("stat-patience");
        var powerLabel = card.Q<Label>("stat-power");
        var sleepLabel = card.Q<Label>("stat-sleep");
        var angerLabel = card.Q<Label>("stat-anger");
        var avatarBox = card.Q<VisualElement>("avatar");
        var btn = card.Q<Button>("action-btn");

        if (nameLabel != null) nameLabel.text = data.name;

        if (idLabel != null) idLabel.text = $"ID: {data.templateId}";

        if (posLabel != null) posLabel.text = data.currentPosition.ToString();

        if (patienceLabel != null) patienceLabel.text = data.patience.ToString();
        if (powerLabel != null) powerLabel.text = data.workPower.ToString();
        if (sleepLabel != null) sleepLabel.text = data.sleepiness.ToString();
        if (angerLabel != null) angerLabel.text = data.angriness.ToString();

        if (avatarBox != null && data.avatar != null)
            avatarBox.style.backgroundImage = new StyleBackground(data.avatar);

        if (btn != null)
        {
            btn.style.backgroundColor = new StyleColor(StyleKeyword.Null);

            if (data.isAssigned)
            {
                if (data.assignedWorkplaceId == _targetDesk.workplaceId)
                {
                    btn.text = "УЖЕ ТУТ";
                    btn.SetEnabled(false);
                    btn.style.opacity = 0.5f;
                }
                else
                {
                    btn.text = $"ЗА СТАНЦИЕЙ: {data.assignedWorkplaceId}";
                    btn.SetEnabled(true);
                    btn.style.backgroundColor = new StyleColor(new Color(0f, 0.6f, 0.8f, 0.8f));
                    btn.style.fontSize = 10;
                }
            }
            else
            {
                btn.text = "НАЗНАЧИТЬ";
                btn.SetEnabled(true);
                btn.style.opacity = 1.0f;
                btn.style.fontSize = 12;
                btn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.5f, 0.2f, 0.9f));
            }

            btn.clicked += () => {
                _targetDesk.AssignWorker(data);
                Close();
            };
        }
    }

    private void Unassign()
    {
        if (_targetDesk != null)
        {
            _targetDesk.AssignWorker(null); // Это само очистит и 3D модель, и статус рабочего
        }
        Close();
    }

    public void Close()
    {
        _root.style.display = DisplayStyle.None;
        if (_cachedPlayerInput != null) _cachedPlayerInput.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}