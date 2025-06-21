using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class LaunchPlate : MonoBehaviour
    {
        [Header("Trajectory Path")]
        [Tooltip("Parent with child nodes defining the path. First child should be the pad itself.")]
        [SerializeField] private Transform trajectoryParent;

        private List<Transform> _trajectories = new();

        [Header("Force Settings")]
        [Tooltip("Strength of the impulse force towards next node")]
        [SerializeField] private float impulseForce = 20f;
        [Tooltip("Distance to consider a node reached")]
        [SerializeField] private float pointReachThreshold = 0.5f;
        [Tooltip("Max time the object follows the path")]
        [SerializeField] private float maxTravelTime = 2f;

        [Header("Cooldown")]
        [SerializeField] private float cooldown = 0.5f;

        private bool _canLaunch = true;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;

            if (trajectoryParent != null)
            {
                _trajectories.Clear();
                for (int i = 0; i < trajectoryParent.childCount; i++)
                {
                    _trajectories.Add(trajectoryParent.GetChild(i));
                }
            }
            else
            {
                Debug.LogWarning($"[LaunchPlate] No trajectory parent assigned on {gameObject.name}");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_canLaunch || _trajectories.Count < 2) return;

            Rigidbody rb = other.attachedRigidbody ?? other.GetComponent<Rigidbody>();
            if (!rb) return;

            StartCoroutine(LaunchWithForces(rb));
        }

        private IEnumerator LaunchWithForces(Rigidbody rb)
        {
            _canLaunch = false;

            // Move to first node (pad origin)
            rb.position = _trajectories[0].position;

            int currentIndex = 1; // start from second point
            float elapsed = 0f;

            while (currentIndex < _trajectories.Count && elapsed < maxTravelTime)
            {
                Vector3 toNext = _trajectories[currentIndex].position - rb.position;
                float distance = toNext.magnitude;

                if (distance < pointReachThreshold)
                {
                    currentIndex++;
                    continue;
                }

                Vector3 forceDir = toNext.normalized;
                rb.AddForce(forceDir * impulseForce, ForceMode.Acceleration);

                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(cooldown);
            _canLaunch = true;
        }
    }
}
