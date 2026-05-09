using Assets.Modules.Perks;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Modules.Interractables.Impl
{
    public class PerkShopInteractable : MonoBehaviour, IInteractable
    {
        [Header("Settings")]
        [SerializeField] private string prompt = "Улучшения начальника";
        [SerializeField] private Transform interactionPivot;

        [Header("UI Link")]
        [SerializeField] private PerkShopController shopController;

        // Реализация интерфейса IInteractable
        public string InteractionPrompt => $"{prompt} [E]";
        public Transform InteractionPivot => interactionPivot;
        public InteractionType InteractionType => InteractionType.Click;
        public float HoldDuration => 0;
        private Outline _outline;


        public void OnHoverEnter()
        {
            if (_outline != null) _outline.enabled = true;
        }

        public void OnHoverExit()
        {
            if (_outline != null) _outline.enabled = false;
        }

        private void Start()
        {
            // Кэшируем компонент один раз при старте
            _outline = GetComponent<Outline>();

            // На всякий случай гарантируем, что он выключен
            if (_outline != null) _outline.enabled = false;
        }

        public void Interact(GameObject interactor)
        {
            // Открываем интерфейс магазина
            if (shopController != null)
            {
                shopController.Open(interactor);
            }
        }
    }
}