using System;
using System.Collections.Generic;
using Assets.Modules.Room;
using UnityEngine;

namespace Assets.Modules.Shift
{
    public class ShiftManager : MonoBehaviour
    {
        public static ShiftManager Instance { get; private set; }

        [Header("Time Settings")]
        public float timeMultiplier = 2.0f; // 1 сек реальная = 2 сек игровые
        private float _currentTimeInSeconds;
        private bool _isShiftActive;

        [Header("Shift Data")]
        public int currentDay = 1;
        public float currentProgress;
        public float targetGoal = 5000;

        [Header("Rooms")]
        [SerializeField] private List<RoomState> rooms;

        public event Action<float, float> OnProgressChanged;
        public event Action<string> OnTimeChanged;
        public bool IsShiftActive => _isShiftActive;

        private void Awake() => Instance = this;

        private void Start() => SetupDay();

        public void StartShift()
        {
            if (_isShiftActive) return;
            _isShiftActive = true;
            _currentTimeInSeconds = 10 * 3600; // 10:00 в секундах
            Debug.Log($"Смена дня {currentDay} началась!");
        }

        private void Update()
        {
            if (!_isShiftActive) return;

            // Двигаем время
            _currentTimeInSeconds += Time.deltaTime * timeMultiplier;
            UpdateClockUI();

            // Проверка завершения (20:00)
            if (_currentTimeInSeconds >= 20 * 3600)
            {
                EndShift();
            }
        }

        private void SetupDay()
        {
            // Высчитываем общую цель на день на основе открытых комнат
            targetGoal = 0;
            foreach (var room in rooms)
            {
                bool shouldBeOpen = currentDay >= room.unlockDay;
                room.blackBlocker.SetActive(!shouldBeOpen);
                room.roomManager.isOpened = shouldBeOpen;

                if (shouldBeOpen) targetGoal += room.goalTarget;
            }
            currentProgress = 0;
            OnProgressChanged?.Invoke(0, targetGoal);
        }

        public void AddProgress(float amount)
        {
            if (!_isShiftActive) return;
            currentProgress += amount;
            OnProgressChanged?.Invoke(currentProgress, targetGoal);
        }

        private void EndShift()
        {
            _isShiftActive = false;
            currentDay++;
            Debug.Log("Смена окончена!");
            SetupDay(); // Готовим следующий день
        }

        private void UpdateClockUI()
        {
            int hours = (int)(_currentTimeInSeconds / 3600);
            int minutes = (int)((_currentTimeInSeconds % 3600) / 60);
            OnTimeChanged?.Invoke($"{hours:00}:{minutes:00}");
        }
    }
}