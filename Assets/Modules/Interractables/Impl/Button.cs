using Assets.Modules.Interractables;
using UnityEngine;

namespace Assets.Modules.Interractables{
    public class Button : MonoBehaviour, IInteractable
    {
        [SerializeField] private string promptText = "Потянуть за рычаг";
        [SerializeField] private InteractionType interactionType = InteractionType.Click;
        [SerializeField] private float holdDuration = 1.0f;
        [SerializeField] private Transform interactionPivot;

        public string InteractionPrompt => promptText;
        public Transform InteractionPivot => interactionPivot;
        public InteractionType InteractionType => interactionType;
        public float HoldDuration => holdDuration;

        public void Interact(GameObject interactor)
        {
            Debug.Log("Действие выполнено!");
        }
    }
}