using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ElevatorStickyZone : MonoBehaviour
{
    public Rigidbody platformRb;
    public Model debugModel;

    [Header("Box Detect (en vez de esfera)")]
    public float castDistance = 0.6f;          // alto del volumen de detección sobre la plataforma
    public float castStartOffset = 0.02f;      // cuánto despegar el volumen desde el top de la plataforma
    public Vector2 castPaddingXZ = new Vector2(0.05f, 0.05f); // margen extra en X/Z
    public LayerMask castMask = ~0;
    public bool drawGizmos = true;

    [Header("Follow Down")]
    [Range(0f, 1f)] public float amount = 1f;
    public float downSmoothingSeconds = 0.0f;
    public float downDeadzone = 0.0005f;

    [Header("Contacto firme")]
    public float contactSkin = 0.002f;
    public float maxSnapStep = 0.25f;
    public float contactBiasDown = 0.0015f;

    [Header("Apex")]
    public float apexThreshold = 0.05f;

    // offset extra para ajustar la altura “pies–piso” a tu gusto
    public float valor;

    Rigidbody _playerRb;
    Collider _playerCol;
    Collider _col;
    float _lastPlatY;
    float _lastPlatVy;
    float _smoothedPlatVy;
    bool _apexZeroDone;

    void Awake()
    {
        if (platformRb == null) platformRb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        if (_col && _col.isTrigger) _col.isTrigger = false;

        _lastPlatY = platformRb ? platformRb.position.y : 0f;
        _lastPlatVy = 0f;
        _smoothedPlatVy = 0f;
        _apexZeroDone = false;
    }

    bool NearPlayer()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.player == null) return true;
        return Vector3.Distance(transform.position, gm.player.transform.position) < 12f;
    }

    void FixedUpdate()
    {
        if (platformRb == null) return;
        if (!NearPlayer()) return;

        bool detected = TryDetectPlayer(out Rigidbody hitRb, out Model hitModel, out Collider hitCol);

        float platY = platformRb.position.y;
        float rawPlatVy = (platY - _lastPlatY) / Time.fixedDeltaTime;

        float alpha = (downSmoothingSeconds <= 0f) ? 1f
            : Mathf.Clamp01(Time.fixedDeltaTime / (downSmoothingSeconds + Time.fixedDeltaTime));
        _smoothedPlatVy = Mathf.Lerp(_smoothedPlatVy, rawPlatVy, alpha);

        if (detected)
        {
            if (_playerRb != hitRb)
            {
                _playerRb = hitRb;
                _playerCol = hitCol != null ? hitCol : hitRb.GetComponentInChildren<Collider>();
            }
            debugModel = hitModel;

            // APEX: al llegar arriba, un solo zero suave
            if (!_apexZeroDone && _lastPlatVy > apexThreshold && Mathf.Abs(rawPlatVy) <= apexThreshold)
            {
                var v0 = _playerRb.velocity; v0.y = 0f; _playerRb.velocity = v0;
                _apexZeroDone = true;
            }
            if (rawPlatVy > apexThreshold) _apexZeroDone = false;

            // DESCENSO: aplicar de inmediato
            if (_smoothedPlatVy < -downDeadzone)
            {
                _playerRb.useGravity = false;

                // 1) Igualar velocidad vertical al menos a la de la plataforma
                var v = _playerRb.velocity;
                float targetVy = _smoothedPlatVy;
                float blended = Mathf.Lerp(v.y, targetVy, Mathf.Clamp01(amount));
                if (blended > targetVy) blended = targetVy; // nunca más lento que el piso
                if (blended > 0f) blended = 0f;       // no empujar hacia arriba
                v.y = blended;
                _playerRb.velocity = v;

                // 2) Snap para mantener pies tocando la plataforma
                if (_playerCol != null && _col != null)
                {
                    float topY = _col.bounds.max.y;
                    float feetY = _playerCol.bounds.min.y;
                    float wantY = topY + contactSkin + valor;     // usa +valor para ajustar
                    float gap = feetY - wantY;                 // >0: está por encima (flotando)

                    if (gap > 0f)
                    {
                        float down = (maxSnapStep <= 0f) ? (gap + contactBiasDown)
                                                          : Mathf.Min(gap + contactBiasDown, maxSnapStep);
                        Vector3 p = _playerRb.position;
                        _playerRb.MovePosition(new Vector3(p.x, p.y - down, p.z));

                        var vv = _playerRb.velocity;
                        if (vv.y > 0f) vv.y = 0f;
                        _playerRb.velocity = vv;
                    }
                }
            }
            else
            {
                // No baja: devolver control a la física del player
                _playerRb.useGravity = true;
            }
        }
        else
        {
            if (_playerRb != null) _playerRb.useGravity = true;
            _playerRb = null;
            _playerCol = null;
            debugModel = null;
            _apexZeroDone = false;
        }

        _lastPlatY = platY;
        _lastPlatVy = rawPlatVy;
    }

    // ====== Detección con CAJA (OverlapBox) ======
    bool TryDetectPlayer(out Rigidbody rb, out Model m, out Collider playerCol)
    {
        rb = null; m = null; playerCol = null;
        if (_col == null) return false;

        Bounds b = _col.bounds;

        // half extents del volumen: usa el tamaño de la plataforma + padding, y altura = castDistance
        Vector3 half = new Vector3(
            b.extents.x + castPaddingXZ.x,
            castDistance * 0.5f,
            b.extents.z + castPaddingXZ.y
        );

        // centro del volumen: justo arriba del top de la plataforma
        Vector3 center = new Vector3(
            b.center.x,
            b.max.y + castStartOffset + half.y,
            b.center.z
        );

        // detecta cualquier collider dentro del volumen
        var hits = Physics.OverlapBox(center, half, Quaternion.identity, castMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            var hrb = c.attachedRigidbody;
            if (hrb == null) continue;

            var hm = hrb.GetComponent<Model>();
            if (hm == null) continue;

            rb = hrb;
            m = hm;

            // mejor collider del player
            playerCol = hrb.GetComponent<Collider>() ?? hrb.GetComponentInChildren<Collider>() ?? c;
            return true;
        }
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        var col = GetComponent<Collider>();
        if (col == null) return;

        Bounds b = col.bounds;
        Vector3 half = new Vector3(
            b.extents.x + castPaddingXZ.x,
            castDistance * 0.5f,
            b.extents.z + castPaddingXZ.y
        );
        Vector3 center = new Vector3(
            b.center.x,
            b.max.y + castStartOffset + half.y,
            b.center.z
        );

        Gizmos.color = Color.cyan;
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, half * 2f);
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
