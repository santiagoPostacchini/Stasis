using System;
using Managers.Events;
using Player.Scripts.Interactor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Player.Scripts.MovementFSM.MVC;
using Puzzle_Elements.Hedron.Scripts;

namespace Player.Stasis
{
    public class StasisGun : MonoBehaviour
    {
        [Header("Visual")] [SerializeField] private Transform stasisOrigin;
        [SerializeField] private GameObject stasisBeamPrefab;
        [SerializeField] private GameObject particleStasisMissed;

        [Header("Raycast")] [Tooltip("Radio del SphereCast para perdonar errores de puntería.")] [SerializeField]
        private float radiusStasis = 0.2f;

        [Tooltip("Distancia máxima del raycast/spherecast.")] [SerializeField]
        private float maxDistance = 300f;

        [Tooltip("Capas a considerar como objetivo (excluye Player).")] [SerializeField]
        private LayerMask layer;

        [Header("Fuego")] [SerializeField] private float cooldown = 0.25f;
        public bool canShootStasis = true;

        [Header("Debug")] [SerializeField] private bool debugDraw = true;
        [SerializeField] private bool debugLogs;
        [SerializeField] private float debugPersist = 0.25f;
        [SerializeField] private Color debugRayColor = new Color(0f, 0.9f, 1f);
        [SerializeField] private Color debugBeamColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color debugNormalColor = new Color(1f, 1f, 0f);
        [SerializeField] private Color debugMissColor = new Color(1f, 0.3f, 0.3f);

        private readonly int _maxStasisObjects = 2;
        private readonly List<(GameObject obj, IStasis stasis)> _stasisList = new();

        private StasisBeam _activeBeam;
        private PlayerInteractor _playerInteractor;
        private View _view;
        [HideInInspector] public UnityEngine.Camera mainCam;

        public event Action OnShoot = delegate { };

        [HideInInspector] public bool stasisActivate = false;

        private void Start()
        {
            _playerInteractor = GetComponent<PlayerInteractor>();
            _view = GetComponentInParent<View>();

            mainCam = GetComponentInChildren<UnityEngine.Camera>()
                      ?? GetComponentInParent<UnityEngine.Camera>()
                      ?? UnityEngine.Camera.current
                      ?? UnityEngine.Camera.main;

            if (_view) OnShoot += _view.OnShootEvent;

            if (!mainCam && debugLogs)
                Debug.LogWarning("[StasisGun] No se encontró cámara. Asigna una Camera en escena.");
        }

        private void Update()
        {
            if (!canShootStasis) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // 1) Intento directo: usar releasing target si es válido y está dentro de ventana
            PhysicsBox releasing = null;
            if (_playerInteractor)
            {
                releasing = _playerInteractor.GetReleasingTarget();
                // sanity check: a veces el objeto ya no está activo (destruido/deshabilitado)
                if (releasing && !releasing.gameObject.activeInHierarchy)
                    releasing = null;
            }

            if (releasing)
            {
                // Garantizar IStasis
                var stasisComp = (IStasis)releasing;
                // hitPoint “bonito” para el beam
                Vector3 hitPoint = releasing.transform.position;
                var col = releasing.GetComponentInChildren<Collider>();
                if (col) hitPoint = col.bounds.center;

                DirectStasisTo(releasing.gameObject, stasisComp, hitPoint);
                return; // no seguimos al raycast
            }

            // 2) Sin releasing válido → flujo normal
            TryApplyStasis();
        }
        
        public void ActivateGun() => canShootStasis = true;
        public void DeactivateGun() => canShootStasis = false;

        public void RemoveToListStasis()
        {
            for (int i = _stasisList.Count - 1; i >= 0; i--)
            {
                if (!_stasisList[i].stasis.IsFreezed)
                    _stasisList.RemoveAt(i);
            }
        }

        public void StasisActivate()
        {
            stasisActivate = true;
        }
        private void TryApplyStasis()
        {
            if (!stasisActivate) return;
            if (!canShootStasis || !mainCam) return;

            canShootStasis = false;
            StartCoroutine(ResetShootAfter(cooldown));

            Ray ray = GetCenterScreenRay(mainCam);

            bool gotHit = Physics.SphereCast(ray, radiusStasis, out RaycastHit hit, maxDistance, layer,
                              QueryTriggerInteraction.Collide)
                          || Physics.Raycast(ray, out hit, maxDistance, layer, QueryTriggerInteraction.Ignore);

            bool stasisHit = false;

            if (gotHit)
            {
                var hitGo = hit.collider.gameObject;

                if (hitGo.TryGetComponent<IStasis>(out var stasisComponent))
                {
                    var staseable = stasisComponent;
                    var objStaseable = ((MonoBehaviour)stasisComponent).gameObject;

                    var root = hitGo.GetComponentInParent<StasisRoot>();
                    if (root)
                    {
                        var found = root.GetComponentsInChildren<MonoBehaviour>().OfType<IStasis>().FirstOrDefault();
                        if (found != null)
                        {
                            staseable = found;
                            objStaseable = ((MonoBehaviour)staseable).gameObject;
                        }
                    }

                    stasisHit = true;
                    StartCoroutine(WaitStasisEffect(objStaseable, staseable));
                }
                else
                {
                    SpawnMissFx(hit.point, hit.normal);
                }

                if (_activeBeam) Destroy(_activeBeam.gameObject);
                OnShoot?.Invoke();
                StartCoroutine(SpawnBeamNextFrame(hit.point, stasisHit));
            }
            else
            {
                Vector3 missPoint = ray.origin + ray.direction * Mathf.Min(25f, maxDistance * 0.2f);
                SpawnMissFx(missPoint, -ray.direction);
                DrawDebugShot(ray, false, default, false);
            }

            if (gotHit) DrawDebugShot(ray, true, hit, stasisHit);
        }

