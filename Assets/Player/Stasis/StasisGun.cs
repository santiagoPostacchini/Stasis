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
        [HideInInspector]public UnityEngine.Camera _mainCam;

        [SerializeField] private bool _canShootStasis;
        [SerializeField] private Transform posShot;
        [SerializeField] private LayerMask _layer;

        [SerializeField] private View _viewPlayer;

        private bool canShoot = false;

        void Start()
        {
            _playerInteractor = GetComponent<PlayerInteractor>();
            _mainCam = UnityEngine.Camera.main;
            _viewPlayer = GetComponentInParent<View>();
            StartCoroutine(waitCanShoot());
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
                if (!canShoot) return;
                if (_playerInteractor && _playerInteractor.HasObjectInHand())
                    return;
                
               
                TryApplyStasis(_mainCam.transform);
            }
        }
        IEnumerator waitCanShoot()
        {
            yield return new WaitForSeconds(1.2f);
            canShoot = true;
        }
        private void TryApplyStasis(Transform playerCameraTransform)
        {
            if (!_canShootStasis) return;
            Vector3 origin = playerCameraTransform.position;
            Vector3 direction = playerCameraTransform.forward;
            
            if (Physics.Raycast(origin, direction, out RaycastHit hit, Mathf.Infinity, _layer))
            {
                bool stasisHit = false;
                
                GameObject hitObject = hit.collider.gameObject;

                if (hitObject.TryGetComponent<IStasis>(out var stasisComponent))
                {

                    StartCoroutine(waitStasisEffect(hitObject, stasisComponent));
                    stasisHit = true;
                    
                    
                }
                if (_activeBeam)
                {
                    Destroy(_activeBeam.gameObject);
                }
                _viewPlayer.Shoot();
                StartCoroutine(waitShot(hit, stasisHit));
            }
        }
        IEnumerator waitStasisEffect(GameObject hitObject,IStasis stasisComponent)
        {
            yield return new WaitForSeconds(0.3f);
            ApplyStasisEffect(hitObject, stasisComponent);
        }
        IEnumerator waitShot(RaycastHit hit, bool stasisHit)
        {
            yield return new WaitForSeconds(0.3f);
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
    }
}