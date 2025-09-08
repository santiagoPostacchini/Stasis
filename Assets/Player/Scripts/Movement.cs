using System.Collections.Generic;
using UnityEngine;

namespace Player.Scripts
{
    [RequireComponent(typeof(Rigidbody))]
    public class Movement : MonoBehaviour
    {
        private static readonly int XAxis = Animator.StringToHash("xAxis");
        private static readonly int ZAxis = Animator.StringToHash("zAxis");
        private static readonly int IsStopping = Animator.StringToHash("isStopping");

        [Header("<color=orange>States</color>")]
        [SerializeField] public bool isRunning;
        [SerializeField] public bool isWalking;
        [SerializeField] public bool isInIdle;
        [SerializeField] public bool isStopping;

        [Header("<color=yellow>Bools</color>")]
        [SerializeField] public bool canMove = true;  
        [SerializeField] public bool canRun = true;

        [Header("<color=green>Movement Settings</color>")]
        [SerializeField] private KeyCode runningKey = KeyCode.LeftShift;
        [SerializeField] private float walkingSpeed = 3f;
        [SerializeField] private float runningSpeed = 6f;
        [SerializeField] private float acceleration = 20f;
        [SerializeField] private float deceleration = 30f;
        [SerializeField] public float xAxis, zAxis;          // Suavizados (GetAxis)

        // --- NUEVO: ejes crudos para lógica de stop ---
        private float _rawX, _rawZ;                            // GetAxisRaw (sin smoothing)

        [Header("<color=cyan>Animator Settings</color>")]
        public float animX, animZ;     
        private float _targetAnimX, _targetAnimZ;
        [SerializeField] private float animLerpSpeed = 8f; 

        [Header("<color=red>Collition Settings</color>")]
        [SerializeField] public float moveCheckDist = 0.75f;
        [SerializeField] public LayerMask moveCheckMask;

        private Rigidbody _rigidbody;
        private Ray _moveCheckRay;
        private readonly List<System.Func<float>> _speedOverrides = new List<System.Func<float>>();
        private Animator _animator;

        private bool _wasMovingByInput;
        [SerializeField] private float stopThreshold = 0.05f;
        [SerializeField] private float moveThreshold = 0.20f;
        [SerializeField] private float stopCooldown = 0.2f;
        private float _stopTimer;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (canMove)  
            {
                // Ejes para gameplay/anim (suavizados)
                xAxis = Mathf.Clamp(Input.GetAxis("Horizontal"), -1f, 1f);
                zAxis = Mathf.Clamp(Input.GetAxis("Vertical"), -1f, 1f);
                
                _rawX = Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f);
                _rawZ = Mathf.Clamp(Input.GetAxisRaw("Vertical"), -1f, 1f);

                float targetMax = isRunning ? 1f : 0.5f; // camina llega a 0.5, corre a 1.0
                _targetAnimX = Mathf.Clamp(xAxis, -1f, 1f) * targetMax;
                _targetAnimZ = Mathf.Clamp(zAxis, -1f, 1f) * targetMax;

                animX = Mathf.Lerp(animX, _targetAnimX, Time.deltaTime * animLerpSpeed);
                animZ = Mathf.Lerp(animZ, _targetAnimZ, Time.deltaTime * animLerpSpeed);

                // Nunca pasar de -1..1 al Animator
                animX = Mathf.Clamp(animX, -1f, 1f);
                animZ = Mathf.Clamp(animZ, -1f, 1f);

                if(_animator)
                {
                    _animator.SetFloat(XAxis, animX);
                    _animator.SetFloat(ZAxis, animZ);
                    _animator.SetBool(IsStopping, isStopping);
                }

