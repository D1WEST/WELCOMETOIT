using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Assets.Modules.Interractables.Impl
{
    public class BreakerSwitch : MonoBehaviour, IInteractable
    {
        [Header("Settings")]
        [SerializeField] private string breakerName = "Главный рубильник";
        public bool isOn = true;

        [Header("Visuals")]
        [SerializeField] private Material materialOn;
        [SerializeField] private Material materialOff;
        [SerializeField] private GameObject modelRoot;
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
            : $"ВЫБИЛО ПРОБКИ! Поднять рубильник";

        public Transform InteractionPivot => interactionPivot;
        public InteractionType InteractionType => interactionType;
        public float HoldDuration => holdDuration;

        private void Awake()
        {
            if (modelRoot != null)
            {
                _modelRenderers.AddRange(modelRoot.GetComponentsInChildren<MeshRenderer>());
            }
        }

        private async void Start()
        {
            CalculateNewRandomSpeed();
            ApplyState();

            // Ждем готовности менеджера и играем звук
            await UniTask.WaitUntil(() => AudioManager.Instance != null);
            await UniTask.Delay(500); // Даем сцене "прогрузиться"

            if (isOn)
            {
                StartWorkSound();
            }
        }

        private void CalculateNewRandomSpeed()
        {
            float randomHoursToTrip = Random.Range(3f, 11f);
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

            // --- ЗВУК: ПРОБКИ ВЫБИЛО (ГЛОБАЛЬНО) ---
            // Не используем .At(), чтобы игрок услышал это в любой комнате
            AudioManager.Instance.PlayAudio(
                AudioQuery.ByKey("Generator")
                .ByIndex(1)
                .AsInstance()
                .WithVolume(1.0f)
            ).Forget();

            // Останавливаем гул работы на объекте
            StopWorkSound();

        }

        public void Interact(GameObject interactor)
        {
            if (isOn) return;

            isOn = true;
            currentLoad = 0;

            ApplyState();

            AudioManager.Instance.StopAudio(
                AudioQuery.ByKey("Generator").At(this.transform)
            );

            AudioManager.Instance.PlayAudio(
                AudioQuery.ByKey("Generator")
                    .ByIndex(0)
                    .AsInstance()
                    .WithVolume(1.0f)
            ).Forget();

            if (!isOn) return;

            StartWorkSound();

        }

        private void StartWorkSound()
        {
            // --- ЗВУК: ПОСТОЯННЫЙ ГУЛ (ЛОКАЛЬНО + ЦИКЛ) ---
            AudioManager.Instance.PlayAudio(
                AudioQuery.ByKey("Generator")
                .ByIndex(2) // GenWork
                .At(this.transform)
                .AsInstance()
                .Cycle()
                .WithVolume(0.02f)
            ).Forget();

        }

        private void StopWorkSound()
        {
            // Останавливаем конкретно гул на этом объекте
            AudioManager.Instance.StopAudio(
                AudioQuery.ByKey("Generator").At(this.transform)
            );
        }

        private void ApplyState()
        {
            foreach (var lightObj in targetLights)
                if (lightObj != null) lightObj.SetActive(isOn);

            foreach (var room in targetRooms)
                if (room != null) room.SetRoomPower(isOn);


            UpdateModelMaterials();

        }

        private void UpdateModelMaterials()
        {
            Material targetMat = isOn ? materialOn : materialOff;
            if (targetMat == null) return;
            foreach (var renderer in _modelRenderers)
                if (renderer != null) renderer.material = targetMat;
        }
    }
}