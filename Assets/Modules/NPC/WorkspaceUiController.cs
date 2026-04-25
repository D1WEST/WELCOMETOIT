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

        // Безопасно ищем элементы. Если имя в UXML не совпадет, переменная будет null
        var nameLabel = card.Q<Label>("name-label");
        var idLabel = card.Q<Label>("id-label");
        var posLabel = card.Q<Label>("pos-label");
        var patienceLabel = card.Q<Label>("stat-patience");
        var powerLabel = card.Q<Label>("stat-power");
        var sleepLabel = card.Q<Label>("stat-sleep");
        var angerLabel = card.Q<Label>("stat-anger");
        var avatarBox = card.Q<VisualElement>("avatar");

        // ПРИСВАИВАЕМ ЗНАЧЕНИЯ ТОЛЬКО ЕСЛИ ЭЛЕМЕНТЫ НАЙДЕНЫ
        if (nameLabel != null) nameLabel.text = data.name;
        else Debug.LogWarning("FillCardData: Не найден элемент 'name-label'");

        if (idLabel != null) idLabel.text = $"ID: {data.templateId}";

        if (posLabel != null) posLabel.text = data.currentPosition.ToString();

        if (patienceLabel != null) patienceLabel.text = data.patience.ToString();
        if (powerLabel != null) powerLabel.text = data.workPower.ToString();
        if (sleepLabel != null) sleepLabel.text = data.sleepiness.ToString();
        if (angerLabel != null) angerLabel.text = data.angriness.ToString();

        if (avatarBox != null && data.avatar != null)
            avatarBox.style.backgroundImage = new StyleBackground(data.avatar);

        if (idLabel != null)
        {
            string workId = string.IsNullOrEmpty(data.assignedWorkplaceId) ? "ОТСУТСТВУЕТ" : data.assignedWorkplaceId;
            idLabel.text = $"ID: {data.templateId} | МЕСТО: {workId}";
        }

        var btn = card.Q<Button>("action-btn");
        if (btn != null)
        {
            // Если рабочий уже назначен на какой-то ДРУГОЙ стол
            if (data.isAssigned)
            {
                var currentDesk = WorkplaceInteractable.FindDeskByWorker(data);
                // Если он сидит именно за ЭТИМ столом, который мы открыли
                if (currentDesk == _targetDesk)
                {
                    btn.text = "УЖЕ ТУТ";
                    btn.SetEnabled(false); // Нельзя назначить на то же самое место
                }
                else
                {
                    btn.text = "ПЕРЕВЕСТИ";
                    btn.style.backgroundColor = new StyleColor(Color.cyan);
                    btn.SetEnabled(true);
                }
            }
            else
            {
                btn.text = "НАЗНАЧИТЬ";
                btn.SetEnabled(true);
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