using Assets.Modules.Audio;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.Modules.Interractables.Impl
{
    public class ShiftStartButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform pivot;

        // Цвета для разных состояний
        [SerializeField] private Color readyColor = Color.cyan; // Цвет, пока ждем старта
        [SerializeField] private Color hoverColor = Color.white; // Цвет при наведении во время смены

        public string InteractionPrompt => ShiftManager.Instance.IsShiftActive ? "Смена уже идет" : "Начать смену [E]";
        public Transform InteractionPivot => pivot;
        public InteractionType InteractionType => InteractionType.Click;
        public float HoldDuration => 0;

        private Outline _outline;

        private void Start()
        {
            _outline = GetComponent<Outline>();

            // Настраиваем базовый вид обводки
            if (_outline != null)
            {
                _outline.OutlineWidth = 5f;
            }
        }

        private void Update()
        {
            if (_outline == null) return;

            // ГЛАВНАЯ ЛОГИКА МАЯКА
            if (!ShiftManager.Instance.IsShiftActive)
            {
                // Если смена НЕ идет — светимся ВСЕГДА и СКВОЗЬ СТЕНЫ
                _outline.enabled = true;
                _outline.OutlineMode = Outline.Mode.OutlineAll; // Рентген
                _outline.OutlineColor = readyColor;
            }
            else
            {
                // Если смена ИДЕТ — переходим в обычный режим (не светим сквозь стены)
                // Но только если мы не навели на кнопку прицел (чтобы OnHover работал)
                if (_outline.OutlineMode == Outline.Mode.OutlineAll)
                {
                    _outline.OutlineMode = Outline.Mode.OutlineVisible;
                    _outline.enabled = false; // Выключаем постоянное свечение
                }
            }
        }

        public void OnHoverEnter()
        {
            if (_outline == null) return;

            // Если смена идет, включаем обычную подсветку при наведении
            if (ShiftManager.Instance.IsShiftActive)
            {
                _outline.enabled = true;
                _outline.OutlineColor = hoverColor;
                _outline.OutlineMode = Outline.Mode.OutlineVisible;
            }
            else
            {
                // Если смена не идет, просто делаем линию толще при наведении
                _outline.OutlineWidth = 8f;
            }
        }

        public void OnHoverExit()
        {
            if (_outline == null) return;

            if (ShiftManager.Instance.IsShiftActive)
            {
                _outline.enabled = false;
            }
            else
            {
                // Возвращаем стандартную толщину маяка
                _outline.OutlineWidth = 5f;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (!ShiftManager.Instance.IsShiftActive)
            {
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("StartGame").ByIndex(0).RandomSound()
                ).Forget();

                ShiftManager.Instance.StartShift();

                // Мгновенно меняем режим после нажатия
                if (_outline != null)
                {
                    _outline.OutlineMode = Outline.Mode.OutlineVisible;
                    _outline.enabled = false;
                }
            }
        }
    }
}