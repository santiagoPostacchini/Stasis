using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Camera;
using Player.Stasis;

public class FirstMovementCameraPlayer : MonoBehaviour
{
    [SerializeField] private PlayerCam _playerCam;
    [SerializeField] private StasisGun _stasisGun;
    // Start is called before the first frame update
    void Start()
    {
        _playerCam = GetComponentInParent<PlayerCam>();
        _stasisGun = GetComponentInParent<StasisGun>();
    }

    public void InitCamRotation()
    {
        _playerCam.CanRotateCamera();
        
    }
}
