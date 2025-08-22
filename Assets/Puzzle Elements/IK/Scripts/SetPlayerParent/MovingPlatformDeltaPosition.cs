using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingPlatformDeltaPosition : MonoBehaviour
{
    public Vector3 DeltaPosition { get; private set; }
    public Quaternion DeltaRotation { get; private set; }

    private Vector3 _lastPos;
    private Quaternion _lastRot;

    private void Awake()
    {
        _lastPos = transform.position;
        _lastRot = transform.rotation;
    }

    // Usamos LateUpdate para capturar el delta real de este frame
    private void LateUpdate()
    {
        DeltaPosition = transform.position - _lastPos;
        DeltaRotation = transform.rotation * Quaternion.Inverse(_lastRot);

        _lastPos = transform.position;
        _lastRot = transform.rotation;
    }
}