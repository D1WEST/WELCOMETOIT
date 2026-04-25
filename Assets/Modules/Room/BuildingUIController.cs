using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Modules.Room
{
    public class BuildingUIController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private List<RoomManager> rooms; // Список всех комнат в здании

        private VisualElement _roomListContainer;

        private void OnEnable()
        {
            var root = uiDocument.rootVisualElement;

            _roomListContainer = root.Q<VisualElement>("room-list");

            if (_roomListContainer == null)
            {
                Debug.LogError("BuildingUIController: Не найден элемент 'room-list' в UXML!");
                return;
            }

            InvokeRepeating(nameof(RefreshUI), 0f, 0.5f);
        }

        private void RefreshUI()
        {
            if (_roomListContainer == null) return;
            _roomListContainer.Clear();

            foreach (var room in rooms)
            {
                if (room == null) continue;

                if (!room.isOpened) continue;

                var (current, target, error) = room.GetProductivity();
                _roomListContainer.Add(CreateRoomRow(room.roomName, current, target, error));
            }
        }

        private VisualElement CreateRoomRow(string roomName, int current, int target, bool hasError)
        {
            var row = new VisualElement();
            row.AddToClassList("room-row");

            var nameLabel = new Label($"{roomName} : ");
            nameLabel.AddToClassList("room-name-label");
            row.Add(nameLabel);

            if (hasError)
            {
                var warnIcon = new Label("⚠️");
                warnIcon.AddToClassList("room-warning-icon");
                row.Add(warnIcon);
            }
            else
            {
                var valueLabel = new Label($"{current}/{target}");
                valueLabel.AddToClassList("room-value-label");

                float ratio = (target > 0) ? (float)current / target : 0;
                valueLabel.style.color = GetColorByRatio(ratio);

                row.Add(valueLabel);
            }

            return row;
        }

        private Color GetColorByRatio(float ratio)
        {
            // Твоя логика цветов:
            if (ratio >= 0.95f) return new Color(0.2f, 1f, 0.2f);
            if (ratio >= 0.75f) return Color.yellow;
            if (ratio >= 0.50f) return new Color(1f, 0.6f, 0f);
            return Color.red;
        }
    }
}