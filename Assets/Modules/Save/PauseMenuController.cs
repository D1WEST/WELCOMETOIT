using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Assets.Modules.Save
{
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private InputActionReference exitToggleAction;

        private VisualElement _root;
        private bool _isPaused = false;
        private PlayerInput _playerInput;

        private void Awake()
        {
            _root = uiDocument.rootVisualElement;
            _root.style.display = DisplayStyle.None;

            // Подписка на кнопки
            _root.Q<Button>("btn-resume").clicked += TogglePause;
            _root.Q<Button>("btn-restart").clicked += RestartShift;
            _root.Q<Button>("btn-main-menu").clicked += ExitToMainMenu;
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

            // Блокировка игрока и курсора
            if (_playerInput == null) _playerInput = FindFirstObjectByType<PlayerInput>();

            if (_playerInput != null) _playerInput.enabled = !_isPaused;

            Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _isPaused;

            // Опционально: останавливаем время в игре
            Time.timeScale = _isPaused ? 0f : 1f;
        }

        private void RestartShift()
        {
            Time.timeScale = 1f;
            // Используем наш механизм чекпоинтов
            GameDataManager.Instance.RestoreCheckpoint();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ExitToMainMenu()
        {
            Time.timeScale = 1f;
            // Сохраняем прогресс перед выходом
            GameDataManager.Instance.SaveGame(ShiftManager.Instance.currentDay);
            SceneManager.LoadScene(0); // Загружаем сцену с индексом 0 (меню)
        }
    }
}