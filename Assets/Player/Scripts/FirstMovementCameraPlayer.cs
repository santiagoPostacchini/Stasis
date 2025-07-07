using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Camera;

public class FirstMovementCameraPlayer : MonoBehaviour
{
    [SerializeField] private PlayerCam _playerCam;
    // Start is called before the first frame update
    void Start()
    {
        _playerCam = GetComponentInParent<PlayerCam>();
    }

    public void InitCamRotation()
    {
        _playerCam.CanRotateCamera();
    }
}
