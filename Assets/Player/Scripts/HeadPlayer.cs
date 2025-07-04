using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Scripts.MVC;

public class HeadPlayer : MonoBehaviour
{
    private Model _player;
    public GameObject _objectUpPlayer;
    private void Start()
    {
        _player = GetComponentInParent<Model>();
    }
    private void OnTriggerStay(Collider other)
    {
        if(other != null)
        {
            _objectUpPlayer = other.gameObject;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if(other!= null)
        {
            _objectUpPlayer = null;

            _player.UpdateCrouchInput(false);
        }
    }
}
