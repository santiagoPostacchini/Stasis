using Player.Scripts;
using Player.Scripts.MovementFSM.MVC;
using Puzzle_Elements.Hedron.Scripts;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Water.Scripts
{
    public class Water : MonoBehaviour
    {
        [SerializeField] private Transform _posPlayer;
        [SerializeField] private Transform _posHedro;
        public UnityEvent OnFallInWater;
        private void OnTriggerEnter(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if(player != null)
            {
                player.transform.position = _posPlayer.transform.position;
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
                hedro.transform.position = _posHedro.transform.position;
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
                player.transform.position = _posPlayer.transform.position;
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
                hedro.transform.position = _posHedro.transform.position;
                Rigidbody rb = hedro.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                }
            }
        }

    }
}
