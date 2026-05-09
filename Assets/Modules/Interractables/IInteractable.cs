using UnityEngine;

namespace Assets.Modules.Interractables
{
    public enum InteractionType { Click, Hold }

    public interface IInteractable
    {
        string InteractionPrompt { get; }
        Transform InteractionPivot { get; }
        InteractionType InteractionType { get; }
        float HoldDuration { get; } // Сколько секунд держать
        void Interact(GameObject interactor);

        void OnHoverEnter();
        void OnHoverExit();
    }
}