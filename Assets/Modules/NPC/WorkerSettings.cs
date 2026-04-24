using JetBrains.Annotations;
using System;
using UnityEngine;

namespace Assets.Modules.NPC
{
    [CreateAssetMenu(fileName = "NewWorkerNPC", menuName = "NPCWorker/New NPC")]
    public class WorkerSettings : ScriptableObject
    {
        public string templateId;
        public string workerName;
        public Position position;
        public int patience, workPower, sleepiness, angriness;
        public Sprite avatar;
        public int buyPrice;
        public int sellPrice;
    }

    [System.Serializable]
    public class WorkerInstance
    {
        public string instanceId;
        public string templateId;
        public string name;
        public Position currentPosition;
        public int patience, workPower, sleepiness, angriness;
        public bool isResting;
        [System.NonSerialized] public Sprite avatar; // Не сохраняем в JSON
        public int buyPrice;
        public int sellPrice;

        public WorkerInstance(WorkerSettings settings)
        {
            instanceId = Guid.NewGuid().ToString();
            templateId = settings.templateId;
            name = settings.workerName;
            currentPosition = settings.position;
            patience = settings.patience;
            workPower = settings.workPower;
            sleepiness = settings.sleepiness;
            angriness = settings.angriness;
            avatar = settings.avatar;
            buyPrice = settings.buyPrice;
            sellPrice = settings.sellPrice;
        }
    }
}
