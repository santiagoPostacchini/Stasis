using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

public class FollowMultipleTargetController : MonoBehaviour
{
    [Header("Lista de objetos a seguir")]
    public List<Transform> brothers = new List<Transform>(); // Lista de objetos que definen posición y rotación
    public AnimationCurve remapLerp; // Para suavizar movimiento y rotación
    public float moveDelay = 0f; // Tiempo de espera antes de ir al siguiente punto
    public float stopDuration = 0.5f; // Tiempo que espera en cada punto

    private int currentIndex = 0;
    private bool isMoving = false;
    private bool forward = true; // Si vamos hacia adelante o hacia atrás

    public bool CanMove = true;

    private void Update()
    {
        if (!CanMove || isMoving || brothers.Count == 0)
            return;

        MoveToNextBrother();
    }

    private void MoveToNextBrother()
    {
        // Ajustar índice según dirección
        if (forward)
        {
            if (currentIndex >= brothers.Count)
            {
                forward = false;
                currentIndex = brothers.Count - 2; // Retrocedemos
            }
        }
        else
        {
            if (currentIndex < 0)
            {
                forward = true;
                currentIndex = 1; // Avanzamos
            }
        }

        Transform target = brothers[currentIndex];
        currentIndex += forward ? 1 : -1;

        MoveAsync(transform, target);
    }

    private async void MoveAsync(Transform from, Transform to)
    {
        isMoving = true;

        if (moveDelay > 0f)
            await Task.Delay((int)(moveDelay * 1000));

        await SomeAsyncMethod(from, to);

        if (stopDuration > 0f)
            await Task.Delay((int)(stopDuration * 1000));

        isMoving = false;
    }

    private async Task SomeAsyncMethod(Transform from, Transform to)
    {
        for (float t = 0; t < 1f; t += Time.deltaTime)
        {
            // Si el objeto fue destruido o CanMove es false, salimos
            if (!CanMove || this == null)
                break;

            transform.position = Vector3.Lerp(from.position, to.position, remapLerp.Evaluate(t));
            transform.rotation = Quaternion.Lerp(from.rotation, to.rotation, remapLerp.Evaluate(t));
            await Task.Yield();
        }

        if (this != null && CanMove)
        {
            transform.position = to.position;
            transform.rotation = to.rotation;
        }
    }
}