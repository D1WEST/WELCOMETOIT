using UnityEngine;
using System.Collections.Generic;
using Assets.Modules.Interractables;
using Assets.Modules.Interractables.Impl;
using Assets.Modules.Audio;
using Cysharp.Threading.Tasks;
using System.Threading;

public class AnimalAI : MonoBehaviour, IInteractable
{
    [Header("Movement")]
    public float speed = 6f;
    public float panicDistance = 8f;
    public float rotationSpeed = 7f;
    public float wallDetectionDist = 3f;

    public float detectionRadius = 15f; // В каком радиусе лиса "видит" мониторы
    private Vector3 _wanderDir;
    private float _wanderTimer;

    [Header("Visuals")]
    private Animator _animator;
    [SerializeField] private float burstScaleMultiplier = 2.5f; // Во сколько раз раздуется перед взрывом
    [SerializeField] private float burstDuration = 0.15f;      // Время раздувания
    private Vector3 _smoothMoveDir;

    private Rigidbody _rb;
    private Transform _player;
    private SpringJoint _leash;
    private MonitorPhysical _capturedMonitor;
    private CancellationTokenSource _cts;

    private GameObject _target;
    private bool _isBurst = false;
    private bool _isWalkingSoundPlaying = false;
    private Outline _outline;

    public string InteractionPrompt => _isBurst ? "" : "Лопнуть вредителя";
    public Transform InteractionPivot
    {
        get
        {
            if (this == null || _isBurst) return null;
            return transform;
        }
    }
    public InteractionType InteractionType => InteractionType.Click;
    public float HoldDuration => 0;

    public void OnHoverEnter()
    {
        if (_outline != null) _outline.enabled = true;
    }

    public void OnHoverExit()
    {
        if (_outline != null) _outline.enabled = false;
    }

