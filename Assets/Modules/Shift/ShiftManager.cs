using Assets.Modules.Audio;
using Assets.Modules.Save;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ShiftManager : MonoBehaviour
{
    public static ShiftManager Instance { get; private set; }

    [Header("Настройки времени")]
    public float timeMultiplier = 120.0f; // 1 сек реал = 2 мин игр
    private float _currentTimeInSeconds;
    private bool _isShiftActive;

    [Header("Прогресс")]
    public int currentDay = 1;
    public float currentProgress;
    public float targetGoal;

    [Header("Комнаты")]
    [SerializeField] private List<RoomState> rooms;

    public event Action<float, float> OnProgressChanged;
    public event Action<string> OnTimeChanged;
    public bool IsShiftActive => _isShiftActive;

    // Переменная для отслеживания смены часа
    private int _lastHourSounded = 10;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // АВТО-ПОИСК БЛОКЕРОВ ПРИ ЗАПУСКЕ
        InitializeRoomBlockers();
    }

    private void InitializeRoomBlockers()
    {
        foreach (var room in rooms)
        {
            if (room.roomManager == null) continue;

            // Ищем всех детей в комнате
            Transform[] allChildren = room.roomManager.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                // Если у ребенка тег Blocker — добавляем в список этой комнаты
                if (child.CompareTag("Blocker"))
                {
                    room.doorBlockers.Add(child.gameObject);
                }
            }
        }
    }

    private void Start()
    {
        if (GameDataManager.Instance != null)
        {
            currentDay = GameDataManager.Instance.loadedDay;
        }

        // Ваша музыка (оставляем без изменений)
        AudioManager.Instance.PlayAudio(AudioQuery.ByKey("Music").AsRandomPlaylist().WithVolume(0.06f).Cycle()).Forget();

        SetupNewDay();
    }

    public void StartShift()
    {
        if (_isShiftActive) return;

        AudioManager.Instance.PlayAudio(
            AudioQuery.ByKey("StartGame")
                .ByIndex(0)
                .RandomSound()
                .WithVolume(1f)
        ).Forget();

        // СОЗДАЕМ ЧЕКПОИНТ
        GameDataManager.Instance.CreateCheckpoint();

        _currentTimeInSeconds = 10 * 3600; // Старт в 10:00
        _lastHourSounded = 10; // Сброс счетчика часов

        timeMultiplier = 120.0f;
        _isShiftActive = true;
        currentProgress = 0;
        SetupNewDay();
    }

    private void Update()
    {
        if (!_isShiftActive) return;

        _currentTimeInSeconds += Time.deltaTime * timeMultiplier;
        UpdateClockUI();

        // --- ЛОГИКА ЗВУКА КАЖДОГО ЧАСА (Индекс 0) ---
        int currentHour = TimeSpan.FromSeconds(_currentTimeInSeconds).Hours;
        if (currentHour > _lastHourSounded)
        {
            _lastHourSounded = currentHour;

            // Играем звук часа только если время не летит в режиме Time Skip
            if (timeMultiplier < 1000f)
            {
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("timescope").ByIndex(0).RandomSound()
                ).Forget();
            }
        }

        if (_currentTimeInSeconds >= 20 * 3600) // Финиш в 20:00
        {
            EndShift();
        }
    }

    private void SetupNewDay()
    {
        targetGoal = 0;
        foreach (var room in rooms)
        {
            bool isUnlocked = currentDay >= room.unlockDay;

            foreach (var blocker in room.doorBlockers)
            {
                if (blocker != null)
                {
                    blocker.SetActive(!isUnlocked);
                }
            }

            room.roomManager.isOpened = isUnlocked;

            if (isUnlocked)
                targetGoal += room.goalTarget;
        }

        float dayMultiplier = 1f + (currentDay * 0.05f);
        targetGoal = Mathf.RoundToInt(targetGoal * dayMultiplier);

        OnProgressChanged?.Invoke(0, targetGoal);
    }

    public void AddProgress(float amount, Vector3 worldPos)
    {
        if (!_isShiftActive) return;

        currentProgress += amount;
        OnProgressChanged?.Invoke(currentProgress, targetGoal);

        // ПОБЕДА: Если набрали очки раньше времени
        if (currentProgress >= targetGoal)
        {
            timeMultiplier = 12000f; // ВРЕМЯ ЛЕТИТ (Time Skip)
        }
    }

    private void EndShift()
    {
        _isShiftActive = false;
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        // --- ЗВУК КОНЦА СМЕНЫ (Индекс 1) ---
        AudioManager.Instance.PlayAudio(
            AudioQuery.ByKey("timescope").ByIndex(1).RandomSound()
        ).Forget();

        if (currentProgress < targetGoal - 0.1f)
        {
            PaycheckUIController.Instance.ShowResult(false, 0, player);
        }
        else
        {
            int activeRoomsCount = rooms.FindAll(r => r.roomManager.isOpened).Count;

            // Расчет бонуса
            float bonusCalc = (activeRoomsCount * 1000f) * (1 + currentDay * 0.05f);
            int finalBonus = Mathf.Max(100, Mathf.RoundToInt(bonusCalc));

            // Вызов UI результата (Там внутри должен быть звук индекса 2)
            PaycheckUIController.Instance.ShowResult(true, finalBonus, player);
        }
    }

    private async void ShowFailEffect()
    {
        await UniTask.Delay(2000);

        GameDataManager.Instance.RestoreCheckpoint();
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void UpdateClockUI()
    {
        TimeSpan t = TimeSpan.FromSeconds(_currentTimeInSeconds);
        string timeStr = string.Format("{0:D2}:{1:D2}", t.Hours, t.Minutes);
        OnTimeChanged?.Invoke(timeStr);
    }
}

[System.Serializable]
public class RoomState
{
    public string name;
    public RoomManager roomManager;
    public GameObject blackBlocker;
    public int unlockDay;
    public int goalTarget;

    [HideInInspector] public List<GameObject> doorBlockers = new List<GameObject>();
}