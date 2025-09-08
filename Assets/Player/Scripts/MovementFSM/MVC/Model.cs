using System;
using Player.FullBody_Scripts.MovementFSM;
using Player.Scripts.Interactor;
using Player.Stasis;
using UnityEngine;

namespace Player.Scripts.MovementFSM.MVC
{
    public class Model : MonoBehaviour
    {
        public event Action OnLand = delegate { };
        public event Action<float, float> OnMove = delegate { };
        public event Action OnJump = delegate { };
        public event Action OnShoot = delegate { };
        public event Action<bool> OnStop = delegate { };
        public event Action<bool> OnRun = delegate { };
        public event Action OnCrouchStart = delegate { };
        public event Action OnCrouchEnd = delegate { };
        public event Action OnVaultStart = delegate { };
        public event Action OnVaultEnd = delegate { };
        public event Action OnClimbStart = delegate { };
        public event Action OnClimbEnd = delegate { };
        public event Action OnSlideStart = delegate { };
        public event Action OnSlideEnd = delegate { };
        public event Action OnGetDamage = delegate { };
        public event Action OnDeath = delegate { };

        IController _controller;

        private StasisGun _stasisGun;

        private PlayerInteractor _interactor;

        private FSM _fsm;

        public Rigidbody rb;
        
        private StairStepper _stair;

        [Header("Movement Keys")] public KeyCode runningKey = KeyCode.LeftShift;
        public KeyCode jumpKey = KeyCode.Space;

        [Header("Mouse Keys")] public KeyCode mouseLeft = KeyCode.Mouse0;
        public KeyCode mouseRight = KeyCode.Mouse1;

        [Header("Camera Settings")] public Transform cameraHolderTransform;

        [Header("<color=green>Movement Settings</color>")]
        public float walkingSpeed = 4f;

        public float runningSpeed = 8f;
        public float acceleration = 20f;
        public float deceleration = 30f;
        public float jumpHeight = 5f;

        [Header("Jump Assist")] public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.12f;

        [Header("Air Control")] public bool airEnteredFromGround;
        public float airMaxSpeed = 6f;
        public float airAcceleration = 12f;
        public float minAirTime = 0.08f;
        public float landVelThreshold = -2.5f;

        [Header("Grounding")] public CapsuleCollider capsule; // arrastrá tu capsule aquí
        public float groundCheckDistance = 0.2f; // > 0.15 para tolerancia
        public float maxGroundSlope = 55f; // grados
        public LayerMask groundMask;

        // Compartidos entre estados (timestamps)
        [HideInInspector] public float lastJumpPressedTime = -999f;
        [HideInInspector] public float lastLeftGroundTime = -999f;

        [HideInInspector] public float xAxis, zAxis, rawX, rawZ;
        [HideInInspector] public bool runningKeyPressed;
        [HideInInspector] public float stopThreshold = 0.05f;
        [HideInInspector] public float moveThreshold = 0.20f;
        [HideInInspector] public float stopCooldown = 0.2f;

        [HideInInspector] public bool wasMovingByInput;
        [HideInInspector] public float stopTimer;

        public bool canMove = true;
        public bool canRun = true;

        public bool HasJumpBuffered() => (Time.time - lastJumpPressedTime) <= jumpBufferTime;
        public void BufferJumpNow() => lastJumpPressedTime = Time.time;

        public bool HasJumpBufferedAfterLeftGround()
        {
            return lastJumpPressedTime >= (lastLeftGroundTime + 0.0001f);
        }

        public void ClearJumpBuffer()
        {
            lastJumpPressedTime = -999f;
        }
        
        [HideInInspector] public bool jumpDownThisFrame;
        
        public void RegisterJumpDownThisFrame()
        {
            jumpDownThisFrame = true;
        }

        private void Start()
        {
            _controller = new Controller(this, GetComponent<View>());
            _stasisGun = GetComponentInChildren<StasisGun>();
            _interactor = GetComponentInChildren<PlayerInteractor>();
            rb = GetComponent<Rigidbody>();
            _stair = GetComponent<StairStepper>();

            _fsm = new FSM();
            _fsm.CreateState(FSM.States.Grounded, new S_Grounded(_fsm, this, cameraHolderTransform));
            _fsm.CreateState(FSM.States.Climb, new S_Climb(_fsm, this));
            _fsm.CreateState(FSM.States.Slide, new S_Slide(_fsm, this));
            _fsm.CreateState(FSM.States.Vault, new S_Vault(_fsm, this));
            _fsm.CreateState(FSM.States.Air, new S_Air(_fsm, this, cameraHolderTransform));
            _fsm.CreateState(FSM.States.Wallrun, new S_Wallrun(_fsm, this));
            _fsm.ChangeState(FSM.States.Grounded);
        }

