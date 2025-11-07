using Player.Scripts.MovementFSM.MVC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SensorLaser : MonoBehaviour
{
    [Tooltip("Lista de lasers")]
    public Laser[] lasers;


    [Header("Events")]
    [Tooltip("Eventos que se llaman cuando el laser detecta al Player")]
    public UnityEvent OnIntruderDetected;

    private bool _detectPlayer = false;
    private bool alreadyEventInit = false;
    private void Update()
    {
        if (PlayerConfirmByTrigger() && !alreadyEventInit)
        {
            OnIntruderDetected?.Invoke();
            alreadyEventInit = true;
            //StartCoroutine(WaitForNextEvent());
        }
        if (alreadyEventInit)
        {
            if (!PlayerConfirmByTrigger()) alreadyEventInit = false;
        }
    }
    public void CanShootLasers(bool a)
    {
        foreach (var item in lasers)
        {
            item.canShootLaserByStasis = a;
        }
    }
    //private IEnumerator WaitForNextEvent()
    //{
    //    canInvokeEvent = false;
    //    yield return new WaitForSeconds(1f);
    //    canInvokeEvent = true;
    //}
    
    //bool PlayerConfirm()
    //{
    //    foreach (var item in lasers)
    //    {
    //        if (item.canInvokeEvent)
    //        {
    //            if (item.intruderConfirm)
    //            {
    //                foreach (var item2 in lasers)
    //                {
    //                    if (!item2.intruderConfirm)
    //                        item2.otherDetectIntruder = true;
    //                }
    //                return true;
    //            }
    //        }
           
            
    //    }
    //    foreach (var item in lasers)
    //    {
    //        item.otherDetectIntruder = false;
    //    }
    //    return false;
    //}
    bool PlayerConfirmByTrigger()
    {
        return _detectPlayer;
    }
    private void OnTriggerEnter(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if(player != null)
        {
            Debug.Log("Player intruso");
            _detectPlayer = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if (player != null)
        {
            _detectPlayer = false;
        }
    }
}
