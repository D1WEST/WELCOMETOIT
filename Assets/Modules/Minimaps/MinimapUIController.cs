using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Modules.Minimaps
{
    public class MinimapUIController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private RenderTexture minimapTexture;
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private List<RoomManager> rooms;

        private VisualElement _labelsContainer;
        private Dictionary<RoomManager, Label> _roomLabels = new Dictionary<RoomManager, Label>();

        private void OnEnable()
        {
            var root = uiDocument.rootVisualElement;
            var minimapView = root.Q<VisualElement>("minimap-view");
            _labelsContainer = root.Q<VisualElement>("labels-container");

            // Устанавливаем текстуру
            minimapView.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(minimapTexture));

            // Включаем зеленую отладку, чтобы видеть границы контейнера
            _labelsContainer.style.backgroundColor = new Color(0, 1, 0, 0f);

            CreateLabels();
        }

        private void CreateLabels()
        {
            _labelsContainer.Clear();
            _roomLabels.Clear();

            foreach (var room in rooms)
            {
                if (room == null) continue;

                var label = new Label(room.roomName);
                label.style.position = Position.Absolute;
                label.style.color = Color.white;
                label.style.backgroundColor = Color.gray;
                label.style.fontSize = 14;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;

                // Фиксируем размер, чтобы не гадать
                label.style.width = 50;
                label.style.height = 20;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;

                _labelsContainer.Add(label);
                _roomLabels.Add(room, label);
            }
        }

        private void LateUpdate()
        {
            if (minimapCamera == null) return;

            foreach (var pair in _roomLabels)
            {
                RoomManager room = pair.Key;
                Label label = pair.Value;

                // ДЛЯ ТЕСТА: Отключаем проверку isOpened. 
                // Если плашки появятся - значит проблема была в том, что room.isOpened == false
                // label.style.display = room.isOpened ? DisplayStyle.Flex : DisplayStyle.None;

                // 1. Используем ViewportPoint. Это самый надежный способ.
                // Получаем координаты от 0 до 1 относительно обзора камеры миникарты
                Vector3 viewportPos = minimapCamera.WorldToViewportPoint(room.transform.position);

                // Проверяем, находится ли комната в поле зрения камеры
                bool isInView = viewportPos.z > 0 && viewportPos.x >= 0 && viewportPos.x <= 1 && viewportPos.y >= 0 && viewportPos.y <= 1;

                if (isInView)
                {
                    label.style.display = DisplayStyle.Flex;

                    // 2. Координаты в UI Toolkit идут сверху вниз (Y инвертирован относительно Viewport)
                    // Умножаем на размер контейнера (250x250)
                    float x = viewportPos.x * _labelsContainer.layout.width;
                    float y = (1 - viewportPos.y) * _labelsContainer.layout.height;

                    // 3. Устанавливаем позицию (центрируем лейбл 50x20)
                    label.style.left = x - 25;
                    label.style.top = y - 10;
                }
                else
                {
                    label.style.display = DisplayStyle.None;
                }
            }
        }
    }
}