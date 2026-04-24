using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;

public class GlobalSceneLoader : MonoBehaviour
{
    public static GlobalSceneLoader Instance;

    private UIDocument _uiDocument;
    private VisualElement _loadingContainer;
    private VisualElement _barFill;

    private void Awake()
    {
        // Исправляем ошибку "Destroying object is not allowed"
        if (Instance != null && Instance != this)
        {
            // Используем DestroyImmediate, если обычный Destroy ругается
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;

        // Проверяем, является ли объект корневым (чтобы DontDestroyOnLoad не выдавал варнинг)
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);
        _uiDocument = GetComponent<UIDocument>();
    }

    private void Start()
    {
        // Скрываем экран загрузки сразу при запуске игры
        var root = _uiDocument.rootVisualElement;
        _loadingContainer = root.Q<VisualElement>("loading-container");

        if (_loadingContainer != null)
        {
            _loadingContainer.style.display = DisplayStyle.None;
        }
    }

    public void LoadSceneAsync(int sceneIndex)
    {
        // Ищем элементы ПРЯМО ПЕРЕД использованием, чтобы избежать NullReference
        var root = _uiDocument.rootVisualElement;
        _loadingContainer = root.Q<VisualElement>("loading-container");
        _barFill = root.Q<VisualElement>("bar-fill");

        // Если все равно не нашли — выдаем понятную ошибку и просто грузим сцену
        if (_loadingContainer == null || _barFill == null)
        {
            Debug.LogError($"[Loader] Критическая ошибка: Элементы не найдены в UXML! Проверьте имена в UI Builder. Загружаю сцену {sceneIndex} без анимации.");
            SceneManager.LoadScene(sceneIndex);
            return;
        }

        StartCoroutine(LoadSceneCoroutine(sceneIndex));
    }

    private IEnumerator LoadSceneCoroutine(int sceneIndex)
    {
        // Показываем экран
        _loadingContainer.style.display = DisplayStyle.Flex;
        _barFill.style.width = Length.Percent(0);

        // Даем Unity один кадр на отрисовку появления экрана
        yield return null;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            _barFill.style.width = Length.Percent(targetProgress * 100f);

            if (operation.progress >= 0.9f)
            {
                yield return new WaitForSeconds(0.3f); // Небольшая пауза для красоты
                operation.allowSceneActivation = true;
            }
            yield return null;
        }

        // Скрываем после завершения
        _loadingContainer.style.display = DisplayStyle.None;
    }
}