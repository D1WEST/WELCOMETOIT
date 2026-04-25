using JetBrains.Annotations;
using System;
using UnityEngine;

namespace Assets.Modules.NPC
{
    public enum WorkerStatus { Working, Sleeping, Angry, Fidgeting, NoEquipment }

    [CreateAssetMenu(fileName = "NewWorkerNPC", menuName = "NPCWorker/New NPC")]
    public class WorkerSettings : ScriptableObject
    {
        public string templateId;
        public string workerName;
        public GameObject npcPrefab;
        public Position position;
        public int patience, workPower, sleepiness, angriness;
        public Sprite avatar;
        public int buyPrice;
        public int sellPrice;
    }

    [System.Serializable]
    public class WorkerInstance
    {
        [Header("Dynamic Stats (0-100)")]
        public float currentSleepiness;   // Сонливость
        public float currentAnger;        // Раздражительность
        public float currentRestlessness; // Непоседливость

        public string instanceId;
        public string templateId;
        public string name;
        public Position currentPosition;
        public int patience, workPower, sleepiness, angriness;
        public bool isResting;
        public WorkerStatus status = WorkerStatus.Working;
        [System.NonSerialized] public Sprite avatar; // Не сохраняем в JSON
        public string assignedWorkplaceId;
        public int buyPrice;
        public int sellPrice;
        public bool isAssigned;

        public WorkerInstance(WorkerSettings settings)
        {
            currentSleepiness = 0;
            currentAnger = 0;
            currentRestlessness = 0;
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
