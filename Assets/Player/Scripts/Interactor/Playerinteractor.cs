using System;
using Managers.Events;
using Player.Stasis;
using Puzzle_Elements.Hedron.Scripts;
using UI.Scripts;
using UnityEngine;
using System.Collections;
using Player.Scripts.MovementFSM.MVC;

namespace Player.Scripts.Interactor
{
    public class PlayerInteractor : MonoBehaviour
    {
        public event Action OnInteractableFocusEnter = delegate { };
        public event Action OnInteractableFocusExit = delegate { };
        public event Action OnInteractPerformed = delegate { };

        [Header("Interaction Settings")] [SerializeField]
        private float throwCharge;

        [SerializeField] private float pickUpRange = 4f;
        [SerializeField] private float holdTime;
        [SerializeField] private float throwHoldThreshold = 0.15f;
        public float ThrowCharge => Mathf.Clamp01(throwCharge / throwHoldThreshold);
        private bool _isHoldingThrow;

        [SerializeField] private float throwForce = 10f;
        [SerializeField] private Transform objectGrabPointTransform;
        [SerializeField] private Transform objectGrabPointBackTransform;

        [Header("Grab System")] [SerializeField]
        private float minHoldDistance = -0.1f;

        [SerializeField] private float maxHoldDistance = 1.5f;
        [SerializeField] private float holderOffset = 0.05f;

        private PhysicsBox _objectGrabbable;

        [Header("Environment")] [SerializeField]
        private LayerMask environmentMask = ~0;

        [Header("Smoothing")] [SerializeField] private float rotationSmoothSpeed = 10f;

        [Header("Custom Movement")] [SerializeField]
        private AnimationCurve holdMoveCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [SerializeField] private float headDropStartDist = 0.5f;
        [SerializeField] private float maxHeadDrop = 0.5f;

        [Header("FX Settings")] [SerializeField]
        private StasisObjectEffects stasisEffects;

        [SerializeField] private StasisGun stasisGun;

        private Quaternion _rotationSmoothQuat;
        private View _view;

        public event Action OnGrabItem = delegate { };

        [Header("Interactable Focus")] [Tooltip("Radio del SphereCast para tolerancia al apuntado")] [SerializeField]
        private float focusSphereRadius = 0.15f;

        [Tooltip("Capas consideradas como interactuables (colliders de los objetos)")] [SerializeField]
        private LayerMask interactableMask = ~0;

        [Tooltip("Distancia para ENTRAR en foco")] [SerializeField]
        private float enterDistance = 4f;

        [Tooltip("Distancia para SALIR de foco (histéresis)")] [SerializeField]
        private float exitDistance = 4.5f;

        [Tooltip("Ángulo (grados) respecto al forward para ENTRAR")] [Range(1f, 30f)] [SerializeField]
        private float enterAngleDeg = 6f;

        [Tooltip("Ángulo (grados) para SALIR (histéresis)")] [Range(1f, 45f)] [SerializeField]
        private float exitAngleDeg = 8f;

        [Tooltip("Tiempo que debe mantenerse válido antes de disparar ENTER")] [SerializeField]
        private float enterStableTime = 0.06f;

        [Tooltip("Tiempo que debe mantenerse inválido antes de disparar EXIT")] [SerializeField]
        private float exitStableTime = 0.08f;

        [Tooltip("Offset inicial del cast para evitar auto-colisión")] [SerializeField]
        private float castStartOffset = 0.05f;

        private GameObject _currentFocused;
        private float _lastFocusHitDistance = Mathf.Infinity;

        // Debounce
        private GameObject _pendingEnterTarget;
        private float _pendingEnterSince = -999f;
        private float _lostSince = -999f;

        void Start()
        {
            _rotationSmoothQuat = objectGrabPointTransform.rotation;
            _view = GetComponentInParent<View>();
            OnGrabItem += _view.OnGrabEvent;

            // sincronizar con enter/exit distance por defecto
            enterDistance = Mathf.Min(enterDistance, pickUpRange);
            exitDistance = Mathf.Max(exitDistance, enterDistance + 0.25f);
        }

        void Update()
        {
            UpdateInteractableFocus();

            GameObject hitObject = GetBestInteractable(out _, out _);

            // dentro de Update(), donde presionás E y detectás IInteractable:
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (!_objectGrabbable)
                {
                    if (hitObject)
                    {
                        var interactable = hitObject.GetComponent<IInteractable>();
                        if (interactable != null)
                        {
                            OnInteractPerformed();
                            interactable.Interact();
                        }
                        else
                        {
                            TryGrabObject(hitObject);
                        }
                    }
                }
                else
                {
                    _isHoldingThrow = true;
                    holdTime = 0f;
                    throwCharge = 0f;
                }
            }

