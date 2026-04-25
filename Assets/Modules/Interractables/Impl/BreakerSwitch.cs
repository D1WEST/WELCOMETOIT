using UnityEngine;
using System.Collections.Generic;
using Assets.Modules.Interractables;
using Assets.Modules.Interractables.Impl;

namespace Assets.Modules.Interractables.Impl
{
    public class BreakerSwitch : MonoBehaviour, IInteractable
    {
        [Header("Settings")]
        [SerializeField] private string breakerName = "Главный рубильник";
        public bool isOn = true;

        [Header("Visuals")]
        [SerializeField] private Material materialOn;  // Материал когда работает
        [SerializeField] private Material materialOff; // Материал когда выбило
        [SerializeField] private GameObject modelRoot;  // Сюда перетащи объект "Generator" из иерархии
        private List<MeshRenderer> _modelRenderers = new List<MeshRenderer>();

        [Header("Interaction Settings")]
        [SerializeField] private InteractionType interactionType = InteractionType.Hold;
        [SerializeField] private float holdDuration = 2.0f;

        [Header("Load Logic (0-100)")]
        [SerializeField] private float currentLoad = 0f;
        private float _loadSpeed;

        [Header("Dependencies")]
        [SerializeField] private Transform interactionPivot;
        [SerializeField] private List<GameObject> targetLights = new List<GameObject>();
        [SerializeField] private List<RoomManager> targetRooms = new List<RoomManager>();

        public string InteractionPrompt => isOn
            ? $"Система стабильна (Нагрузка: {(int)currentLoad}%)"
            : $"ВЫБИЛО ПРОБКИ! Поднять рубильник [E]";

        public Transform InteractionPivot => interactionPivot;
        public InteractionType InteractionType => interactionType;
        public float HoldDuration => holdDuration;

        private void Awake()
        {
            // Автоматически находим все MeshRenderer внутри модели генератора
            if (modelRoot != null)
            {
                _modelRenderers.AddRange(modelRoot.GetComponentsInChildren<MeshRenderer>());
            }
        }

        private void Start()
        {
            CalculateNewRandomSpeed();
            ApplyState();
        }

        private void CalculateNewRandomSpeed()
        {
            float randomHoursToTrip = Random.Range(4f, 8f);
            float secondsToTrip = randomHoursToTrip * 3600f;
            _loadSpeed = 100f / secondsToTrip;
        }

        private void Update()
        {
            if (!ShiftManager.Instance.IsShiftActive || !isOn) return;

            float gameTimeStep = Time.deltaTime * ShiftManager.Instance.timeMultiplier;
            currentLoad += _loadSpeed * gameTimeStep;

            if (currentLoad >= 100f)
            {
                TripBreaker();
            }
        }

        private void TripBreaker()
        {
            isOn = false;
            currentLoad = 0f;
            CalculateNewRandomSpeed();
            ApplyState();
            Debug.LogWarning($"[BREAKER] {breakerName} ВЫБИЛО!");
        }

        public void Interact(GameObject interactor)
        {
            if (isOn) return;
            isOn = true;
            currentLoad = 0;
            ApplyState();
            Debug.Log($"[BREAKER] {breakerName} снова в сети.");
        }

        private void ApplyState()
        {
            // 1. Управляем светом
            foreach (var lightObj in targetLights)
            {
                if (lightObj != null) lightObj.SetActive(isOn);
            }

            // 2. Управляем комнатами
            foreach (var room in targetRooms)
            {
                if (room != null) room.SetRoomPower(isOn);
            }

            // 3. МЕНЯЕМ МАТЕРИАЛЫ ВСЕМ КУБИКАМ
            UpdateModelMaterials();
        }

        private void UpdateModelMaterials()
        {
            Material targetMat = isOn ? materialOn : materialOff;

            if (targetMat == null) return;

            foreach (var renderer in _modelRenderers)
            {
                if (renderer != null)
                {
                    renderer.material = targetMat;
                }
            }
        }
    }
}