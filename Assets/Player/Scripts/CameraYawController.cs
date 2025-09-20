using UnityEngine;

/// <summary>
/// Controlador independiente para el yaw de la cámara.
/// Lee el input del mouse y aplica la rotación Y al target.
/// No modifica FirstPersonCameraBackUp.
/// </summary>
[DefaultExecutionOrder(10001)]
public class CameraYawController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Script original que maneja el pitch")]
    public FirstPersonCameraBackUp cameraScript;

    [Tooltip("Transform que se va a rotar en Y (normalmente el mismo que usaba FirstPersonCameraBackUp como _target)")]
    public Transform yawTarget;

    [Header("Ajustes de Yaw")]
    public float sensitivityMultiplier = 1f;
    public bool lockYaw = false;
    public bool useSmoothing = true;
    [Range(0.01f, 1f)] public float smoothFactor = 0.15f;

    private float yawCurrent;
    private float yawTargetValue;
    private float yawVelocity;

    void Start()
    {
        if (!cameraScript)
            cameraScript = GetComponent<FirstPersonCameraBackUp>();

        if (!yawTarget && cameraScript != null)
        {
            // Usa el target que ya definiste en el script original
            var targetField = typeof(FirstPersonCameraBackUp)
                .GetField("_target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            yawTarget = targetField?.GetValue(cameraScript) as Transform;
        }

        if (yawTarget)
        {
            yawCurrent = yawTargetValue = yawTarget.localEulerAngles.y;
        }
    }

    void Update()
    {
        if (!yawTarget || lockYaw) return;

        // Tomamos el delta del mouse horizontal
        float mx = Input.GetAxis("Mouse X");

        // Escalamos por sensibilidad propia y por la del script original (si existe)
        float baseSens = (cameraScript != null)
            ? GetPrivateField<float>(cameraScript, "_sensitivity")
            : 3f;

        float sens = baseSens * sensitivityMultiplier;
        yawTargetValue += mx * sens;
    }

    void LateUpdate()
    {
        if (!yawTarget) return;

        if (useSmoothing)
        {
            yawCurrent = Mathf.SmoothDampAngle(
                yawCurrent,
                yawTargetValue,
                ref yawVelocity,
                smoothFactor
            );
        }
        else
        {
            yawCurrent = yawTargetValue;
        }

        yawTarget.localRotation = Quaternion.Euler(0f, yawCurrent, 0f);
    }

    // Helper para leer campos privados sin romper el encapsulamiento del otro script
    T GetPrivateField<T>(object obj, string fieldName)
    {
        var f = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) return (T)f.GetValue(obj);
        return default;
    }
}

