using System;
using Managers.Events;
using Player.Scripts.Interactor;
using UnityEngine;
using Player.Scripts.MVC;
using System.Collections;

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
        [HideInInspector] public UnityEngine.Camera mainCam;

        public bool canShootStasis;
        [SerializeField] private Transform posShot;
        [SerializeField] private LayerMask layer;
        [SerializeField] private float cooldown;
        
        public event Action OnShoot = delegate { };

        private View _view;

        void Start()
        {
            _playerInteractor = GetComponent<PlayerInteractor>();
            mainCam = UnityEngine.Camera.main;
            _view = GetComponentInParent<View>();
            OnShoot += _view.OnShotEvent;
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
            if (Physics.Raycast(origin, direction, out RaycastHit hit, Mathf.Infinity, layer))
            {
                bool stasisHit = false;
                
                GameObject hitObject = hit.collider.gameObject;

                if (hitObject.TryGetComponent<IStasis>(out var stasisComponent))
                {

                    StartCoroutine(WaitStasisEffect(hitObject, stasisComponent));
                    
                    stasisHit = true;
                }
                if (_activeBeam)
                {
                    Destroy(_activeBeam.gameObject);
                }

                OnShoot();
                StartCoroutine(WaitShot(hit, stasisHit));
            }
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