        /// <summary>
        /// Aplica stasis directo (sin raycast) a un objeto / componente IStasis dado.
        /// Maneja cooldown, beam FX y debug igual que el flujo normal.
        /// </summary>
        private void DirectStasisTo(GameObject targetObj, IStasis stasisComp, Vector3 hitPoint)
        {
            // Cooldown y evento de disparo
            canShootStasis = false;
            StartCoroutine(ResetShootAfter(cooldown));
            OnShoot?.Invoke();

            // Aplica stasis con el mismo pequeño delay que el flujo normal
            StartCoroutine(WaitStasisEffect(targetObj, stasisComp));

            // Beam visual: desde stasisOrigin hacia el punto del objeto
            if (_activeBeam) Destroy(_activeBeam.gameObject);
            StartCoroutine(SpawnBeamNextFrame(hitPoint, true));

            // Debug visual (si hay cámara, dibujamos como si fuera un hit)
            if (debugDraw)
            {
                Ray ray;
                if (mainCam)
                    ray = GetCenterScreenRay(mainCam);
                else
                    ray = new Ray(stasisOrigin ? stasisOrigin.position : transform.position,
                        (hitPoint - (stasisOrigin ? stasisOrigin.position : transform.position)).normalized);

                var fakeHit = new RaycastHit
                {
                    point = hitPoint,
                    normal = (stasisOrigin ? (hitPoint - stasisOrigin.position).normalized : -transform.forward)
                };

                DrawDebugShot(ray, true, fakeHit, true);
            }
            
            if (_playerInteractor != null)
            {
                // método nuevo (ver abajo) para limpiar
                _playerInteractor.ClearReleasingTargetIf(targetObj);
            }
            
            if (debugLogs) Debug.Log($"[StasisGun] Direct STASIS to releasing target: {targetObj.name}", targetObj);
        }


        private Ray GetCenterScreenRay(UnityEngine.Camera cam)
        {
            Ray r = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float offset = cam.nearClipPlane + 0.03f;
            return new Ray(r.origin + r.direction * offset, r.direction);
        }

        private IEnumerator ResetShootAfter(float t)
        {
            yield return new WaitForSeconds(t);
            canShootStasis = true;
        }

        private IEnumerator WaitStasisEffect(GameObject hitObject, IStasis stasisComponent)
        {
            yield return new WaitForSeconds(0.06f);
            ApplyStasisEffect(hitObject, stasisComponent);
        }

        private IEnumerator SpawnBeamNextFrame(Vector3 hitPoint, bool stasisHit)
        {
            yield return null;

            if (!stasisOrigin || !stasisBeamPrefab) yield break;

            GameObject beamInstance = Instantiate(stasisBeamPrefab, stasisOrigin.position, Quaternion.identity);
            _activeBeam = beamInstance.GetComponent<StasisBeam>();
            _activeBeam.SetBeam(stasisOrigin.position, hitPoint, stasisHit);
            EventManager.TriggerEvent("LaserFX", gameObject);
        }

        private void ApplyStasisEffect(GameObject newObject, IStasis newStasisComponent)
        {
            int idx = _stasisList.FindIndex(x => x.obj == newObject);
            if (idx != -1)
            {
                _stasisList[idx].stasis.StatisEffectDeactivate();
                _stasisList.RemoveAt(idx);
                return;
            }

            if (_stasisList.Count >= _maxStasisObjects)
            {
                _stasisList[0].stasis.StatisEffectDeactivate();
                _stasisList.RemoveAt(0);
            }

            _stasisList.Add((newObject, newStasisComponent));
            newStasisComponent.StatisEffectActivate();
        }

        private void SpawnMissFx(Vector3 point, Vector3 normal)
        {
            if (!particleStasisMissed) return;

            Vector3 spawnPos = point + normal.normalized * 0.15f;
            GameObject fx = Instantiate(particleStasisMissed, spawnPos, Quaternion.LookRotation(normal));
            fx.SetActive(true);
            var ps = fx.GetComponent<ParticleSystem>();
            if (ps) ps.Play();
            Destroy(fx, 5f);
        }

        private void OnDisable() => UnfreezeAllObjects();

        private void UnfreezeAllObjects()
        {
            foreach (var (obj, st) in _stasisList)
                st.StatisEffectDeactivate();
            _stasisList.Clear();
        }

        private void DrawDebugShot(Ray ray, bool gotHit, RaycastHit hit, bool stasisHit)
        {
            if (!debugDraw) return;

            if (gotHit)
            {
                Debug.DrawRay(ray.origin, ray.direction * hit.distance, stasisHit ? Color.green : debugRayColor,
                    debugPersist);
                if (stasisOrigin)
                    Debug.DrawLine(stasisOrigin.position, hit.point, stasisHit ? Color.green : debugBeamColor,
                        debugPersist);
                Debug.DrawRay(hit.point, hit.normal * 0.5f, debugNormalColor, debugPersist);

                if (stasisOrigin)
                {
                    var toHitFromMuzzle = (hit.point - stasisOrigin.position).normalized;
                    float aimVsBeamAngle = Vector3.Angle(ray.direction, toHitFromMuzzle);
                    if (debugLogs && aimVsBeamAngle > 5f)
                        Debug.LogWarning(
                            $"[StasisGun] Aim vs Beam angle = {aimVsBeamAngle:F1}° (posible desalineación de FX o mano).");
                }
            }
            else
            {
                Debug.DrawRay(ray.origin, ray.direction * Mathf.Min(50f, maxDistance), debugMissColor, debugPersist);
            }
        }
    }
}