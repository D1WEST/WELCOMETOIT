using Assets.Modules.Interractables.Impl;
using Assets.Modules.NPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Assets.Modules.Save
{
    public class GameDataManager : MonoBehaviour
    {
        public bool bossHintsEnabled = true;
        public float currentInvasionEnergy = 0f;
        [SerializeField] private int _playerMoney = 1000;
        public int playerMoney => _playerMoney;
        public static GameDataManager Instance { get; private set; }

        public static event Action<int, int, int> OnMoneyChanged;

        public int loadedDay { get; private set; } = 1;

        public List<WorkerInstance> myWorkers = new List<WorkerInstance>();
        public List<WorkerInstance> marketWorkers = new List<WorkerInstance>();

        [SerializeField] private List<WorkerSettings> allPossibleTemplates; // Заполнить в инспекторе

        private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
        private string _checkpointJson;

        private void Awake()
        {
            Instance = this;
            LoadGame();
        }

        public void SaveGame(int currentDay)
        {
            string json = JsonUtility.ToJson(new SaveWrapper
            {
                workers = myWorkers,
                money = _playerMoney,
                currentDay = currentDay,
                perks = playerPerks,
                invasionEnergy = currentInvasionEnergy,
                bossHintsEnabled = this.bossHintsEnabled
            });
            File.WriteAllText(SavePath, json);
        }

        public void CreateCheckpoint()
        {
            _checkpointJson = JsonUtility.ToJson(new SaveWrapper
            {
                workers = myWorkers,
                money = _playerMoney,
                currentDay = ShiftManager.Instance != null ? ShiftManager.Instance.currentDay : loadedDay,
                perks = playerPerks,
                invasionEnergy = currentInvasionEnergy,
                bossHintsEnabled = this.bossHintsEnabled
            });
        }

        public void RestoreCheckpoint()
        {
            if (string.IsNullOrEmpty(_checkpointJson)) return;

            var data = JsonUtility.FromJson<SaveWrapper>(_checkpointJson);
            myWorkers = data.workers;
            _playerMoney = data.money;
            playerPerks = data.perks;
            currentInvasionEnergy = data.invasionEnergy;
            bossHintsEnabled = data.bossHintsEnabled;

            foreach (var worker in myWorkers)
            {
                var template = allPossibleTemplates.Find(t => t.templateId == worker.templateId);
                if (template != null) worker.avatar = template.avatar;
                worker.isAssigned = false;
            }

            SaveGame(data.currentDay);
        }

        public void LoadGame()
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<SaveWrapper>(json);

                myWorkers = data.workers;
                _playerMoney = data.money;
                loadedDay = data.currentDay > 0 ? data.currentDay : 1;

                playerPerks = data.perks ?? new PerkData();
                currentInvasionEnergy = data.invasionEnergy;
                bossHintsEnabled = data.bossHintsEnabled;

                if (ShiftManager.Instance != null)
                    ShiftManager.Instance.currentDay = loadedDay;

                foreach (var worker in myWorkers)
                {
                    var template = allPossibleTemplates.Find(t => t.templateId == worker.templateId);
                    if (template != null) worker.avatar = template.avatar;
                }
            }
        }

        public void RefreshMarket()
        {
            marketWorkers.Clear();
            for (int i = 0; i < 6; i++)
            {
                var template = allPossibleTemplates[Random.Range(0, allPossibleTemplates.Count)];
                var worker = new WorkerInstance(template);

                worker.workPower = Random.Range(1, 11);
                worker.patience = Random.Range(1, 8);
                worker.sleepiness = Random.Range(1, 8);
                worker.angriness = Random.Range(1, 8);

                float positionMultiplier = 1f;
                if (worker.workPower <= 2) { worker.currentPosition = Position.Junior; positionMultiplier = 1f; }
                else if (worker.workPower <= 4) { worker.currentPosition = Position.Middle; positionMultiplier = 1.5f; }
                else if (worker.workPower <= 6) { worker.currentPosition = Position.Senior; positionMultiplier = 2.5f; }
                else if (worker.workPower <= 10) { worker.currentPosition = Position.Prodigy; positionMultiplier = 5f; }
                else { worker.currentPosition = Position.Eng_LEGEND; positionMultiplier = 12f; }

                float basePrice = template.buyPrice > 0 ? template.buyPrice : 50f;

                float skillsValue = (worker.workPower * 25f)
                                  + (worker.patience * 10f)
                                  - (worker.sleepiness * 8f)
                                  - (worker.angriness * 8f);

                float finalPrice = (basePrice + skillsValue) * positionMultiplier;

                worker.buyPrice = (int)Mathf.Max(25, finalPrice);
                worker.sellPrice = (int)(worker.buyPrice * 0.7f);

                marketWorkers.Add(worker);
            }
        }

        public void HireWorker(WorkerInstance worker)
        {
            if (playerMoney >= worker.buyPrice)
            {
                ChangeMoney(-worker.buyPrice);
                marketWorkers.Remove(worker);
                myWorkers.Add(worker);
                SaveGame(ShiftManager.Instance.currentDay);
            }
        }

        public void ChangeMoney(int amount)
        {
            int oldMoney = _playerMoney;
            _playerMoney += amount;
            OnMoneyChanged?.Invoke(oldMoney, _playerMoney, amount);
            SaveGame(ShiftManager.Instance.currentDay);
        }

        public GameObject GetWorkerPrefab(string templateId)
        {
            var template = allPossibleTemplates.Find(t => t.templateId == templateId);
            return template != null ? template.npcPrefab : null;
        }
        public void SellWorker(WorkerInstance worker)
        {
            var desk = WorkplaceInteractable.FindDeskByWorker(worker);
            if (desk != null)
            {
                desk.AssignWorker(null);
            }

            myWorkers.Remove(worker);
            ChangeMoney(worker.sellPrice);
            SaveGame(ShiftManager.Instance.currentDay);
        }
        // Единая логика множителей для всех расчетов
        private float GetPositionMultiplier(Position pos)
        {
            switch (pos)
            {
                case Position.Junior: return 1.0f;
                case Position.Middle: return 1.8f;
                case Position.Senior: return 3.5f;
                case Position.Prodigy: return 7.0f;
                case Position.Eng_LEGEND: return 15.0f;
                default: return 1.0f;
            }
        }

        public int GetUpgradeCost(WorkerInstance worker)
        {
            if (worker.currentPosition == Position.Eng_LEGEND) return 0;

            int currentMarketValue = CalculateValue(worker, true);

            Position nextPos = worker.currentPosition + 1;

            float predictedSkillValue = ((worker.workPower + 3) * 25f)
                                        + (worker.patience * 10f)
                                        - (worker.sleepiness * 8f)
                                        - (worker.angriness * 8f);

            float nextMultiplier = GetPositionMultiplier(nextPos);
            int nextLevelValue = (int)((100f + predictedSkillValue) * nextMultiplier);

            int priceDifference = nextLevelValue - currentMarketValue;
            int upgradePrice = (int)(priceDifference * 0.90f);

            return Mathf.Max(100, upgradePrice);
        }

        private int CalculateValue(WorkerInstance worker, bool isBuying)
        {
            float basePrice = 100f;

            float skillValue = (worker.workPower * 25f)
                               + (worker.patience * 10f)
                               - (worker.sleepiness * 8f)
                               - (worker.angriness * 8f);

            float multiplier = GetPositionMultiplier(worker.currentPosition);
            float totalValue = (basePrice + skillValue) * multiplier;

            float finalValue = isBuying ? totalValue : totalValue * 0.7f;

            return (int)Mathf.Max(50, finalValue);
        }
        public void PromoteWorker(WorkerInstance worker)
        {
            if (worker.currentPosition == Position.Eng_LEGEND) return;

            int cost = GetUpgradeCost(worker);

            if (playerMoney >= cost)
            {
                ChangeMoney(-cost);

                worker.currentPosition++;
                worker.workPower += Random.Range(2, 5);
                worker.patience += Random.Range(1, 3);

                worker.sleepiness = Mathf.Max(1, worker.sleepiness - 1);
                worker.angriness = Mathf.Max(1, worker.angriness - 1);

                worker.sellPrice = CalculateValue(worker, false);

                SaveGame(ShiftManager.Instance.currentDay);
            }
        }

        [System.Serializable]
        private class SaveWrapper
        {
            public List<WorkerInstance> workers;
            public int money;
            public int currentDay; // ДОБАВЬ ЭТО СЮДА
            public PerkData perks;
            public float invasionEnergy;
            public bool bossHintsEnabled;
        }

        [System.Serializable]
        public class PerkData
        {
            public int slapLevel = 0;      // Хлесткий шлепок
            public int noseLevel = 0;      // Чуткий нос
            public int goldMineLevel = 0;  // Золотая жила
            public int tastyBonusLevel = 0;// Вкусная поручка
        }

        public PerkData playerPerks = new PerkData();

        private readonly int[] perkPrices = { 1000, 5000, 10000, 25000, 75000 };

        public int GetPerkPrice(int currentLevel)
        {
            if (currentLevel >= 5) return -1;
            return perkPrices[currentLevel];
        }
    }
}