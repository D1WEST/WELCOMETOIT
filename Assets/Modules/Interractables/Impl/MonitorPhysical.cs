using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Assets.Modules.Audio; // Не забудь неймспейс
using Cysharp.Threading.Tasks;

namespace Assets.Modules.Interractables.Impl
{
    [RequireComponent(typeof(Rigidbody))]
    public class MonitorPhysical : MonoBehaviour, IInteractable
    {
        [ReadOnly] public string targetWorkplaceId;
        public WorkplaceInteractable sourceDesk;

        [Header("Visuals")]
        [SerializeField] private Material materialOn;
        [SerializeField] private Material materialOff;
        private List<MeshRenderer> _allRenderers = new List<MeshRenderer>();

        private Rigidbody _rb;
        private bool _isCarried = false;

        public string InteractionPrompt => _isCarried ? "" : $"Поднять монитор ({targetWorkplaceId}) [E]";
        public Transform InteractionPivot => transform;
        public InteractionType InteractionType => InteractionType.Click;
        public float HoldDuration => 0;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _allRenderers.AddRange(GetComponentsInChildren<MeshRenderer>());

            SetPhysics(false);
            SetVisualState(false);
        }

        public void Initialize(string id, WorkplaceInteractable desk)
        {
            targetWorkplaceId = id;
            sourceDesk = desk;
            SetPhysics(false);
            SetVisualState(true);

            // --- ЗВУК: УСТАНОВКА ПК (Индекс 1) ---
            // Проигрываем, только если игра уже запущена (чтобы не шуметь при загрузке сцены)
            if (Time.timeSinceLevelLoad > 1f)
            {
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("PCDrop").ByIndex(1).WithVolume(0.5f).RandomSound().At(this.transform)
                ).Forget();
            }
        }

        public void Interact(GameObject interactor)
        {
            if (PlayerInteraction.Instance.IsCarryingItem) return;

            // Звук отрывания играет ТОЛЬКО если монитор реально стоял на столе
            if (sourceDesk != null)
            {
                // --- ЗВУК: ОТРЫВАНИЕ ПК (Индекс 0) ---
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("PCDrop").ByIndex(0).WithVolume(0.5f).RandomSound().At(this.transform)
                ).Forget();

                sourceDesk.OnMonitorManualPickUp();
                sourceDesk = null; // ОБНУЛЯЕМ ССЫЛКУ ТУТ
            }

            SetVisualState(false);
            PlayerInteraction.Instance.PickUpMonitor(this);
        }

        public void SetPhysics(bool state)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = !state;
            _rb.useGravity = state;
            if (state)
            {
                if (TryGetComponent<Collider>(out var col)) col.enabled = true;
            }
        }

        public void GetKicked()
        {
            // Звук отрывания при пинке играет только если он еще на столе
            if (sourceDesk != null)
            {
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("PCDrop").ByIndex(0).WithVolume(0.5f).RandomSound().At(this.transform)
                ).Forget();

                sourceDesk = null; // КРИТИЧЕСКИЙ ФИКС: монитор больше не принадлежит столу
            }

            transform.SetParent(null);
            SetPhysics(true);
            SetVisualState(false);

            Vector3 kickDir = (transform.forward + Vector3.up + Random.insideUnitSphere * 0.5f).normalized;
            float kickPower = 12f;

            _rb.AddForce(kickDir * kickPower, ForceMode.Impulse);
            _rb.AddTorque(Random.onUnitSphere * 10f, ForceMode.Impulse);
        }

        // --- ЛОГИКА ПАДЕНИЯ НА ПОЛ ---
        private void OnCollisionEnter(Collision collision)
        {
            if (!_rb.isKinematic && collision.relativeVelocity.magnitude > 3f)
            {
                // Выбираем рандомно между индексом 2 и 3 (твои звуки падения)
                int fallIndex = Random.Range(2, 4);

                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("PCDrop")
                    .ByIndex(fallIndex)
                    .RandomSound()
                    .At(this.transform)
                    .WithVolume(0.3f)
                ).Forget();
            }
        }

        public void SetVisualState(bool isOn)
        {
            Material targetMat = isOn ? materialOn : materialOff;
            if (targetMat == null) return;
            foreach (var rend in _allRenderers)
            {
                if (rend != null) rend.material = targetMat;
            }
        }
    }
}