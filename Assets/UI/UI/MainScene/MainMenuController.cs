using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // Добавлено для загрузки сцен

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private UIDocument _uiDocument;
    [SerializeField] private InputActionAsset _inputActions;

    private VisualElement _mainMenu;
    private VisualElement _optionsMenu;
    private VisualElement _rebindList;

    void OnEnable()
    {
        var root = _uiDocument.rootVisualElement;

        _mainMenu = root.Q<VisualElement>("main-menu");
        _optionsMenu = root.Q<VisualElement>("options-menu");
        _rebindList = root.Q<VisualElement>("rebind-list");

        // --- Кнопки главного меню ---
        root.Q<Button>("btn-play").clicked += OnPlayClicked; // Новая кнопка "Играть"
        root.Q<Button>("btn-options").clicked += OpenOptions;
        root.Q<Button>("btn-exit").clicked += OnExitClicked; // Выход из игры

        // --- Кнопки настроек ---
        root.Q<Button>("btn-back").clicked += CloseOptions;
        root.Q<Button>("btn-save").clicked += SaveBindings;
        root.Q<Button>("btn-reset").clicked += ResetBindings;

        LoadBindings();
    }

    // ==========================================
    // ЛОГИКА ГЛАВНОГО МЕНЮ
    // ==========================================

    private void OnPlayClicked()
    {
        Debug.Log("Загрузка Сцены 1...");
        // Загружает сцену с индексом 1 (убедитесь, что она есть в Build Settings)
        SceneManager.LoadScene(1);
    }

    private void OnExitClicked()
    {
        Debug.Log("Выход из игры...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ==========================================
    // ЛОГИКА НАСТРОЕК (ВАШ КОД)
    // ==========================================

    void OpenOptions()
    {
        _mainMenu.style.display = DisplayStyle.None;
        _optionsMenu.style.display = DisplayStyle.Flex;
        RefreshRebindUI();
    }

    void CloseOptions()
    {
        _mainMenu.style.display = DisplayStyle.Flex;
        _optionsMenu.style.display = DisplayStyle.None;
    }

    void RefreshRebindUI()
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

    void CreateRebindRow(InputAction action, int bindingIndex, string displayName)
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

    void StartRebind(InputAction action, int bindingIndex, Button btn)
    {
        btn.text = "WAITING...";
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

    void SaveBindings()
    {
        PlayerPrefs.SetString("rebinds", _inputActions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
        Debug.Log("Настройки управления сохранены!");
    }

    void LoadBindings()
    {
        string rebinds = PlayerPrefs.GetString("rebinds");
        if (!string.IsNullOrEmpty(rebinds))
            _inputActions.LoadBindingOverridesFromJson(rebinds);
    }

    void ResetBindings()
    {
        _inputActions.RemoveAllBindingOverrides();
        RefreshRebindUI();
    }
}