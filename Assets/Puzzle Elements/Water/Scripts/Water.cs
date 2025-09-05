using Player.Scripts.MVC;
using Puzzle_Elements.Hedron.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        }

        PhysicsBox hedro = other.GetComponent<PhysicsBox>();
        if(hedro != null)
        {
            hedro.transform.position = _posHedro.transform.position;
        }
    }
}
