using Puzzle_Elements.Button.Scripts;
using Puzzle_Elements.Hedron.Scripts;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class HedronContainerIn : MonoBehaviour
{
    [SerializeField] private Button _button;

    public Transform anchor;
    [SerializeField] private Animator _anim;

    [SerializeField] private string openTrigger = "OPEN";
    [SerializeField] private string closeTrigger = "Close";

    [SerializeField] private Transform pos;              // destino para la expulsión
    [SerializeField] private float posDelaySeconds = 1.5f; // espera exacta antes de mover a 'pos'

    private PhysicsBox _box = null;

    public UnityEvent onHedronPlaced;
    public UnityEvent onHedronRemoved;

    public float attractionSpeed = 5f;
    public float stopDistance = 0.05f;

    public LayerMask acceptMask = ~0;

    public Renderer rightDoorRenderer;
    public Renderer leftDoorRenderer;
    public float brightnessFrequency = 2f;
    public float brightnessAmplitude = 0.05f;
    public Color glowColor = Color.white;

    public float openToEjectDelay = 2f;
    public bool reenableColliderOnEject = true;
    public float ejectNudgeDistance = 0.08f;
    public float scaleUpSeconds = 0.35f;
    public float ejectTorque = 0f;

    public float ejectCooldown = 0.15f;

    Material _rightMat, _leftMat;
    Color _baseEmissionRight, _baseEmissionLeft;

    Transform _current;
    Rigidbody _rb;
    bool _attracting;
    bool _placedFired;
    bool _isEjecting;
    float _lastEjectTime;

    public bool HasOccupant => _current != null;

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

                    if (_anim != null && !string.IsNullOrEmpty(closeTrigger))
                        _anim.SetTrigger(closeTrigger);

                    if (_button != null)
                        _button.SetText(_button.E, "¿Deseas extraer el Hedron?");

                    if (_rightMat != null && _leftMat != null)
                        StartCoroutine(GlowPulseOnce());
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

        StartCoroutine(ShrinkOverTime(other.gameObject.transform, new Vector3(0.5f, 0.5f, 0.5f), 1.2f));
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

    // === ORDEN EXIGIDO: 1) Anim OPEN  2) esperar 1.5s  3) posicionar en 'pos' ===
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
            // si no hay 'pos', usar flujo clásico (sin nudge fuerte ni fuerzas)
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
            StartCoroutine(EjectToPosAfterDelay(0f)); // sin esperar anim si llamás EjectNow manual
        else
            StartCoroutine(EjectAfterDelay(0f));
    }

    IEnumerator EjectToPosAfterDelay(float delay)
    {
        if (!HasOccupant || _box == null) { _isEjecting = false; yield break; }
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (_rb == null) _rb = _box.GetComponent<Rigidbody>();
        Transform t = _current != null ? _current : _box.transform;
        Rigidbody rb = _rb;
        if (t == null || rb == null) { _isEjecting = false; yield break; }

        // Desanclar
        t.SetParent(null, true);
        if (reenableColliderOnEject) EnableAllColliders(t, true);

        // Preparar físico para teletransporte limpio
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Mover a 'pos' (posición y rotación)
        t.position = pos.position;
        t.rotation = pos.rotation;

        // (Opcional) restaurar escala a 1 suavemente si venía reducido
        if (scaleUpSeconds > 0.01f)
            yield return ScaleTo(t, Vector3.one, scaleUpSeconds);
        else
            t.localScale = Vector3.one;

        // Reactivar física
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
        if (!HasOccupant || _box == null) { _isEjecting = false; yield break; }
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (_rb == null) _rb = _box.GetComponent<Rigidbody>();
        Transform t = _current != null ? _current : _box.transform;
        Rigidbody rb = _rb;
        if (t == null || rb == null) { _isEjecting = false; yield break; }

        t.SetParent(null, true);
        if (reenableColliderOnEject) EnableAllColliders(t, true);

        // Pequeño empuje fuera del anchor si no hay 'pos'
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
}
