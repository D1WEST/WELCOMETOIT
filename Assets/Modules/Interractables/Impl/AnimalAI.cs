using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.Modules.Interractables.Impl
{
    public class AnimalAI : MonoBehaviour, IInteractable
    {
        [Header("Settings")]
        public float speed = 8.5f;
        public float panicDistance = 6f;
        public float wallAvoidDistance = 1.5f;

        private Rigidbody _rb;
        private Transform _player;
        private SpringJoint _leash;
        private MonitorPhysical _capturedMonitor;

        private GameObject _targetObject; // Может быть столом или другой лисой
        private bool _hasMonitor = false;

        // Оставляем только кнопку "Лопнуть"
        public string InteractionPrompt => "Лопнуть вредителя";
        public Transform InteractionPivot => transform;
        public InteractionType InteractionType => InteractionType.Click;
        public float HoldDuration => 0;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        public void Init()
        {
            SearchForTarget().Forget();
            MoveLoop().Forget();
        }

        private async UniTaskVoid SearchForTarget()
        {
            while (this != null)
            {
                if (!_hasMonitor)
                {
                    // 1. Ищем столы с мониторами
                    var allDesks = WorkplaceInteractable.AllDesks.FindAll(d => d.hasMonitor);
                    // 2. Ищем других лис, у которых есть монитор
                    var allFoxes = FindObjectsOfType<AnimalAI>();
                    var targetFoxes = new List<AnimalAI>(allFoxes).FindAll(f => f != this && f._hasMonitor);

                    if (targetFoxes.Count > 0 && Random.value > 0.5f) // 50% шанс пойти воровать у лисы
                    {
                        _targetObject = targetFoxes[Random.Range(0, targetFoxes.Count)].gameObject;
                    }
                    else if (allDesks.Count > 0)
                    {
                        _targetObject = allDesks[Random.Range(0, allDesks.Count)].gameObject;
                    }
                }
                await UniTask.Delay(2000); // Пересчитываем цель каждые 2 сек
            }
        }

        private async UniTaskVoid MoveLoop()
        {
            while (this != null)
            {
                Vector3 moveDir = Vector3.zero;
                float distToPlayer = _player ? Vector3.Distance(transform.position, _player.position) : 100f;

                // 1. ПРИОРИТЕТ: УБЕГАЕМ ОТ ИГРОКА
                if (distToPlayer < panicDistance)
                {
                    moveDir = (transform.position - _player.position).normalized;
                }
                // 2. ИДЕМ К ЦЕЛИ (красть)
                else if (_targetObject != null && !_hasMonitor)
                {
                    moveDir = (_targetObject.transform.position - transform.position).normalized;

                    if (Vector3.Distance(transform.position, _targetObject.transform.position) < 1.8f)
                    {
                        TrySteal();
                    }
                }
                // 3. БРОДИМ
                else
                {
                    moveDir = transform.forward;
                }

                // --- ОБХОД СТЕН (Raycast Steering) ---
                RaycastHit hit;
                if (Physics.Raycast(transform.position, transform.forward, out hit, wallAvoidDistance))
                {
                    // Поворачиваемся в сторону от нормали стены
                    moveDir += hit.normal * 2f;
                }

                // ПРИМЕНЯЕМ ДВИЖЕНИЕ
                moveDir.y = 0;
                if (moveDir != Vector3.zero)
                {
                    _rb.linearVelocity = new Vector3(moveDir.normalized.x * speed, _rb.linearVelocity.y, moveDir.normalized.z * speed);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 10f);
                }

                await UniTask.Yield();
            }
        }

        private void TrySteal()
        {
            if (_targetObject == null) return;

            // Если цель - стол
            if (_targetObject.TryGetComponent<WorkplaceInteractable>(out var desk))
            {
                if (desk.hasMonitor)
                {
                    MonitorPhysical mon = desk.GetComponentInChildren<MonitorPhysical>();
                    desk.KickMonitor();
                    AttachMonitor(mon);
                }
            }
            // Если цель - другая лиса
            else if (_targetObject.TryGetComponent<AnimalAI>(out var otherFox))
            {
                if (otherFox._hasMonitor && otherFox._capturedMonitor != null)
                {
                    var mon = otherFox._capturedMonitor;
                    otherFox.ReleaseMonitor();
                    AttachMonitor(mon);
                }
            }
        }

        private void AttachMonitor(MonitorPhysical mon)
        {
            if (mon == null) return;
            _capturedMonitor = mon;
            _leash = gameObject.AddComponent<SpringJoint>();
            _leash.connectedBody = mon.GetComponent<Rigidbody>();
            _leash.autoConfigureConnectedAnchor = false;
            _leash.anchor = new Vector3(0, 0, -0.6f); // Тащим сзади
            _leash.spring = 200f;
            _leash.damper = 10f;
            _leash.minDistance = 0.5f;
            _leash.maxDistance = 1.5f;
            _hasMonitor = true;
        }

        public void ReleaseMonitor()
        {
            if (_leash != null) Destroy(_leash);
            _capturedMonitor = null;
            _hasMonitor = false;
            _targetObject = null;
        }

        public void Interact(GameObject interactor)
        {
            ReleaseMonitor();
            Destroy(gameObject);
        }
    }
}