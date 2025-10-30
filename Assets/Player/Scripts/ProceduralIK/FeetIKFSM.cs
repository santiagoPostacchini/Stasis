using UnityEngine;
using UnityEngine.Animations.Rigging;
using Player.Scripts.MovementFSM.MVC;
using Player.Scripts.MovementFSM.Player.Scripts.MovementFSM;

namespace Player.Scripts.IK
{
    [DefaultExecutionOrder(90)]
    public class FeetIkfsm : MonoBehaviour
    {
        private enum FootState { Off, Grounded }

        [Header("Refs")]
        public Model model;
        public ParkourScanner scanner;

        [Header("Rig Constraints")]
        public TwoBoneIKConstraint leftLegIK;
        public TwoBoneIKConstraint rightLegIK;
        public Transform leftFootTarget;
        public Transform rightFootTarget;

        [Header("Ray Orígenes (opcional, si querés rayear desde huesos)")]
        public Transform leftFootBone;
        public Transform rightFootBone;

        [Header("Tuning")]
        public LayerMask groundMask;
        public float castDownDist = 0.8f;
        public float targetUpOffset = 0.02f;
        public float targetLerp = 22f;
        public float targetRotLerpDegPerSec = 1080f;
        public float weightLerp = 12f;

        [Header("Peso")]
        [Range(0,1)] public float groundedWeight = 1f;

        FootState _state = FootState.Off;
        float _currWeightL, _currWeightR;

        void Reset()
        {
            model = GetComponentInParent<Model>();
            scanner = GetComponentInParent<ParkourScanner>();
            groundMask = model ? model.groundMask : Physics.DefaultRaycastLayers;
        }

        void Awake()
        {
            if (!model) model = GetComponentInParent<Model>();
            if (!scanner) scanner = GetComponentInParent<ParkourScanner>();
        }

        public void TryGround()
        {
            _state = model.IsGroundedNow() ? FootState.Grounded : FootState.Off;
        }

        void Update()
        {
            float targetW = (_state == FootState.Grounded) ? groundedWeight : 0f;

            _currWeightL = Mathf.MoveTowards(_currWeightL, targetW, Time.deltaTime * weightLerp);
            _currWeightR = Mathf.MoveTowards(_currWeightR, targetW, Time.deltaTime * weightLerp);

            if (leftLegIK)  leftLegIK.weight  = _currWeightL;
            if (rightLegIK) rightLegIK.weight = _currWeightR;

            if (_state == FootState.Grounded)
                SolveFeet(Time.deltaTime);
        }

        void SolveFeet(float dt)
        {
            if (!leftFootTarget || !rightFootTarget) return;

            SolveOne(leftFootBone ? leftFootBone.position : leftFootTarget.position, leftFootTarget, dt);
            SolveOne(rightFootBone ? rightFootBone.position : rightFootTarget.position, rightFootTarget, dt);
        }

        void SolveOne(Vector3 rayOrigin, Transform target, float dt)
        {
            Vector3 from = rayOrigin + Vector3.up * 0.2f; // evitar castear “desde dentro”
            if (Physics.Raycast(from, Vector3.down, out var hit, castDownDist + 0.2f, groundMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 n = hit.normal.normalized;
                Vector3 pos = hit.point + n * targetUpOffset;

                // forward del pie = forward del player proyectado al plano del suelo
                Vector3 fwd = model.cameraHolderTransform ? model.cameraHolderTransform.forward : model.transform.forward;
                fwd = ProjectOnPlane(fwd, n);
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(Vector3.right, n).normalized; else fwd.Normalize();

                Quaternion rot = Quaternion.LookRotation(fwd, n);

                target.position = Vector3.Lerp(target.position, pos, 1f - Mathf.Exp(-targetLerp * dt));
                target.rotation = Quaternion.RotateTowards(target.rotation, rot, targetRotLerpDegPerSec * dt);
            }
        }

        static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v - n.normalized * Vector3.Dot(v, n.normalized);
    }
}