            if (_isHoldingThrow && _objectGrabbable)
            {
                holdTime += Time.deltaTime;
                throwCharge = holdTime;
                ThrowUISlider.Instance?.SetFill(Mathf.Clamp01(throwCharge / throwHoldThreshold));

                if (holdTime >= throwHoldThreshold)
                {
                    if (_objectGrabbable && !_objectGrabbable.IsOverlappingAnything)
                    {
                        _objectGrabbable.Throw(throwForce);
                        _objectGrabbable = null;
                        _isHoldingThrow = false;
                        holdTime = 0f;
                        throwCharge = 0f;
                        ThrowUISlider.Instance?.SetFill(0);

                        EventManager.TriggerEvent("OnObjectThrow", gameObject);
                    }
                }
            }

            if (Input.GetKeyUp(KeyCode.E) && _objectGrabbable && _isHoldingThrow)
            {
                if (holdTime < throwHoldThreshold) TryDropObject();

                _isHoldingThrow = false;
                holdTime = 0f;
                throwCharge = 0f;
            }

            if (_objectGrabbable) UpdateHolderPosition();
        }

        private void OnDisable()
        {
            if (_currentFocused)
            {
                OnInteractableFocusExit();
                _currentFocused = null;
                _pendingEnterTarget = null;
                _pendingEnterSince = -999f;
                _lostSince = -999f;
                _lastFocusHitDistance = Mathf.Infinity;
            }
        }

        // -------------------- LÓGICA DE FOCUS CON HISTÉRESIS + DEBOUNCE --------------------
        private void UpdateInteractableFocus()
        {
            var best = GetBestInteractable(out float dist, out float angleDeg);

            // Reglas de entrada/salida con histéresis
            bool canEnter = best &&
                            dist <= enterDistance &&
                            angleDeg <= enterAngleDeg &&
                            HasLineOfSight(best);

            bool mustExit = false;
            if (_currentFocused)
            {
                // si el mejor ya no es el actual, o se fue de distancia/ángulo/LOS
                bool currentVisible = best == _currentFocused &&
                                      dist <= exitDistance &&
                                      angleDeg <= exitAngleDeg &&
                                      HasLineOfSight(_currentFocused);

                mustExit = !currentVisible;
            }

            // Debounce ENTER
            if (!_currentFocused)
            {
                if (canEnter)
                {
                    if (_pendingEnterTarget != best)
                    {
                        _pendingEnterTarget = best;
                        _pendingEnterSince = Time.time;
                    }
                    else
                    {
                        if ((Time.time - _pendingEnterSince) >= enterStableTime)
                        {
                            _currentFocused = _pendingEnterTarget;
                            _pendingEnterTarget = null;
                            _pendingEnterSince = -999f;
                            _lostSince = -999f;
                            _lastFocusHitDistance = dist;
                            OnInteractableFocusEnter();
                        }
                    }
                }
                else
                {
                    _pendingEnterTarget = null;
                    _pendingEnterSince = -999f;
                }
            }
            // Debounce EXIT
            else
            {
                if (mustExit)
                {
                    if (_lostSince < 0f) _lostSince = Time.time;

                    if ((Time.time - _lostSince) >= exitStableTime)
                    {
                        OnInteractableFocusExit();
                        _currentFocused = null;
                        _lostSince = -999f;
                        _pendingEnterTarget = null;
                        _pendingEnterSince = -999f;
                        _lastFocusHitDistance = Mathf.Infinity;
                    }
                }
                else
                {
                    _lostSince = -999f;
                    _lastFocusHitDistance = dist;
                }
            }
        }

        // Selecciona el mejor interactuable: primero un ray directo; si no, spherecast-all, el más cercano dentro del cono.
        private GameObject GetBestInteractable(out float distance, out float angleDeg)
        {
            Vector3 origin = transform.position + transform.forward * castStartOffset;
            Vector3 dir = transform.forward;

            distance = Mathf.Infinity;
            angleDeg = 999f;

            // 1) Ray fino prioritario
            if (Physics.Raycast(origin, dir, out RaycastHit rh, exitDistance, interactableMask,
                    QueryTriggerInteraction.Ignore))
            {
                var go = rh.collider.gameObject;
                if (go.GetComponent<IInteractable>() != null)
                {
                    distance = rh.distance;
                    angleDeg = 0f;
                    return go;
                }
            }

            // 2) SphereCastAll para tolerancia y elegir el más cercano dentro del cono
            RaycastHit[] hits = Physics.SphereCastAll(origin, focusSphereRadius, dir, exitDistance,
                interactableMask, QueryTriggerInteraction.Ignore);

            GameObject best = null;
            float bestDist = Mathf.Infinity;
            float bestAngle = 999f;

            float maxAngle = (!_currentFocused) ? enterAngleDeg : exitAngleDeg;

            foreach (var t in hits)
            {
                var go = t.collider.gameObject;
                if (!go || go.GetComponent<IInteractable>() == null) continue;

                Vector3 to = t.point - origin;
                float d = to.magnitude;
                if (d <= 0.0001f) continue;

                float ang = Vector3.Angle(dir, to.normalized);
                if (ang > maxAngle) continue;

                if (d < bestDist && HasLineOfSight(go))
                {
                    best = go;
                    bestDist = d;
                    bestAngle = ang;
                }
            }

            distance = bestDist;
            angleDeg = bestAngle;
            return best;
        }

