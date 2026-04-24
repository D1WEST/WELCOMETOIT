using Assets.Modules.NPC;
using UnityEngine;

namespace Assets.Modules.Interractables.Impl
{
    public class ComputerInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Открыть компьютер";
        [SerializeField] private Transform pivot;
        [SerializeField] private ComputerUIController uiController;

        public string InteractionPrompt => prompt;
        public Transform InteractionPivot => pivot;
        public InteractionType InteractionType => InteractionType.Click;
        public float HoldDuration => 0;

        public void Interact(GameObject interactor)
        {
            if (uiController != null)
            {
                uiController.Open(interactor);
            }
        }
    }
}