﻿using Managers.Events;
using Player.Scripts.Interactor;
using UnityEngine;

namespace Player.Stasis
{
    public class StasisGun : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Transform stasisOrigin;
        [SerializeField] private GameObject stasisBeamPrefab;

        private GameObject _firstFrozenObject;
        private IStasis _firstStasisComponent;

        private GameObject _secondFrozenObject;
        private IStasis _secondStasisComponent;

        private StasisBeam _activeBeam;
        private Coroutine _beamCoroutine;

        private PlayerInteractor _playerInteractor;
        private UnityEngine.Camera _mainCam;

        [SerializeField] private bool _canShootStasis;
    
        void Start()
        {
            _playerInteractor = GetComponent<PlayerInteractor>();
            _mainCam = UnityEngine.Camera.main;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                _canShootStasis = !_canShootStasis;
            }
            
            if (!_canShootStasis)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                if (_playerInteractor && _playerInteractor.HasObjectInHand())
                    return;
                
               
                TryApplyStasis(_mainCam.transform);
            }
        }

        private void TryApplyStasis(Transform playerCameraTransform)
        {
            if (!_canShootStasis) return;
            Vector3 origin = playerCameraTransform.position;
            Vector3 direction = playerCameraTransform.forward;
            
            
            
            if (Physics.Raycast(origin, direction, out RaycastHit hit))
            {
                bool stasisHit = false;
                
                GameObject hitObject = hit.collider.gameObject;

                if (hitObject.TryGetComponent<IStasis>(out var stasisComponent))
                {
                    ApplyStasisEffect(hitObject, stasisComponent);
                    stasisHit = true;
                    
                }
                Debug.DrawRay(origin, hit.point, Color.cyan, 1f);
                if (_activeBeam)
                {
                    Destroy(_activeBeam.gameObject);
                }

                GameObject beamInstance = Instantiate(stasisBeamPrefab, stasisOrigin.position, Quaternion.identity);
                _activeBeam = beamInstance.GetComponent<StasisBeam>();
                _activeBeam.SetBeam(stasisOrigin.position, hit.point, stasisHit);
                EventManager.TriggerEvent("Shot", gameObject.transform.parent.gameObject);
            }
        }
        
        void ApplyStasisEffect(GameObject newObject, IStasis newStasisComponent)
        {
            if (newObject == _firstFrozenObject)
            {
                _firstStasisComponent.StatisEffectDeactivate();
                _firstFrozenObject = null;
                _firstStasisComponent = null;
                return;
            }
            if (newObject == _secondFrozenObject)
            {
                _secondStasisComponent.StatisEffectDeactivate();
                _secondFrozenObject = null;
                _secondStasisComponent = null;
                return;
            }
            
            if (_firstFrozenObject && _secondFrozenObject)
            {
                _firstStasisComponent.StatisEffectDeactivate();
                
                _firstFrozenObject = _secondFrozenObject;
                _firstStasisComponent = _secondStasisComponent;
                _secondFrozenObject = null;
                _secondStasisComponent = null;
            }

            // Si hay lugar en el segundo slot, poner el nuevo ahí
            if (!_firstFrozenObject)
            {
                _firstFrozenObject = newObject;
                _firstStasisComponent = newStasisComponent;
                _firstStasisComponent.StatisEffectActivate();
            }
            else if (!_secondFrozenObject)
            {
                _secondFrozenObject = newObject;
                _secondStasisComponent = newStasisComponent;
                _secondStasisComponent.StatisEffectActivate();
            }
        }

        private void OnDisable()
        {
            UnfreezeAllObjects();
        }

        private void UnfreezeAllObjects()
        {
            _firstStasisComponent?.StatisEffectDeactivate();
            _secondStasisComponent?.StatisEffectDeactivate();

            _firstFrozenObject = null;
            _secondFrozenObject = null;
            _firstStasisComponent = null;
            _secondStasisComponent = null;
        }
    }
}