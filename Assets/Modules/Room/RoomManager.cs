using System.Collections.Generic;
using System.Linq;
using Assets.Modules.Interractables.Impl;
using Assets.Modules.NPC;
using UnityEngine;

namespace Assets.Modules.Room
{
    public class RoomManager : MonoBehaviour
    {
        public string roomName = "K 1";
        public bool isOpened = true;

        [SerializeField] private List<WorkplaceInteractable> desks;

        // Авто-поиск всех столов в комнате при настройке в инспекторе
        private void OnValidate()
        {
            if (desks == null || desks.Count == 0)
                desks = GetComponentsInChildren<WorkplaceInteractable>().ToList();
        }

        public (int current, int target) GetProductivity()
        {
            if (!isOpened || desks.Count == 0) return (0, 0);

            int sumActivePower = 0;
            int sumInactivePower = 0;
            int activeWorkersCount = 0;
            int totalMaxPower = 0;

            foreach (var desk in desks)
            {
                if (desk.Worker == null) continue;

                int power = desk.Worker.workPower;
                totalMaxPower += power;

                if (desk.Worker.status == WorkerStatus.Working)
                {
                    sumActivePower += power;
                    activeWorkersCount++;
                }
                else
                {
                    sumInactivePower += power;
                }
            }

            float workingPercent = (float)activeWorkersCount / desks.Count;

            // Формула: (Сумма работающих - Сумма неработающих) * % работающих в комнате
            float currentCalc = (sumActivePower - sumInactivePower) * workingPercent;

            int finalCurrent = Mathf.Max(0, Mathf.RoundToInt(currentCalc));
            return (finalCurrent, totalMaxPower);
        }
    }
}