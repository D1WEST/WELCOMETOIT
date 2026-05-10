using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Assets.Modules.Audio;
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

        private Outline _outline;

        public void OnHoverEnter()
        {
            // Подсвечиваем только если он не горит "красным алертом" прямо сейчас
            // Или можно менять цвет на белый при наведении
            if (_outline != null && sourceDesk != null)
            {
                _outline.enabled = true;
                _outline.OutlineColor = Color.white;
                _outline.OutlineMode = Outline.Mode.OutlineVisible;
            }
        }

        public void OnHoverExit()
        {
            // Если монитор на столе, выключаем обводку при уводе взгляда
            if (_outline != null && sourceDesk != null)
            {
                _outline.enabled = false;
            }
        }

        private void Start()
        {
            _outline = GetComponent<Outline>();
            UpdateOutlineState(); // Проверяем состояние при старте
        }

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
            _isCarried = false; // Сбрасываем флаг переноски

            SetPhysics(false);
            SetVisualState(true);
            UpdateOutlineState(); // Выключит красный свет

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

            if (sourceDesk != null)
            {
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("PCDrop").ByIndex(0).WithVolume(0.5f).RandomSound().At(this.transform)
                ).Forget();

                sourceDesk.OnMonitorManualPickUp();
                sourceDesk = null;
            }

            _isCarried = true; // Помечаем что несем
            UpdateOutlineState(); // Выключит обводку пока в руках

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

                // Если физика включена (выбросили предмет), снимаем флаг переноски
                _isCarried = false;
                UpdateOutlineState(); // Включит красный алерт если он на полу
            }
        }

        public void GetKicked()
        {
            if (sourceDesk != null)
            {
                AudioManager.Instance.PlayAudio(
                    AudioQuery.ByKey("PCDrop").ByIndex(0).WithVolume(0.5f).RandomSound().At(this.transform)
                ).Forget();
                sourceDesk = null;
            }

            transform.SetParent(null);
            SetPhysics(true);
            SetVisualState(false);

            UpdateOutlineState(); // Включит КРАСНЫЙ рентген

            Vector3 kickDir = (transform.forward + Vector3.up + Random.insideUnitSphere * 0.5f).normalized;
            _rb.AddForce(kickDir * 12f, ForceMode.Impulse);
            _rb.AddTorque(Random.onUnitSphere * 10f, ForceMode.Impulse);
        }

        // ГЛАВНЫЙ МЕТОД УПРАВЛЕНИЯ ПОДСВЕТКОЙ
        private void UpdateOutlineState()
        {
            if (_outline == null) return;

            // Если монитор НЕ на столе и НЕ в руках игрока -> КРАСНЫЙ АЛЕРТ
            if (sourceDesk == null && !_isCarried)
            {
                _outline.enabled = true;
                _outline.OutlineColor = Color.red;
                _outline.OutlineMode = Outline.Mode.OutlineAll; // Сквозь стены
                _outline.OutlineWidth = 5f;
            }
            else
            {
                // В руках или на столе — возвращаем в обычный режим (выключен, ждет наведения)
                _outline.OutlineColor = Color.white;
                _outline.OutlineMode = Outline.Mode.OutlineVisible;
                _outline.enabled = false;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_rb.isKinematic && collision.relativeVelocity.magnitude > 3f)
            {
                int fallIndex = Random.Range(2, 4);
                AudioManager.Instance.PlayAudio(AudioQuery.ByKey("PCDrop").ByIndex(fallIndex).RandomSound().At(this.transform).WithVolume(0.3f)).Forget();
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