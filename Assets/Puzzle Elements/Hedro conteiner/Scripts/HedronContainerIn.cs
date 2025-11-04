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
    [SerializeField] private PhysicsBox _box = null;

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
    [SerializeField] private float shrinkDuration = 1.2f; // (queda para otros usos)
    [SerializeField] private float panelShowDelay = 2f;
    [SerializeField] private float panelCloseDelay = 1f;

    [Header("=== MOVEMENT & PHYSICS ===")]
    [SerializeField] private float attractionSpeed = 5f; // (legacy - ya no se usa en Update)
    [SerializeField] private float stopDistance = 0.05f;
    [SerializeField] private float ejectNudgeDistance = 0.08f;
    [SerializeField] private float ejectTorque = 0f;
    [SerializeField] private bool reenableColliderOnEject = true;

    [Header("=== SCALING SETTINGS ===")]
    [SerializeField] private Vector3 targetScale = new Vector3(0.5f, 0.5f, 0.5f);

    [Header("=== ATTRACTION SMOOTHING ===")]
    [SerializeField] private bool useSmoothAttract = true;
    [SerializeField] private float attractMaxSeconds = 1.2f;
    [SerializeField] private AnimationCurve posCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve rotCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Dock cuando la escala actual esté dentro de este margen respecto a targetScale (ej. 1.05 = ±5%).")]
    [SerializeField] private float dockScaleThreshold = 1.05f;
    [SerializeField] private bool alignRotationOnDock = true;

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
    private Material _rightMat, _leftMat;
    private Color _baseEmissionRight, _baseEmissionLeft;
    private Transform _current;
    private Rigidbody _rb;
    private bool _attracting;
    private bool _placedFired;
    private bool _isEjecting;
    private float _lastEjectTime;

    // Public properties
    public bool HasOccupant => _current != null;
    public bool IsAttracting => _attracting;
    public bool IsEjecting => _isEjecting;
    public PhysicsBox CurrentBox => _box;
    public Transform CurrentHedron => _current;
    public Vector3 TargetScale { get => targetScale; set => targetScale = value; }
    public float PanelShowDelay { get => panelShowDelay; set => panelShowDelay = value; }

    private bool canAtracction = true;

    // Nueva referencia al coroutine de atracción
    private Coroutine _attractRoutine;

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
        // Se elimina el Lerp brusco en Update; ahora se usa AttractAndDock coroutine.
    }

    // ==== NUEVO FLUJO DE ATRACCIÓN ====
    void OnTriggerEnter(Collider other)
    {
        if (_current != null) return;
        if (!enabled || anchor == null) return;
        if (!canAtracction) return;
        if (((1 << other.gameObject.layer) & acceptMask) == 0) return;
        if (!HasPhysicsBox(other.gameObject)) return; // setea _box si existe

        _current = other.transform;
        _rb = other.attachedRigidbody;

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.useGravity = false;
            _rb.isKinematic = true; // evita saltos de física durante atracción
        }

        // Cancelar cualquier atracción previa
        if (_attractRoutine != null) StopCoroutine(_attractRoutine);

        if (useSmoothAttract)
            _attractRoutine = StartCoroutine(AttractAndDock(_current, _rb, _box));
        else
        {
            // Fallback: legacy shrink + atracción inmediata (no recomendado)
            StartCoroutine(ShrinkOverTime(other.gameObject.transform, targetScale, shrinkDuration));
            _attracting = true; // si quisieras mantener algún UI de “atrayendo…”
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.transform == _current && _attracting)
        {
            // Si se sale mientras atrae, limpiamos el estado y paramos la rutina
            if (_attractRoutine != null) StopCoroutine(_attractRoutine);
            _attractRoutine = null;
            ClearState();
        }
    }

    private IEnumerator AttractAndDock(Transform t, Rigidbody rb, PhysicsBox box)
    {
        if (t == null || anchor == null) yield break;

        _attracting = true;

        // Datos iniciales
        Vector3 startPos = t.position;
        Quaternion startRot = t.rotation;
        Vector3 startScale = t.localScale;

        Vector3 endPos = anchor.position;
        Quaternion endRot = anchor.rotation;
        Vector3 endScale = targetScale;

        float dur = Mathf.Max(0.05f, attractMaxSeconds);
        float elapsed = 0f;

        // Seguridad física durante atracción
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t01 = Mathf.Clamp01(elapsed / dur);

            float p = posCurve.Evaluate(t01);
            float r = rotCurve.Evaluate(t01);
            float s = scaleCurve.Evaluate(t01);

            // Interpolaciones suaves
            t.position = Vector3.LerpUnclamped(startPos, endPos, p);
            t.rotation = Quaternion.SlerpUnclamped(startRot, endRot, r);
            t.localScale = Vector3.LerpUnclamped(startScale, endScale, s);

            // Criterios de “dock”
            bool closeEnough = Vector3.Distance(t.position, endPos) <= stopDistance;

            // margen relativo respecto a targetScale
            bool smallEnough =
                Mathf.Abs(t.localScale.x - endScale.x) <= endScale.x * (dockScaleThreshold - 1f) &&
                Mathf.Abs(t.localScale.y - endScale.y) <= endScale.y * (dockScaleThreshold - 1f) &&
                Mathf.Abs(t.localScale.z - endScale.z) <= endScale.z * (dockScaleThreshold - 1f);

            if (smallEnough && closeEnough)
                break;

            yield return null;
        }

        // Snap final
        t.position = endPos;
        if (alignRotationOnDock) t.rotation = endRot;
        t.localScale = endScale;

        // Encastre / eventos (tu flujo original)
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        var mainCol = t.GetComponent<BoxCollider>();
        if (mainCol != null) mainCol.enabled = false;

        t.SetParent(anchor, true);

        if (!_placedFired)
        {
            _placedFired = true;
            onHedronPlaced?.Invoke();

            var rbBox = _box != null ? _box.GetComponent<Rigidbody>() : null;
            if (rbBox != null) rbBox.useGravity = false;

            if (_box != null)
            {
                _box.transform.SetParent(transform, true);
                _box.gameObject.SetActive(false);
            }

            if (_anim != null && !string.IsNullOrEmpty(closeTrigger))
                _anim.SetTrigger(closeTrigger);

            if (_button != null)
                _button.SetText(_button.E, buttonExtractionText);

            if (_rightMat != null && _leftMat != null)
                StartCoroutine(GlowPulseOnce());

            if (panel != null)
                ActivatePanel();
        }

        _attracting = false;
        _attractRoutine = null;
    }

    // ==== UTILIDADES EXISTENTES / EYECCIÓN ====

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

    IEnumerator CantAtracction()
    {
        canAtracction = false;
        yield return new WaitForSeconds(3f);
        canAtracction = true;
    }

    public IEnumerator MoveForward6Units(Transform target)
    {
        if (target == null) yield break;
        _box.transform.position = pos.transform.position;
        _box.transform.rotation = pos.transform.rotation;

        Vector3 startPos = target.position;
        Vector3 targetPos = startPos + target.forward * 2f;

        BoxCollider _boxHedro = target.GetComponent<BoxCollider>();
        while (Vector3.Distance(target.position, targetPos) > 0.01f)
        {
            target.position = Vector3.MoveTowards(target.position, targetPos, 6 * Time.deltaTime);
            if(_boxHedro != null)
            {
                if (_boxHedro.enabled)
                {
                    _boxHedro.enabled = false;
                }
            }
            yield return null;
        }
        if(_boxHedro != null)
        {
            _boxHedro.enabled = true;
        }
      
        target.position = targetPos; // asegurar posición final exacta
    }

    public void OpenAndEject()
    {
        if (_isEjecting) return;
        if (!HasOccupant || _box == null) return;
        if (Time.time - _lastEjectTime < ejectCooldown) return;

        StartCoroutine(CantAtracction());
        _isEjecting = true;
        _lastEjectTime = Time.time;

        if (_anim != null && !string.IsNullOrEmpty(openTrigger))
            _anim.SetTrigger(openTrigger);

        if (pos != null)
            StartCoroutine(EjectToPosAfterDelay(posDelaySeconds));
        else
            StartCoroutine(EjectAfterDelay(openToEjectDelay));
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
    public void ForceEject() => EjectNow();

    // Public method to clear container manually
    public void ClearContainer()
    {
        if (_attractRoutine != null) StopCoroutine(_attractRoutine);
        _attractRoutine = null;
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
        if (_box != null) _box.enabled = false;
        _box.transform.SetParent(null, true);

        if (!HasOccupant || _box == null) { _isEjecting = false; yield break; }

        _box.transform.position = pos.transform.position;
        _box.transform.rotation = pos.transform.rotation;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (_rb == null) _rb = _box.GetComponent<Rigidbody>();
        Transform t = _current != null ? _current : _box.transform;
        Rigidbody rb = _rb;
        if (t == null || rb == null) { _isEjecting = false; yield break; }

        t.SetParent(null, true);
        if (reenableColliderOnEject) EnableAllColliders(t, true);

        StartCoroutine(MoveForward6Units(_box.transform));

        if (scaleUpSeconds > 0.01f)
            yield return ScaleTo(t, Vector3.one, scaleUpSeconds);
        else
            t.localScale = Vector3.one;

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
        if (panel != null) panel.SetActive(true);
        if (E != null)
        {
            E.gameObject.SetActive(true);
            E.text = extractionMessage;
        }
    }

    public void Interact()
    {
        if (panel != null && panel.gameObject.activeSelf)
        {
            if (E != null) E.gameObject.SetActive(false);
            if (message != null) message.gameObject.SetActive(true);
            OpenAndEject();
            StartCoroutine(WaitClosePanel());
        }
    }

    IEnumerator WaitClosePanel()
    {
        yield return new WaitForSeconds(panelCloseDelay);
        if (message != null) message.gameObject.SetActive(false);
        if (panel != null) panel.gameObject.SetActive(false);
    }
}
