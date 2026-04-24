using UnityEngine;

namespace Assets.Modules.Interractables
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        Transform InteractionPivot { get; }
        void Interact(GameObject interactor);
    }
}
