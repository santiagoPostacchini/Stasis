using Puzzle_Elements.Hedron.Scripts;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class HedronContainerIn : MonoBehaviour
{
    [Header("Anchor (centro de acople)")]
    public Transform anchor;
    [SerializeField]private Animator _anim;
    private PhysicsBox _box = null;
    [Header("Evento")]
    public UnityEvent onHedronPlaced;   // Se dispara cuando el PhysicsBox queda acoplado y quieto

    [Header("Atracción")]
    public float attractionSpeed = 5f;
    public float stopDistance = 0.05f;

    [Header("Filtro")]
    public LayerMask acceptMask = ~0;

    [Header("Brillo (titila una vez al colocarse)")]
    public Renderer rightDoorRenderer;
    public Renderer leftDoorRenderer;
    public float brightnessFrequency = 2f;
    public float brightnessAmplitude = 0.05f;
    public Color glowColor = Color.white;

    // ---- Internos brillo ----
    Material _rightMat, _leftMat;
    Color _baseEmissionRight, _baseEmissionLeft;

    // ---- Internos IN ----
    Transform _current;
    Rigidbody _rb;
    bool _attracting;
    bool _placedFired; // para no disparar dos veces si re-entra por algún motivo

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

    void Update()
    {
        if (_attracting && _current != null && anchor != null)
        {
            Vector3 target = anchor.position;
            _current.position = Vector3.Lerp(_current.position, target, Time.deltaTime * attractionSpeed);

            if (Vector3.Distance(_current.position, target) <= stopDistance)
            {
                // Snap y quieto
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

                // Disparar una sola vez
                if (!_placedFired)
                {
                    _placedFired = true;
                    onHedronPlaced?.Invoke();
                    _box.GetComponent<Rigidbody>().useGravity = false;
                    _box.transform.SetParent(transform, true);
                    _anim.SetTrigger("Close");
                    if(_rightMat != null && _leftMat != null)
                    {
                        StartCoroutine(GlowPulseOnce());
                    }
                    
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_current != null) return;                 // ya hay ocupante
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
            _rb.isKinematic = false; // dinámico mientras atrae
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

            // Interpolación suave (EaseInOut)
            float smooth = Mathf.SmoothStep(0, 1, t);
            target.localScale = Vector3.LerpUnclamped(initialScale, finalScale, smooth);

            yield return null;
        }

        target.localScale = finalScale;
    }

    void OnTriggerExit(Collider other)
    {
        // Si se fue antes de ser acoplado, limpiar estado
        if (other.transform == _current && _attracting)
        {
            ClearState();
        }
    }

    void ClearState()
    {
        _current = null;
        _rb = null;
        _attracting = false;
        _placedFired = false;
    }

    bool HasPhysicsBox(GameObject go)
    {
        PhysicsBox box = go.GetComponent<PhysicsBox>();
        if (box != null)
        {
            _box = box;
            return true;
        } 
        return false;
    } 

    // ---- Pulso de brillo único ----
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
            float brightness = (Mathf.Cos(phase) + 1f) * 0.5f; // 0..1

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

        // Reset
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
