using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public class CinemachineCameraSwitcher : MonoBehaviour
{
    [Header("Cámaras virtuales")]
    public CinemachineVirtualCameraBase cameraA;
    public CinemachineVirtualCameraBase cameraB;
    public CinemachineVirtualCameraBase cameraC;

    [Header("Configuración")]
    public float blendTime = 1f; // Debe coincidir con el Default Blend del CinemachineBrain

    [Header("Activadores de transición (toggle manual)")]
    public bool transitionToA;
    public bool transitionToB;
    public bool transitionToC;

    private bool isTransitioning = false;

    // Guardamos el estado previo de los bools para detectar cambios
    private bool prevA, prevB, prevC;

    private void Update()
    {
        // Detectar cuándo cambian de false -> true
        if (transitionToA && !prevA)
            StartCoroutine(SwitchRoutine(cameraA));

        if (transitionToB && !prevB)
            StartCoroutine(SwitchRoutine(cameraB));

        if (transitionToC && !prevC)
            StartCoroutine(SwitchRoutine(cameraC));

        // Guardar estado actual
        prevA = transitionToA;
        prevB = transitionToB;
        prevC = transitionToC;
    }

    private IEnumerator SwitchRoutine(CinemachineVirtualCameraBase newCam)
    {
        if (isTransitioning)
            yield break; // Evita solapamientos

        isTransitioning = true;

        // Asegurarse de que todas las cámaras estén activas
        foreach (var vcam in FindObjectsOfType<CinemachineVirtualCameraBase>())
            vcam.gameObject.SetActive(true);

        // Bajar prioridad de todas menos la nueva
        foreach (var vcam in FindObjectsOfType<CinemachineVirtualCameraBase>())
        {
            if (vcam != newCam)
                vcam.Priority = 5;
        }

        // Subir prioridad de la cámara objetivo
        newCam.Priority = 20;

        // Esperar el tiempo de blend
        yield return new WaitForSeconds(blendTime);

        // Ya no desactivamos las otras cámaras
        // CinemachineBrain las ignora automáticamente

        isTransitioning = false;
    }
}



