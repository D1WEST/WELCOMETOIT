using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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

        // Кнопки главного меню
        root.Q<Button>("btn-options").clicked += OpenOptions;
        root.Q<Button>("btn-exit").clicked += () => Application.Quit();

        // Кнопки настроек
        root.Q<Button>("btn-back").clicked += CloseOptions;
        root.Q<Button>("btn-save").clicked += SaveBindings;
        root.Q<Button>("btn-reset").clicked += ResetBindings;

        LoadBindings();
    }

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
            // Добавляем заголовок группы (Action Map Name)
            Label header = new Label(map.name.Replace("Actions", "").ToUpper());
            header.AddToClassList("group-header");
            _rebindList.Add(header);

            foreach (var action in map.actions)
            {
                // Проверяем, является ли это композитом (как Move WASD)
                if (action.bindings.Count > 1 && action.bindings[0].isComposite)
                {
                    // Если это композит, создаем строки для каждой его части (Up, Down...)
                    for (int i = 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; i++)
                    {
                        CreateRebindRow(action, i, $"{action.name} {action.bindings[i].name}");
                    }
                }
                else
                {
                    // Обычная одиночная кнопка
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

        // Получаем текущую клавишу для конкретного индекса бинда
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