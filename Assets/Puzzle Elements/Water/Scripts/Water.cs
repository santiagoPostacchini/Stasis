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

        [SerializeField] private LinearCheckpointSystem _checkpoint;
        private void OnTriggerEnter(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if(player != null)
            {
                
                OnFallInWater?.Invoke();
                StartCoroutine(waitDeath(player));
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
            Model player = collision.gameObject.GetComponent<Model>();
            if (player != null)
            {
                OnFallInWater?.Invoke();
                StartCoroutine(waitDeath(player));
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
        IEnumerator waitDeath(Model player)
        {
            yield return new WaitForSeconds(0.3f);
            player.transform.position = _checkpoint.CurrentCheckpointPos();
        }

    }
}
