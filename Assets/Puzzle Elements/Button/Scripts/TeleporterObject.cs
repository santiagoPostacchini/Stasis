using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleporterObject : MonoBehaviour
{
    [SerializeField] private GameObject objectToTeleport;
    [SerializeField] private Transform pos;

    public void Teleport()
    {
        VFXHedro particlesHedro = objectToTeleport.GetComponent<VFXHedro>();
        if(particlesHedro != null)
        {
            //particlesHedro.DecreaseChildrenScale
        }
        objectToTeleport.transform.position = pos.position;
    }
}
