using UnityEngine;

public class RagdollHang : MonoBehaviour
{
    [Header("Ragdoll Hang Settings")]
    public Rigidbody neckRigidbody;      // El rigidbody de la cabeza o cuello
    public Transform hangPoint;          // El punto de suspensión
    public float spring = 1000f;         // Fuerza del resorte (qué tan rígido está sostenido)
    public float damper = 50f;           // Amortiguación para que no vibre
    public float maxDistance = 0.1f;     // Qué tanto puede moverse del punto

    private ConfigurableJoint joint;

    void Start()
    {
        if (neckRigidbody == null || hangPoint == null)
        {
            Debug.LogWarning("Faltan referencias en RagdollHang");
            return;
        }

        // Crear un joint configurable dinámicamente
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

    void Update()
    {
        if (joint != null && hangPoint != null)
        {
            // Mantener el punto de anclaje actualizado si el hangPoint se mueve
            joint.connectedAnchor = hangPoint.position;
        }
    }
}

