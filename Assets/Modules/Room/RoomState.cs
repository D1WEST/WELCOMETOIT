using UnityEngine;

namespace Assets.Modules.Room
{
    [System.Serializable]
    public class RoomState
    {
        public string roomName;
        public RoomManager roomManager;
        public GameObject blackBlocker; // Тот самый черный куб
        public int unlockDay; // На какой день открывается комната
        public int goalTarget; // Сколько очков должна принести эта комната (для прогресса)
    }
}