using Player.Scripts.MovementFSM.MVC;
using System;
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


    public Action OnLaser;
    public Action OnPlayerHit;
    private void Start()
    {
        OnLaser?.Invoke();
    }
    private void Update()
    {
        if (PlayerConfirmByTrigger() && !alreadyEventInit)
        {
            OnIntruderDetected?.Invoke();
            OnPlayerHit?.Invoke();
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
