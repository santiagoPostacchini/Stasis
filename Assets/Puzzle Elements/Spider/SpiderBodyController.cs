using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(80)] // se ejecuta después del IK (50)
public class SpiderBodyController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform body;          // SpiderBodyParent
    [SerializeField] private List<SpiderFoot> feet;   // arrastrá los 6 SpiderFoot

    [Header("Altura")]
    [SerializeField] private float bodyHeightOffset = 0.35f;
    [SerializeField] private float heightSmoothTime = 0.08f;
    [SerializeField] private float maxStepUpDown = 0.5f; // clamp por frame

    [Header("Orientación")]
    [SerializeField] private float tiltStrength = 0.4f;  // 0..1
    [SerializeField] private float tiltSlerp = 10f;      // rapidez del blend

    private float _heightVel;

    void LateUpdate()
    {
        if (!body || feet == null || feet.Count == 0) return;

        Vector3 avgPos = Vector3.zero;
        Vector3 avgNormal = Vector3.zero;
        int count = 0;

        foreach (var f in feet)
        {
            if (f == null || !f.IsPlanted) continue;
            avgPos += f.transform.position;
            avgNormal += f.LastGroundNormal; // propiedad en SpiderFoot (abajo)
            count++;
        }

        if (count < 3) return; // muy pocas patas plantadas, no estabilizar

        avgPos /= count;
        avgNormal = (avgNormal / count).normalized;

        // Altura objetivo = media de pies + offset
        Vector3 targetPos = body.position;
        targetPos.y = avgPos.y + bodyHeightOffset;

        // Clamp vertical para evitar “saltos”
        float dy = targetPos.y - body.position.y;
        dy = Mathf.Clamp(dy, -maxStepUpDown * Time.deltaTime, maxStepUpDown * Time.deltaTime);
        float newY = body.position.y + dy;

        // Suavizado (solo Y)
        newY = Mathf.SmoothDamp(body.position.y, newY, ref _heightVel, heightSmoothTime);
        body.position = new Vector3(body.position.x, newY, body.position.z);

        // Orientación: blend del up actual hacia la normal media
        Vector3 desiredUp = Vector3.Slerp(body.up, avgNormal, Mathf.Clamp01(tiltStrength));
        Quaternion look = Quaternion.FromToRotation(body.up, desiredUp) * body.rotation;
        body.rotation = Quaternion.Slerp(body.rotation, look, 1f - Mathf.Exp(-tiltSlerp * Time.deltaTime));
    }
}
