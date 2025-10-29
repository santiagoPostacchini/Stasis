using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;

public class DobleActiveEvent : MonoBehaviour
{
    [SerializeField] bool _leftActivate;
    [SerializeField] bool _rightActivate;


    public UnityEvent events;


    // Start is called before the first frame update
    void Start()
    {
        _leftActivate = false;
        _rightActivate = false;
    }
    public void ChangeLeftActivator()
    {
        _leftActivate = !_leftActivate;
        TryEvent();
    }
    public void ChangeRightActivator() 
    {
        _rightActivate = !_rightActivate;
        TryEvent();
    }
    public void TryEvent()
    {
        if (!_rightActivate || !_leftActivate) return;
        events?.Invoke();

    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
