using UnityEngine;
using System.Collections;

public class RagdollHanger : MonoBehaviour
{
    [Header("Ragdoll Hang Settings")]
    public Rigidbody neckRigidbody;      // El rigidbody de la cabeza o cuello
    public Transform hangPoint;          // El punto de suspensión
    public float spring = 1000f;         // Fuerza del resorte (qué tan rígido está sostenido)
    public float damper = 50f;           // Amortiguación para que no vibre
    public float maxDistance = 0.1f;     // Qué tanto puede moverse del punto

    [Header("Release Settings")]
    public bool releaseRagdoll = false;      // Si está en true, suelta el ragdoll manualmente
    public bool useTimer = false;            // Si está en true, el ragdoll se soltará después de cierto tiempo
    public float releaseDelay = 2f;          // Tiempo (en segundos) antes de soltar el ragdoll

    [Header("Fade Settings")]
    public bool fadeBlack = false;           // Bool que se activa tras la espera
    public float fadeDelayAfterRelease = 2f; // Segundos después de soltar el ragdoll para activar fade

    [Header("Scripts to Disable On Release")]
    [Tooltip("Arrastrá acá los scripts que querés desactivar cuando se haga el release")]
    public MonoBehaviour[] scriptsToDisable;

    [Header("Rigidbody to Activate On Release")]
    [Tooltip("Arrastrá acá el Rigidbody que querés activar cuando se suelte el ragdoll")]
    public Rigidbody rigidbodyToActivate;

    private ConfigurableJoint joint;
    public bool hasReleased = false;        // Para evitar que se suelte más de una vez
    private float timer = 0f;

    void Start()
    {
        if (neckRigidbody == null || hangPoint == null)
        {
            Debug.LogWarning("Faltan referencias en RagdollHang");
            return;
        }

        CreateJoint();
    }

    void Update()
    {
        // Suelta manualmente si el bool está activo
        if (releaseRagdoll && !hasReleased)
        {
            ReleaseRagdoll();
        }

        // Suelta automáticamente con timer
        if (useTimer && !hasReleased)
        {
            timer += Time.deltaTime;
            if (timer >= releaseDelay)
            {
                ReleaseRagdoll();
            }
        }

        // Actualizar el punto de anclaje si el hangPoint se mueve
        if (joint != null && hangPoint != null)
        {
            joint.connectedAnchor = hangPoint.position;
        }
    }

    void CreateJoint()
    {
        joint = neckRigidbody.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = null;
        joint.anchor = Vector3.zero;
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedAnchor = hangPoint.position;

        // Configurar movimiento limitado (como un resorte)
        SoftJointLimitSpring springSettings = new SoftJointLimitSpring
        {
            spring = spring,
            damper = damper
        };
        joint.linearLimitSpring = springSettings;

        SoftJointLimit limit = new SoftJointLimit
        {
            limit = maxDistance
        };
        joint.linearLimit = limit;

        joint.xMotion = ConfigurableJointMotion.Limited;
        joint.yMotion = ConfigurableJointMotion.Limited;
        joint.zMotion = ConfigurableJointMotion.Limited;

        // Rotación libre (para que cuelgue naturalmente)
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;
    }

    public void ReleaseRagdoll()
    {
        if (joint != null)
        {
            Destroy(joint);
            joint = null;
        }

        hasReleased = true;

        // 🔹 Desactiva los scripts asignados
        foreach (var script in scriptsToDisable)
        {
            if (script != null)
                script.enabled = false;
        }

        // 🔹 Activa el Rigidbody asignado
        if (rigidbodyToActivate != null)
        {
            rigidbodyToActivate.isKinematic = false;
            rigidbodyToActivate.WakeUp();
        }

        // Inicia el contador para activar el fade
        StartCoroutine(FadeAfterDelay());
    }

    private IEnumerator FadeAfterDelay()
    {
        yield return new WaitForSeconds(fadeDelayAfterRelease);
        fadeBlack = true;
    }
}





