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

        // ПЕРЕМЕННЫЕ СОСТОЯНИЯ (Для восстановления после паузы)
        private CursorLockMode _lastLockMode = CursorLockMode.Locked;
        private bool _lastCursorVisible = false;

        private bool _wasInputEnabled = true;
        private bool _wasLocomotionEnabled = true;
        private bool _wasCameraEnabled = true;

        private void Awake()
        {
            _root = uiDocument.rootVisualElement;
            _root.style.display = DisplayStyle.None;

            _mainButtonsBlock = _root.Q<VisualElement>("main-buttons");
            _optionsBlock = _root.Q<VisualElement>("options-view");

            SetupButton("btn-resume", TogglePause);
            SetupButton("btn-options", OpenOptions);
            SetupButton("btn-options-back", CloseOptions);
            SetupButton("btn-restart", RestartShift);
            SetupButton("btn-main-menu", ExitToMainMenu);
        }

        private void SetupButton(string name, System.Action callback)
        {
            if (_root == null) return;
            var btn = _root.Q<Button>(name);
            if (btn != null) btn.clicked += callback;
        }

        private void SetupSliders()
        {
            if (GameDataManager.Instance == null) return;
            var settings = GameDataManager.Instance.playerSettings;

            var volSlider = _root.Q<Slider>("slider-volume");
            var volLabel = _root.Q<Label>("lbl-volume-val");
            if (volSlider != null)
            {
                volSlider.value = settings.volume;
                if (volLabel != null) volLabel.text = settings.volume.ToString("F1");
                volSlider.RegisterValueChangedCallback(evt => {
                    settings.volume = evt.newValue;
                    if (volLabel != null) volLabel.text = evt.newValue.ToString("F1");
                    AudioManager.Instance.UpdateLiveVolume(evt.newValue);
                });
            }

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
            SetupSliders();
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

        private void OnDisable() => exitToggleAction.action.performed -= OnExitPressed;

        private void OnExitPressed(InputAction.CallbackContext context) => TogglePause();

        public void TogglePause()
        {
            _isPaused = !_isPaused;
            _root.style.display = _isPaused ? DisplayStyle.Flex : DisplayStyle.None;

            if (!_isPaused) CloseOptions();

            if (_playerInput == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) _playerInput = playerObj.GetComponent<PlayerInput>();
            }

            if (_isPaused)
            {
                // --- ЗАХВАТ СОСТОЯНИЯ ---
                _lastLockMode = Cursor.lockState;
                _lastCursorVisible = Cursor.visible;

                if (_playerInput != null)
                {
                    // Запоминаем каждый скрипт отдельно
                    _wasInputEnabled = _playerInput.enabled;

                    if (_playerInput.TryGetComponent<PlayerLocomotion>(out var locomotion))
                    {
                        _wasLocomotionEnabled = locomotion.enabled;
                        locomotion.enabled = false; // Выключаем на время паузы
                    }

                    if (_playerInput.TryGetComponent<PlayerCameraService>(out var cameraService))
                    {
                        _wasCameraEnabled = cameraService.enabled;
                        cameraService.StopCameraInertia();
                        cameraService.enabled = false; // Выключаем на время паузы
                    }

                    _playerInput.enabled = false;
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Time.timeScale = 0f;
            }
            else
            {
                // --- ВОССТАНОВЛЕНИЕ СОСТОЯНИЯ ---
                CloseOptions();

                if (_playerInput != null)
                {
                    // Возвращаем каждому скрипту ТО состояние, которое было ДО Esc
                    _playerInput.enabled = _wasInputEnabled;

                    if (_playerInput.TryGetComponent<PlayerLocomotion>(out var locomotion))
                        locomotion.enabled = _wasLocomotionEnabled;

                    if (_playerInput.TryGetComponent<PlayerCameraService>(out var cameraService))
                        cameraService.enabled = _wasCameraEnabled;
                }

                Cursor.lockState = _lastLockMode;
                Cursor.visible = _lastCursorVisible;

                Time.timeScale = 1f;
            }
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