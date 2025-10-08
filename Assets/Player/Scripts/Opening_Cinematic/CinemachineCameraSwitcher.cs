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

    [Header("Cámara inicial")]
    public StartCamera startCamera = StartCamera.CameraA;

    [Header("Activadores de transición (toggle manual)")]
    public bool transitionToA;
    public bool transitionToB;
    public bool transitionToC;

    [Header("Referencia al RagdollHanger")]
    public RagdollHanger ragdollHanger;

    [Header("Objeto a desactivar en transición A→B")]
    public GameObject objectToDeactivate;

    private bool isTransitioning = false;

    // Guardamos el estado previo de los bools para detectar cambios
    private bool prevA, prevB, prevC;
    private bool fadeBlackPrev = false;

    public enum StartCamera
    {
        CameraA,
        CameraB,
        CameraC
    }

    private void Start()
    {
        SetInitialCamera();
    }

    private void Update()
    {
        if (transitionToA && !prevA)
            StartCoroutine(SwitchRoutine(cameraA));

        if (transitionToB && !prevB)
            StartCoroutine(SwitchRoutine(cameraB));

        if (transitionToC && !prevC)
            StartCoroutine(SwitchRoutine(cameraC));

        prevA = transitionToA;
        prevB = transitionToB;
        prevC = transitionToC;

        // Detectar cambio en fadeBlack
        if (ragdollHanger != null && ragdollHanger.fadeBlack && !fadeBlackPrev)
        {
            StartCoroutine(SwitchRoutine(cameraB));
        }

        fadeBlackPrev = (ragdollHanger != null) ? ragdollHanger.fadeBlack : false;
    }

    private void SetInitialCamera()
    {
        if (cameraA) cameraA.gameObject.SetActive(true);
        if (cameraB) cameraB.gameObject.SetActive(true);
        if (cameraC) cameraC.gameObject.SetActive(true);

        switch (startCamera)
        {
            case StartCamera.CameraA:
                SetPriority(cameraA);
                break;
            case StartCamera.CameraB:
                SetPriority(cameraB);
                break;
            case StartCamera.CameraC:
                SetPriority(cameraC);
                break;
        }
    }

    private void SetPriority(CinemachineVirtualCameraBase activeCam)
    {
        foreach (var vcam in FindObjectsOfType<CinemachineVirtualCameraBase>())
        {
            vcam.Priority = (vcam == activeCam) ? 20 : 5;
        }
    }

    private IEnumerator SwitchRoutine(CinemachineVirtualCameraBase newCam)
    {
        if (isTransitioning)
            yield break;

        isTransitioning = true;

        foreach (var vcam in FindObjectsOfType<CinemachineVirtualCameraBase>())
            vcam.gameObject.SetActive(true);

        foreach (var vcam in FindObjectsOfType<CinemachineVirtualCameraBase>())
        {
            if (vcam != newCam)
                vcam.Priority = 5;
        }

        newCam.Priority = 20;

        yield return new WaitForSeconds(blendTime);

        // Si la transición fue A→B, desactivamos el objeto asignado
        if (newCam == cameraB && objectToDeactivate != null)
        {
            objectToDeactivate.SetActive(false);
        }

        isTransitioning = false;
    }
}




