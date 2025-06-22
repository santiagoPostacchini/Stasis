using System.Collections;
using System.Collections.Generic;
using Player.Scripts;
using Player.Scripts.MVC;
using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class LaunchPlate : MonoBehaviour
    {
        [Header("Player Path")]
        [SerializeField] private Transform playerTrajectoryParent;
        private readonly List<Transform> _playerTrajectory = new();

        [Header("Object Path")]
        [SerializeField] private Transform objectTrajectoryParent;
        private readonly List<Transform> _objectTrajectory = new();

        [Header("Physics")]
        [SerializeField] private float timeToNextNode = 0.5f;
        [SerializeField] private float pointReachThreshold = 0.2f;

        [Header("Cooldown")]
        [SerializeField] private float cooldown = 0.5f;

        private bool _canLaunch = true;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;

            BuildPath(playerTrajectoryParent, _playerTrajectory);
            BuildPath(objectTrajectoryParent, _objectTrajectory);
        }

        private void OnValidate()
        {
            BuildPath(playerTrajectoryParent, _playerTrajectory);
            BuildPath(objectTrajectoryParent, _objectTrajectory);
        }

        private static void BuildPath(Transform parent, List<Transform> path)
        {
            path.Clear();
            if (!parent) return;
            for (int i = 0; i < parent.childCount; i++)
                path.Add(parent.GetChild(i));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_canLaunch) return;

            bool isPlayer = other.CompareTag("Player") || other.GetComponent<Model>();
            var trajectory = isPlayer ? _playerTrajectory : _objectTrajectory;
            if (trajectory.Count == 0) return;

            if (isPlayer)
            {
                var model = other.GetComponent<Model>() ?? other.GetComponentInParent<Model>();
                StartCoroutine(LaunchCharacterRoutine(other.transform, trajectory, model));
            }
            else
            {
                Rigidbody rb = other.attachedRigidbody ?? other.GetComponent<Rigidbody>();
                if (!rb) return;
                StartCoroutine(LaunchRigidbodyRoutine(rb, trajectory));
            }
        }

        private IEnumerator LaunchCharacterRoutine(Transform tr, List<Transform> path, Model model)
        {
            _canLaunch = false;
            int currentIndex = 0;

            while (currentIndex < path.Count)
            {
                Vector3 target = path[currentIndex].position;
                Vector3 velocity = CalculateBallisticVelocity(tr.position, target, timeToNextNode);


                if (model)
                    model.Launch(new Vector3(velocity.x, 0f, velocity.z), velocity.y);

                float elapsed = 0f;
                bool reached = false;
                while (elapsed < timeToNextNode + 0.1f)
                {
                    if (Vector3.Distance(tr.position, target) <= pointReachThreshold)
                    {
                        reached = true;
                        break;
                    }
                    elapsed += Time.deltaTime;
                    yield return new WaitForFixedUpdate();
                }

                if (!reached) break;

                currentIndex++;
            }

            yield return new WaitForSeconds(cooldown);
            _canLaunch = true;
        }

        private IEnumerator LaunchRigidbodyRoutine(Rigidbody rb, List<Transform> path)
        {
            _canLaunch = false;
            int currentIndex = 0;

            while (currentIndex < path.Count)
            {
                Vector3 target = path[currentIndex].position;
                Vector3 velocity = CalculateBallisticVelocity(rb.position, target, timeToNextNode);
                rb.velocity = velocity;

                float elapsed = 0f;
                bool reached = false;
                while (elapsed < timeToNextNode + 0.1f)
                {
                    if (Vector3.Distance(rb.position, target) <= pointReachThreshold)
                    {
                        reached = true;
                        break;
                    }
                    elapsed += Time.deltaTime;
                    yield return new WaitForFixedUpdate();
                }

                if (!reached) break;

                currentIndex++;
            }

            yield return new WaitForSeconds(cooldown);
            _canLaunch = true;
        }

        private Vector3 CalculateBallisticVelocity(Vector3 start, Vector3 end, float time)
        {
            Vector3 distance = end - start;
            Vector3 distanceXZ = new Vector3(distance.x, 0f, distance.z);

            float sy = distance.y;
            float sxz = distanceXZ.magnitude;
            float vxz = sxz / time;

            float vy = (sy - 0.5f * Physics.gravity.y * time * time) / time;

            Vector3 result = distanceXZ.normalized * vxz;
            result.y = vy;
            return result;
        }
    }
}