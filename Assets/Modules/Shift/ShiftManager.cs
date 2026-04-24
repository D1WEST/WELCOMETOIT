using System;
using System.Collections.Generic;
using Assets.Modules.Save;
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

    private void Start() => SetupNewDay();

    public void StartShift()
    {
        if (_isShiftActive) return;

        _currentTimeInSeconds = 10 * 3600; // Старт в 10:00
        _isShiftActive = true;
        currentProgress = 0;

        Debug.Log($"Смена дня {currentDay} началась!");
        OnProgressChanged?.Invoke(currentProgress, targetGoal);
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
            // Если день игрока >= дня открытия комнаты - убираем блок
            bool isUnlocked = currentDay >= room.unlockDay;

            if (room.blackBlocker != null)
                room.blackBlocker.SetActive(!isUnlocked);

            room.roomManager.isOpened = isUnlocked;

            if (isUnlocked) targetGoal += room.goalTarget;
        }

        OnProgressChanged?.Invoke(0, targetGoal);
    }

    public void AddProgress(float amount, Vector3 worldPos)
    {
        // Если здесь будет false, прогресс никогда не прибавится
        if (!_isShiftActive)
        {
            Debug.LogWarning("AddProgress вызван, но смена не активна!");
            return;
        }

        currentProgress += amount;

        // Проверка: вызывается ли событие?
        OnProgressChanged?.Invoke(currentProgress, targetGoal);
    }

    private void EndShift()
    {
        _isShiftActive = false;
        currentDay++;
        Debug.Log("Смена завершена. Подготовка к следующему дню...");
        SetupNewDay();
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