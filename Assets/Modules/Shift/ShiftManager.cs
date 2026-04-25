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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (GameDataManager.Instance != null)
        {
            currentDay = GameDataManager.Instance.loadedDay;
        }
        AudioManager.Instance.PlayAudio(AudioQuery.ByKey("Music").AsRandomPlaylist().WithVolume(0.06f).Cycle()).Forget();
        SetupNewDay();
    }

    public void StartShift()
    {
        if (_isShiftActive) return;

        // СОЗДАЕМ ЧЕКПОИНТ
        GameDataManager.Instance.CreateCheckpoint();

        _currentTimeInSeconds = 10 * 3600;
        timeMultiplier = 120.0f; // Сбрасываем множитель на нормальный   НЕ ЗАБУДЬ ПОМЕНЯТЬ КОГДА ПРИЙДЕТ ВРЕМЯ МЕНЯТЬ МУЛЬТИПЛАЕР
        _isShiftActive = true;
        currentProgress = 0;
        SetupNewDay();
    }

    private void Update()
    {
        if (!_isShiftActive) return;

        _currentTimeInSeconds += Time.deltaTime * timeMultiplier;
        UpdateClockUI();

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

            if (room.blackBlocker != null)
                room.blackBlocker.SetActive(!isUnlocked);

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

        if (currentProgress < targetGoal - 0.1f)
        {
            PaycheckUIController.Instance.ShowResult(false, 0, player);
        }
        else
        {
            int activeRoomsCount = rooms.FindAll(r => r.roomManager.isOpened).Count;

            float bonusCalc = (activeRoomsCount * 1000f) * (currentDay * 0.05f);
            int finalBonus = Mathf.Max(100, Mathf.RoundToInt(bonusCalc));

            PaycheckUIController.Instance.ShowResult(true, finalBonus, player);
        }
    }

    private async void ShowFailEffect()
    {
        await UniTask.Delay(2000);

        GameDataManager.Instance.RestoreCheckpoint();
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void CalculatePaycheck()
    {
        int activeRooms = rooms.FindAll(r => r.roomManager.isOpened).Count;
        float amount = (activeRooms * 1000) * (1+(currentDay * 0.05f));
        int finalBonus = Mathf.RoundToInt(amount);

        // Находим игрока на сцене, чтобы заблочить его
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        // Показываем чек и передаем ссылку на игрока
        PaycheckUIController.Instance.ShowResult(true,finalBonus, player);
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
    public int goalTarget; // Сколько очков прогресса должна давать эта комната
}