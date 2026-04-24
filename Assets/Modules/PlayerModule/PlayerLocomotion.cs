namespace Assets.Modules.PlayerModule
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.InputSystem;

    [RequireComponent(typeof(CharacterController))]
    public class PlayerLocomotion : MonoBehaviour
    {
        [Header("DoNotTouch")]
        public Vector2 MovementVector { get; set; } = Vector2.zero;
        public Vector2 LookVectorDelta {get;set;} = Vector2.zero;

        private CancellationTokenSource _crouchCTS;

        [Header("Movement")]
        [SerializeField] private float _gravity = -30f;
        [SerializeField] private float _crouchSpeed = 2f;
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _runSpeed = 7f;
        [SerializeField] private float _jumpHeight = 1.5f;
        [SerializeField] private CharacterController _controller;
        [SerializeField] private Camera _camera;

        [Header("Movement Physics")]
        [SerializeField] private float _acceleration = 10f;
        [SerializeField] private float _deceleration = 10f;
        [Range(0f, 1f)]
        [SerializeField] private float _airControlMultiplier = 0.2f;

        private float _selectedSpeed = 0f;
        private Vector3 _velocity;
        private Vector3 _currentHorizontalVelocity;
        private bool isCrouching = false;
        private bool _isInDuckPosition = false;

        [Header("CameraPhysics")]
        [SerializeField] private float _headSize = 0.25f;
        [SerializeField] private float _bodySize = 1.75f;
        [Range(0,1)]
        [SerializeField] private float _crouchToStandRatio = 0.4f;

        [SerializeField] private float _crouchSmoothTime = 0.05f;

        [Header("Animation")]

        [SerializeField] private PlayerCameraService _playerCamera;

        [SerializeField] private PlayerHUD _hud;

        [Header("Stamina Settings")]
        [SerializeField] private float _maxStamina = 100f;
        [SerializeField] private float _sprintDepletionRate = 16f;
        [SerializeField] private float _regenRate = 15f;
        [SerializeField] private float _regenDelay = 0.5f;

        private float _currentStamina;
        private bool _isSprinting = false;
        private CancellationTokenSource _staminaUpdateCTS;

        private void Start()
        {
            BuildCharacter();
        }

        private void BuildCharacter()
        {
            _selectedSpeed = _walkSpeed;
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_playerCamera == null) _playerCamera = GetComponent<PlayerCameraService>();
            _playerCamera.Initialize();
            InitializeStamina();
        }

        private void Update()
        {
            _playerCamera.Look(_playerCamera.SelectOperatingVector(LookVectorDelta, MovementVector));
            Move(_playerCamera._lookType);
        }

        /// <summary>
        /// Jump action.
        /// </summary>
        /// <param name="obj">Callback.</param>
        public void DoJump(InputAction.CallbackContext obj)
        {
            if (_controller.isGrounded && _currentStamina >= 15f)
            {
                _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
                ConsumeStaminaInstant(15f);
            }
        }

        /// <summary>
        /// Crouch action.
        /// </summary>
        /// <param name="obj">Callback.</param>
        public void DoCrouch(InputAction.CallbackContext obj)
        {
            isCrouching = true;
            _selectedSpeed = _crouchSpeed;
            TriggerCrouchAnimation(false, (-0.5f * _bodySize) + ((_bodySize - _headSize / 2) * _crouchToStandRatio));
        }

        /// <summary>
        /// Crouch stop action.
        /// </summary>
        /// <param name="obj">Callback.</param>
        public void StopCrouch(InputAction.CallbackContext obj)
        {
            TriggerCrouchAnimation(true, (-0.5f * _bodySize) + (_bodySize - (_headSize / 2)));
        }

        /// <summary>
        /// Sprint action.
        /// </summary>
        /// <param name="obj">Callback.</param>
        public void DoSprint(InputAction.CallbackContext obj)
        {
            if (isCrouching) return;

            if (obj.performed && _currentStamina > 5f)
            {
                _isSprinting = true;
                _selectedSpeed = _runSpeed;
            }
            else if (obj.canceled)
            {
                _isSprinting = false;
                _selectedSpeed = _walkSpeed;
            }

            // Запускаем асинхронную логику стамины (бег + реген)
            StartStaminaLogicTask().Forget();
        }

        public void StopSprint()
        {
            _isSprinting = false;
            _selectedSpeed = _walkSpeed;
        }

        /// <summary>
        /// Move action.
        /// </summary>
        public void Move(LookType lookType)
        {
            if (_controller.isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            Vector3 inputDirection = SelectMovementInputVector(lookType);
            if (inputDirection.magnitude > 1f)
            {
                inputDirection.Normalize();
            }

            Vector3 targetVelocity = inputDirection * _selectedSpeed;
            float speedChangeRate = MovementVector.magnitude > 0.1f ? _acceleration : _deceleration;

            if (!_controller.isGrounded)
            {
                speedChangeRate *= _airControlMultiplier;
            }
            _currentHorizontalVelocity = Vector3.Lerp(_currentHorizontalVelocity, targetVelocity, speedChangeRate * Time.deltaTime);

            _velocity.y += _gravity * Time.deltaTime;
            Vector3 finalVelocity = _currentHorizontalVelocity + new Vector3(0, _velocity.y, 0);
            _controller.Move(finalVelocity * Time.deltaTime);
        }

        /// <summary>
        /// Метод для разового забора стамины (например, для прыжка).
        /// </summary>
        public void ConsumeStaminaInstant(float amount)
        {
            _currentStamina = Mathf.Clamp(_currentStamina - amount, 0, _maxStamina);
            UpdateHUD();

            // Если мы не бежим, перезапускаем цикл регенерации
            if (!_isSprinting)
            {
                StartStaminaLogicTask().Forget();
            }
        }

        private void InitializeStamina()
        {
            _currentStamina = _maxStamina;
            if (_hud == null) _hud = FindObjectOfType<PlayerHUD>();
        }

        /// <summary>
        /// Основной цикл обработки стамины.
        /// </summary>
        private async UniTaskVoid StartStaminaLogicTask()
        {
            // Отменяем предыдущую задачу, если она была
            _staminaUpdateCTS?.Cancel();
            _staminaUpdateCTS = new CancellationTokenSource();
            CancellationToken token = _staminaUpdateCTS.Token;

            try
            {
                float lastActionTime = Time.time;

                while (true)
                {
                    if (_isSprinting)
                    {
                        // Трата стамины при беге
                        _currentStamina -= _sprintDepletionRate * Time.deltaTime;

                        if (_currentStamina <= 0)
                        {
                            _currentStamina = 0;
                            StopSprint(); // Принудительно останавливаем бег
                        }
                        lastActionTime = Time.time;
                    }
                    else if (_currentStamina < _maxStamina)
                    {
                        // Логика регенерации с задержкой
                        if (Time.time > lastActionTime + _regenDelay)
                        {
                            _currentStamina += _regenRate * Time.deltaTime;
                            _currentStamina = Mathf.Clamp(_currentStamina, 0, _maxStamina);
                        }
                    }
                    else
                    {
                        // Стамина полная и мы не бежим - выходим из цикла для экономии ресурсов
                        _currentStamina = _maxStamina;
                        UpdateHUD();
                        break;
                    }

                    UpdateHUD();
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Задача была отменена - это нормально
            }
        }

        private void UpdateHUD()
        {
            if (_hud != null)
            {
                _hud.stamina = _currentStamina; // Передаем значение в ваш HUD
            }
        }

        /// <summary>
        /// Updates camera y position smoothly.
        /// </summary>
        /// <param name="endingCrouch">Is crouch ending?</param>
        /// <param name="targetCamY">Target position.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns></returns>
        private async UniTask SmoothCameraCrouch(bool endingCrouch, float targetCamY, CancellationToken ct)
        {
            float currentVelocityHeight = 0f;
            float currentVelocityCenter = 0f;
            float currentVelocityCam = 0f;

            float targetHeight = endingCrouch ? (_bodySize + _headSize) : (_bodySize + _headSize) * _crouchToStandRatio;
            float targetCenterY = endingCrouch ? 0f : -0.5f * (_bodySize + _headSize) * _crouchToStandRatio;

            while (Mathf.Abs(_controller.height - targetHeight) > 0.001f)
            {
                if (endingCrouch)
                {
                    if (Physics.SphereCast(transform.position, _headSize / 2f, Vector3.up, out _, 1, LayerMask.GetMask("Environment")))
                    {
                        _isInDuckPosition = true;
                        isCrouching = true;
                        _selectedSpeed = _crouchSpeed;
                        return;
                    }
                }

                _controller.height = Mathf.SmoothDamp(_controller.height, targetHeight, ref currentVelocityHeight, _crouchSmoothTime);
                float newCenterY = Mathf.SmoothDamp(_controller.center.y, targetCenterY, ref currentVelocityCenter, _crouchSmoothTime);
                _controller.center = new Vector3(0, newCenterY, 0);

                if (_playerCamera._lookType == LookType.FPP)
                {
                    float newCamY = Mathf.SmoothDamp(_camera.transform.localPosition.y, targetCamY, ref currentVelocityCam, _crouchSmoothTime);
                    _camera.transform.localPosition = new Vector3(_camera.transform.localPosition.x, newCamY, _camera.transform.localPosition.z);
                }

                _isInDuckPosition = !endingCrouch;
                isCrouching = !endingCrouch;
                _selectedSpeed = endingCrouch ? _walkSpeed : _crouchSpeed;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            _controller.height = targetHeight;
            _controller.center = new Vector3(0, targetCenterY, 0);
            if (_playerCamera._lookType == LookType.FPP)
            {
                _camera.transform.localPosition = new Vector3(_camera.transform.localPosition.x, targetCamY, _camera.transform.localPosition.z);
            }
        }

        /// <summary>
        /// Triggers crouch camera animation.
        /// </summary>
        /// <param name="endingCrouch">Is crouch ending?</param>
        /// <param name="targetY">Target camera position.</param>
        private void TriggerCrouchAnimation(bool endingCrouch, float targetY)
        {
            _crouchCTS?.Cancel();
            _crouchCTS = new CancellationTokenSource();

            SmoothCameraCrouch(endingCrouch, targetY, _crouchCTS.Token).Forget();
        }

        /// <summary>
        /// Выбирает вектор 
        /// </summary>
        /// <param name="lookType"></param>
        /// <returns></returns>
        private Vector3 SelectMovementInputVector(LookType lookType)
        {
            switch (lookType)
            {
                case LookType.FPP:
                    return transform.right * MovementVector.x + transform.forward * MovementVector.y;

                case LookType.TTP:
                    return new Vector3(MovementVector.y, 0f, -MovementVector.x).normalized;

                default:
                    return Vector3.forward;
            }
        }
    }
}
