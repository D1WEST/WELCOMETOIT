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
                var randomTemplate = allPossibleTemplates[Random.Range(0, allPossibleTemplates.Count)];
                randomTemplate.workPower = (int)Mathf.Clamp((randomTemplate.workPower += Random.Range(-4, 5)), 1f, 20f);
                randomTemplate.patience = (int)Mathf.Clamp((randomTemplate.patience += Random.Range(-4, 5)), 1f, 20f);
                randomTemplate.sleepiness = (int)Mathf.Clamp((randomTemplate.sleepiness += Random.Range(-2, 3)), 1f, 20f);
                randomTemplate.angriness = (int)Mathf.Clamp((randomTemplate.angriness += Random.Range(-2, 3)), 1f, 10f);
                randomTemplate.buyPrice = (int)(randomTemplate.sellPrice * 1.5f - randomTemplate.sleepiness - randomTemplate.angriness + randomTemplate.patience * 1.5f + randomTemplate.workPower * 2f);
                randomTemplate.sellPrice = (int)Mathf.Clamp(randomTemplate.buyPrice, 1f, 10f);
                if (randomTemplate.workPower > 0 && randomTemplate.workPower <= 2) { randomTemplate.position = Position.Junior; }
                else if (randomTemplate.workPower > 2 && randomTemplate.workPower <= 4) { randomTemplate.position = Position.Middle; }
                else if (randomTemplate.workPower > 4 && randomTemplate.workPower <= 6) { randomTemplate.position = Position.Senior; }
                else if (randomTemplate.workPower > 6 && randomTemplate.workPower <= 10) { randomTemplate.position = Position.Prodigy; }
                else if (randomTemplate.workPower > 10) { randomTemplate.position = Position.Eng_LEGEND; }
                marketWorkers.Add(new WorkerInstance(randomTemplate));
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

        public void PromoteWorker(WorkerInstance worker)
        {
            if (worker.currentPosition == Position.Eng_LEGEND) return;

            // Стоимость повышения (например)
            int cost = (int)worker.currentPosition * 50 + 50;

            // Проверка денег...

            worker.currentPosition++;
            worker.workPower = (int)Mathf.Clamp((worker.workPower += Random.Range(0, 5)), 1f, 20f);
            worker.patience = (int)Mathf.Clamp((worker.patience += Random.Range(-2, 3)), 1f, 20f);
            worker.sleepiness = (int)Mathf.Clamp((worker.sleepiness += Random.Range(-2, 1)), 1f, 20f);
            worker.angriness = (int)Mathf.Clamp((worker.angriness+=Random.Range(-2, 1)), 1f,10f);
            worker.sellPrice = (int)(worker.sellPrice * 1.5f - worker.sleepiness - worker.angriness + worker.patience * 1.5f + worker.workPower);

            SaveGame();
        }

        [System.Serializable] private class SaveWrapper { public List<WorkerInstance> workers; }
    }
}