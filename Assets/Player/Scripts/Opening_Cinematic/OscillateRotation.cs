using UnityEngine;

[ExecuteAlways] // También se mueve en el editor si querés ver la animación
public class OscillateRotation : MonoBehaviour
{
    [Header("Eje de rotación (usa solo uno en cada eje)")]
    public Vector3 rotationAxis = Vector3.up; // Ej: (1,0,0)=X, (0,1,0)=Y, (0,0,1)=Z

    [Header("Movimiento de oscilación")]
    public float amplitude = 30f; // Ángulo máximo (grados)
    public float speed = 1f;      // Velocidad de oscilación

    [Header("Opciones")]
    public bool localRotation = true; // Oscila en espacio local o global
    public bool randomOffset = true;  // Para que varias instancias no estén sincronizadas

    private float timeOffset;
    private Quaternion initialRotation;

    void Start()
    {
        initialRotation = transform.localRotation;
        timeOffset = randomOffset ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    void Update()
    {
        float angle = Mathf.Sin(Time.time * speed + timeOffset) * amplitude;
        Quaternion oscillation = Quaternion.AngleAxis(angle, rotationAxis.normalized);

        if (localRotation)
            transform.localRotation = initialRotation * oscillation;
        else
            transform.rotation = initialRotation * oscillation;
    }
}

