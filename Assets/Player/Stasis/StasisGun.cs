using System;
using Managers.Events;
using Player.Scripts.Interactor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Player.Scripts.MovementFSM.MVC;

namespace Player.Stasis
{
    public class StasisGun : MonoBehaviour
    {
        [Header("Visual Settings")] [SerializeField]
        private Transform stasisOrigin;

        [SerializeField] private GameObject stasisBeamPrefab;
        [SerializeField] private float radiusStasis = 0.2f;

        [Header("Cantidad de objetos staseables")]
        private readonly List<(GameObject obj, IStasis stasis)> _stasisList = new List<(GameObject, IStasis)>();

        private readonly int _maxStasisObjects = 2;

        private StasisBeam _activeBeam;
        private Coroutine _beamCoroutine;

        private PlayerInteractor _playerInteractor;
        [HideInInspector] public UnityEngine.Camera mainCam;

        public bool canShootStasis;
        [SerializeField] private LayerMask layer;
        [SerializeField] private float cooldown;

        public event Action OnShoot = delegate { };

        private View _view;

        [SerializeField] private GameObject particleStasisMissed;

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

                TryApplyStasis();
            }
        }

        public void RemoveToListStasis()
        {
            for (int i = _stasisList.Count - 1; i >= 0; i--)
            {
                if (!_stasisList[i].stasis.IsFreezed)
                    _stasisList.RemoveAt(i);
            }
        }

        private IEnumerator WaitCanShoot(float a)
        {
            yield return new WaitForSeconds(a);
            canShootStasis = true;
        }

        private void TryApplyStasis()
        {
            if (!canShootStasis) return;

            canShootStasis = false;
            StartCoroutine(WaitCanShoot(cooldown));

            // Ray desde el centro de la pantalla
            Ray ray;
            if (mainCam)
            {
                ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

                // Pequeño offset para no pegarle al propio near plane/cabeza
                float offset = (mainCam.nearClipPlane + 0.01f);
                ray = new Ray(ray.origin + ray.direction * offset, ray.direction);
            }
            else
            {
                // Fallback por si no hay referencia a la cámara (no debería pasar)
                ray = new Ray(transform.position, transform.forward);
            }

            if (Physics.SphereCast(ray, radiusStasis, out RaycastHit hit, Mathf.Infinity, layer,
                    QueryTriggerInteraction.Ignore))
            {
                bool stasisHit = false;
                GameObject hitObject = hit.collider.gameObject;

                if (hitObject.TryGetComponent<IStasis>(out var stasisComponent))
                {
                    IStasis staseable = stasisComponent;
                    GameObject objStaseable = ((MonoBehaviour)stasisComponent).gameObject;

                    // Buscar root opcional
                    StasisRoot root = hitObject.GetComponentInParent<StasisRoot>();
                    if (root)
                    {
                        var found = root.GetComponentsInChildren<MonoBehaviour>().OfType<IStasis>().FirstOrDefault();
                        if (found != null)
                        {
                            staseable = found;
                            objStaseable = ((MonoBehaviour)staseable).gameObject;
                        }
                    }

                    if (staseable != null)
                    {
                        Debug.Log("El objeto staseable es " + objStaseable);
                        StartCoroutine(WaitStasisEffect(objStaseable, staseable));
                        // Si usás el flag para colorear el rayo, podés marcarlo aquí:
                        // stasisHit = true;
                    }
                }
                else
                {
                    if (particleStasisMissed)
                    {
                        float normalOffset = 0.2f;
                        Vector3 spawnPos = hit.point + hit.normal * normalOffset;
                        GameObject fx = Instantiate(particleStasisMissed, spawnPos,
                            Quaternion.LookRotation(hit.normal));
                        fx.SetActive(true);
                        var particle = fx.GetComponent<ParticleSystem>();
                        if (particle) particle.Play();
                        Destroy(fx, 5f);
                    }
                }

                if (_activeBeam) Destroy(_activeBeam.gameObject);

                OnShoot?.Invoke();
                StartCoroutine(WaitShot(hit, stasisHit));
            }

            // Debug visual opcional
            // Debug.DrawRay(ray.origin, ray.direction * 100f, Color.cyan, 0.2f);
        }

        private IEnumerator WaitStasisEffect(GameObject hitObject, IStasis stasisComponent)
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