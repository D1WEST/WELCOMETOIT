using Assets.Modules.Perks;
using UnityEngine;

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