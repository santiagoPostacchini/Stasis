using System;
using Managers.Events;
using Player.Scripts.Interactor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Player.Scripts.MovementFSM;
using Player.Scripts.MovementFSM.MVC;

namespace Player.Stasis
{
    public class StasisGun : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Transform stasisOrigin;
        [SerializeField] private GameObject stasisBeamPrefab;
        [SerializeField] private float _radiusStasis = 0.2f;

        [Header("Cantidad de objetos staseables")]
        [HideInInspector] public List<(GameObject obj, IStasis stasis)> _stasisList = new List<(GameObject, IStasis)>();
        private int _maxStasisObjects = 2;

        private StasisBeam _activeBeam;
        private Coroutine _beamCoroutine;

        private PlayerInteractor _playerInteractor;
        [HideInInspector] public UnityEngine.Camera mainCam;

        public bool canShootStasis;
        [SerializeField] private LayerMask layer;
        [SerializeField] private float cooldown;
        
        public event Action OnShoot = delegate { };

        private View _view;

        [SerializeField] private GameObject _particleStasisMissed;

        void Start()
        {
            _playerInteractor = GetComponent<PlayerInteractor>();
            mainCam = UnityEngine.Camera.main;
            _view = GetComponentInParent<View>();
            OnShoot += _view.OnShootEvent;
        }

        void Update()
        {
            if (!canShootStasis)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                if (_playerInteractor && _playerInteractor.HasObjectInHand())
                    return;
                
                TryApplyStasis(mainCam.transform);
            }
        }
        public void RemoveToListStasis()
        {
            for (int i = 0; i < _stasisList.Count; i++)
            {
                if (!_stasisList[i].stasis.IsFreezed) _stasisList.RemoveAt(i);
            }
        }

        private IEnumerator WaitCanShoot(float a)
        {
            yield return new WaitForSeconds(a);
            canShootStasis = true;
        }
        private void TryApplyStasis(Transform playerCameraTransform)
        {
            if (!canShootStasis) return;
            canShootStasis = false;
            Vector3 origin = playerCameraTransform.position;
            Vector3 direction = playerCameraTransform.forward;
            StartCoroutine(WaitCanShoot(cooldown));
            if(Physics.SphereCast(origin, _radiusStasis, direction, out RaycastHit hit, Mathf.Infinity, layer))
            {
                bool stasisHit = false;

                GameObject hitObject = hit.collider.gameObject;


                if (hitObject.TryGetComponent<IStasis>(out var stasisComponent))
                {
                    IStasis staseable = stasisComponent;
                    GameObject objStaseable = ((MonoBehaviour)stasisComponent).gameObject;

                    // Buscamos el StasisRoot en los padres
                    StasisRoot root = hitObject.GetComponentInParent<StasisRoot>();

                    if (root != null)
                    {
                        // Buscamos el IStasis correcto en el root
                        staseable = root.GetComponentsInChildren<MonoBehaviour>().OfType<IStasis>().FirstOrDefault();
                        if (staseable != null)
                            objStaseable = ((MonoBehaviour)staseable).gameObject;
                    }

                    if (staseable != null)
                    {
                        Debug.Log("El objeto staseable es " + objStaseable);
                        StartCoroutine(WaitStasisEffect(objStaseable, staseable));
                    }
                    
                       
                }
                else
                {
                    if (_particleStasisMissed == null) return;

                    float offset = 0.2f; // pequeño offset para que no quede dentro de la pared
                    Vector3 spawnPos = hit.point + hit.normal * offset;

                    GameObject testEffect = Instantiate(_particleStasisMissed, spawnPos, Quaternion.LookRotation(hit.normal));
                    testEffect.SetActive(true); // asegúrate que está activo
                    ParticleSystem particle = testEffect.GetComponent<ParticleSystem>();
                    particle.Play();
                    Destroy(testEffect, 5f);
                }


                if (_activeBeam)
                {
                    Destroy(_activeBeam.gameObject);
                }

                OnShoot();
                StartCoroutine(WaitShot(hit, stasisHit));
            }
            
            //if (Physics.Raycast(origin, direction, out RaycastHit hit, Mathf.Infinity, layer))
            //{
                
            //}
        }
        private IEnumerator WaitStasisEffect(GameObject hitObject,IStasis stasisComponent)
        {
            yield return new WaitForSeconds(0.1f);
            ApplyStasisEffect(hitObject, stasisComponent);
        }

        private IEnumerator WaitShot(RaycastHit hit, bool stasisHit)
        {
            yield return new WaitForSeconds(0.1f);
            GameObject beamInstance = Instantiate(stasisBeamPrefab, stasisOrigin.position, Quaternion.identity);
            _activeBeam = beamInstance.GetComponent<StasisBeam>();
            _activeBeam.SetBeam(stasisOrigin.position, hit.point, stasisHit);
            EventManager.TriggerEvent("LaserFX", gameObject);
        }

        void ApplyStasisEffect(GameObject newObject, IStasis newStasisComponent)
        {
            // Si ya estaba congelado
            var existing = _stasisList.FindIndex(x => x.obj == newObject);
            if (existing != -1)
            {
                _stasisList[existing].stasis.StatisEffectDeactivate();
                _stasisList.RemoveAt(existing);
                return;
            }

            // Si estamos en el límite
            if (_stasisList.Count >= _maxStasisObjects)
            {
                _stasisList[0].stasis.StatisEffectDeactivate(); // descongelar el primero
                _stasisList.RemoveAt(0);
            }

            // Agregamos al final
            _stasisList.Add((newObject, newStasisComponent));
            newStasisComponent.StatisEffectActivate();
        }
        private void OnDisable()
        {
            UnfreezeAllObjects();
        }

        private void UnfreezeAllObjects()
        {
            foreach (var item in _stasisList)
            {
                item.stasis.StatisEffectDeactivate();
            }

            _stasisList.Clear();
        }

        public void ActivateGun()
        {
            canShootStasis = true;
        }
        public void DeactivateGun()
        {
            canShootStasis = false;
        }
    }
}