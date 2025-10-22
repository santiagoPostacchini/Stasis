// using Puzzle_Elements.Button.Scripts;
// using Puzzle_Elements.Hedron.Scripts;
// using System.Collections;
// using UnityEngine;
// using UnityEngine.Events;
// using Player.Scripts.Interactor;
// using TMPro;
//     [RequireComponent(typeof(Collider))]
// public class HedronContainerIn : MonoBehaviour,IInteractable
// {
//     [SerializeField] private Button _button;
//
//     public Transform anchor;
//     [SerializeField] private Animator _anim;
//
//     [SerializeField] private string openTrigger = "OPEN";
//     [SerializeField] private string closeTrigger = "Close";
//
//     [SerializeField] private Transform pos;              // destino para la expulsi�n
//     [SerializeField] private float posDelaySeconds = 1.5f; // espera exacta antes de mover a 'pos'
//
//     private PhysicsBox _box = null;
//
//     public UnityEvent onHedronPlaced;
//     public UnityEvent onHedronRemoved;
//
//     public float attractionSpeed = 5f;
//     public float stopDistance = 0.05f;
//
//     public LayerMask acceptMask = ~0;
//
//     public Renderer rightDoorRenderer;
//     public Renderer leftDoorRenderer;
//     public float brightnessFrequency = 2f;
//     public float brightnessAmplitude = 0.05f;
//     public Color glowColor = Color.white;
//
//     public float openToEjectDelay = 2f;
//     public bool reenableColliderOnEject = true;
//     public float ejectNudgeDistance = 0.08f;
//     public float scaleUpSeconds = 0.35f;
//     public float ejectTorque = 0f;
//
//     public float ejectCooldown = 0.15f;
//
//     Material _rightMat, _leftMat;
//     Color _baseEmissionRight, _baseEmissionLeft;
//
//     Transform _current;
//     Rigidbody _rb;
//     bool _attracting;
//     bool _placedFired;
//     bool _isEjecting;
//     float _lastEjectTime;
//
//     public bool HasOccupant => _current != null;
//
//
//     public GameObject panel;
//     public TextMeshProUGUI E;
//     public TextMeshProUGUI message;
//
//     void Awake()
//     {
//         var col = GetComponent<Collider>();
//         col.isTrigger = true;
//
//         if (rightDoorRenderer != null)
//         {
//             _rightMat = Instantiate(rightDoorRenderer.sharedMaterial);
//             rightDoorRenderer.material = _rightMat;
//             _baseEmissionRight = _rightMat.GetColor("_EmissionColor");
//         }
//         if (leftDoorRenderer != null)
//         {
//             _leftMat = Instantiate(leftDoorRenderer.sharedMaterial);
//             leftDoorRenderer.material = _leftMat;
//             _baseEmissionLeft = _leftMat.GetColor("_EmissionColor");
//         }
//     }
//
//     void Start()
//     {
//         if (_button != null) _button.SetText(_button.E, "");
//         if (panel != null) panel.gameObject.SetActive(false);
//     }
//
//     void Update()
//     {
//         Atracction();
//     }
//
//     void Atracction()
//     {
//         if (_box == null) return;
//
//         if (_attracting && _current != null && anchor != null)
//         {
//             Vector3 target = anchor.position;
//             _current.position = Vector3.Lerp(_current.position, target, Time.deltaTime * attractionSpeed);
//
//             if (Vector3.Distance(_current.position, target) <= stopDistance)
//             {
//                 _attracting = false;
//                 _current.position = target;
//                 _current.rotation = anchor.rotation;
//
//                 if (_rb != null)
//                 {
//                     _rb.velocity = Vector3.zero;
//                     _rb.angularVelocity = Vector3.zero;
//                     _rb.useGravity = false;
//                     _rb.isKinematic = true;
//                 }
//
//                 var mainCol = _current.GetComponent<BoxCollider>();
//                 if (mainCol != null) mainCol.enabled = false;
//
//                 _current.SetParent(anchor, true);
//
//                 if (!_placedFired)
//                 {
//                     _placedFired = true;
//                     onHedronPlaced?.Invoke();
//
//                     var rbBox = _box.GetComponent<Rigidbody>();
//                     if (rbBox != null) rbBox.useGravity = false;
//
//                     _box.transform.SetParent(transform, true);
//
//                     if (_anim != null && !string.IsNullOrEmpty(closeTrigger))
//                         _anim.SetTrigger(closeTrigger);
//
//                     if (_button != null)
//                         _button.SetText(_button.E, "�Deseas extraer el Hedron?");
//
//                     if (_rightMat != null && _leftMat != null)
//                         StartCoroutine(GlowPulseOnce());
//                     if(panel != null)
//                     {
//                         ActivatePanel();
//                     }
//                    
//                 }
//             }
//         }
//     }
//
//     void OnTriggerEnter(Collider other)
//     {
//         if (_current != null) return;
//         if (!enabled || anchor == null) return;
//         if (((1 << other.gameObject.layer) & acceptMask) == 0) return;
//         if (!HasPhysicsBox(other.gameObject)) return;
//
//         StartCoroutine(ShrinkOverTime(other.gameObject.transform, new Vector3(0.5f, 0.5f, 0.5f), 1.2f));
//         _current = other.transform;
//         _rb = other.attachedRigidbody;
//
//         if (_rb != null)
//         {
//             _rb.velocity = Vector3.zero;
//             _rb.angularVelocity = Vector3.zero;
//             _rb.useGravity = false;
//             _rb.isKinematic = false;
//         }
//
//         _attracting = true;
//     }
//
//     public IEnumerator ShrinkOverTime(Transform target, Vector3 finalScale, float duration)
//     {
//         if (target == null) yield break;
//
//         Vector3 initialScale = target.localScale;
//         float elapsed = 0f;
//
//         while (elapsed < duration)
//         {
//             elapsed += Time.deltaTime;
//             float t = Mathf.Clamp01(elapsed / duration);
//             float smooth = Mathf.SmoothStep(0, 1, t);
//             target.localScale = Vector3.LerpUnclamped(initialScale, finalScale, smooth);
//             yield return null;
//         }
//
//         target.localScale = finalScale;
//     }
//
//     void OnTriggerExit(Collider other)
//     {
//         if (other.transform == _current && _attracting) ClearState();
//     }
//
//     void ClearState()
//     {
//         _current = null;
//         _rb = null;
//         _attracting = false;
//         _placedFired = false;
//         _box = null;
//         _isEjecting = false;
//         if (_button != null) _button.SetText(_button.E, "");
//     }
//
//     bool HasPhysicsBox(GameObject go)
//     {
//         PhysicsBox box = go.GetComponent<PhysicsBox>();
//         if (box != null) { _box = box; return true; }
//         _box = null;
//         return false;
//     }
//
//     // === ORDEN EXIGIDO: 1) Anim OPEN  2) esperar 1.5s  3) posicionar en 'pos' ===
//     public void OpenAndEject()
//     {
//         if (_isEjecting) return;
//         if (!HasOccupant || _box == null) return;
//         if (Time.time - _lastEjectTime < ejectCooldown) return;
//
//         _isEjecting = true;
//         _lastEjectTime = Time.time;
//
//         if (_anim != null && !string.IsNullOrEmpty(openTrigger))
//             _anim.SetTrigger(openTrigger);
//
//         if (pos != null)
//         {
//             StartCoroutine(EjectToPosAfterDelay(posDelaySeconds));
//         }
//         else
//         {
//             // si no hay 'pos', usar flujo cl�sico (sin nudge fuerte ni fuerzas)
//             StartCoroutine(EjectAfterDelay(openToEjectDelay));
//         }
//     }
//
//     public void EjectNow()
//     {
//         if (_isEjecting) return;
//         if (!HasOccupant || _box == null) return;
//         _isEjecting = true;
//         _lastEjectTime = Time.time;
//
//         if (pos != null)
//             StartCoroutine(EjectToPosAfterDelay(0f)); // sin esperar anim si llam�s EjectNow manual
//         else
//             StartCoroutine(EjectAfterDelay(0f));
//     }
//
//     IEnumerator EjectToPosAfterDelay(float delay)
//     {
//         if (!HasOccupant || _box == null) { _isEjecting = false; yield break; }
//         if (delay > 0f) yield return new WaitForSeconds(delay);
//
//         if (_rb == null) _rb = _box.GetComponent<Rigidbody>();
//         Transform t = _current != null ? _current : _box.transform;
//         Rigidbody rb = _rb;
//         if (t == null || rb == null) { _isEjecting = false; yield break; }
//
//         // Desanclar
//         t.SetParent(null, true);
//         if (reenableColliderOnEject) EnableAllColliders(t, true);
//
//         // Preparar f�sico para teletransporte limpio
//         rb.isKinematic = true;
//         rb.useGravity = false;
//         rb.velocity = Vector3.zero;
//         rb.angularVelocity = Vector3.zero;
//
//         // Mover a 'pos' (posici�n y rotaci�n)
//         t.position = pos.position;
//         t.rotation = pos.rotation;
//
//         // (Opcional) restaurar escala a 1 suavemente si ven�a reducido
//         if (scaleUpSeconds > 0.01f)
//             yield return ScaleTo(t, Vector3.one, scaleUpSeconds);
//         else
//             t.localScale = Vector3.one;
//
//         // Reactivar f�sica
//         rb.isKinematic = false;
//         rb.useGravity = true;
//         rb.WakeUp();
//
//         if (ejectTorque > 0f)
//         {
//             Vector3 rand = Random.onUnitSphere * ejectTorque;
//             rb.AddTorque(rand, ForceMode.VelocityChange);
//         }
//
//         onHedronRemoved?.Invoke();
//
//         if (_box.transform.parent == transform)
//             _box.transform.SetParent(null, true);
//
//         _isEjecting = false;
//         ClearState();
//     }
//
//     IEnumerator EjectAfterDelay(float delay)
//     {
//         if (!HasOccupant || _box == null) { _isEjecting = false; yield break; }
//         if (delay > 0f) yield return new WaitForSeconds(delay);
//
//         if (_rb == null) _rb = _box.GetComponent<Rigidbody>();
//         Transform t = _current != null ? _current : _box.transform;
//         Rigidbody rb = _rb;
//         if (t == null || rb == null) { _isEjecting = false; yield break; }
//
//         t.SetParent(null, true);
//         if (reenableColliderOnEject) EnableAllColliders(t, true);
//
//         // Peque�o empuje fuera del anchor si no hay 'pos'
//         Vector3 dir = (anchor != null ? anchor.forward : transform.forward).normalized;
//         NudgeOutFromAnchor(t, dir, Mathf.Max(0f, ejectNudgeDistance));
//
//         rb.isKinematic = true;
//         rb.useGravity = false;
//         rb.velocity = Vector3.zero;
//         rb.angularVelocity = Vector3.zero;
//
//         onHedronRemoved?.Invoke();
//
//         yield return ScaleTo(t, Vector3.one, Mathf.Max(0.01f, scaleUpSeconds));
//
//         rb.isKinematic = false;
//         rb.useGravity = true;
//         rb.WakeUp();
//
//         if (ejectTorque > 0f)
//         {
//             Vector3 rand = Random.onUnitSphere * ejectTorque;
//             rb.AddTorque(rand, ForceMode.VelocityChange);
//         }
//
//         if (_box.transform.parent == transform)
//             _box.transform.SetParent(null, true);
//
//         _isEjecting = false;
//         ClearState();
//     }
//
//     IEnumerator ScaleTo(Transform target, Vector3 finalScale, float duration)
//     {
//         if (target == null) yield break;
//         Vector3 start = target.localScale;
//         float elapsed = 0f;
//
//         while (elapsed < duration)
//         {
//             elapsed += Time.deltaTime;
//             float t = Mathf.Clamp01(elapsed / duration);
//             float s = Mathf.SmoothStep(0f, 1f, t);
//             target.localScale = Vector3.LerpUnclamped(start, finalScale, s);
//             yield return null;
//         }
//         target.localScale = finalScale;
//     }
//
//     void EnableAllColliders(Transform root, bool enabled)
//     {
//         var cols = root.GetComponentsInChildren<Collider>(true);
//         for (int i = 0; i < cols.Length; i++) cols[i].enabled = enabled;
//     }
//
//     void NudgeOutFromAnchor(Transform t, Vector3 dir, float distance)
//     {
//         if (anchor == null || distance <= 0f) return;
//         t.position = anchor.position + dir * distance;
//     }
//
//     IEnumerator GlowPulseOnce()
//     {
//         if (_rightMat != null) _rightMat.EnableKeyword("_EMISSION");
//         if (_leftMat != null) _leftMat.EnableKeyword("_EMISSION");
//
//         float f = Mathf.Max(0.0001f, brightnessFrequency);
//         float duration = Mathf.PI / f;
//         float t = 0f;
//
//         while (t < duration)
//         {
//             t += Time.deltaTime;
//             float phase = t * f;
//             float brightness = (Mathf.Cos(phase) + 1f) * 0.5f;
//
//             if (_rightMat != null)
//             {
//                 Color e = _baseEmissionRight + (glowColor * brightness * brightnessAmplitude);
//                 _rightMat.SetColor("_EmissionColor", e);
//             }
//             if (_leftMat != null)
//             {
//                 Color e = _baseEmissionLeft + (glowColor * brightness * brightnessAmplitude);
//                 _leftMat.SetColor("_EmissionColor", e);
//             }
//
//             yield return null;
//         }
//
//         if (_rightMat != null)
//         {
//             _rightMat.SetColor("_EmissionColor", _baseEmissionRight);
//             _rightMat.DisableKeyword("_EMISSION");
//         }
//         if (_leftMat != null)
//         {
//             _leftMat.SetColor("_EmissionColor", _baseEmissionLeft);
//             _leftMat.DisableKeyword("_EMISSION");
//         }
//     }
//
//     private void ActivatePanel()
//     {
//         StartCoroutine(waitShowPanel());
//
//     }
//     IEnumerator waitShowPanel()
//     {
//         yield return new WaitForSeconds(2f);
//         panel.SetActive(true);
//         E.gameObject.SetActive(true);
//         E.text = "¿Deseas extraer el hedro?";
//     }
//     public void Interact()
//     {
//         if (panel.gameObject.activeSelf)
//         {
//             E.gameObject.SetActive(false);
//             message.gameObject.SetActive(true);
//             OpenAndEject();
//             StartCoroutine(waitClosePanel());
//         }
//
//     }
//     IEnumerator waitClosePanel()
//     {
//         yield return new WaitForSeconds(1f);
//         message.gameObject.SetActive(false);
//         panel.gameObject.SetActive(false);
//     }
// }
using Puzzle_Elements.Button.Scripts;
using Puzzle_Elements.Hedron.Scripts;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Player.Scripts.Interactor;
using TMPro;

