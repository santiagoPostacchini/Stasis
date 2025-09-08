using UnityEngine;
using System;

namespace Player.FullBody_Scripts
{
    public class GroundCheck : MonoBehaviour
    {
        [Header("<color=orange>GroundCheck</color>")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float sphereRadius = 0.25f;
        [SerializeField] private float checkDistance = 0.05f;
        [SerializeField] private float coyoteTime = 0.1f;

        public bool IsGrounded { get; private set; }
        public float CoyoteCounter { get; private set; }
        public event Action OnLanded;

        const float OriginOffset = 0.01f;

        void FixedUpdate()
        {
            Vector3 origin = transform.position + Vector3.up * OriginOffset;
            bool hit = Physics.SphereCast(origin, sphereRadius, Vector3.down, out _, checkDistance + OriginOffset,
                groundMask, QueryTriggerInteraction.Ignore);

            bool wasGrounded = IsGrounded;
            IsGrounded = hit;

            if (IsGrounded)
            {
                CoyoteCounter = coyoteTime;
                if (!wasGrounded) OnLanded?.Invoke();
            }
            else
            {
                CoyoteCounter -= Time.fixedDeltaTime;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = IsGrounded ? Color.white : Color.red;
            Vector3 origin = transform.position + Vector3.up * OriginOffset;
            Gizmos.DrawWireSphere(origin + Vector3.down * checkDistance, sphereRadius);
        }
    }
}