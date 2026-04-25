using System.Collections.Generic;
using Assets.Modules.Interractables.Impl;
using UnityEngine;

namespace Assets.Modules.AIFox
{
    public class AnimalInvasionManager : MonoBehaviour
    {
        public static AnimalInvasionManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private GameObject animalPrefab;
        [SerializeField] private List<RoomManager> allRooms;

        [Header("Invasion Logic")]
        [SerializeField] private float currentInvasionEnergy = 0f;
        [SerializeField] private float energyThreshold = 100f; // Порог срабатывания

        // Базовая скорость накопления (настраивается так, чтобы при множителе 1.0 
        // шкала заполнялась за 1.5 смены)
        private float _baseGrowthRate;

        private void Awake() => Instance = this;

        private void Start()
        {
            // Рассчитываем базовый прирост:
            // 10 часов смены * 3600 сек / множитель времени 2.0 = 18000 реальных секунд на смену.
            // 1.5 смены = 27000 секунд.
            // Чтобы набрать 100 очков за 27000 сек, нужно 100 / 27000 в секунду.
            float shiftDurationRealSec = (10f * 3600f) / ShiftManager.Instance.timeMultiplier;
            _baseGrowthRate = energyThreshold / (shiftDurationRealSec * 1.5f);
        }

        private void Update()
        {
            if (!ShiftManager.Instance.IsShiftActive) return;

            // 1. Считаем общую эффективность офиса
            float totalEfficiency = CalculateGlobalEfficiency();

            // 2. Рассчитываем множитель (от 1.0 до 2.0)
            // Если эффективность >= 90% (0.9), множитель = 2.0
            float efficiencyMultiplier = Mathf.Lerp(1f, 2f, totalEfficiency / 0.9f);

            // 3. Добавляем энергию с учетом рандома (небольшой разброс, чтобы не было предсказуемо)
            float randomJitter = Random.Range(0.8f, 1.5f);
            currentInvasionEnergy += _baseGrowthRate * efficiencyMultiplier * randomJitter * Time.deltaTime * (1 + (ShiftManager.Instance.currentDay*0.1f));

            // 4. Проверка порога
            if (currentInvasionEnergy >= energyThreshold)
            {
                currentInvasionEnergy = 0f;
                TriggerInvasion();
            }
        }

        private float CalculateGlobalEfficiency()
        {
            float totalPercent = 0f;
            int openedRoomsCount = 0;

            foreach (var room in allRooms)
            {
                if (room.isOpened)
                {
                    var (current, target, _) = room.GetProductivity();
                    if (target > 0)
                    {
                        totalPercent += (float)current / target;
                    }
                    openedRoomsCount++;
                }
            }

            return openedRoomsCount > 0 ? totalPercent / openedRoomsCount : 0f;
        }

        private void TriggerInvasion()
        {
            var openedRooms = allRooms.FindAll(r => r.isOpened);
            if (openedRooms.Count == 0) return;

            RoomManager targetRoom = openedRooms[Random.Range(0, openedRooms.Count)];

            // Спавним больше лис для хаоса
            int count = Random.Range(5, 9);

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnOffset = new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f));
                Vector3 spawnPos = targetRoom.transform.position + spawnOffset;

                GameObject animal = Instantiate(animalPrefab, spawnPos, Quaternion.identity);
                if (animal.TryGetComponent<AnimalAI>(out var ai))
                {
                    ai.Init();
                }
            }
        }

        // Для UI, если захочешь видеть полоску "Опасности"
        public float GetInvasionProgress() => currentInvasionEnergy / energyThreshold;
    }
}