        private bool HasLineOfSight(GameObject target)
        {
            if (!target) return false;

            Vector3 origin = transform.position + transform.forward * castStartOffset;
            Vector3 targetPos = ClosestPointOrCenter(target, origin);

            // Si hay algo del environment entre medio, no hay LOS.
            if (Physics.Linecast(origin, targetPos, out RaycastHit hit, environmentMask,
                    QueryTriggerInteraction.Ignore))
            {
                // Si lo primero que toca NO pertenece al target, está ocluido
                if (hit.collider && hit.collider.gameObject != target)
                    return false;
            }

            return true;
        }

        private Vector3 ClosestPointOrCenter(GameObject go, Vector3 from)
        {
            var col = go.GetComponent<Collider>();
            if (col)
            {
                // punto más cercano sobre el colisionador
                return col.ClosestPoint(from);
            }

            return go.transform.position;
        }

        // -------------------- RESTO (igual que antes) --------------------
        private void TryGrabObject(GameObject hitObject)
        {
            OnGrabItem();
            StartCoroutine(WaitGrab(hitObject));
        }

        private IEnumerator WaitGrab(GameObject hitObject)
        {
            yield return new WaitForSeconds(0.2f);
            if (hitObject && hitObject.TryGetComponent(out PhysicsBox physicsObject))
            {
                objectGrabPointTransform.position = hitObject.transform.position;
                physicsObject.SetReferences(objectGrabPointTransform);
                physicsObject.Grab();

                _objectGrabbable = physicsObject;

                EventManager.TriggerEvent("Grab", gameObject);
            }
        }

        public void TryDropObject()
        {
            if (_objectGrabbable)
            {
                if (!_objectGrabbable.IsFreezed)
                {
                    ThrowUISlider.Instance?.SetFill(0);
                    _objectGrabbable.Drop();
                    _objectGrabbable = null;

                    EventManager.TriggerEvent("OnObjectDrop", gameObject);
                }
                else if (!_objectGrabbable.IsOverlappingAnything)
                {
                    _objectGrabbable.Drop();
                    _objectGrabbable = null;
                    EventManager.TriggerEvent("OnObjectDrop", gameObject);
                }
            }
        }

        private void UpdateHolderPosition()
        {
            float targetDistance = maxHoldDistance;
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, maxHoldDistance,
                    environmentMask))
            {
                targetDistance = Mathf.Clamp(hit.distance - holderOffset, minHoldDistance, maxHoldDistance);
            }

            float t = Mathf.InverseLerp(maxHoldDistance, minHoldDistance, targetDistance);
            float curveT = holdMoveCurve.Evaluate(t);

            Vector3 frontPos = transform.position + transform.forward * maxHoldDistance;
            Vector3 backPos = objectGrabPointBackTransform.position;

            Vector3 desiredPos = Vector3.Lerp(frontPos, backPos, curveT);

            if (targetDistance < headDropStartDist)
            {
                float headT = Mathf.InverseLerp(headDropStartDist, minHoldDistance, targetDistance);
                float dropAmt = Mathf.Lerp(0f, maxHeadDrop, headT);
                desiredPos.y -= dropAmt;
            }

            Vector3 dirToPlayer = (transform.position - desiredPos).normalized;
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer, Vector3.up);
            _rotationSmoothQuat =
                Quaternion.Slerp(_rotationSmoothQuat, targetRot, Time.deltaTime * rotationSmoothSpeed);

            objectGrabPointTransform.SetPositionAndRotation(desiredPos, _rotationSmoothQuat);
            _objectGrabbable.transform.SetPositionAndRotation(desiredPos, _rotationSmoothQuat);
        }

        public bool HasObjectInHand() => _objectGrabbable && _objectGrabbable.gameObject.activeInHierarchy;
    }
}