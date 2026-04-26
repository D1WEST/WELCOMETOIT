using Assets.Modules.Interractables.Impl;
using Assets.Modules.Save;
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
        [SerializeField] private float energyThreshold = 100f; // Порог срабатывания

        private bool _isInvasionMusicPlaying = false;

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
            if (GameDataManager.Instance != null)
            {
                currentInvasionEnergy = GameDataManager.Instance.currentInvasionEnergy;
            }
        }

        private void Update()
        {
            if (!ShiftManager.Instance.IsShiftActive) return;

            if (_isInvasionMusicPlaying)
            {
                CheckInvasionEnd();
            }

            float totalEfficiency = CalculateGlobalEfficiency();

            float efficiencyMultiplier = Mathf.Lerp(1f, 2f, totalEfficiency / 0.9f);

            float noseModifier = 1f - (GameDataManager.Instance.playerPerks.noseLevel * 0.1f);

            float randomJitter = Random.Range(0.8f, 1.5f);
            float dayMultiplier = 1 + (ShiftManager.Instance.currentDay * 0.1f);

            currentInvasionEnergy += _baseGrowthRate * efficiencyMultiplier * randomJitter * Time.deltaTime * dayMultiplier * noseModifier;

            GameDataManager.Instance.currentInvasionEnergy = currentInvasionEnergy;

            if (currentInvasionEnergy >= energyThreshold)
            {
                currentInvasionEnergy = 0f;
                GameDataManager.Instance.currentInvasionEnergy = 0f;
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
            int count = Random.Range(5, 9);

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnOffset = new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f));
                Vector3 spawnPos = targetRoom.transform.position + spawnOffset;
                GameObject animal = Instantiate(animalPrefab, spawnPos, Quaternion.identity);
                if (animal.TryGetComponent<AnimalAI>(out var ai)) ai.Init();
            }

            if (!_isInvasionMusicPlaying)
            {
                _isInvasionMusicPlaying = true;

                // ГЛАВНЫЙ ФИКС: Убрали .AsInstance(). Теперь Foxes (0) заменит текущую Music через Crossfade
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("Foxes")
                        .ByIndex(0)
                        .WithVolume(1f)
                        .Cycle()
                ).Forget();
            }
        }

        private void CheckInvasionEnd()
        {
            var activeFoxes = Object.FindObjectsByType<AnimalAI>(FindObjectsSortMode.None);

            if (activeFoxes.Length == 0)
            {
                _isInvasionMusicPlaying = false;

                // Просто запускаем обычную музыку. Менеджер сам сделает Crossfade и выключит лис.
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("Music")
                        .AsRandomPlaylist()
                        .WithVolume(0.15f)
                        .Cycle()
                ).Forget();
            }
        }

        // Для UI, если захочешь видеть полоску "Опасности"
        public float GetInvasionProgress() => currentInvasionEnergy / energyThreshold;
    }
}