using Assets.Modules.Interractables;
using UnityEngine;

public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionPrompt;
    public string InteractionPrompt => interactionPrompt;
    [SerializeField] private Transform interactionPivot;
    public Transform InteractionPivot => interactionPivot;

    // virtual позволяет переопределить метод, но иметь базовую логику
    public virtual void Interact(GameObject interactor)
    {
        Debug.Log($"Interacted with {gameObject.name}");
    }
}