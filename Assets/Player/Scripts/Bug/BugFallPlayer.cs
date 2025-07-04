using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Scripts.MVC;

public class BugFallPlayer : MonoBehaviour
{
    [SerializeField] private Transform pos;
    private void OnTriggerEnter(Collider other)
    {
        Model _player = other.GetComponent<Model>();
        if(_player != null)
        {
            _player.transform.position = pos.position;
        }
    }
}
