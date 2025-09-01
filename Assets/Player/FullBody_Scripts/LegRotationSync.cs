using UnityEngine;

public class LegIdleRotationRig : MonoBehaviour
{
    [Header("References")]
    public Transform torso;        // Hueso/spine superior
    public Transform legTarget;    // Target del Multi-Parent Constraint

    [Header("Settings")]
    public float rotationThreshold = 30f;
    public float rotationLerpSpeed = 5f;

    private Movement movement;
    private float legsCurrentY;

    void Start()
    {
        movement = GetComponentInParent<Movement>();
        if (legTarget != null)
            legsCurrentY = legTarget.eulerAngles.y;
    }

    void Update()
    {
        if (torso == null || legTarget == null || movement == null) return;

        bool isIdle = movement._isInIdle;

        float torsoY = torso.eulerAngles.y;
        float legsY = legTarget.eulerAngles.y;
        float angleDiff = Mathf.DeltaAngle(legsY, torsoY);

        if (isIdle)
        {
            if (Mathf.Abs(angleDiff) > rotationThreshold)
            {
                float targetY = torsoY;
                legsCurrentY = Mathf.LerpAngle(legsY, targetY, Time.deltaTime * rotationLerpSpeed);
                legTarget.rotation = Quaternion.Euler(0, legsCurrentY, 0);
            }
            // Si está dentro del threshold, no movemos el target
        }
        else
        {
            float targetY = torsoY;
            legsCurrentY = Mathf.LerpAngle(legsY, targetY, Time.deltaTime * rotationLerpSpeed);
            legTarget.rotation = Quaternion.Euler(0, legsCurrentY, 0);
        }
    }
}

