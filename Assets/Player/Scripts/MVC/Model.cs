using System;
using UnityEngine;

namespace Player.Scripts.MVC
{
    public class Model : MonoBehaviour
    {
        public float SprintSpeed { get; set; } = 7f;
        public float CrouchSpeed { get; set; } = 3f;
        public float Acceleration { get; set; } = 20f;
        public float Deceleration { get; set; } = 30f;
        public float AirControlMultiplier { get; set; } = 0.4f;
        public float JumpForce { get; set; } = 8f;
        public float Gravity { get; set; } = -18f;
        public float StandHeight { get; set; } = 2f;
        public float CrouchHeight { get; set; } = 1f;
        public float StandCenterY { get; set; } = 1f;
        public float CrouchCenterY { get; set; } = 0.5f;
        public float EyeOffset { get; set; } = 0.6f;

        public float VerticalVelocity { get; private set; }
        public Vector3 CurrentVelocity { get; private set; }
        public bool ReadyToJump { get; private set; } = true;
        public MovementState State { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool WasGrounded { get; private set; }

        public enum MovementState { Sprinting, Crouching, Air }

        public event Action OnJump, OnLand, OnIdle, OnCrouchEnter, OnCrouchExit, OnShot, OnGrab, OnDrop, OnThrow;

        [SerializeField] private CharacterController _characterController;

        private float _horizontalInput, _verticalInput;

        public void SetInput(float horizontal, float vertical)
        {
            _horizontalInput = horizontal;
            _verticalInput = vertical;
        }

        public void Update()
        {
            UpdateState(_characterController.isGrounded);
            HandleMovement(Time.deltaTime);
            ApplyGravity(Time.deltaTime);
            HandleJumpLogic();

            Vector3 velocity = CurrentVelocity;
            velocity.y = VerticalVelocity;
            _characterController.Move(velocity * Time.deltaTime);

            HandleCrouchTransition();
            SetWasGrounded(_characterController.isGrounded);
        }

        public void TryJump()
        {
            if (ReadyToJump && _characterController.isGrounded && !IsCrouching)
            {
                VerticalVelocity = JumpForce;
                ReadyToJump = false;
                OnJump?.Invoke();
            }
        }

        public void TryCrouch(bool crouching)
        {
            if (crouching && !IsCrouching)
                OnCrouchEnter?.Invoke();
            if (!crouching && IsCrouching)
                OnCrouchExit?.Invoke();
            IsCrouching = crouching;
        }

        public void Shot()  => OnShot?.Invoke();
        public void Grab()  => OnGrab?.Invoke();
        public void Drop()  => OnDrop?.Invoke();
        public void Throw() => OnThrow?.Invoke();

        private void UpdateState(bool grounded)
        {
            if (IsCrouching)
                State = MovementState.Crouching;
            else if (grounded)
                State = MovementState.Sprinting;
            else
                State = MovementState.Air;
        }

        private void HandleMovement(float deltaTime)
        {
            Vector3 moveDirection = (transform.forward * _verticalInput + transform.right * _horizontalInput).normalized;
            float targetSpeed = IsCrouching ? CrouchSpeed : SprintSpeed;
            Vector3 desiredVelocity = moveDirection * targetSpeed;
            Vector3 flatCurrentVel = new Vector3(CurrentVelocity.x, 0f, CurrentVelocity.z);

            if (flatCurrentVel.magnitude <= 0.1f)
                OnIdle?.Invoke();

            float effAcc = _characterController.isGrounded ? Acceleration : Acceleration * AirControlMultiplier;
            float effDec = _characterController.isGrounded ? Deceleration : Deceleration * AirControlMultiplier;

            if (moveDirection.magnitude > 0.1f)
                flatCurrentVel = Vector3.MoveTowards(flatCurrentVel, desiredVelocity, effAcc * deltaTime);
            else
                flatCurrentVel = Vector3.MoveTowards(flatCurrentVel, Vector3.zero, effDec * deltaTime);

            CurrentVelocity = new Vector3(flatCurrentVel.x, CurrentVelocity.y, flatCurrentVel.z);
        }

        private void ApplyGravity(float deltaTime)
        {
            VerticalVelocity += Gravity * deltaTime;
        }

        private void HandleJumpLogic()
        {
            if (_characterController.isGrounded && VerticalVelocity < 0)
                VerticalVelocity = -2f;
            if (!_characterController.isGrounded && !ReadyToJump && _characterController.velocity.y < 0)
                ReadyToJump = true;
        }

        private void HandleCrouchTransition()
        {
            float targetHeight = IsCrouching ? CrouchHeight : StandHeight;
            float targetCenterY = IsCrouching ? CrouchCenterY : StandCenterY;
            _characterController.height = targetHeight;
            _characterController.center = new Vector3(0, targetCenterY, 0);
        }

        private void SetWasGrounded(bool value)
        {
            if (value && !WasGrounded)
                OnLand?.Invoke();
            WasGrounded = value;
        }
    }
}