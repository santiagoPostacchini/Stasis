using UnityEngine;
using UnityEngine.Animations.Rigging;
using Player.Scripts.MovementFSM.MVC;
using Player.Scripts.MovementFSM.Player.Scripts.MovementFSM;

namespace Player.Scripts.IK
{
    [DefaultExecutionOrder(90)]
    public class HandIkFsm : MonoBehaviour
    {
        public enum HandState { Idle, FrontLean, Wallrun, Vault, Climb }

        [Header("Refs")]
        public Model model;
        public ParkourScanner scanner;

        [Header("Rig Constraints")]
        public TwoBoneIKConstraint leftArmIK;
        public TwoBoneIKConstraint rightArmIK;
        [Tooltip("Targets que el TBone usa. Los mueve este script.")]
        public Transform leftHandTarget;
        public Transform rightHandTarget;

        [Header("General")]
        [Tooltip("Velocidad de blend del weight.")]
        public float weightLerp = 10f;
        [Tooltip("Velocidad de seguimiento de los targets (pos).")]
        public float targetLerp = 18f;
        [Tooltip("Velocidad de seguimiento de los targets (rot).")]
        public float targetRotLerpDegPerSec = 1080f;
        [Tooltip("Offset extra de rotación para alinear palma (en euler mundiales).")]
        public Vector3 palmRotationOffsetEuler;

        [Header("Front Lean (apoyarse de frente)")]
        public float leanDepth = 0.07f;     // distancia a la pared
        public float leanSide = 0.18f;      // separación ±X tangente pared
        public float leanUp = 0.05f;        // desplazar arriba desde el pecho
        public float leanWeight = 1f;

        [Header("Wallrun")]
        public float wrDepth = 0.05f;
        public float wrSide = 0.22f;
        public float wrUp = 0.10f;
        [Range(0f,1f)] public float wrWeight = 0.45f;

        [Header("Vault")]
        public float vaultSide = 0.22f;       // separación sobre tapa
        public float vaultForward = 0.06f;    // empuje hacia adelante (p.vaultForward)
        public float vaultUp = 0.02f;         // levantar apenas de la tapa
        [Range(0f,1f)] public float vaultWeight = 1f;

        [Header("Climb")]
        public float climbEdgeDepth = 0.04f;  // “meter” dedos en borde (hacia la pared)
        public float climbSide = 0.20f;
        public float climbUp = 0.02f;
        [Range(0f,1f)] public float climbWeight = 1f;

        [Header("Detección FrontLean")]
        [Tooltip("Activa FrontLean cuando hay Climb válido y estás quieto, de frente.")]
        public bool autoFrontLeanWhenClimbProbe = true;
        public float frontLeanMaxSpeed = 0.15f;
        public float frontLeanCooldown = 0.15f;

        HandState _state = HandState.Idle;
        float _targetWeight;
        float _currWeight;
        float _lastFrontLeanChange;

        // caches
        Transform _t;

        void Reset()
        {
            model = GetComponentInParent<Model>();
            scanner = GetComponentInParent<ParkourScanner>();
        }

        void Awake()
        {
            _t = transform;
            if (!model) model = GetComponentInParent<Model>();
            if (!scanner) scanner = GetComponentInParent<ParkourScanner>();
        }

        public void ForceState(HandState s)
        {
            _state = s;
            _lastFrontLeanChange = Time.time;
        }

        void Update()
        {
            // Auto FrontLean si procede (sin pisar estados fuertes)
            if (autoFrontLeanWhenClimbProbe && _state is HandState.Idle or HandState.FrontLean)
            {
                var p = model.probe;
                bool facingClimb = p.action == MovementFSM.ParkourAction.Climb;
                bool lowSpeed = PlanarSpeed(model.rb) < frontLeanMaxSpeed;

                if (facingClimb && lowSpeed && Time.time - _lastFrontLeanChange > frontLeanCooldown)
                    _state = HandState.FrontLean;
                else if (!facingClimb && _state == HandState.FrontLean)
                    _state = HandState.Idle;
            }

            // Resolver target-weight según estado
            _targetWeight = _state switch
            {
                HandState.Idle      => 0f,
                HandState.FrontLean => leanWeight,
                HandState.Wallrun   => wrWeight,
                HandState.Vault     => vaultWeight,
                HandState.Climb     => climbWeight,
                _ => 0f
            };

            // Blend de weight
            _currWeight = Mathf.MoveTowards(_currWeight, _targetWeight, Time.deltaTime * weightLerp);
            ApplyWeight(_currWeight);

            // Mover/rotar targets
            UpdateTargets(Time.deltaTime);
        }

        void ApplyWeight(float w)
        {
            if (leftArmIK)  leftArmIK.weight  = w;
            if (rightArmIK) rightArmIK.weight = w;
        }

        void UpdateTargets(float dt)
        {
            if (!leftHandTarget || !rightHandTarget || !scanner || !model) return;

            switch (_state)
            {
                case HandState.FrontLean:
                    SolveFrontLean(dt);
                    break;
                case HandState.Wallrun:
                    SolveWallrun(dt);
                    break;
                case HandState.Vault:
                    SolveVault(dt);
                    break;
                case HandState.Climb:
                    SolveClimb(dt);
                    break;
            }
        }

        // ----------- Solvers -----------

