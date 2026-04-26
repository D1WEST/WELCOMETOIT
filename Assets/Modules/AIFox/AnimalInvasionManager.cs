using Assets.Modules.Interractables.Impl;
using Assets.Modules.Save;
using Assets.Modules.Audio;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
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
        [SerializeField] private float energyThreshold = 100f;

        private bool _isInvasionActive = false; // Флаг: идет ли сейчас налет
        private float _baseGrowthRate;

        private void Awake() => Instance = this;

        private void Start()
        {
            float shiftDurationRealSec = (10f * 3600f) / ShiftManager.Instance.timeMultiplier;
            _baseGrowthRate = energyThreshold / (shiftDurationRealSec * 1.5f);

            if (GameDataManager.Instance != null)
                currentInvasionEnergy = GameDataManager.Instance.currentInvasionEnergy;
        }

        private void Update()
        {
            if (!ShiftManager.Instance.IsShiftActive) return;

            // 1. Если инвазия активна, проверяем, не пора ли её закончить
            if (_isInvasionActive)
            {
                HandleInvasionMusicLogic();
            }
            else
            {
                // 2. Если инвазии нет, копим энергию
                HandleEnergyAccumulation();
            }
        }

        private void HandleEnergyAccumulation()
        {
            float totalEfficiency = CalculateGlobalEfficiency();
            float efficiencyMultiplier = Mathf.Lerp(1f, 2f, totalEfficiency / 0.9f);
            float noseModifier = 1f - (GameDataManager.Instance.playerPerks.noseLevel * 0.1f);
            float randomJitter = Random.Range(0.8f, 1.5f);
            float dayMultiplier = 1 + (ShiftManager.Instance.currentDay * 0.1f);

            currentInvasionEnergy += _baseGrowthRate * efficiencyMultiplier * randomJitter * Time.deltaTime * dayMultiplier * noseModifier;
            GameDataManager.Instance.currentInvasionEnergy = currentInvasionEnergy;

            if (currentInvasionEnergy >= energyThreshold)
            {
                TriggerInvasion();
            }
        }

        private void TriggerInvasion()
        {
            var openedRooms = allRooms.FindAll(r => r.isOpened);
            if (openedRooms.Count == 0) return;

            currentInvasionEnergy = 0f;
            GameDataManager.Instance.currentInvasionEnergy = 0f;
            _isInvasionActive = true;

            RoomManager targetRoom = openedRooms[Random.Range(0, openedRooms.Count)];
            int count = Random.Range(5, 7);

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = targetRoom.transform.position + new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f));
                GameObject animal = Instantiate(animalPrefab, spawnPos, Quaternion.identity);
                if (animal.TryGetComponent<AnimalAI>(out var ai)) ai.Init();
            }

            // ВКЛЮЧАЕМ МУЗЫКУ ЛИС (Один раз)
            AudioManager.Instance.PlayAudio(
                AudioQuery.ByKey("Foxes").ByIndex(0).WithVolume(0.5f).Cycle()
            ).Forget();
        }

        private void HandleInvasionMusicLogic()
        {
            // Ищем лис, которые еще НЕ лопнули (IsBurst у нас в AnimalAI)
            var foxes = Object.FindObjectsByType<AnimalAI>(FindObjectsSortMode.None);

            // Фильтруем только живых
            int aliveCount = 0;
            foreach (var fox in foxes)
            {
                // Проверяем через наличие компонента и его внутреннее состояние, если нужно
                if (fox != null && fox.gameObject.activeInHierarchy) aliveCount++;
            }

            if (aliveCount == 0)
            {
                _isInvasionActive = false;
                StopInvasionAndReturnMusic().Forget();
            }
        }

        private async UniTaskVoid StopInvasionAndReturnMusic()
        {
            Debug.Log("Инвазия окончена, возвращаем музыку.");

            // Даем затухнуть звукам взрывов последних лис
            await UniTask.Delay(500);

            // Возвращаем обычную музыку
            AudioManager.Instance.PlayAudio(
                AudioQuery.ByKey("Music")
                    .AsRandomPlaylist()
                    .WithVolume(0.15f) // Увеличил громкость, чтобы было слышно
                    .Cycle()
            ).Forget();
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
                    if (target > 0) totalPercent += (float)current / target;
                    openedRoomsCount++;
                }
            }
            return openedRoomsCount > 0 ? totalPercent / openedRoomsCount : 0f;
        }

        public float GetInvasionProgress() => currentInvasionEnergy / energyThreshold;
    }
}