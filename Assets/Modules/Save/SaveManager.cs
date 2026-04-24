using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Assets.Modules.NPC;
using UnityEngine;

namespace Assets.Modules.Save
{
    public class GameDataManager : MonoBehaviour
    {
        public static GameDataManager Instance { get; private set; }

        public List<WorkerInstance> myWorkers = new List<WorkerInstance>();
        public List<WorkerInstance> marketWorkers = new List<WorkerInstance>();

        [SerializeField] private List<WorkerSettings> allPossibleTemplates; // Заполнить в инспекторе

        private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        private void Awake() => Instance = this;

        public void SaveGame()
        {
            string json = JsonUtility.ToJson(new SaveWrapper { workers = myWorkers });
            File.WriteAllText(SavePath, json);
        }

        public void LoadGame()
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                myWorkers = JsonUtility.FromJson<SaveWrapper>(json).workers;
                foreach (var worker in myWorkers)
                {
                    worker.avatar = allPossibleTemplates.Find(t => t.templateId == worker.templateId).avatar;
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

                float skillsValue = (worker.workPower * 25f)  // Скорость - самый дорогой стат
                                  + (worker.patience * 10f)   // Концентрация - полезно
                                  - (worker.sleepiness * 8f)  // Сонливость - штраф
                                  - (worker.angriness * 8f);  // Гнев - штраф

                float finalPrice = (basePrice + skillsValue) * positionMultiplier;

                worker.buyPrice = (int)Mathf.Max(25, finalPrice);
                worker.sellPrice = (int)(worker.buyPrice * 0.7f);

                marketWorkers.Add(worker);
            }
        }

        public void HireWorker(WorkerInstance worker)
        {
            // Тут можно добавить проверку на наличие денег
            marketWorkers.Remove(worker);
            myWorkers.Add(worker);
            SaveGame();
        }

        public void SellWorker(WorkerInstance worker)
        {
            myWorkers.Remove(worker);
            // Добавить деньги игроку: PlayerWallet.Add(worker.sellPrice);
            SaveGame();
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

        private int CalculateValue(WorkerInstance worker, bool isBuying)
        {
            float basePrice = 100f; // Базовая константа

            float skillValue = (worker.workPower * 25f)
                               + (worker.patience * 10f)
                               - (worker.sleepiness * 8f)
                               - (worker.angriness * 8f);

            float multiplier = GetPositionMultiplier(worker.currentPosition);
            float totalValue = (basePrice + skillValue) * multiplier;

            float finalValue = isBuying ? totalValue : totalValue * 0.7f;

            return (int)Mathf.Max(50, finalValue); // Минимум 50$
        }
        public void PromoteWorker(WorkerInstance worker)
        {
            if (worker.currentPosition == Position.Eng_LEGEND) return;

            int upgradeCost = (int)(GetPositionMultiplier(worker.currentPosition) * 400f);

            // TODO: Здесь должна быть твоя проверка денег игрока

            worker.currentPosition++;
            worker.workPower += Random.Range(2, 5);
            worker.patience += Random.Range(1, 3);

            worker.sleepiness = Mathf.Max(1, worker.sleepiness - Random.Range(0, 2));
            worker.angriness = Mathf.Max(1, worker.angriness - Random.Range(0, 2));

            worker.sellPrice = CalculateValue(worker, false);

            SaveGame();
        }

        [System.Serializable] private class SaveWrapper { public List<WorkerInstance> workers; }
    }
}