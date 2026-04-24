using UnityEngine;
using System.Collections.Generic;
using Assets.Modules.Interractables;

namespace Assets.Modules.Interractables.Impl
{
    public class LightButton : MonoBehaviour, IInteractable
    {
        [Header("Settings")]
        [SerializeField] private string actionName = "свет";
        [SerializeField] private bool isOn = false;
        [SerializeField] private InteractionType type = InteractionType.Click;
        [SerializeField] private float holdDuration = 1.0f;

        [Header("Dependencies")]
        [SerializeField] private Transform interactionPivot;
        [Tooltip("Список объектов (Light или просто GameObject), которые будут включаться/выключаться")]
        [SerializeField] private List<GameObject> targetLights = new List<GameObject>();

        // Реализация интерфейса
        // Динамически меняем текст в зависимости от состояния
        public string InteractionPrompt => isOn ? $"Выключить {actionName}" : $"Включить {actionName}";

        public Transform InteractionPivot => interactionPivot;
        public InteractionType InteractionType => type;
        public float HoldDuration => holdDuration; // Если захотите сделать Hold, можно настроить

        private void Start()
        {
            // При старте синхронизируем состояние объектов с переменной isOn
            ApplyState();
        }

        public void Interact(GameObject interactor)
        {
            // Инвертируем состояние
            isOn = !isOn;

            // Применяем состояние ко всем объектам в списке
            ApplyState();

            // Лог для проверки
            Debug.Log($"[LightButton] {gameObject.name} теперь {(isOn ? "Включен" : "Выключен")}");
        }

        private void ApplyState()
        {
            if (targetLights == null) return;

            foreach (var lightObj in targetLights)
            {
                if (lightObj != null)
                {
                    lightObj.SetActive(isOn);
                }
            }
        }
    }
}