                HandleStoppingLogic(); // ahora basado en input crudo + histéresis
            }
        }

        private void FixedUpdate()
        {
            if (canMove)  
            {
                HandleRunning();
                HandleMovement();
                UpdateMovementState();
            }
            else
            {
                _rigidbody.velocity = new Vector3(0, _rigidbody.velocity.y, 0);
            }
        }

        private void HandleRunning()
        {
            if (canMove)  
            {
                isRunning = canRun && Input.GetKey(runningKey) && 
                             (Mathf.Abs(xAxis) > 0.1f || Mathf.Abs(zAxis) > 0.1f);
            }
        }

        private void HandleMovement()
        {
            if (canMove) 
            {
                float targetSpeed = isRunning ? runningSpeed : walkingSpeed;
                if (_speedOverrides.Count > 0)
                {
                    targetSpeed = _speedOverrides[^1]();
                }

                Vector2 inputDirection = new Vector2(xAxis, zAxis);
                if (inputDirection.magnitude > 1f) inputDirection.Normalize();

                Vector3 movementDirection = transform.rotation * new Vector3(inputDirection.x, 0, inputDirection.y);

                if (inputDirection.magnitude > 0 && !IsBlocked(inputDirection.x, inputDirection.y))
                {
                    ApplyAcceleration(movementDirection, targetSpeed);
                }
                else
                {
                    ApplyDeceleration();
                }

                ClampVelocity(targetSpeed);
            }
        }

        private void ApplyAcceleration(Vector3 direction, float targetSpeed)
        {
            Vector3 targetVelocity = direction * targetSpeed;
            Vector3 velocityChange = targetVelocity - new Vector3(_rigidbody.velocity.x, 0, _rigidbody.velocity.z);
            velocityChange = Vector3.ClampMagnitude(velocityChange, acceleration * Time.fixedDeltaTime);
            _rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        private void ApplyDeceleration()
        {
            Vector3 horizontalVelocity = new Vector3(_rigidbody.velocity.x, 0, _rigidbody.velocity.z);
            Vector3 decelerationForce = -horizontalVelocity * (deceleration * Time.fixedDeltaTime);
            _rigidbody.AddForce(decelerationForce, ForceMode.VelocityChange);

            if (horizontalVelocity.magnitude < 0.1f)
            {
                _rigidbody.velocity = new Vector3(0, _rigidbody.velocity.y, 0);
            }
        }

        private void ClampVelocity(float maxSpeed)
        {
            Vector3 clampedVelocity = Vector3.ClampMagnitude(new Vector3(_rigidbody.velocity.x, 0, _rigidbody.velocity.z), maxSpeed);
            _rigidbody.velocity = new Vector3(clampedVelocity.x, _rigidbody.velocity.y, clampedVelocity.z);
        }

        private bool IsBlocked(float x, float z)
        {
            Vector3 moveOrigin = new Vector3(transform.position.x, transform.position.y + 0.1f, transform.position.z);
            Vector3 moveCheckDir = (transform.right * x + transform.forward * z);

            _moveCheckRay = new Ray(moveOrigin, moveCheckDir);

            return Physics.Raycast(_moveCheckRay, moveCheckDist, moveCheckMask);
        }

        private void UpdateMovementState()
        {
            if (isRunning)
            {
                isWalking = false;
                isInIdle = false;
            }
            else if (Mathf.Abs(xAxis) > 0.1f || Mathf.Abs(zAxis) > 0.1f)
            {
                isWalking = true;
                isInIdle = false;
            }
            else
            {
                isWalking = false;
                isInIdle = true;
            }
        }

        private void HandleStoppingLogic()
        {
            // Usamos magnitud del input crudo para evitar el smoothing de GetAxis
            float rawMag = new Vector2(_rawX, _rawZ).magnitude;

            bool inputIsZero   = rawMag < stopThreshold;  // “solté” (casi cero)
            bool hadInputBefore = _wasMovingByInput;      // frame anterior
            bool hasInputNow   = rawMag > moveThreshold;  // “me estaba moviendo” (umbral superior)

            // Dispara al detectar transición “tenía input” -> “ahora cero”
            if (hadInputBefore && inputIsZero && !isStopping)
            {
                isStopping = true;
                _stopTimer = stopCooldown;
            }

            // Duración del pulso
            if (isStopping)
            {
                _stopTimer -= Time.deltaTime;
                if (_stopTimer <= 0f)
                {
                    isStopping = false;
                }
            }

            // Actualizamos el estado “tenía input”
            _wasMovingByInput = hasInputNow;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(_moveCheckRay);
        }
    }
}