        private void Update()
        {
            _controller.OnUpdate();
            _fsm.ArtificialUpdate();
        }

        private void FixedUpdate()
        {
            _fsm.ArtificialFixedUpdate();
            _stair.ManualFixedStep();
        }

        private void LateUpdate()
        {
            jumpDownThisFrame = false;
        }

        public void UpdateAxisInput(float x, float z, float rx, float rz)
        {
            xAxis = x;
            zAxis = z;
            rawX = rx;
            rawZ = rz;
            OnMove?.Invoke(x, z);
        }

        public void UpdateRunKey(bool pressed)
        {
            runningKeyPressed = pressed;
        }

        public void JumpInput()
        {
            OnJump?.Invoke();
        }

        public void ShootInput()
        {
            OnShoot?.Invoke();
        }

        public void UpdateStopping(bool stp)
        {
            OnStop?.Invoke(stp);
        }

        public void UpdateIsRunning(bool run)
        {
            OnRun?.Invoke(run);
        }

        internal bool IsGroundedNow()
        {
            if (!capsule) return false;
            
            const float skin = 0.02f;
            const float minUpDot = 0.55f;
            const float maxSlopeDeg = 55f;
            float slopeLimit = maxGroundSlope > 0f ? maxGroundSlope : maxSlopeDeg;
            
            float radius = Mathf.Max(0.01f, capsule.radius - skin);
            Vector3 center = transform.TransformPoint(capsule.center);
            float half = capsule.height * 0.5f - radius;

            Vector3 top = center + Vector3.up * half;
            Vector3 bottom = center - Vector3.up * half;
            
            Vector3 bottomSphereCenter = bottom;
            
            var cols = Physics.OverlapCapsule(
                top, bottom, radius + skin, groundMask, QueryTriggerInteraction.Ignore);

            foreach (var col in cols)
            {
                if (!col || col.attachedRigidbody == rb) continue;
                
                if (Physics.ComputePenetration(
                        capsule, transform.position, transform.rotation,
                        col, col.transform.position, col.transform.rotation,
                        out Vector3 sepDir, out float sepDist))
                {
                    // 1.a) El contacto debe empujarnos mayormente HACIA ARRIBA
                    float upDot = Vector3.Dot(sepDir.normalized, Vector3.up);
                    if (upDot < minUpDot) continue; // pared/lado -> NO es suelo

                    // 1.b) Y debe estar BAJO la media esfera inferior (evita aceptar costados)
                    // Proyectamos el vector desde el centro de la hemisfera inferior hacia el collider
                    Vector3 p = col.ClosestPoint(bottomSphereCenter);
                    
                    Vector3 toP = p - bottomSphereCenter;
                    // Si el punto está por encima del plano ecuatorial (y > 0), sería “lateral”
                    if (Vector3.Dot(toP.normalized, Vector3.up) > 0f) continue;

                    // 1.c) (Opcional) validar pendiente con un ray corto hacia abajo
                    if (Physics.Raycast(center, Vector3.down, out RaycastHit rh,
                            half + groundCheckDistance + 0.5f, groundMask, QueryTriggerInteraction.Ignore))
                    {
                        float slope = Vector3.Angle(rh.normal, Vector3.up);
                        if (slope <= slopeLimit) return true;
                    }
                    else
                    {
                        // Sin normal confiable: confiar en upDot del overlap
                        return true;
                    }
                }
            }

            // === 2) GAP pequeño: no hay overlap; probar cast hacia abajo bajo los pies ===

            // 2.a) SphereCast desde la hemisfera inferior, así no pescamos paredes laterales
            float castDist = Mathf.Max(0.05f, groundCheckDistance);
            if (Physics.SphereCast(bottomSphereCenter + Vector3.up * 0.01f, radius, Vector3.down,
                    out RaycastHit hitS, castDist + 0.02f, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (hitS.normal.y >= Mathf.Cos(slopeLimit * Mathf.Deg2Rad)) return true;
            }

            // 2.b) Fallback: CapsuleCast hacia abajo (más amplio, pero con filtro de pendiente)
            if (Physics.CapsuleCast(top, bottom, radius, Vector3.down, out RaycastHit hitC,
                    castDist, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (hitC.normal.y >= Mathf.Cos(slopeLimit * Mathf.Deg2Rad)) return true;
            }

            return false;
        }
    }
}