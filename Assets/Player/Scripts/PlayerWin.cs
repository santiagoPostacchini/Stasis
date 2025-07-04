using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Scripts.MVC;

public class PlayerWin : MonoBehaviour
{

    [SerializeField] private GameObject _winPanel;



    
    private void OnTriggerEnter(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if(player != null)
        {
            _winPanel.gameObject.SetActive(true);
        }
    }
}
