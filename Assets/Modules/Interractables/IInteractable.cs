using UnityEngine;

namespace Assets.Modules.Interractables
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        void Interact(GameObject interactor);
    }
}
