using UnityEngine;

public class SpiderPlacementFollower : MonoBehaviour
{
    [SerializeField] private Transform body; // SpiderBodyParent
    [SerializeField] private Vector3 localOffset = Vector3.zero; // distinto por pata
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float rayHeight = 0.5f;
    [SerializeField] private float rayMaxDist = 2f;

    void LateUpdate()
    {
        if (!body) return;

        Vector3 world = body.TransformPoint(localOffset);
        Vector3 origin = world + body.up * rayHeight;

        if (Physics.Raycast(origin, -body.up, out var hit, rayMaxDist + rayHeight, groundMask, QueryTriggerInteraction.Ignore))
            transform.position = hit.point;
        else
            transform.position = world; // fallback
    }
    //
}