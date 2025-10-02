using Player.Scripts;
using Player.Scripts.MovementFSM.MVC;
using Puzzle_Elements.Hedron.Scripts;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Water.Scripts
{
    public class Water : MonoBehaviour
    {

        public UnityEvent OnFallInWater;

        [SerializeField] private LinearCheckpointSystem _checkpoint;
        private void OnTriggerEnter(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if(player != null)
            {
                player.transform.position = _checkpoint.CurrentCheckpointPos();
                OnFallInWater?.Invoke();
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if(rb != null)
                {
                    rb.velocity = Vector3.zero;
                }
            }

            PhysicsBox hedro = other.GetComponent<PhysicsBox>();
            if(hedro != null)
            {
                hedro.transform.position = hedro._posInitial;
                Rigidbody rb = hedro.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                }
            }
        }
        private void OnCollisionEnter(Collision collision)
        {
            Model player = collision.gameObject.GetComponent<Model>();
            if (player != null)
            {
                player.transform.position = _checkpoint.CurrentCheckpointPos();
                OnFallInWater?.Invoke();
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                }
            }

            PhysicsBox hedro = collision.gameObject.GetComponent<PhysicsBox>();
            if (hedro != null)
            {
                hedro.transform.position = hedro._posInitial;

                Rigidbody rb = hedro.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                }
            }
        }

    }
}
