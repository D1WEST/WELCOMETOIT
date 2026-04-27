using Assets.Modules.Audio;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks; // Необходимо для работы с файлами
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument _uiDocument;
    [SerializeField] private InputActionAsset _inputActions;

    private VisualElement _mainMenu;
    private VisualElement _optionsMenu;
    private VisualElement _rebindList;


    private void Start()
    {
        AudioManager.Instance.PlayAudio(AudioQuery.ByKey("Music").ByIndex(0).WithVolume(0.06f).Cycle()).Forget();
    }

    // Путь к файлу сохранения — должен быть таким же, как в GameDataManager
    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    private void OnEnable()
    {
        var root = _uiDocument.rootVisualElement;

        // Кэшируем контейнеры
        _mainMenu = root.Q<VisualElement>("main-menu");
        _optionsMenu = root.Q<VisualElement>("options-menu");
        _rebindList = root.Q<VisualElement>("rebind-list");

        // --- Кнопки главного меню ---
        var btnContinue = root.Q<Button>("btn-continue");
        var btnNewGame = root.Q<Button>("btn-new-game");
        var btnOptions = root.Q<Button>("btn-options");
        var btnExit = root.Q<Button>("btn-exit");

        if (btnContinue != null) btnContinue.clicked += OnContinueClicked;
        if (btnNewGame != null) btnNewGame.clicked += OnNewGameClicked;
        if (btnOptions != null) btnOptions.clicked += OpenOptions;
        if (btnExit != null) btnExit.clicked += OnExitClicked;

        // --- Кнопки настроек ---
        var btnBack = root.Q<Button>("btn-back");
        var btnSave = root.Q<Button>("btn-save");
        var btnReset = root.Q<Button>("btn-reset");

        if (btnBack != null) btnBack.clicked += CloseOptions;
        if (btnSave != null) btnSave.clicked += SaveBindings;
        if (btnReset != null) btnReset.clicked += ResetBindings;

        // --- Проверка сохранения при запуске ---
        CheckSaveFile(btnContinue, root.Q<VisualElement>("wrapper-continue"));

        LoadBindings();
    }

    private void CheckSaveFile(Button continueBtn, VisualElement wrapper)
    {
        if (continueBtn == null) return;

        if (File.Exists(SavePath))
        {
            // Сохранение есть: кнопка активна
            continueBtn.SetEnabled(true);
            continueBtn.style.opacity = 1.0f;
            if (wrapper != null) wrapper.pickingMode = PickingMode.Position;
        }
        else
        {
            // Сохранения нет: выключаем кнопку
            continueBtn.SetEnabled(false);
            continueBtn.style.opacity = 0.3f;
            // Чтобы не срабатывал hover эффект на враппере
            if (wrapper != null) wrapper.pickingMode = PickingMode.Ignore;
        }
    }

    // ==========================================
    // ЛОГИКА ПЕРЕХОДОВ
    // ==========================================

    private void OnContinueClicked()
    {
        StartGame();
    }

    private void OnNewGameClicked()
    {

        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        // Очищаем другие данные, если нужно (например, текущий день в PlayerPrefs)
        // PlayerPrefs.DeleteKey("CurrentDay");

        StartGame();
    }

    private void StartGame()
    {
        // Используем твой GlobalSceneLoader, если он есть
        if (GlobalSceneLoader.Instance != null)
        {
            GlobalSceneLoader.Instance.LoadSceneAsync(1);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(1);
        }
    }

    private void OnExitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ==========================================
    // ЛОГИКА НАСТРОЕК
    // ==========================================

    private void OpenOptions()
    {
        _mainMenu.style.display = DisplayStyle.None;
        _optionsMenu.style.display = DisplayStyle.Flex;
        RefreshRebindUI();
    }

    private void CloseOptions()
    {
        _mainMenu.style.display = DisplayStyle.Flex;
        _optionsMenu.style.display = DisplayStyle.None;
    }

    private void RefreshRebindUI()
    {
        _rebindList.Clear();

        foreach (var map in _inputActions.actionMaps)
        {
            Label header = new Label(map.name.Replace("Actions", "").ToUpper());
            header.AddToClassList("group-header");
            _rebindList.Add(header);

            foreach (var action in map.actions)
            {
                if (action.bindings.Count > 1 && action.bindings[0].isComposite)
                {
                    for (int i = 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; i++)
                    {
                        CreateRebindRow(action, i, $"{action.name} {action.bindings[i].name}");
                    }
                }
                else
                {
                    CreateRebindRow(action, 0, action.name);
                }
            }
        }
    }

    private void CreateRebindRow(InputAction action, int bindingIndex, string displayName)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("rebind-row");

        Label label = new Label(displayName.ToUpper());
        label.AddToClassList("rebind-label");

        Button btn = new Button();
        btn.AddToClassList("rebind-button");
        btn.text = action.GetBindingDisplayString(bindingIndex);
        btn.clicked += () => StartRebind(action, bindingIndex, btn);

        row.Add(label);
        row.Add(btn);
        _rebindList.Add(row);
    }

    private void StartRebind(InputAction action, int bindingIndex, Button btn)
    {
        btn.text = "ЖДЕМ КЛАВИШУ...";
        action.Disable();

        var rebind = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Gamepad>")
            .WithControlsExcluding("Scroll")
            .WithControlsExcluding("<Mouse>/delta")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op => {
                btn.text = action.GetBindingDisplayString(bindingIndex);
                op.Dispose();
                action.Enable();
            })
            .OnCancel(op => {
                btn.text = action.GetBindingDisplayString(bindingIndex);
                op.Dispose();
                action.Enable();
            });

        rebind.Start();
    }

    private void SaveBindings()
    {
        PlayerPrefs.SetString("rebinds", _inputActions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    private void LoadBindings()
    {
        string rebinds = PlayerPrefs.GetString("rebinds");
        if (!string.IsNullOrEmpty(rebinds))
            _inputActions.LoadBindingOverridesFromJson(rebinds);
    }

    private void ResetBindings()
    {
        _inputActions.RemoveAllBindingOverrides();
        RefreshRebindUI();
    }
}