    private void Start()
    {
        // Кэшируем компонент один раз при старте
        _outline = GetComponent<Outline>();

        // На всякий случай гарантируем, что он выключен
        if (_outline != null) _outline.enabled = false;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>();
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
            while (!token.IsCancellationRequested && !_isBurst)
            {
                UpdateTarget();

                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, token);

                if (!_isBurst)
                {
                    HandleMovement();
                    UpdateAnimations();
                }
            }
        }
        catch (System.OperationCanceledException) { }
    }

    private void UpdateTarget()
    {
        if (_isBurst) return;

        float minDist = float.MaxValue;
        GameObject bestTarget = null;

        // 1. Ищем мониторы в радиусе
        // Сначала столы
        foreach (var desk in WorkplaceInteractable.AllDesks)
        {
            if (desk == null || !desk.hasMonitor) continue;

            float d = Vector3.Distance(transform.position, desk.transform.position);
            if (d < detectionRadius && d < minDist)
            {
                minDist = d;
                bestTarget = desk.gameObject;
            }
        }

        // 2. Ищем других лис с мониторами в радиусе
        var allFoxes = FindObjectsOfType<AnimalAI>();
        foreach (var fox in allFoxes)
        {
            if (fox == null || fox == this || !fox.HasMonitor() || fox._isBurst) continue;

            float d = Vector3.Distance(transform.position, fox.transform.position);
            if (d < detectionRadius && d < minDist)
            {
                minDist = d;
                bestTarget = fox.gameObject;
            }
        }

        _target = bestTarget;

        // 3. ЛОГИКА БЛУЖДАНИЯ (если целей нет)
        if (_target == null)
        {
            _wanderTimer -= Time.fixedDeltaTime;
            if (_wanderTimer <= 0)
            {
                // Выбираем новое случайное направление раз в 2-4 секунды
                float angle = Random.Range(0, 360f);
                _wanderDir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                _wanderTimer = Random.Range(2f, 4f);
            }
        }
    }

    private void UpdateAnimations()
    {
        if (_animator == null || _isBurst) return;

        // Считаем горизонтальную скорость
        float horizontalSpeed = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;
        bool isMoving = horizontalSpeed > 0.5f; // Чуть увеличим порог

        _animator.SetBool("IsRunning", isMoving);

        // --- ЗВУК ШАГОВ (Индекс 1) ---
        if (isMoving && !_isWalkingSoundPlaying)
        {
            _isWalkingSoundPlaying = true;
            AudioManager.Instance.PlayAudio(
                AudioQuery.ByKey("Foxes").ByIndex(1).RandomSound().WithVolume(0.3f).Cycle().At(this.transform)
            ).Forget();
        }
        else if (!isMoving && _isWalkingSoundPlaying)
        {
            _isWalkingSoundPlaying = false;
            // Глушим звук именно на этом объекте
            AudioManager.Instance.StopAudio(AudioQuery.ByKey("Foxes").At(this.transform));
        }
    }

    private void HandleMovement()
    {
        if (_isBurst) return;

        Vector3 targetDir = Vector3.zero;
        float distToPlayer = _player ? Vector3.Distance(transform.position, _player.position) : 100f;

        // Приоритет 1: Убегаем от игрока
        if (distToPlayer < panicDistance)
        {
            targetDir = (transform.position - _player.position).normalized;
        }
        // Приоритет 2: Обход стен (усы)
        Vector3 avoidance = CalculateAvoidance();

        // Приоритет 3: Преследование цели или Блуждание
        if (targetDir == Vector3.zero && avoidance == Vector3.zero)
        {
            if (_target != null)
            {
                targetDir = (_target.transform.position - transform.position).normalized;
                if (Vector3.Distance(transform.position, _target.transform.position) < 1.5f)
                    TrySteal();
            }
            else
            {
                // Если никого не грабим и игрока нет - просто бежим по своим делам
                targetDir = _wanderDir;
            }
        }

        // Смешиваем основной вектор и обход стен
        Vector3 finalDir = (targetDir + avoidance * 2f).normalized;
        finalDir.y = 0;

        // Плавное сглаживание поворота (убирает дрожание)
        if (finalDir != Vector3.zero)
        {
            _smoothMoveDir = Vector3.Slerp(_smoothMoveDir, finalDir, Time.fixedDeltaTime * rotationSpeed);

            Quaternion lookRot = Quaternion.LookRotation(_smoothMoveDir);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, lookRot, Time.fixedDeltaTime * rotationSpeed));

            Vector3 targetVelocity = _smoothMoveDir * speed;
            _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);
        }
    }

    private Vector3 CalculateAvoidance()
    {
        Vector3 avoidance = Vector3.zero;
        Vector3[] directions = { transform.forward, transform.forward + transform.right, transform.forward - transform.right };

        foreach (var dir in directions)
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, dir.normalized, out RaycastHit hit, wallDetectionDist))
            {
                // Не боимся игрока и цели, боимся только стен
                if (!hit.collider.CompareTag("Player") && hit.collider.gameObject != _target)
                    avoidance += hit.normal;
            }
        }
        return avoidance.normalized;
    }

    private void TrySteal()
    {
        if (_target == null || _isBurst) return;

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
        if (mon == null || _isBurst) return;
        ReleaseMonitor();

        _capturedMonitor = mon;
        _leash = gameObject.AddComponent<SpringJoint>();
        _leash.connectedBody = mon.GetComponent<Rigidbody>();
        _leash.autoConfigureConnectedAnchor = false;
        _leash.anchor = new Vector3(0, 0, -0.6f);
        _leash.spring = 250f;
        _leash.damper = 15f;
    }

    public void ReleaseMonitor()
    {
        if (_leash != null) Destroy(_leash);
        _capturedMonitor = null;
    }

    public bool HasMonitor() => _capturedMonitor != null;

    public void Interact(GameObject interactor)
    {
        if (_isBurst) return;
        PerformBurstSequence().Forget();
    }

    private async UniTaskVoid PerformBurstSequence()
    {
        _isBurst = true;

        // 1. ОСТАНАВЛИВАЕМ ШАГИ
        AudioManager.Instance.StopAudio(AudioQuery.ByKey("Foxes").At(this.transform));

        // 2. ЗВУК ЛОПАНИЯ (Индекс 2)
        // ВАЖНО: Играем БЕЗ .At(), чтобы звук проигрался из центрального менеджера (2D).
        // Это гарантирует, что звук НЕ УМРЕТ вместе с лисой.
        AudioManager.Instance.PlayAudio(
            AudioQuery.ByKey("Foxes").ByIndex(2).RandomSound().WithVolume(1f)
        ).Forget();

        // 3. Отключаем всё лишнее
        _cts?.Cancel();
        ReleaseMonitor();
        if (TryGetComponent<Collider>(out var col)) col.enabled = false;
        _rb.isKinematic = true;

        // 4. ЭФФЕКТ РАЗДУВАНИЯ
        Vector3 initialScale = transform.localScale;
        Vector3 targetScale = initialScale * burstScaleMultiplier;
        float elapsed = 0;

        while (elapsed < burstDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / burstDuration;
            transform.localScale = Vector3.Lerp(initialScale, targetScale, t * t);
            await UniTask.Yield();
        }

        // 5. Удаление
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}