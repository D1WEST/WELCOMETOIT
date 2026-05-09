using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.Modules.Interractables.Impl
{
    public class ShiftStartButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform pivot;
        public string InteractionPrompt => ShiftManager.Instance.IsShiftActive ? "Смена уже идет" : "Начать смену";
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
            AudioManager.Instance.PlayAudio(
                AudioQuery.ByKey("StartShift").RandomSound()
            ).Forget();
            if (!ShiftManager.Instance.IsShiftActive)
            {
                ShiftManager.Instance.StartShift();
            }
        }
    }
}