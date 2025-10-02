using UnityEngine;
using System.Collections.Generic;

public class LinearCheckpointSystem : MonoBehaviour
{
    public Transform player;
    public List<Transform> checkpoints;
    public float activationDistance = 4f;

    [SerializeField] private int currentCheckpoint = 0;

    void Update()
    {
        if (currentCheckpoint >= checkpoints.Count) return;

        float dist = Vector3.Distance(player.position, checkpoints[currentCheckpoint].position);
        if (dist <= activationDistance)
        {
            currentCheckpoint++;
        }
    }

    public Vector3 CurrentCheckpointPos()
    {
        int idx = Mathf.Clamp(currentCheckpoint - 1, 0, checkpoints.Count - 1);
        return checkpoints[idx].position;
    }
}
