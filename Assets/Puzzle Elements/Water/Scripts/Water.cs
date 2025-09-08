using Player.Scripts;
using Puzzle_Elements.Hedron.Scripts;
using UnityEngine;

namespace Puzzle_Elements.Water.Scripts
{
    public class Water : MonoBehaviour
    {
        [SerializeField] private Transform _posPlayer;
        [SerializeField] private Transform _posHedro;
        private void OnTriggerEnter(Collider other)
        {
            Movement player = other.GetComponent<Movement>();
            if(player != null)
            {
                player.transform.position = _posPlayer.transform.position;
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
    }
}
