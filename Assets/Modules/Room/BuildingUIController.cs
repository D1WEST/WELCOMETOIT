using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Modules.Room
{
    public class BuildingUIController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private List<RoomManager> rooms;

        private VisualElement _roomListContainer;

        private void OnEnable()
        {
            var root = uiDocument.rootVisualElement;
            _roomListContainer = root.Q<VisualElement>("room-list");
            InvokeRepeating(nameof(RefreshUI), 0f, 0.5f); // Обновляем раз в полсекунды
        }

        private void RefreshUI()
        {
            _roomListContainer.Clear();

            foreach (var room in rooms)
            {
                var (current, target) = room.GetProductivity();
                _roomListContainer.Add(CreateRoomRow(room.roomName, current, target));
            }
        }

        private VisualElement CreateRoomRow(string name, int current, int target)
        {
            var row = new VisualElement();
            row.AddToClassList("room-row");

            // Название комнаты
            var nameLabel = new Label($"{name} :");
            nameLabel.AddToClassList("room-name-label");
            row.Add(nameLabel);

            // Логика отображения значения или варнинга
            if (target == 0 || current == 0)
            {
                var warn = new Label("⚠️");
                warn.AddToClassList("room-warning-icon");
                row.Add(warn);
            }
            else
            {
                float ratio = (float)current / target;
                var valueLabel = new Label($"{current}/{target}");
                valueLabel.AddToClassList("room-value-label");

                // Красим текст в зависимости от эффективности
                valueLabel.style.color = GetColorByRatio(ratio);
                row.Add(valueLabel);
            }

            return row;
        }

        private Color GetColorByRatio(float ratio)
        {
            if (ratio >= 0.95f) return new Color(0.2f, 1f, 0.2f); // Зеленый
            if (ratio >= 0.75f) return Color.yellow;
            if (ratio >= 0.5f) return new Color(1f, 0.6f, 0f); // Оранжевый
            return Color.red;
        }
    }
}