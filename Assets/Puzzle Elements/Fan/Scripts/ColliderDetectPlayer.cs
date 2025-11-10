using Player.Scripts.MovementFSM.MVC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderDetectPlayer : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerStay(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if(player != null)
        {
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if(rb != null)
            {
                rb.velocity = Vector3.zero;
            }
        }
    }
}
