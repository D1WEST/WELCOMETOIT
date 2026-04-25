using UnityEngine;
using System.Collections.Generic;
using Assets.Modules.Interractables;
using Assets.Modules.Interractables.Impl;
using Cysharp.Threading.Tasks;
using System.Threading;

public class AnimalAI : MonoBehaviour, IInteractable
{
    [Header("Movement")]
    public float speed = 9f;
    public float panicDistance = 8f;      // Боятся игрока из далека
    public float wallDetectionDist = 3f;  // Раннее обнаружение стен

    private Rigidbody _rb;
    private Transform _player;
    private SpringJoint _leash;
    private MonitorPhysical _capturedMonitor;
    private CancellationTokenSource _cts;

    private GameObject _target;
    private bool _isBurst = false;

    public string InteractionPrompt => "Лопнуть вредителя";
    public Transform InteractionPivot
    {
        get
        {
            // Если объект уничтожается, возвращаем null, чтобы PlayerInteraction не упал
            if (this == null || _isBurst) return null;
            return transform;
        }
    }
    public InteractionType InteractionType => InteractionType.Click;
    public float HoldDuration => 0;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        _cts = new CancellationTokenSource();
    }

    public void Init()
    {
        LogicLoop(_cts.Token).Forget();
    }

    private async UniTaskVoid LogicLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                UpdateTarget();
                HandleMovement();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (System.OperationCanceledException) { }
    }

    private void UpdateTarget()
    {
        // Постоянно ищем цель, даже если уже что-то тащим (жадность)
        float minDist = float.MaxValue;
        GameObject bestTarget = null;

        // 1. Ищем мониторы на столах
        foreach (var desk in WorkplaceInteractable.AllDesks)
        {
            if (!desk.hasMonitor) continue;
            float d = Vector3.Distance(transform.position, desk.transform.position);
            if (d < minDist) { minDist = d; bestTarget = desk.gameObject; }
        }

        // 2. Ищем других лис с мониторами (чтобы отнять)
        var allFoxes = FindObjectsOfType<AnimalAI>();
        foreach (var fox in allFoxes)
        {
            if (fox == this || !fox.HasMonitor()) continue;
            float d = Vector3.Distance(transform.position, fox.transform.position);
            if (d < minDist) { minDist = d; bestTarget = fox.gameObject; }
        }

        _target = bestTarget;
    }

    private void HandleMovement()
    {
        Vector3 moveDir = Vector3.zero;
        float distToPlayer = _player ? Vector3.Distance(transform.position, _player.position) : 100f;

        // --- 1. СТРАХ ПЕРЕД ИГРОКОМ (Самый высокий приоритет) ---
        if (distToPlayer < panicDistance)
        {
            moveDir = (transform.position - _player.position).normalized;
        }
        // --- 2. СТРАХ ПЕРЕД СТЕНАМИ (Whiskers system) ---
        moveDir += CalculateWallAvoidance() * 3f;

        // --- 3. ЖАЖДА НАЖИВЫ ---
        if (moveDir == Vector3.zero && _target != null)
        {
            moveDir = (_target.transform.position - transform.position).normalized;

            if (Vector3.Distance(transform.position, _target.transform.position) < 1.5f)
                TrySteal();
        }

        // ПРИМЕНЕНИЕ ФИЗИКИ
        if (moveDir != Vector3.zero)
        {
            moveDir.y = 0;
            Vector3 targetVelocity = moveDir.normalized * speed;
            _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);

            if (targetVelocity != Vector3.zero)
                _rb.rotation = Quaternion.Slerp(_rb.rotation, Quaternion.LookRotation(moveDir.normalized), Time.deltaTime * 8f);
        }
    }

    private Vector3 CalculateWallAvoidance()
    {
        Vector3 avoidance = Vector3.zero;
        // Лучи: вперед, влево-вперед, вправо-вперед
        Vector3[] directions = { transform.forward, transform.forward + transform.right, transform.forward - transform.right };

        foreach (var dir in directions)
        {
            if (Physics.Raycast(transform.position, dir.normalized, out RaycastHit hit, wallDetectionDist))
            {
                // Если стена — толкаем в противоположную от неё сторону
                avoidance += hit.normal;
            }
        }
        return avoidance.normalized;
    }

    private void TrySteal()
    {
        if (_target == null) return;

        if (_target.TryGetComponent<WorkplaceInteractable>(out var desk))
        {
            if (desk.hasMonitor)
            {
                MonitorPhysical mon = desk.GetComponentInChildren<MonitorPhysical>();
                desk.KickMonitor();
                AttachMonitor(mon);
            }
        }
        else if (_target.TryGetComponent<AnimalAI>(out var otherFox))
        {
            if (otherFox.HasMonitor())
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
        ReleaseMonitor(); // Сбрасываем старый, если был

        _capturedMonitor = mon;
        _leash = gameObject.AddComponent<SpringJoint>();
        _leash.connectedBody = mon.GetComponent<Rigidbody>();
        _leash.autoConfigureConnectedAnchor = false;
        _leash.anchor = new Vector3(0, 0, -0.6f);
        _leash.spring = 250f; // Жесткая пружина
        _leash.damper = 15f;
        _leash.minDistance = 0.3f;
        _leash.maxDistance = 1f;
    }

    public void ReleaseMonitor()
    {
        if (_leash != null) Destroy(_leash);
        _capturedMonitor = null;
    }

    public bool HasMonitor() => _capturedMonitor != null;

    public void Interact(GameObject interactor)
    {
        if (_isBurst || this == null) return;
        _isBurst = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        ReleaseMonitor();

        if (TryGetComponent<Collider>(out var col)) col.enabled = false;

        gameObject.SetActive(false);

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}