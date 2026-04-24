using UnityEngine;

namespace Assets.Modules.Interractables.Impl
{
    public class Button : MonoBehaviour, IInteractable // Напрямую от интерфейса
    {
        public string InteractionPrompt => "Сесть";

        public void Interact(GameObject interactor)
        {
            Debug.Log($"Interacted with {gameObject.name}");
        }
    }
}
