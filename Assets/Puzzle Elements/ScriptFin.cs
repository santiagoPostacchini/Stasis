using Player.Scripts.MovementFSM.MVC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScriptFin : MonoBehaviour
{
    [SerializeField] private GameObject _panel; 
    private void OnTriggerEnter(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if(player != null)
        {
            _panel.gameObject.SetActive(true);
        }
    }
}