        void SolveFrontLean(float dt)
        {
            var p = model.probe; // viene de Climb
            Vector3 n = (p.hitNormal == Vector3.zero) ? _t.forward * -1f : p.hitNormal.normalized;

            // Origen: pecho
            float chest = Mathf.Clamp(scanner ? scanner.capsule.height * 0.55f : 1f, 0.8f, 1.1f);
            Vector3 chestOrigin = model.transform.position + Vector3.up * chest;

            // Plano pared
            Vector3 planePoint = p.hitPoint;
            // Proyectar pecho al plano y dejar "leanDepth"
            float dist = Vector3.Dot(chestOrigin - planePoint, n);
            Vector3 basePos = chestOrigin - n * (dist - leanDepth) + Vector3.up * leanUp;

            Vector3 tangent = Vector3.Cross(Vector3.up, n).normalized;
            Vector3 fwdOnPlane = ProjectOnPlane(model.cameraHolderTransform ? model.cameraHolderTransform.forward : model.transform.forward, n);

            Vector3 L = basePos - tangent * leanSide;
            Vector3 R = basePos + tangent * leanSide;

            Quaternion rot = AlignTo(n, fwdOnPlane) * Quaternion.Euler(palmRotationOffsetEuler);

            MoveTarget(leftHandTarget, L, rot, dt);
            MoveTarget(rightHandTarget, R, rot, dt);
        }

        void SolveWallrun(float dt)
        {
            var p = model.probe;
            Vector3 n = (p.wallRunNormal == Vector3.zero) ? _t.right * -1f : p.wallRunNormal.normalized;

            float mid = Mathf.Clamp(scanner ? scanner.capsule.height * 0.5f : 0.9f, 0.8f, 1.0f);
            Vector3 chest = model.transform.position + Vector3.up * mid;

            float dist = Vector3.Dot(chest - p.wallRunWallPoint, n);
            Vector3 basePos = chest - n * (dist - wrDepth) + Vector3.up * wrUp;

            Vector3 tangent = Vector3.Cross(Vector3.up, n).normalized;
            Vector3 fwdOnPlane = ProjectOnPlane(model.cameraHolderTransform ? model.cameraHolderTransform.forward : model.transform.forward, n);

            Vector3 L = basePos - tangent * wrSide;
            Vector3 R = basePos + tangent * wrSide;

            Quaternion rot = AlignTo(n, fwdOnPlane) * Quaternion.Euler(palmRotationOffsetEuler);

            MoveTarget(leftHandTarget, L, rot, dt);
            MoveTarget(rightHandTarget, R, rot, dt);
        }

        void SolveVault(float dt)
        {
            var p = model.probe;

            // Top horizontal aproximado
            Vector3 upN = Vector3.up;
            Vector3 fwd = (p.vaultForward.sqrMagnitude > 0f ? p.vaultForward : (model.cameraHolderTransform ? model.cameraHolderTransform.forward : model.transform.forward));
            fwd = ProjectOnPlane(fwd, upN);

            Vector3 top = p.vaultTopPoint + Vector3.up * vaultUp + fwd * vaultForward;
            Vector3 tangent = Vector3.Cross(upN, fwd).normalized; // borde perpendicular a tu avance

            Vector3 L = top - tangent * (vaultSide * 0.5f);
            Vector3 R = top + tangent * (vaultSide * 0.5f);

            Quaternion rot = AlignTo(upN, fwd) * Quaternion.Euler(palmRotationOffsetEuler);

            MoveTarget(leftHandTarget, L, rot, dt);
            MoveTarget(rightHandTarget, R, rot, dt);
        }

        void SolveClimb(float dt)
        {
            var p = model.probe; // usa hitNormal (cara vertical) + climbLedgePoint (borde)
            Vector3 n = (p.hitNormal == Vector3.zero) ? -_t.forward : p.hitNormal.normalized;

            // Punto de borde
            Vector3 edge = p.climbLedgePoint;
            if (edge == Vector3.zero) edge = p.hitPoint;

            Vector3 tangent = Vector3.Cross(Vector3.up, n).normalized;
            Vector3 fwdOnPlane = ProjectOnPlane(model.cameraHolderTransform ? model.cameraHolderTransform.forward : model.transform.forward, n);

            Vector3 basePos = edge - n * climbEdgeDepth + Vector3.up * climbUp;
            Vector3 L = basePos - tangent * (climbSide * 0.5f);
            Vector3 R = basePos + tangent * (climbSide * 0.5f);

            Quaternion rot = AlignTo(n, fwdOnPlane) * Quaternion.Euler(palmRotationOffsetEuler);

            MoveTarget(leftHandTarget, L, rot, dt);
            MoveTarget(rightHandTarget, R, rot, dt);
        }

        // ----------- Utils -----------

        static float PlanarSpeed(Rigidbody rb) => rb ? new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude : 0f;

        static Vector3 ProjectOnPlane(Vector3 v, Vector3 n)
        {
            n = n.normalized;
            return v - n * Vector3.Dot(v, n);
        }

        static Quaternion AlignTo(Vector3 normal, Vector3 forwardHint)
        {
            Vector3 up = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up;
            Vector3 f = ProjectOnPlane(forwardHint.sqrMagnitude > 0.0001f ? forwardHint : Vector3.forward, up);
            if (f.sqrMagnitude < 1e-6f) f = Vector3.Cross(Vector3.right, up).normalized;
            else f.Normalize();
            return Quaternion.LookRotation(f, up);
        }

        void MoveTarget(Transform t, Vector3 pos, Quaternion rot, float dt)
        {
            if (!t) return;
            t.position = Vector3.Lerp(t.position, pos, 1f - Mathf.Exp(-targetLerp * dt));
            t.rotation = Quaternion.RotateTowards(t.rotation, rot, targetRotLerpDegPerSec * dt);
        }
    }
}
