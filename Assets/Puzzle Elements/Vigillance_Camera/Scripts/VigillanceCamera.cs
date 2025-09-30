using System.Linq;
using UnityEngine;

[ExecuteAlways]
public class VigillanceCamera : MonoBehaviour
{
    public Transform target;
    [HideInInspector]public Transform targetRootOverride;

    public Vector3 localLookAxis = Vector3.right;
    public Vector3 localUpAxis = Vector3.up;

    public Vector2 yawMinMax = new Vector2(-90f, 90f);
    public Vector2 pitchMinMax = new Vector2(-20f, 20f);
    public bool invertPitch = false;

    public float turnSpeed = 8f;
    public bool lockRollX = true;

    public float occlusionRadius = 0.08f;
    public float minOccluderSize = 0.3f;

    Transform _parent;
    Quaternion _lastValidRotation;

    void Awake()
    {
        _parent = transform.parent;
        _lastValidRotation = transform.localRotation;
    }
    void LateUpdate()
    {
        if (!target) return;

        Vector3 toWorld = target.position - transform.position;
        float dist = toWorld.magnitude;
        if (dist < 1e-6f) return;

        if (IsOccluded(toWorld, dist))
        {
            ApplyRotation(_lastValidRotation);
            return;
        }

        Vector3 toLocal = _parent ? _parent.InverseTransformDirection(toWorld) : toWorld;
        Quaternion desiredLocal = FromToRotationLocal(localLookAxis, toLocal, localUpAxis);
        Vector3 eDesired = NormalizeEuler(desiredLocal.eulerAngles);

        float yaw = eDesired.y;
        if (yaw < yawMinMax.x || yaw > yawMinMax.y)
        {
            ApplyRotation(_lastValidRotation);
            return;
        }

        eDesired.y = Mathf.Clamp(eDesired.y, yawMinMax.x, yawMinMax.y);

        float p = eDesired.z;
        if (invertPitch) p = -p;
        p = Mathf.Clamp(p, pitchMinMax.x, pitchMinMax.y);
        if (invertPitch) p = -p;
        eDesired.z = p;

        if (lockRollX) eDesired.x = 0f;

        Quaternion clamped = Quaternion.Euler(eDesired);
        _lastValidRotation = clamped;
        ApplyRotation(clamped);
    }

    bool IsOccluded(Vector3 toWorld, float dist)
    {
        Vector3 dir = toWorld / dist;
        var hits = Physics.SphereCastAll(transform.position, occlusionRadius, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        var sorted = hits.OrderBy(h => h.distance);
        Transform targetRoot = targetRootOverride ? targetRootOverride : target;

        foreach (var h in sorted)
        {
            if (!h.collider || !h.collider.transform) continue;
            Transform t = h.collider.transform;
            bool isTarget = t == target || t.IsChildOf(targetRoot);
            if (isTarget) return false;

            Vector3 s = h.collider.bounds.size;
            float maxSize = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            if (maxSize >= minOccluderSize) return true;
        }
        return false;
    }

    void ApplyRotation(Quaternion targetRot)
    {
        if (turnSpeed <= 0f)
            transform.localRotation = targetRot;
        else
        {
            float a = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, a);
        }
    }

    static Quaternion FromToRotationLocal(Vector3 fromLocalAxis, Vector3 toLocalDir, Vector3 upHint)
    {
        if (toLocalDir.sqrMagnitude < 1e-6f) return Quaternion.identity;
        Quaternion lookZ = Quaternion.LookRotation(toLocalDir.normalized, upHint.sqrMagnitude > 1e-6f ? upHint.normalized : Vector3.up);
        Quaternion corr = Quaternion.FromToRotation(fromLocalAxis.normalized, Vector3.forward);
        return lookZ * corr;
    }

    static Vector3 NormalizeEuler(Vector3 e)
    {
        e.x = Wrap180(e.x);
        e.y = Wrap180(e.y);
        e.z = Wrap180(e.z);
        return e;
    }

    static float Wrap180(float a) => Mathf.Repeat(a + 180f, 360f) - 180f;
}
