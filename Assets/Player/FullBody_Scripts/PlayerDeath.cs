using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerDeath : MonoBehaviour
{
    [Header("<color=red>Dependencies</color>")]
    [SerializeField] private PlayerRagdoll _playerRagdoll;

    [Header("<color=green>Death Bool</color>")]
    public bool _isDead = false;

    [Header("<color=yellow>Scripts Turn Off</color>")]
    [SerializeField] private List<MonoBehaviour> _scriptsToDisable = new List<MonoBehaviour>();

    [Header("<color=blue>Camera Target</color>")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float cameraZOffsetOnDeath = 0.15f;

    private Vector3 initialCameraTargetLocalPos; 
    private bool _hasDied = false;

    private void Start()
    {
        if (_playerRagdoll == null)
            _playerRagdoll = GetComponent<PlayerRagdoll>();

        if (cameraTarget == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>())
            {
                if (t.name == "Camera_Target")
                {
                    cameraTarget = t;
                    break;
                }
            }
        }

        if (cameraTarget != null)
            initialCameraTargetLocalPos = cameraTarget.localPosition;

        Movement movementScript = GetComponent<Movement>();
        if (movementScript != null && !_scriptsToDisable.Contains(movementScript))
            _scriptsToDisable.Add(movementScript);

        Jump jumpScript = GetComponent<Jump>();
        if (jumpScript != null && !_scriptsToDisable.Contains(jumpScript))
            _scriptsToDisable.Add(jumpScript);

        HeadRotationClamper headRotationScript = GetComponent<HeadRotationClamper>();
        if (headRotationScript != null && !_scriptsToDisable.Contains(headRotationScript))
            _scriptsToDisable.Add(headRotationScript);

        CinemachinePanTilt panTiltScript = GetComponentInChildren<CinemachinePanTilt>();
        if (panTiltScript != null && !_scriptsToDisable.Contains(panTiltScript))
            _scriptsToDisable.Add(panTiltScript);

        FirstPersonCamera firstPersonScript = GetComponentInChildren<FirstPersonCamera>();
        if (firstPersonScript != null && !_scriptsToDisable.Contains(firstPersonScript))
            _scriptsToDisable.Add(firstPersonScript);
    }

    private void Update()
    {
        if (_isDead && !_hasDied)
        {
            Die();
        }
        else if (!_isDead && _hasDied)
        {
            UnDead();
        }
    }

    public void Die()
    {
        _hasDied = true;

        Debug.Log("<color=red>PLAYER DIED</color>");

        _playerRagdoll.SetRagdollActive(true);
        _playerRagdoll.deactivateRagdoll = false;

        foreach (var script in _scriptsToDisable)
        {
            if (script != null)
                script.enabled = false;
        }

        if (cameraTarget != null)
        {
            Vector3 pos = cameraTarget.localPosition;
            pos.z += cameraZOffsetOnDeath;
            cameraTarget.localPosition = pos;
        }
    }

    public void UnDead()
    {
        _hasDied = false;
        _playerRagdoll.deactivateRagdoll = true;

        foreach (var script in _scriptsToDisable)
        {
            if (script != null)
                script.enabled = true;
        }

        if (cameraTarget != null)
            cameraTarget.localPosition = initialCameraTargetLocalPos;

        Debug.Log("<color=green>PLAYER UNDEAD</color>");
    }
}




