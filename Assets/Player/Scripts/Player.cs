using Managers.Events;
using Puzzle_Elements.AllInterfaces;
using UnityEngine;

namespace Player.Scripts
{
    public class Player : MonoBehaviour, IPlateActivator
    {
        [Header("Movement")]
        [SerializeField] private float sprintSpeed = 7f;
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

        private CharacterController _controller;
        private Coroutine _currentCrouchRoutine;

        public enum MovementState { Sprinting, Crouching, Air }
        public MovementState state;
        private bool IsIdle { get; set; }

        private bool _wasGrounded;
        private bool _isCrouching;

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            UpdateCameraHeight();
        }

        private void Update()
        {
            HandleInput();
            StateHandler();
            HandleJump();
            ApplyGravity();
            HandleMovement();
            HandleCrouchTransition();

            CheckGroundedEvents();
            CheckCrouchEvents();
        }

        private void HandleInput()
        {
            _horizontalInput = Input.GetAxisRaw("Horizontal");
            _verticalInput = Input.GetAxisRaw("Vertical");

            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                _crouchTargetHeight = crouchHeight;
                _crouchTargetCenterY = crouchCenterY;
            }
            else if (Input.GetKeyUp(KeyCode.LeftControl))
            {
                _crouchTargetHeight = standHeight;
                _crouchTargetCenterY = standCenterY;
            }
        }

        private void HandleMovement()
        {
            _moveDirection = (orientation.forward * _verticalInput + orientation.right * _horizontalInput).normalized;
            float targetSpeed = (state == MovementState.Crouching) ? crouchSpeed : sprintSpeed;
            Vector3 desiredVelocity = _moveDirection * targetSpeed;

            var flatCurrentVel = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
            IsIdle = flatCurrentVel.magnitude <= 0.1f;

            if (IsIdle)
            {
                EventManager.TriggerEvent("OnIdle", gameObject);
            }

            float effectiveAcceleration = acceleration;
            float effectiveDeceleration = deceleration;
            if (!_controller.isGrounded)
            {
                effectiveAcceleration *= airControlMultiplier;
                effectiveDeceleration *= airControlMultiplier;
            }

            flatCurrentVel = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);
            var flatDesiredVel = new Vector3(desiredVelocity.x, 0f, desiredVelocity.z);

            // Lerp o MoveTowards según input
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
            _controller.Move(finalVelocity * Time.deltaTime);
        }

        private void HandleJump()
        {
            if (_controller.isGrounded && _verticalVelocity < 0)
                _verticalVelocity = -2f;

            if (Input.GetKeyDown(KeyCode.Space) && _controller.isGrounded && state != MovementState.Crouching && _readyToJump)
            {
                _verticalVelocity = jumpForce;
                _readyToJump = false;
                Invoke(nameof(ResetJump), jumpCooldown);

                EventManager.TriggerEvent("OnJump", gameObject);
            }
        }

        private void ApplyGravity()
        {
            _verticalVelocity += gravity * Time.deltaTime;
        }

        private void HandleCrouchTransition()
        {
            _controller.height = Mathf.SmoothDamp(
                _controller.height,
                _crouchTargetHeight,
                ref _heightSmoothVelocity,
                crouchSmoothTime
            );

            _controller.center = new Vector3(
                0f,
                Mathf.SmoothDamp(
                    _controller.center.y,
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
                _controller.center.y + eyeOffset,
                cameraTransform.localPosition.z
            );
        }

        private void StateHandler()
        {
            if (Input.GetKey(KeyCode.LeftControl))
                state = MovementState.Crouching;
            else if (_controller.isGrounded)
                state = MovementState.Sprinting;
            else
                state = MovementState.Air;
        }

        private void ResetJump()
        {
            _readyToJump = true;
            EventManager.TriggerEvent("OnIdle", gameObject);
        }

        private void CheckGroundedEvents()
        {
            if (_controller.isGrounded && !_wasGrounded)
                EventManager.TriggerEvent("OnLand", gameObject);

            _wasGrounded = _controller.isGrounded;
        }

        private void CheckCrouchEvents()
        {
            bool currentlyCrouching = state == MovementState.Crouching;

            if (currentlyCrouching && !_isCrouching)
                EventManager.TriggerEvent("OnCrouchEnter", gameObject);
            else if (!currentlyCrouching && _isCrouching)
                EventManager.TriggerEvent("OnCrouchExit", gameObject);

            _isCrouching = currentlyCrouching;
        }
    }
}