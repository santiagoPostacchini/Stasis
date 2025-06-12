using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class LaunchPlate : MonoBehaviour
    {
        [Header("Trajectory Path")]
        [Tooltip("Waypoints defining the launch arc in order")]
        [SerializeField] private List<Transform> waypoints;

        [Header("Timing")]
        [Tooltip("Total time to follow the path")]
        [SerializeField] private float travelTime = 1f;
        [Tooltip("Easing curve for the movement")]
        [SerializeField] private AnimationCurve timeCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Cooldown")]
        [Tooltip("Delay before the plate can launch again")]
        [SerializeField] private float cooldown = 0.5f;

        private bool _canLaunch = true;

        private void Awake()
        {
            // Ensure collider is trigger
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_canLaunch) return;

            // Try to get the Rigidbody on player or object
            Rigidbody rb = other.attachedRigidbody ?? other.GetComponent<Rigidbody>();
            if (rb == null) return;

            StartCoroutine(LaunchRoutine(rb));
        }

        private IEnumerator LaunchRoutine(Rigidbody rb)
        {
            _canLaunch = false;

            // Prepare for kinematic path following
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.detectCollisions = false;

            Vector3 lastPos = rb.transform.position;
            float elapsed = 0f;

            // Follow the spline path
            while (elapsed < travelTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / travelTime);
                float eval = timeCurve.Evaluate(t);
                Vector3 targetPos = GetPositionOnSpline(eval);
                rb.transform.position = targetPos;
                yield return null;
                lastPos = targetPos;
            }

            // Snap to final waypoint
            rb.transform.position = waypoints[waypoints.Count - 1].position;

            // Compute smooth exit velocity
            float dt = Time.deltaTime;
            Vector3 exitVelocity = (rb.transform.position - lastPos) / dt;

            // Restore physics
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.velocity = exitVelocity;

            // Cooldown
            yield return new WaitForSeconds(cooldown);
            _canLaunch = true;
        }

        /// <summary>
        /// Evaluates a simple linear spline between waypoints.
        /// </summary>
        private Vector3 GetPositionOnSpline(float t)
        {
            if (waypoints == null || waypoints.Count == 0)
                return transform.position;
            if (waypoints.Count == 1)
                return waypoints[0].position;

            float totalSegments = waypoints.Count - 1;
            float scaled = Mathf.Clamp01(t) * totalSegments;
            int idx = Mathf.FloorToInt(scaled);
            idx = Mathf.Clamp(idx, 0, waypoints.Count - 2);
            float localT = scaled - idx;

            Vector3 p0 = waypoints[idx].position;
            Vector3 p1 = waypoints[idx + 1].position;
            return Vector3.Lerp(p0, p1, localT);
        }
    }
}