using Assets.Modules.Interractables.Impl;
using Assets.Modules.NPC;
using Assets.Modules.PlayerModule;
using Assets.Modules.Save;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public class WorkplaceUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset cardTemplate; // Твой WorkerCard.uxml

    private VisualElement _root;
    private ScrollView _listContainer;
    private WorkplaceInteractable _targetDesk;
    private PlayerInput _cachedPlayer;

    private void OnEnable()
    {
        _root = uiDocument.rootVisualElement;
        _root.style.display = DisplayStyle.None;

        _listContainer = _root.Q<ScrollView>("worker-list");
        _root.Q<Button>("btn-close").clicked += Close;

        // Кнопка очистки места
        var unassignBtn = _root.Q<Button>("btn-unassign");
        unassignBtn.clicked += () => {
            _targetDesk.AssignWorker(null);
            Close();
        };
    }

    public void Open(WorkplaceInteractable desk, GameObject player)
    {
        _targetDesk = desk;
        _root.style.display = DisplayStyle.Flex;

        // Блокируем игрока
        _cachedPlayer = player.GetComponent<PlayerInput>();
        if (_cachedPlayer != null) _cachedPlayer.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Показываем кнопку "Освободить", если кто-то уже сидит
        _root.Q<Button>("btn-unassign").style.display = desk.HasWorker ? DisplayStyle.Flex : DisplayStyle.None;

        RefreshUI();
    }

    private void RefreshUI()
    {
        // Используем уже найденный в OnEnable контейнер
        _listContainer.Clear();

        // Если на столе никто не работает, выходим (или показываем пустую плашку)
        if (_targetDesk.Worker == null) return;

        // 1. Клонируем шаблон карточки
        // ВАЖНО: CloneTree возвращает TemplateContainer. 
        // Чтобы поиск .Q работал корректно, лучше брать первый элемент.
        VisualElement card = cardTemplate.CloneTree().ElementAt(0);

        // 2. Заполняем данными
        FillCard(card, _targetDesk.Worker);

        // 3. Добавляем в список
        _listContainer.Add(card);
    }

    private void FillCard(VisualElement card, WorkerInstance data)
    {
        if (data == null) return;

        // Сверяем имена с твоим UXML!
        var nameLabel = card.Q<Label>("name-label"); // Было worker-name
        var idLabel = card.Q<Label>("id-label");
        var posLabel = card.Q<Label>("pos-label");

        // Статы
        var patienceLabel = card.Q<Label>("stat-patience");
        var powerLabel = card.Q<Label>("stat-power");
        var sleepLabel = card.Q<Label>("stat-sleep");
        var angerLabel = card.Q<Label>("stat-anger");

        // Заполняем (предполагаю поля в твоем WorkerInstance)
        if (nameLabel != null) nameLabel.text = data.name;
        if (idLabel != null) idLabel.text = $"ID: {data.instanceId}"; // если есть id

        // Если у тебя есть данные по статам, заполняем их так:
        if (patienceLabel != null) patienceLabel.text = data.patience.ToString();
        if (powerLabel != null) powerLabel.text = data.workPower.ToString();
        if (sleepLabel != null) sleepLabel.text = data.sleepiness.ToString();
        if (angerLabel != null) angerLabel.text = data.angriness.ToString();

        // Обработка кнопки внутри карточки
        var actionBtn = card.Q<Button>("action-btn");
        if (actionBtn != null)
        {
            actionBtn.text = "Уволить"; // Например
            actionBtn.clicked += () => {
                Debug.Log($"Действие с рабочим {data.name}");
                // Тут твоя логика
            };
        }
    }

    public void Close()
    {
        _root.style.display = DisplayStyle.None;
        if (_cachedPlayer != null) _cachedPlayer.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}