[RequireComponent(typeof(Collider))]
public class HedronContainerIn : MonoBehaviour, IInteractable
{
    [Header("=== CORE REFERENCES ===")]
    [SerializeField] private Button _button;
    public Transform anchor;
    [SerializeField] private Animator _anim;
    [SerializeField] private Transform pos;
    
    [Header("=== ANIMATION CONFIGURATION ===")]
    [SerializeField] private string openTrigger = "OPEN";
    [SerializeField] private string closeTrigger = "Close";
    
    [Header("=== TIMING SETTINGS ===")]
    [SerializeField] private float posDelaySeconds = 1.5f;
    [SerializeField] private float openToEjectDelay = 2f;
    [SerializeField] private float ejectCooldown = 0.15f;
    [SerializeField] private float scaleUpSeconds = 0.35f;
    [SerializeField] private float shrinkDuration = 1.2f;
    [SerializeField] private float panelShowDelay = 2f;
    [SerializeField] private float panelCloseDelay = 1f;
    
    [Header("=== MOVEMENT & PHYSICS ===")]
    [SerializeField] private float attractionSpeed = 5f;
    [SerializeField] private float stopDistance = 0.05f;
    [SerializeField] private float ejectNudgeDistance = 0.08f;
    [SerializeField] private float ejectTorque = 0f;
    [SerializeField] private bool reenableColliderOnEject = true;
    
