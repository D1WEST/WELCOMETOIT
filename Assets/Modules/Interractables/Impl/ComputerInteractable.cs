using Assets.Modules.NPC;
using UnityEngine;
using UnityEngine.UI;

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
        private Outline _outline;

        private void Start()
        {
            // Кэшируем компонент один раз при старте
            _outline = GetComponent<Outline>();

            // На всякий случай гарантируем, что он выключен
            if (_outline != null) _outline.enabled = false;
        }

        public void OnHoverEnter()
        {
            if (_outline != null) _outline.enabled = true;
        }

        public void OnHoverExit()
        {
            if (_outline != null) _outline.enabled = false;
        }
        public void Interact(GameObject interactor)
        {
            if (uiController != null)
            {
                uiController.Open(interactor);
            }
        }
    }
}