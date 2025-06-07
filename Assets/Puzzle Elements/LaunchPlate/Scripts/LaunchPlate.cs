using System.Collections;
using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    public class LaunchPlate : MonoBehaviour
    {
        [SerializeField] private float jumpForce = 10f; 
        [SerializeField] private float forwardForce = 5f;
        [SerializeField] private Transform pos;
        private float duration = 0.05f;
        private bool canApplyForce = true;
        private void OnCollisionEnter(Collision collision)
        {
            if (!canApplyForce) return;
            Rigidbody rb = collision.gameObject.GetComponent<Rigidbody>();
            Transform t = collision.gameObject.transform;
            if (rb != null)
            {
                StartCoroutine(ApplyJumpForce(rb, t));
            }
        }
        private IEnumerator ApplyJumpForce(Rigidbody rb, Transform t)
        {
            canApplyForce = false;

            Vector3 startPos = t.position;
            Vector3 endPos = pos.position;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;

            float time = 0f;
            while (time < duration)
            {
                float tLerp = time / duration;
                t.position = Vector3.Lerp(startPos, endPos, tLerp);
                time += Time.deltaTime;
                yield return null;
            }

            // Aseguramos posici�n exacta
            t.position = endPos;

            // Esperamos un frame antes de volver a activar la f�sica
            yield return null;
            rb.isKinematic = false;

            // APLICAMOS LA FUERZA JUSTO DESPU�S DE ACTIVAR LA F�SICA
            yield return new WaitForFixedUpdate(); // Esperamos al siguiente FixedUpdate para que la f�sica est� lista

            Vector3 jumpDirection = (transform.forward * forwardForce) + (transform.up * jumpForce);
            rb.AddForce(jumpDirection, ForceMode.VelocityChange);

            // Cooldown para evitar m�ltiples disparos seguidos
            yield return new WaitForSeconds(0.5f);
            canApplyForce = true;
        }
    }
}