    [Header("=== SCALING SETTINGS ===")]
    [SerializeField] private Vector3 targetScale = new Vector3(0.5f, 0.5f, 0.5f);
    
    [Header("=== VISUAL EFFECTS ===")]
    [SerializeField] private float brightnessFrequency = 2f;
    [SerializeField] private float brightnessAmplitude = 0.05f;
    [SerializeField] private Color glowColor = Color.white;
    public Renderer rightDoorRenderer;
    public Renderer leftDoorRenderer;
    
    [Header("=== UI CONFIGURATION ===")]
    public GameObject panel;
    public TextMeshProUGUI E;
    public TextMeshProUGUI message;
    [SerializeField] private string extractionMessage = "¿Deseas extraer el hedro?";
    [SerializeField] private string buttonExtractionText = "¿Deseas extraer el Hedron?";
    
    [Header("=== FILTER SETTINGS ===")]
    public LayerMask acceptMask = ~0;
    
    [Header("=== EVENTS ===")]
    public UnityEvent onHedronPlaced;
    public UnityEvent onHedronRemoved;

    // Private variables
    private PhysicsBox _box = null;
    private Material _rightMat, _leftMat;
    private Color _baseEmissionRight, _baseEmissionLeft;
    private Transform _current;
    private Rigidbody _rb;
    private bool _attracting;
    private bool _placedFired;
    private bool _isEjecting;
    private float _lastEjectTime;

