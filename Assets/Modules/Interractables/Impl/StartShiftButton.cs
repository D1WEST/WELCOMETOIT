using Assets.Modules.Interractables;
using Assets.Modules.Shift;
using UnityEngine;

public class StartShiftButton : MonoBehaviour, IInteractable
{
    public string InteractionPrompt => "Начать рабочую смену";
    public Transform InteractionPivot => transform; // или твой пивот
    public InteractionType InteractionType => InteractionType.Click;
    public float HoldDuration => 0;

    public void Interact(GameObject interactor)
    {
        ShiftManager.Instance.StartShift();
    }
}