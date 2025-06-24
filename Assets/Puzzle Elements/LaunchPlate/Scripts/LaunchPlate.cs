using System.Collections;
using System.Collections.Generic;
using Player.Scripts.MVC;
using Resources;
using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class LaunchPlate : MonoBehaviour
    {
        [Header("Player Path")] [SerializeField]
        private Transform playerTrajectoryParent;

        private readonly List<Transform> _playerTrajectory = new();

        [Header("Object Path")] [SerializeField]
        private Transform objectTrajectoryParent;

        private readonly List<Transform> _objectTrajectory = new();

        [Header("Physics")] [SerializeField] private float timeToNextNode = 0.5f;
        [SerializeField] private float pointReachThreshold = 0.2f;
        
        [Header("Spline Launch")]
        [SerializeField] private float totalLaunchTime = 1.5f;

        [Header("Cooldown")] [SerializeField] private float cooldown = 0.5f;

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
                StartCoroutine(LaunchCharacterSplineRoutine(other.transform, trajectory, model));
            }
            else
            {
                Rigidbody rb = other.attachedRigidbody ?? other.GetComponent<Rigidbody>();
                if (!rb) return;
                StartCoroutine(LaunchRigidbodyRoutine(rb, trajectory));
            }
        }

        private IEnumerator LaunchCharacterSplineRoutine(Transform tr, List<Transform> path, Model model)
        {
            _canLaunch = false;
            int numNodes = path.Count;
            if (numNodes < 2) yield break;

            float duration = totalLaunchTime;
            float elapsed = 0f;
            
            List<Vector3> points = new List<Vector3>();
            points.Add(path[0].position + (path[0].position - path[1].position));
            for (int i = 0; i < numNodes; i++) points.Add(path[i].position);
            points.Add(path[numNodes - 1].position +
                       (path[numNodes - 1].position - path[numNodes - 2].position));

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float scaledT = t * (numNodes - 1);
                int seg = Mathf.FloorToInt(scaledT);
                float segT = scaledT - seg;
                
                seg = Mathf.Clamp(seg, 0, numNodes - 2);
                
                Vector3 p0 = points[seg];
                Vector3 p1 = points[seg + 1];
                Vector3 p2 = points[seg + 2];
                Vector3 p3 = points[seg + 3];

                Vector3 pos = CatmullRom.CatmullRomCalc(p0, p1, p2, p3, segT);
                
                if (model)
                {
                    model.SetLaunchSplinePosition(pos);
                }
                else
                {
                    tr.position = pos;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (model)
                model.SetLaunchSplinePosition(path[numNodes - 1].position);
            else
                tr.position = path[numNodes - 1].position;

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
            Vector3 delta = end - start;
            Vector3 deltaXZ = new Vector3(delta.x, 0f, delta.z);
            float sxz = deltaXZ.magnitude;

            float vy = (delta.y - 0.5f * Physics.gravity.y * time * time) / time;
            float vxz = sxz / time;

            Vector3 result = deltaXZ.normalized * vxz;
            result.y = vy;
            return result;
        }
        
        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (playerTrajectoryParent && playerTrajectoryParent.childCount >= 2)
            {
                Gizmos.color = Color.cyan;
                var points = new List<Vector3>();
                for (int i = 0; i < playerTrajectoryParent.childCount; i++)
                    points.Add(playerTrajectoryParent.GetChild(i).position);

                points.Insert(0, points[0] + (points[0] - points[1]));
                points.Add(points[^1] + (points[^1] - points[^2]));

                const int steps = 32;
                for (int i = 0; i < points.Count - 3; i++)
                {
                    Vector3 prev = CatmullRom.CatmullRomCalc(points[i], points[i + 1], points[i + 2], points[i + 3], 0);
                    for (int j = 1; j <= steps; j++)
                    {
                        float t = j / (float)steps;
                        Vector3 next = CatmullRom.CatmullRomCalc(points[i], points[i + 1], points[i + 2], points[i + 3], t);
                        Gizmos.DrawLine(prev, next);
                        prev = next;
                    }
                }
            }
        }
    #endif
    }
}