using Player.Scripts;
using Player.Scripts.MovementFSM.MVC;
using Puzzle_Elements.Hedron.Scripts;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Water.Scripts
{
    public class Water : MonoBehaviour
    {

        public UnityEvent OnFallInWater;
        Model player;

        [SerializeField] private LinearCheckpointSystem _checkpoint;
        private void OnTriggerEnter(Collider other)
        {
            player = other.GetComponent<Model>();
            if(player != null)
            {
                
                OnFallInWater?.Invoke();
                player.transform.position = _checkpoint.CurrentCheckpointPos();
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if(rb != null)
                {
                    rb.velocity = Vector3.zero;
                }
            }

            PhysicsBox hedro = other.GetComponent<PhysicsBox>();
            if(hedro != null)
            {
                hedro.transform.position = hedro.posInitial;
                Rigidbody rb = hedro.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                }
            }
        }
        private void OnCollisionEnter(Collision collision)
        {
            player = collision.gameObject.GetComponent<Model>();
            if (player != null)
            {
                OnFallInWater?.Invoke();
                player.transform.position = _checkpoint.CurrentCheckpointPos();
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                }
            }

            PhysicsBox hedro = collision.gameObject.GetComponent<PhysicsBox>();
            if (hedro != null)
            {
                hedro.transform.position = hedro.posInitial;

                Rigidbody rb = hedro.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                }
            }
        }
        public void PlayerDeath()
        {
            player.transform.position = _checkpoint.CurrentCheckpointPos();
        }

    }
}
