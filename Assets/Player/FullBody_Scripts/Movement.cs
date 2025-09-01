using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Movement : MonoBehaviour
{
    [Header("<color=orange>States</color>")]
    [SerializeField] public bool _isRunning;
    [SerializeField] public bool _isWalking;
    [SerializeField] public bool _isInIdle;
    [SerializeField] public bool _isStopping; // <-- NUEVO

    [Header("<color=yellow>Bools</color>")]
    [SerializeField] public bool _canMove = true;  
    [SerializeField] public bool _canRun = true;

    [Header("<color=green>Movement Settings</color>")]
    [SerializeField] private KeyCode _runningKey = KeyCode.LeftShift;
    [SerializeField] private float _walkingSpeed = 3f;
    [SerializeField] private float _runningSpeed = 6f;
    [SerializeField] private float _acceleration = 20f;
    [SerializeField] private float _deceleration = 30f;
    [SerializeField] public float _xAxis, _zAxis;

    [Header("<color=cyan>Animator Settings</color>")]
    public float _animX, _animZ;     
    private float _targetAnimX, _targetAnimZ;
    [SerializeField] private float _animLerpSpeed = 8f; 

    [Header("<color=red>Collition Settings</color>")]
    [SerializeField] public float _moveCheckDist = 0.75f;
    [SerializeField] public LayerMask _moveCheckMask;

    new private Rigidbody rigidbody;
    private Ray _moveCheckRay;
    public List<System.Func<float>> speedOverrides = new List<System.Func<float>>();
    private Animator animator;

    // --- Variables para detectar frenado ---
    private bool _wasMoving;
    [SerializeField] private float stopThreshold = 0.1f; // input casi cero
    [SerializeField] private float moveThreshold = 0.25f; // animX o animZ mayor a 0.25 = se considera en movimiento
    [SerializeField] private float stopCooldown = 0.2f; // cuanto dura el bool de stop
    private float _stopTimer;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (_canMove)  
        {
            _xAxis = Input.GetAxis("Horizontal");
            _zAxis = Input.GetAxis("Vertical");

            float targetMax = _isRunning ? 1f : 0.5f;
            _targetAnimX = Mathf.Clamp(_xAxis, -1f, 1f) * targetMax;
            _targetAnimZ = Mathf.Clamp(_zAxis, -1f, 1f) * targetMax;

            _animX = Mathf.Lerp(_animX, _targetAnimX, Time.deltaTime * _animLerpSpeed);
            _animZ = Mathf.Lerp(_animZ, _targetAnimZ, Time.deltaTime * _animLerpSpeed);

            if(animator != null)
            {
                animator.SetFloat("xAxis", _animX);
                animator.SetFloat("zAxis", _animZ);
                animator.SetBool("isStopping", _isStopping); // <-- Enviar al animator
            }

            HandleStoppingLogic(); // <-- detectar frenado
        }
    }

    private void FixedUpdate()
    {
        if (_canMove)  
        {
            HandleRunning();
            HandleMovement();
            UpdateMovementState();
        }
        else
        {
            rigidbody.velocity = new Vector3(0, rigidbody.velocity.y, 0);
        }
    }

    private void HandleRunning()
    {
        if (_canMove)  
        {
            _isRunning = _canRun && Input.GetKey(_runningKey) && (Mathf.Abs(_xAxis) > 0.1f || Mathf.Abs(_zAxis) > 0.1f);
        }
    }

    private void HandleMovement()
    {
        if (_canMove) 
        {
            float targetSpeed = _isRunning ? _runningSpeed : _walkingSpeed;
            if (speedOverrides.Count > 0)
            {
                targetSpeed = speedOverrides[speedOverrides.Count - 1]();
            }

            Vector2 inputDirection = new Vector2(_xAxis, _zAxis);
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
        Vector3 velocityChange = targetVelocity - new Vector3(rigidbody.velocity.x, 0, rigidbody.velocity.z);
        velocityChange = Vector3.ClampMagnitude(velocityChange, _acceleration * Time.fixedDeltaTime);
        rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    private void ApplyDeceleration()
    {
        Vector3 horizontalVelocity = new Vector3(rigidbody.velocity.x, 0, rigidbody.velocity.z);
        Vector3 decelerationForce = -horizontalVelocity * _deceleration * Time.fixedDeltaTime;
        rigidbody.AddForce(decelerationForce, ForceMode.VelocityChange);

        if (horizontalVelocity.magnitude < 0.1f)
        {
            rigidbody.velocity = new Vector3(0, rigidbody.velocity.y, 0);
        }
    }

    private void ClampVelocity(float maxSpeed)
    {
        Vector3 clampedVelocity = Vector3.ClampMagnitude(new Vector3(rigidbody.velocity.x, 0, rigidbody.velocity.z), maxSpeed);
        rigidbody.velocity = new Vector3(clampedVelocity.x, rigidbody.velocity.y, clampedVelocity.z);
    }

    private bool IsBlocked(float xAxis, float zAxis)
    {
        Vector3 moveOrigin = new Vector3(transform.position.x, transform.position.y + 0.1f, transform.position.z);
        Vector3 moveCheckDir = (transform.right * xAxis + transform.forward * zAxis);

        _moveCheckRay = new Ray(moveOrigin, moveCheckDir);

        return Physics.Raycast(_moveCheckRay, _moveCheckDist, _moveCheckMask);
    }

    private void UpdateMovementState()
    {
        if (_isRunning)
        {
            _isWalking = false;
            _isInIdle = false;
        }
        else if (Mathf.Abs(_xAxis) > 0.1f || Mathf.Abs(_zAxis) > 0.1f)
        {
            _isWalking = true;
            _isInIdle = false;
        }
        else
        {
            _isWalking = false;
            _isInIdle = true;
        }
    }

    private void HandleStoppingLogic()
    {
        bool isMovingNow = Mathf.Abs(_animX) > moveThreshold || Mathf.Abs(_animZ) > moveThreshold;
        bool inputIsZero = Mathf.Abs(_xAxis) < stopThreshold && Mathf.Abs(_zAxis) < stopThreshold;

        // Detecta cuando estaba moviéndose y ahora deja de dar input
        if (_wasMoving && inputIsZero && !_isStopping)
        {
            _isStopping = true;
            _stopTimer = stopCooldown;
        }

        // Controla duración del stopping
        if (_isStopping)
        {
            _stopTimer -= Time.deltaTime;
            if (_stopTimer <= 0f)
            {
                _isStopping = false;
            }
        }

        _wasMoving = isMovingNow;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(_moveCheckRay);
    }
}





