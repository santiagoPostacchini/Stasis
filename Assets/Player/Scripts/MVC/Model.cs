using System;
using Puzzle_Elements.AllInterfaces;
using UnityEngine;

namespace Player.Scripts.MVC
{
    public class Model : MonoBehaviour, IPlateActivator
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float crouchSpeed = 3f;
        [SerializeField] private float acceleration = 20f;
        [SerializeField] private float deceleration = 30f;
        [SerializeField] private float airControlMultiplier = 0.4f;

        [Header("Jumping")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float jumpCooldown = 0.25f;
        [SerializeField] private float gravity = -18f;
        private bool _readyToJump = true;

        [Header("Crouch")]
        [SerializeField] private float standHeight = 2f;
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float standCenterY;
        [SerializeField] private float crouchCenterY = 0.5f;
        [SerializeField] private float eyeOffset = 0.6f;
        [SerializeField, Range(0.01f, 0.2f)] private float crouchSmoothTime = 0.08f;
        
        [Header("InputKeys")]
        public KeyCode jumpKey = KeyCode.Space;
        public KeyCode crouchKey = KeyCode.LeftControl;
        
        [Header("Life")]
        private float _currentLife;
        private float _baseLife;

        private float _crouchTargetHeight = 2f;
        private float _crouchTargetCenterY;
        private float _heightSmoothVelocity;
        private float _centerYSmoothVelocity;

        [SerializeField] private Transform orientation;
        [SerializeField] private Transform cameraTransform;
        
        private float _horizontalInput;
        private float _verticalInput;
        private Vector3 _moveDirection;
        private Vector3 _currentVelocity;
        private float _verticalVelocity;

        [HideInInspector] public CharacterController characterController;
        private Coroutine _currentCrouchRoutine;

        public enum MovementState { Moving, Crouching, Air }
        public MovementState state = MovementState.Moving;
        
        public event Action OnLand = delegate { };
        public event Action<bool> OnCrouch = delegate { };
        public event Action OnJump = delegate { };
        public event Action OnMove = delegate { };
        public event Action<float> OnGetDamage = delegate { };
        public event Action OnDeath = delegate { };
        
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
            ApplyGravity();
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
            _moveDirection = (orientation.forward * _verticalInput + orientation.right * _horizontalInput).normalized;
            float targetSpeed = (state == MovementState.Crouching) ? crouchSpeed : moveSpeed;
            Vector3 desiredVelocity = _moveDirection * targetSpeed;

            var flatCurrentVel = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
            //bool isIdle = flatCurrentVel.magnitude <= 0.1f;
            
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
                OnJump();
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
            _verticalVelocity += gravity * Time.deltaTime;
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

        private void UpdateCameraHeight()
        {
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                characterController.center.y + eyeOffset,
                cameraTransform.localPosition.z
            );
        }

        public void StateUpdater(MovementState newState)
        {
            state = newState;
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
    }
}