using System;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.LaserSensor.Scripts
{
    public class SensorLaser : MonoBehaviour
    {
        [Tooltip("Lista de lasers")]
        public Laser[] lasers;

        public GameObject[] laserGameobject;
        [Header("Events")]
        [Tooltip("Eventos que se llaman cuando el laser detecta al Player")]
        public UnityEvent OnIntruderDetected;

        private bool _detectPlayer = false;
        private bool alreadyEventInit = false;


        public Action OnLaser;
        public Action OnPlayerHit;

        public bool _canKillPlayer = true;
        private void Start()
        {
            OnLaser?.Invoke();
        }
        private void Update()
        {
            if (PlayerConfirmByTrigger() && !alreadyEventInit && _canKillPlayer)
            {
                OnIntruderDetected?.Invoke();
                OnPlayerHit?.Invoke();
                alreadyEventInit = true;
                //StartCoroutine(WaitForNextEvent());
            }
            if (alreadyEventInit)
            {
                if (!PlayerConfirmByTrigger()) alreadyEventInit = false;
            }
        }
        public void CanShootLasers(bool a)
        {
            foreach (var item in lasers)
            {
                item.canShootLaserByStasis = a;
            }
        }
        public void CanKillPlayer()
        {
            _canKillPlayer = true;
            foreach (var item in laserGameobject)
            {
                if(item != null)
                {
                    item.gameObject.SetActive(true);
                }
            }
        }
        public void CantKillPlayer()
        {
            _canKillPlayer = false;
            foreach (var item in laserGameobject)
            {
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                }
            }
        }
        bool PlayerConfirmByTrigger()
        {
            return _detectPlayer;
        }
        private void OnTriggerEnter(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if(player != null)
            {
                Debug.Log("Player intruso");
                _detectPlayer = true;
            }
        }
        private void OnTriggerExit(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if (player != null)
            {
                _detectPlayer = false;
            }
        }
    }
}
