using Player.Scripts.MovementFSM.MVC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrainMovePlayer : MonoBehaviour
{
    [SerializeField]private Model _player;
    public Vector3 velocityTrain = Vector3.zero;
    public Vector3 velocityPlayer = Vector3.zero;

    public Rigidbody trainRb;
    public Rigidbody playerRb;

    private void Start()
    {
        
    }

    private void FixedUpdate()
    {
        if(trainRb != null && playerRb != null)
        {
            velocityTrain = trainRb.velocity;
            velocityPlayer = playerRb.velocity;
        }

        if(_player != null)
        {
            playerRb.position += trainRb.velocity * Time.fixedDeltaTime;
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if(player != null)
        {
            _player = player;
            playerRb = player.GetComponent<Rigidbody>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if (player != null)
        {
            _player = null;
            playerRb = null;
        }
    }
}
