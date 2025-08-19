using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SensorLaser : MonoBehaviour
{
    [Header("Laser Settings (used only if ByLaser)")]
    public Laser[] lasers;



    [Header("Events")]
    public UnityEvent OnIntruderDetected;


    private bool alreadyEventInit = false;
    private void Update()
    {
        if (PlayerConfirm() && !alreadyEventInit)
        {
            OnIntruderDetected?.Invoke();
            alreadyEventInit = true;
            //StartCoroutine(WaitForNextEvent());
        }
        if (alreadyEventInit)
        {
            if (!PlayerConfirm()) alreadyEventInit = false;
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
    
    bool PlayerConfirm()
    {
        foreach (var item in lasers)
        {
            if (item.canInvokeEvent)
            {
                if (item.intruderConfirm)
                {
                    foreach (var item2 in lasers)
                    {
                        if (!item2.intruderConfirm)
                            item2.otherDetectIntruder = true;
                    }
                    return true;
                }
            }
           
            
        }
        foreach (var item in lasers)
        {
            item.otherDetectIntruder = false;
        }
        return false;
    }
}
