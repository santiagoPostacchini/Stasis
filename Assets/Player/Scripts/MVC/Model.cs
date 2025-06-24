using System;
using System.Collections;
using Player.Camera;
using Puzzle_Elements.AllInterfaces;
using UnityEngine;
using Managers.Events;

namespace Player.Scripts.MVC
{
    public class Model : MonoBehaviour, IPlateActivator
    {
        [Header("Movement")] [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float crouchSpeed = 3f;
        [SerializeField] private float wallRunSpeed = 3f;
        [SerializeField] private float acceleration = 20f;
        [SerializeField] private float deceleration = 30f;
        [SerializeField] private float airControlMultiplier = 0.4f;
        [SerializeField] private float terminalVelocity = -50f;
        [SerializeField] private float minVerticalVelocity = -2f;


        [Header("Jumping")] [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float jumpCooldown = 0.25f;
        [SerializeField] private float gravity = -18f;
        private bool _useGravity = true;
        private bool _readyToJump = true;

        [Header("Crouch")] [SerializeField] private float standHeight = 2f;
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float standCenterY;
        [SerializeField] private float crouchCenterY = 0.5f;
        [SerializeField] private float eyeOffset = 0.6f;
        [SerializeField, Range(0.01f, 0.2f)] private float crouchSmoothTime = 0.08f;

        [Header("WallRunning")] 
        public LayerMask whatIsWallrun;
        [SerializeField] private float wallRunForce;
        [SerializeField] private float wallJumpUpForce;
        [SerializeField] private float wallJumpSideForce;
        public float maxWallRunTime;
        private float _wallRunTimer;

        [Header("Exiting Wall")] public bool exitingWall;
        public float exitWallTime;
        [HideInInspector] public float exitWallTimer;

        [Header("Wallrun Detection")] public float wallCheckDistance;
        public float minJumpHeight;
        private RaycastHit _leftWallHit;
        private RaycastHit _rightWallHit;
        [HideInInspector] public bool wallRight;
        [HideInInspector] public bool wallLeft;
        
        [Header("Vaulting")]
        [SerializeField] private float vaultCheckDistance = 1.2f;
        [SerializeField] private float minVaultHeight = 0.5f;
        [SerializeField] private float maxVaultHeight = 1.7f;
        [SerializeField] private float vaultDuration = 0.45f;
        [SerializeField] private AnimationCurve vaultArcCurve;
        [SerializeField] private LayerMask whatIsVaultable;
        [SerializeField] private LayerMask whatIsGround;

        [Header("InputKeys")] public KeyCode jumpKey = KeyCode.Space;
        public KeyCode crouchKey = KeyCode.LeftControl;

        [Header("Life")] private float _currentLife;
        private float _baseLife;

        private float _crouchTargetHeight = 2f;
        private float _crouchTargetCenterY;
        private float _heightSmoothVelocity;
        private float _centerYSmoothVelocity;
        
        [Header("Movement References")]
        [SerializeField] private Transform orientation;
        [SerializeField] private Transform cameraTransform;

        private float _horizontalInput;
        private float _verticalInput;
        private Vector3 _moveDirection;
        private Vector3 _currentVelocity;
        private float _verticalVelocity;

        [HideInInspector] public CharacterController characterController;
        public PlayerCam cam;
        private Coroutine _currentCrouchRoutine;
        private bool _beingLaunched;
        private float _launchTimeLeft;
        private Vector3 _launchVelocity = Vector3.zero;

        public enum MovementState
        {
            Moving,
            Crouching,
            Air,
            Wallrunning,
            Vaulting
        }

        public MovementState state = MovementState.Moving;

        public bool wallrunning;
        public bool isVaulting;

        public event Action OnLand = delegate { };
        public event Action<bool> OnCrouch = delegate { };
        public event Action OnJump = delegate { };
        public event Action OnMove = delegate { };
        public event Action<float> OnGetDamage = delegate { };
        public event Action OnDeath = delegate { };
        public event Action OnVaultStart = delegate { };
        public event Action OnVaultEnd = delegate { };
        public event Action<float> OnSpeedChange = delegate { };

        public event Action OnClimb = delegate { };

        IController _controller;

        private void Start()
        {
            characterController = GetComponent<CharacterController>();
            _controller = new Controller(this, GetComponent<View>(), crouchKey, jumpKey);
            UpdateCameraHeight();
        }

        private void Update()
        {
            _controller.OnUpdate();
            CheckForWall();
            if(!isVaulting)
                CheckForVault();
            
            if (wallrunning)
                WallRunningMovement();

            ApplyGravity();
            //
        }

        public void UpdateMoveInput(float vertical, float horizontal)
        {
            _horizontalInput = horizontal;
            _verticalInput = vertical;
            if (horizontal != 0f || vertical != 0f)
            {
                OnMove();
            }

            Move();
            Vector3 flatCurrentVel = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
            float flatSpeed = flatCurrentVel.magnitude; // Velocidad en plano XZ
            OnSpeedChange(flatSpeed); // ¡Acá notificás la velocidad al Animator!

        }

        public void UpdateCrouchInput(bool isCrouching)
        {
            if (isCrouching)
            {
                _crouchTargetHeight = crouchHeight;
                _crouchTargetCenterY = crouchCenterY;
            }
            else
            {
                _crouchTargetHeight = standHeight;
                _crouchTargetCenterY = standCenterY;
            }

            OnCrouch(isCrouching);
        }

        public void UpdateJumpInput()
        {
            Jump();
        }

        private void Move()
        {
            if (_beingLaunched && _launchTimeLeft > 0f)
            {
                characterController.Move(_launchVelocity * Time.deltaTime);
                _launchTimeLeft -= Time.deltaTime;
                if (_launchTimeLeft <= 0f)
                {
                    _beingLaunched = false;
                    _launchVelocity = Vector3.zero;
                }
                
                return;
            }
            
            if (isVaulting) return;
            _moveDirection = (orientation.forward * _verticalInput + orientation.right * _horizontalInput).normalized;
            float targetSpeed = (state == MovementState.Crouching) ? crouchSpeed : moveSpeed;
            Vector3 desiredVelocity = _moveDirection * targetSpeed;

            var flatCurrentVel = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);

            float effectiveAcceleration = acceleration;
            float effectiveDeceleration = deceleration;
            if (!characterController.isGrounded)
            {
                effectiveAcceleration *= airControlMultiplier;
                effectiveDeceleration *= airControlMultiplier;
            }

            flatCurrentVel = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
            var flatDesiredVel = new Vector3(desiredVelocity.x, 0f, desiredVelocity.z);

            if (_moveDirection.magnitude > 0.1f)
            {
                flatCurrentVel = Vector3.MoveTowards(
                    flatCurrentVel,
                    flatDesiredVel,
                    effectiveAcceleration * Time.deltaTime
                );
            }
            else
            {
                flatCurrentVel = Vector3.MoveTowards(
                    flatCurrentVel,
                    Vector3.zero,
                    effectiveDeceleration * Time.deltaTime
                );
            }

            _currentVelocity = new Vector3(flatCurrentVel.x, _currentVelocity.y, flatCurrentVel.z);

            Vector3 finalVelocity = new Vector3(flatCurrentVel.x, _verticalVelocity, flatCurrentVel.z);
            characterController.Move(finalVelocity * Time.deltaTime);
        }

        private void Jump()
        {
            if (characterController.isGrounded && _verticalVelocity < 0)
                _verticalVelocity = -2f;
            
            if (characterController.isGrounded && state != MovementState.Crouching && _readyToJump)
            {
                Debug.Log("SALTO");
                OnJump();
                EventManager.TriggerEvent("OnJump", gameObject);
                _verticalVelocity = jumpForce;
                _readyToJump = false;
                Invoke(nameof(ResetJump), jumpCooldown);
            }
        }

        private void ResetJump()
        {
            _readyToJump = true;
        }

        private void ApplyGravity()
        {
            if (!_useGravity) return;

            if (characterController.isGrounded)
            {
                if (_verticalVelocity < 0)
                {
                    _verticalVelocity = minVerticalVelocity;
                }
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
                if (_verticalVelocity < terminalVelocity)
                {
                    _verticalVelocity = terminalVelocity;
                }
            }
        }
        
        private void StartWallRunning()
        {
            wallrunning = true;
            _verticalVelocity = 0f;
            _useGravity = false;

            _wallRunTimer = maxWallRunTime;

            cam.DoFov(80f);
            if (wallLeft) cam.DoTilt(-5f);
            if (wallRight) cam.DoTilt(5f);
            Climb();
        }
        public void Climb()
        {
            OnClimb();
            EventManager.TriggerEvent("OnClimb", gameObject);
        }
        private void WallRunningMovement()
        {
            Vector3 wallNormal = wallRight ? _rightWallHit.normal : _leftWallHit.normal;

            Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up);
            if ((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
                wallForward = -wallForward;

            Vector3 move = wallForward * (wallRunForce * Time.deltaTime);

            characterController.Move(move);
        }

        private void StopWallRunning()
        {
            _useGravity = true;
            wallrunning = false;
            _wallRunTimer = maxWallRunTime;
            cam.DoFov(65f);
            cam.DoTilt(0f);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void WallJump()
        {
            exitingWall = true;
            exitWallTimer = exitWallTime;

            Vector3 wallNormal = wallRight ? _rightWallHit.normal : _leftWallHit.normal;

            Vector3 jumpDirection = transform.up * wallJumpUpForce + wallNormal * wallJumpSideForce;

            _verticalVelocity = 0f;

            Vector3 planarJump = new Vector3(jumpDirection.x, 0f, jumpDirection.z);
            float verticalJump = jumpDirection.y;

            _currentVelocity += planarJump;
            _verticalVelocity += verticalJump;

            OnJump();
        }

        void CheckForWall()
        {
            wallRight = Physics.Raycast(transform.position, orientation.right, out _rightWallHit, wallCheckDistance,
                whatIsWallrun);
            wallLeft = Physics.Raycast(transform.position, -orientation.right, out _leftWallHit, wallCheckDistance,
                whatIsWallrun);
        }

        public bool AboveGround()
        {
            return !Physics.Raycast(transform.position, Vector3.down, minJumpHeight, whatIsGround);
        }
        
        private float _nextVaultAllowed;
        [SerializeField] private float vaultCooldown = 0.2f;
        
        // ReSharper disable Unity.PerformanceAnalysis
        void CheckForVault()
        {
            if (isVaulting || Time.time < _nextVaultAllowed) return;
            if (_verticalInput <= 0f) return;

            // Usar centro del Capsule para raycastear (mejor intersección)
            Vector3 rayOrigin = transform.position + Vector3.up * (characterController.height * 0.5f) + characterController.center;
            if (Physics.Raycast(rayOrigin, orientation.forward, out RaycastHit wallHit, vaultCheckDistance, whatIsVaultable))
            {
                float playerFeet = transform.position.y - (characterController.height * 0.5f) + characterController.center.y;
                float obstacleTop = wallHit.collider.bounds.max.y;
                float obstacleHeight = obstacleTop - playerFeet;

                if (obstacleHeight >= minVaultHeight && obstacleHeight <= maxVaultHeight)
                {
                    // Calculamos el punto adelante de la pared, sobre la superficie del piso real
                    Vector3 vaultLandingCheck = wallHit.point + orientation.forward * (characterController.radius + 0.3f);
                    vaultLandingCheck.y = obstacleTop + 0.1f; // Para evitar que el raycast caiga dentro del objeto

                    // Raycast DOWN para encontrar el piso real adelante
                    if (Physics.Raycast(vaultLandingCheck, Vector3.down, out RaycastHit groundHit, 3f, whatIsGround))
                    {
                        Vector3 vaultEndPos = groundHit.point;
                        vaultEndPos.y += 0.01f; // Pequeño offset para no quedar flotando
                        StartCoroutine(VaultRoutine(vaultEndPos, obstacleTop));
                    }
                }
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        IEnumerator VaultRoutine(Vector3 vaultEnd, float obstacleTop)
        {
            isVaulting = true;
            OnVaultStart();
            _nextVaultAllowed = Time.time + vaultCooldown;

            Vector3 start = transform.position;
            Vector3 end = vaultEnd;
            float apexY = Mathf.Max(start.y, obstacleTop + 0.5f);
            Vector3 apex = new Vector3(
                (start.x + end.x) * 0.5f,
                apexY,
                (start.z + end.z) * 0.5f
            );

            float elapsed = 0f;
            float t;
            _useGravity = false;
            
            characterController.enabled = false;

            while (elapsed < vaultDuration)
            {
                t = elapsed / vaultDuration;

                Vector3 lerp1 = Vector3.Lerp(start, apex, t);
                Vector3 lerp2 = Vector3.Lerp(apex, end, t);
                Vector3 pos = Vector3.Lerp(lerp1, lerp2, t);

                transform.position = pos;
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.position = end;
            characterController.enabled = true;

            _useGravity = true;
            isVaulting = false;
            OnVaultEnd();
        }

        public void UpdateCrouchPosition()
        {
            characterController.height = Mathf.SmoothDamp(
                characterController.height,
                _crouchTargetHeight,
                ref _heightSmoothVelocity,
                crouchSmoothTime
            );

            characterController.center = new Vector3(
                0f,
                Mathf.SmoothDamp(
                    characterController.center.y,
                    _crouchTargetCenterY,
                    ref _centerYSmoothVelocity,
                    crouchSmoothTime
                ),
                0f
            );

            UpdateCameraHeight();
        }


        public void StateUpdater(MovementState newState)
        {
            state = newState;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void StateMachine(float verticalInput, bool jumping)
        {
            if ((wallLeft || wallRight) && verticalInput > 0 && AboveGround() && !exitingWall)
            {
                if (!wallrunning)
                {
                    StartWallRunning();
                }

                if (_wallRunTimer > 0)
                {
                    _wallRunTimer -= Time.deltaTime;
                }

                if (_wallRunTimer <= 0 && wallrunning)
                {
                    exitingWall = true;
                    exitWallTimer = exitWallTime;
                }

                if (jumping)
                {
                    WallJump();
                }
            }

            else if (exitingWall)
            {
                if (wallrunning)
                {
                    StopWallRunning();
                }

                if (exitWallTimer > 0)
                {
                    exitWallTimer -= Time.deltaTime;
                }

                if (exitWallTimer <= 0)
                {
                    exitingWall = false;
                }
            }

            else
            {
                if (wallrunning)
                {
                    StopWallRunning();
                }
            }
        }

        private void UpdateCameraHeight()
        {
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                characterController.center.y + eyeOffset,
                cameraTransform.localPosition.z
            );
        }

        public void TakeDamage(float dmg)
        {
            _currentLife -= dmg;

            OnGetDamage(_currentLife / _baseLife);

            if (_currentLife <= 0)
            {
                Death();
            }
        }

        void Death()
        {
            OnDeath();
        }
        
        public void SetLaunchSplinePosition(Vector3 pos)
        {
            characterController.enabled = false;
            transform.position = pos;
            characterController.enabled = true;
        }
    }
}