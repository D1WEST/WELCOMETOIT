using Assets.Modules.PlayerModule;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using PlayerInput = Assets.Modules.PlayerModule.PlayerInput;

namespace Assets.Modules.Save
{
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private InputActionReference exitToggleAction;

        private VisualElement _root;
        private bool _isPaused = false;
        private PlayerInput _playerInput;

        private VisualElement _mainButtonsBlock;
        private VisualElement _optionsBlock;

        private void Awake()
        {
            _root = uiDocument.rootVisualElement;
            // Прячем всё сразу
            _root.style.display = DisplayStyle.None;

            _mainButtonsBlock = _root.Q<VisualElement>("main-buttons");
            _optionsBlock = _root.Q<VisualElement>("options-view");

            // Подписка на кнопки (безопасная)
            SetupButton("btn-resume", TogglePause);
            SetupButton("btn-options", OpenOptions);
            SetupButton("btn-options-back", CloseOptions);
            SetupButton("btn-restart", RestartShift);
            SetupButton("btn-main-menu", ExitToMainMenu);
        }

        private void SetupButton(string name, System.Action callback)
        {
            var btn = _root.Q<Button>(name);
            if (btn != null) btn.clicked += callback;
        }

        private void SetupSliders()
        {
            if (GameDataManager.Instance == null) return;
            var settings = GameDataManager.Instance.playerSettings;

            // Слайдер Громкости
            var volSlider = _root.Q<Slider>("slider-volume");
            var volLabel = _root.Q<Label>("lbl-volume-val");
            if (volSlider != null)
            {
                volSlider.value = settings.volume;
                if (volLabel != null) volLabel.text = settings.volume.ToString("F1"); // Формат 0.0

                volSlider.RegisterValueChangedCallback(evt => {
                    settings.volume = evt.newValue;
                    if (volLabel != null) volLabel.text = evt.newValue.ToString("F1");
                    AudioManager.Instance.UpdateLiveVolume(evt.newValue);
                });
            }

            // Слайдер Сенсы
            var sensSlider = _root.Q<Slider>("slider-sens");
            var sensLabel = _root.Q<Label>("lbl-sens-val");
            if (sensSlider != null)
            {
                sensSlider.value = settings.sensitivity;
                if (sensLabel != null) sensLabel.text = settings.sensitivity.ToString("F1");

                sensSlider.RegisterValueChangedCallback(evt => {
                    settings.sensitivity = evt.newValue;
                    if (sensLabel != null) sensLabel.text = evt.newValue.ToString("F1");
                    GameDataManager.Instance.ApplySettings();
                });
            }
        }

        private void OpenOptions()
        {
            SetupSliders(); // Обновляем значения перед показом
            if (_mainButtonsBlock != null) _mainButtonsBlock.style.display = DisplayStyle.None;
            if (_optionsBlock != null) _optionsBlock.style.display = DisplayStyle.Flex;
        }

        private void CloseOptions()
        {
            if (_mainButtonsBlock != null) _mainButtonsBlock.style.display = DisplayStyle.Flex;
            if (_optionsBlock != null) _optionsBlock.style.display = DisplayStyle.None;
            GameDataManager.Instance.SaveGame(ShiftManager.Instance.currentDay);
        }

        private void OnEnable()
        {
            exitToggleAction.action.Enable();
            exitToggleAction.action.performed += OnExitPressed;
        }

        private void OnDisable()
        {
            exitToggleAction.action.performed -= OnExitPressed;
        }

        private void OnExitPressed(InputAction.CallbackContext context)
        {
            TogglePause();
        }

        public void TogglePause()
        {
            _isPaused = !_isPaused;
            _root.style.display = _isPaused ? DisplayStyle.Flex : DisplayStyle.None;

            // Если закрываем паузу, всегда возвращаемся к главным кнопкам
            if (!_isPaused) CloseOptions();

            if (_playerInput == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) _playerInput = playerObj.GetComponent<PlayerInput>();
            }

            if (_playerInput != null)
            {
                _playerInput.enabled = !_isPaused;

                if (_playerInput.TryGetComponent<PlayerLocomotion>(out var locomotion))
                    locomotion.enabled = !_isPaused;

                if (_playerInput.TryGetComponent<PlayerCameraService>(out var cameraService))
                    cameraService.StopCameraInertia();
            }

            Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _isPaused;

            Time.timeScale = _isPaused ? 0f : 1f;
        }

        private void RestartShift()
        {
            Time.timeScale = 1f;
            GameDataManager.Instance.RestoreCheckpoint();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ExitToMainMenu()
        {
            Time.timeScale = 1f;
            GameDataManager.Instance.SaveGame(ShiftManager.Instance.currentDay);
            SceneManager.LoadScene(0);
        }
    }
}