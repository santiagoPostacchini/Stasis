using System.Collections;
using System.Collections.Generic;
using Player.FullBody_Scripts;
using Player.Scripts;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerCinemachineController : MonoBehaviour
{
    [Header("<color=red>Dependencies</color>")]
    [SerializeField] private Movement _playerMovement;

    [Header("<color=blue>Cinemachine</color>")]
    [SerializeField] private CinemachineCamera _virtualCamera;

    [Header("<color=purple>FOV Settings</color>")]
    [SerializeField] private float originalFOV;
    [SerializeField] private float runFOV;
    [SerializeField] private float runIncreaseFOV = 10f;
    [SerializeField] private float runFovSpeed = 5f;

    void Start()
    {
        if (_virtualCamera != null)
        {
            originalFOV = _virtualCamera.Lens.FieldOfView;
        }

        runFOV = originalFOV + runIncreaseFOV;
    }

    void FixedUpdate()
    {
        AdjustFOVDuringRun();
    }

    private void AdjustFOVDuringRun()
    {
        if (_playerMovement != null)
        {
            if (_playerMovement.isRunning)
            {
                _virtualCamera.Lens.FieldOfView = Mathf.Lerp(_virtualCamera.Lens.FieldOfView, runFOV, Time.deltaTime * runFovSpeed);
            }
            else
            {
                _virtualCamera.Lens.FieldOfView = Mathf.Lerp(_virtualCamera.Lens.FieldOfView, originalFOV, Time.deltaTime * runFovSpeed);
            }
        }
    }

}