    // Public properties for external access
    public bool HasOccupant => _current != null;
    public bool IsAttracting => _attracting;
    public bool IsEjecting => _isEjecting;
    public PhysicsBox CurrentBox => _box;
    public Transform CurrentHedron => _current;
    public Vector3 TargetScale { get => targetScale; set => targetScale = value; }
    public float PanelShowDelay { get => panelShowDelay; set => panelShowDelay = value; }

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (rightDoorRenderer != null)
        {
            _rightMat = Instantiate(rightDoorRenderer.sharedMaterial);
            rightDoorRenderer.material = _rightMat;
            _baseEmissionRight = _rightMat.GetColor("_EmissionColor");
        }
        if (leftDoorRenderer != null)
        {
            _leftMat = Instantiate(leftDoorRenderer.sharedMaterial);
            leftDoorRenderer.material = _leftMat;
            _baseEmissionLeft = _leftMat.GetColor("_EmissionColor");
        }
    }

    void Start()
    {
        if (_button != null) _button.SetText(_button.E, "");
        if (panel != null) panel.gameObject.SetActive(false);
    }

    void Update()
    {
        Atracction();
    }

    void Atracction()
    {
        if (_box == null) return;

        if (_attracting && _current != null && anchor != null)
        {
            Vector3 target = anchor.position;
            _current.position = Vector3.Lerp(_current.position, target, Time.deltaTime * attractionSpeed);

            if (Vector3.Distance(_current.position, target) <= stopDistance)
            {
                _attracting = false;
                _current.position = target;
                _current.rotation = anchor.rotation;

                if (_rb != null)
                {
                    _rb.velocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                    _rb.useGravity = false;
                    _rb.isKinematic = true;
                }

                var mainCol = _current.GetComponent<BoxCollider>();
                if (mainCol != null) mainCol.enabled = false;

                _current.SetParent(anchor, true);

                if (!_placedFired)
                {
                    _placedFired = true;
                    onHedronPlaced?.Invoke();

                    var rbBox = _box.GetComponent<Rigidbody>();
                    if (rbBox != null) rbBox.useGravity = false;

                    _box.transform.SetParent(transform, true);
                    _box.gameObject.SetActive(false);

                    if (_anim != null && !string.IsNullOrEmpty(closeTrigger))
                        _anim.SetTrigger(closeTrigger);

                    if (_button != null)
                        _button.SetText(_button.E, buttonExtractionText);

                    if (_rightMat != null && _leftMat != null)
                        StartCoroutine(GlowPulseOnce());
                    
                    if(panel != null)
                    {
                        ActivatePanel();
                    }
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_current != null) return;
        if (!enabled || anchor == null) return;
        if (((1 << other.gameObject.layer) & acceptMask) == 0) return;
        if (!HasPhysicsBox(other.gameObject)) return;

        StartCoroutine(ShrinkOverTime(other.gameObject.transform, targetScale, shrinkDuration));
        _current = other.transform;
        _rb = other.attachedRigidbody;

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.useGravity = false;
            _rb.isKinematic = false;
        }

        _attracting = true;
    }

    public IEnumerator ShrinkOverTime(Transform target, Vector3 finalScale, float duration)
    {
        if (target == null) yield break;

        Vector3 initialScale = target.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = Mathf.SmoothStep(0, 1, t);
            target.localScale = Vector3.LerpUnclamped(initialScale, finalScale, smooth);
            yield return null;
        }

        target.localScale = finalScale;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.transform == _current && _attracting) ClearState();
    }

    void ClearState()
    {
        _current = null;
        _rb = null;
        _attracting = false;
        _placedFired = false;
        _box = null;
        _isEjecting = false;
        if (_button != null) _button.SetText(_button.E, "");
    }

    bool HasPhysicsBox(GameObject go)
    {
        PhysicsBox box = go.GetComponent<PhysicsBox>();
        if (box != null) { _box = box; return true; }
        _box = null;
        return false;
    }

    public void OpenAndEject()
    {
        if (_isEjecting) return;
        if (!HasOccupant || _box == null) return;
        if (Time.time - _lastEjectTime < ejectCooldown) return;

        _isEjecting = true;
        _lastEjectTime = Time.time;

        if (_anim != null && !string.IsNullOrEmpty(openTrigger))
            _anim.SetTrigger(openTrigger);

        if (pos != null)
        {
            StartCoroutine(EjectToPosAfterDelay(posDelaySeconds));
        }
        else
        {
            StartCoroutine(EjectAfterDelay(openToEjectDelay));
        }
    }

    public void EjectNow()
    {
        if (_isEjecting) return;
        if (!HasOccupant || _box == null) return;
        _isEjecting = true;
        _lastEjectTime = Time.time;

        if (pos != null)
            StartCoroutine(EjectToPosAfterDelay(0f));
        else
            StartCoroutine(EjectAfterDelay(0f));
    }

    // Public method to force ejection
    public void ForceEject()
    {
        EjectNow();
    }

    // Public method to clear container manually
    public void ClearContainer()
    {
        ClearState();
    }

    // Public method to check if object can be accepted
    public bool CanAcceptObject(GameObject obj)
    {
        if (_current != null || !enabled || anchor == null) return false;
        if (((1 << obj.layer) & acceptMask) == 0) return false;
        return HasPhysicsBox(obj);
    }

    IEnumerator EjectToPosAfterDelay(float delay)
    {
        _box.gameObject.SetActive(true);
        if (!HasOccupant || _box == null) { _isEjecting = false; yield break; }
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (_rb == null) _rb = _box.GetComponent<Rigidbody>();
        Transform t = _current != null ? _current : _box.transform;
        Rigidbody rb = _rb;
        if (t == null || rb == null) { _isEjecting = false; yield break; }

        t.SetParent(null, true);
        if (reenableColliderOnEject) EnableAllColliders(t, true);

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        t.position = pos.position;
        t.rotation = pos.rotation;

        if (scaleUpSeconds > 0.01f)
            yield return ScaleTo(t, Vector3.one, scaleUpSeconds);
        else
            t.localScale = Vector3.one;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.WakeUp();

        if (ejectTorque > 0f)
        {
            Vector3 rand = Random.onUnitSphere * ejectTorque;
            rb.AddTorque(rand, ForceMode.VelocityChange);
        }

        onHedronRemoved?.Invoke();

        if (_box.transform.parent == transform)
            _box.transform.SetParent(null, true);

        _isEjecting = false;
        ClearState();
    }

    IEnumerator EjectAfterDelay(float delay)
    {
        _box.gameObject.SetActive(true);
        if (!HasOccupant || _box == null) { _isEjecting = false; yield break; }
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (_rb == null) _rb = _box.GetComponent<Rigidbody>();
        Transform t = _current != null ? _current : _box.transform;
        Rigidbody rb = _rb;
        if (t == null || rb == null) { _isEjecting = false; yield break; }

        t.SetParent(null, true);
        if (reenableColliderOnEject) EnableAllColliders(t, true);

        Vector3 dir = (anchor != null ? anchor.forward : transform.forward).normalized;
        NudgeOutFromAnchor(t, dir, Mathf.Max(0f, ejectNudgeDistance));

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        onHedronRemoved?.Invoke();

        yield return ScaleTo(t, Vector3.one, Mathf.Max(0.01f, scaleUpSeconds));

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.WakeUp();

        if (ejectTorque > 0f)
        {
            Vector3 rand = Random.onUnitSphere * ejectTorque;
            rb.AddTorque(rand, ForceMode.VelocityChange);
        }

        if (_box.transform.parent == transform)
            _box.transform.SetParent(null, true);

        _isEjecting = false;
        ClearState();
    }

    IEnumerator ScaleTo(Transform target, Vector3 finalScale, float duration)
    {
        if (target == null) yield break;
        Vector3 start = target.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = Mathf.SmoothStep(0f, 1f, t);
            target.localScale = Vector3.LerpUnclamped(start, finalScale, s);
            yield return null;
        }
        target.localScale = finalScale;
    }

    void EnableAllColliders(Transform root, bool enabled)
    {
        var cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = enabled;
    }

    void NudgeOutFromAnchor(Transform t, Vector3 dir, float distance)
    {
        if (anchor == null || distance <= 0f) return;
        t.position = anchor.position + dir * distance;
    }

    IEnumerator GlowPulseOnce()
    {
        if (_rightMat != null) _rightMat.EnableKeyword("_EMISSION");
        if (_leftMat != null) _leftMat.EnableKeyword("_EMISSION");

        float f = Mathf.Max(0.0001f, brightnessFrequency);
        float duration = Mathf.PI / f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float phase = t * f;
            float brightness = (Mathf.Cos(phase) + 1f) * 0.5f;

            if (_rightMat != null)
            {
                Color e = _baseEmissionRight + (glowColor * brightness * brightnessAmplitude);
                _rightMat.SetColor("_EmissionColor", e);
            }
            if (_leftMat != null)
            {
                Color e = _baseEmissionLeft + (glowColor * brightness * brightnessAmplitude);
                _leftMat.SetColor("_EmissionColor", e);
            }

            yield return null;
        }

        if (_rightMat != null)
        {
            _rightMat.SetColor("_EmissionColor", _baseEmissionRight);
            _rightMat.DisableKeyword("_EMISSION");
        }
        if (_leftMat != null)
        {
            _leftMat.SetColor("_EmissionColor", _baseEmissionLeft);
            _leftMat.DisableKeyword("_EMISSION");
        }
    }

    private void ActivatePanel()
    {
        StartCoroutine(WaitShowPanel());
    }

    IEnumerator WaitShowPanel()
    {
        yield return new WaitForSeconds(panelShowDelay);
        panel.SetActive(true);
        E.gameObject.SetActive(true);
        E.text = extractionMessage;
    }

    public void Interact()
    {
        if (panel.gameObject.activeSelf)
        {
            E.gameObject.SetActive(false);
            message.gameObject.SetActive(true);
            OpenAndEject();
            StartCoroutine(WaitClosePanel()); 
            
        }
    }

    IEnumerator WaitClosePanel()
    {
        yield return new WaitForSeconds(panelCloseDelay);
        message.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);
    }
}