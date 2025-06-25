using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleporterObject : MonoBehaviour
{
    [SerializeField] private GameObject objectToTeleport;
    [SerializeField] private Transform pos;

    public void Teleport()
    {
        objectToTeleport.transform.position = pos.position;
    }
}
