using UnityEngine;

namespace Assets.Modules.Interractables.Impl
{
    public class Button : MonoBehaviour, IInteractable // Напрямую от интерфейса
    {
        [SerializeField] private string promptText = "Нажать на кнопку";
        [SerializeField] private Transform interactionPivot;

        public string InteractionPrompt => promptText; public 
            Transform InteractionPivot => interactionPivot;

        public void Interact(GameObject interactor)
        {
            Debug.Log($"Interacted with {gameObject.name}");
        }
    }
}
