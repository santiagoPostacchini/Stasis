using System.Collections;
using UnityEngine;
using Player.Scripts.MovementFSM.MVC;

[RequireComponent(typeof(Collider))]
public class PlataformaMovil : MonoBehaviour
{
    [Tooltip("Tiempo de gracia para enganchar otra plataforma antes de desparentar.")]
    [SerializeField] private float unparentDelay = 0.5f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Model>() != null)
        {
            // Parent inmediato a ESTA plataforma (manteniendo posición mundial)
            other.transform.SetParent(transform, true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<Model>() != null)
        {
            // Desparentar diferido: solo si después del delay
            // el padre sigue siendo ESTA plataforma.
            StartCoroutine(UnparentAfterDelay(other.transform, unparentDelay));
        }
    }

    private IEnumerator UnparentAfterDelay(Transform target, float delay)
    {
        // Guardamos referencia al padre “candidato” (esta plataforma)
        Transform expectedParent = transform;

        float t = 0f;
        while (t < delay)
        {
            // Si durante la espera el target ya cambió de padre,
            // abortamos (se enganchó a otra plataforma).
            if (target == null || target.parent != expectedParent)
                yield break;

            t += Time.deltaTime;
            yield return null;
        }

        // Si sigue siendo hijo de esta plataforma, recién ahí lo soltamos.
        if (target != null && target.parent == expectedParent)
            target.SetParent(null, true);
    }
}
