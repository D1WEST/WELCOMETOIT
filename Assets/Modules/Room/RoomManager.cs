using System.Collections.Generic;
using System.Linq;
using Assets.Modules.Interractables.Impl;
using Assets.Modules.NPC;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public string roomName = "К 1";

    public bool isOpened = true;

    public bool isRoomActive = true;

    [SerializeField] private List<WorkplaceInteractable> desks;

    private void OnValidate() => desks = GetComponentsInChildren<WorkplaceInteractable>().ToList();

    public void SetRoomPower(bool state)
    {
        isRoomActive = state;
    }

    public (int current, int target, bool hasError) GetProductivity()
    {
        if (!isOpened || desks.Count == 0) return (0, 0, true);

        float sumActivePower = 0;
        float sumInactivePower = 0;
        int workingPeopleCount = 0;
        int totalWorkersAtDesks = 0;
        int maxPossiblePower = 0;

        foreach (var desk in desks)
        {
            if (desk.Worker == null) continue;

            totalWorkersAtDesks++;
            int power = desk.Worker.workPower;
            maxPossiblePower += power;

            if (!isRoomActive || desk.Worker.isResting) continue;

            if (desk.Worker.status == WorkerStatus.Working)
            {
                sumActivePower += power;
                workingPeopleCount++;
            }
            else
            {
                sumInactivePower += power;
            }
        }

        if (totalWorkersAtDesks == 0) return (0, 0, true);

        float workingRatio = (float)workingPeopleCount / totalWorkersAtDesks;
        float calc = (sumActivePower - sumInactivePower) * workingRatio;

        int finalCurrent = Mathf.Max(0, Mathf.RoundToInt(calc));

        bool hasError = !isRoomActive || finalCurrent <= 0;

        return (finalCurrent, maxPossiblePower, hasError);
    }
}