using UnityEngine;

public class ConstantPropulsor : MonoBehaviour
{
    [Header("Player a impulsar")]
    public Rigidbody playerRb;

    [Header("Punto de salida del impulso")]
    public Transform launchPoint;

    [Header("Fuerza / velocidad final del impulso")]
    public float force = 20f;

    [Header("Dirección del impulso (si no usás LaunchPoint forward)")]
    public Vector3 direction = Vector3.forward;

    [Header("Curva que define la velocidad (0 a 1 del trayecto)")]
    public AnimationCurve forceCurve = AnimationCurve.Linear(0, 1, 1, 1);
    public float duration = 0.5f;

    private bool useCurve = false;

    void Start()
    {
        if (forceCurve != null && forceCurve.keys.Length > 1)
            useCurve = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!playerRb) return;
        if (other.attachedRigidbody != playerRb) return;

        Launch();
    }

    void Launch()
    {
        if (launchPoint)
            playerRb.position = launchPoint.position;

        playerRb.velocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;

        if (useCurve)
        {
            StopAllCoroutines();
            StartCoroutine(LaunchCurve());
        }
        else
        {
            Vector3 dir = launchPoint ? launchPoint.forward : direction.normalized;
            playerRb.velocity = dir * force;
        }
    }

    System.Collections.IEnumerator LaunchCurve()
    {
        float t = 0f;
        Vector3 dir = launchPoint ? launchPoint.forward : direction.normalized;

        while (t < duration)
        {
            float mult = forceCurve.Evaluate(t / duration);

            // ✔ Esto reemplaza la velocidad en vez de sumarla
            playerRb.velocity = dir * (force * mult);

            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Al final, dejá la velocidad fija según la curva en 1.0
        playerRb.velocity = dir * (force * forceCurve.Evaluate(1f));
    }
}


