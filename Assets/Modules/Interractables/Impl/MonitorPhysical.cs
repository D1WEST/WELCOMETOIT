using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Assets.Modules.Interractables.Impl
{
    [RequireComponent(typeof(Rigidbody))]
    public class MonitorPhysical : MonoBehaviour, IInteractable
    {
        [ReadOnly] public string targetWorkplaceId;
        public WorkplaceInteractable sourceDesk;

        [Header("Visuals")]
        [SerializeField] private Material materialOn;  // Весь монитор "горит" (или обычный вид)
        [SerializeField] private Material materialOff; // Весь монитор "погас" (темный)
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

            // Находим все части монитора, которые имеют меш
            _allRenderers.AddRange(GetComponentsInChildren<MeshRenderer>());

            SetPhysics(false);
            SetVisualState(false); // Изначально выключен
        }

        public void Initialize(string id, WorkplaceInteractable desk)
        {
            targetWorkplaceId = id;
            sourceDesk = desk;
            SetPhysics(false);
            SetVisualState(true); // Включаем при установке на стол
        }

        public void Interact(GameObject interactor)
        {
            if (PlayerInteraction.Instance.IsCarryingItem) return;

            if (sourceDesk != null)
            {
                sourceDesk.OnMonitorManualPickUp();
                sourceDesk = null;
            }

            SetVisualState(false); // Гасим при подборе
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
            transform.SetParent(null);
            SetPhysics(true);
            SetVisualState(false); // Гасим при пинке

            Vector3 kickDir = (transform.forward + Vector3.up + Random.insideUnitSphere * 0.5f).normalized;
            float kickPower = 12f;

            _rb.AddForce(kickDir * kickPower, ForceMode.Impulse);
            _rb.AddTorque(Random.onUnitSphere * 10f, ForceMode.Impulse);
        }

        // МЕТОД ДЛЯ СМЕНЫ МАТЕРИАЛА ВСЕГО ОБЪЕКТА
        public void SetVisualState(bool isOn)
        {
            Material targetMat = isOn ? materialOn : materialOff;

            if (targetMat == null) return;

            foreach (var rend in _allRenderers)
            {
                if (rend != null)
                {
                    rend.material = targetMat;
                }
            }
        }
    }
}