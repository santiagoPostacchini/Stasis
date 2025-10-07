using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ElevatorStickyZone : MonoBehaviour
{
    [Tooltip("Rigidbody de la plataforma (si no se asigna, se toma automáticamente).")]
    public Rigidbody platformRb;

    [Tooltip("Solo para depuración: muestra el modelo del jugador detectado.")]
    public Model debugModel;

    [Header("Box Detect (en vez de esfera)")]
    [Tooltip("Altura del volumen de detección sobre la plataforma.")]
    public float castDistance = 0.6f;

    [Tooltip("Distancia desde el top de la plataforma hasta donde empieza el volumen de detección.")]
    public float castStartOffset = 0.02f;

    [Tooltip("Margen adicional en los ejes X y Z para el volumen de detección.")]
    public Vector2 castPaddingXZ = new Vector2(0.05f, 0.05f);

    [Tooltip("Máscara de capas que detectará el OverlapBox.")]
    public LayerMask castMask = ~0;

    [Tooltip("Si está activado, dibuja el volumen de detección en la escena.")]
    public bool drawGizmos = true;

    [Header("Follow Down")]
    [Tooltip("Cuánto se mezcla la velocidad vertical del jugador con la de la plataforma al bajar.")]
    [Range(0f, 1f)] public float amount = 1f;

    [Tooltip("Tiempo de suavizado para el movimiento vertical de la plataforma.")]
    public float downSmoothingSeconds = 0.0f;

    [Tooltip("Zona muerta descendente: en módulo menor a esto no se considera descenso.")]
    public float downDeadzone = 0.0005f;

    [Header("Follow Up")]
    [Tooltip("Zona muerta ascendente: mayor a esto se considera que la plataforma está subiendo.")]
    public float upDeadzone = 0.0005f;

    [Header("Contacto firme")]
    [Tooltip("Distancia mínima entre los pies del jugador y la superficie de la plataforma.")]
    public float contactSkin = 0.002f;

    [Tooltip("Desplazamiento máximo permitido para ajustar al contacto.")]
    public float maxSnapStep = 0.25f;

    [Tooltip("Pequeño sesgo descendente para mantener contacto constante.")]
    public float contactBiasDown = 0.0015f;

    [Header("Apex")]
    [Tooltip("Umbral de velocidad vertical para considerar que llegó al punto más alto.")]
    public float apexThreshold = 0.05f;

    [Tooltip("Offset adicional para ajustar la altura entre pies y plataforma.")]
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

        bool goingDown = _smoothedPlatVy < -downDeadzone;
        bool goingUp = _smoothedPlatVy > upDeadzone;

        if (detected)
        {
            if (_playerRb != hitRb)
            {
                _playerRb = hitRb;
                _playerCol = hitCol != null ? hitCol : hitRb.GetComponentInChildren<Collider>();
            }
            debugModel = hitModel;

            // APEX: al llegar arriba, un solo zero suave (opcional)
            if (!_apexZeroDone && _lastPlatVy > apexThreshold && Mathf.Abs(rawPlatVy) <= apexThreshold)
            {
                var v0 = _playerRb.velocity; v0.y = 0f; _playerRb.velocity = v0;
                _apexZeroDone = true;
            }
            if (rawPlatVy > apexThreshold) _apexZeroDone = false;

            if (goingDown)
            {
                // REGLA: descendiendo dentro del box
                _playerRb.useGravity = true;     // tu decisión anterior
                if (!_playerRb.isKinematic) _playerRb.isKinematic = true;

                // (opcional) acompaño velocidad vertical hacia abajo y mantengo contacto
                var v = _playerRb.velocity;
                float targetVy = _smoothedPlatVy;
                float blended = Mathf.Lerp(v.y, targetVy, Mathf.Clamp01(amount));
                if (blended > targetVy) blended = targetVy;
                if (blended > 0f) blended = 0f;
                v.y = blended;
                _playerRb.velocity = v;

                if (_playerCol != null && _col != null)
                {
                    float topY = _col.bounds.max.y;
                    float feetY = _playerCol.bounds.min.y;
                    float wantY = topY + contactSkin + valor;
                    float gap = feetY - wantY;

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
                if (_playerRb.isKinematic) _playerRb.isKinematic = false;
                _playerRb.useGravity = true;

                if (goingUp && _playerRb.isKinematic) _playerRb.isKinematic = false;
            }
        }
        else
        {
            if (_playerRb != null)
            {
                _playerRb.useGravity = true;
                if (_playerRb.isKinematic) _playerRb.isKinematic = false;
            }
            _playerRb = null;
            _playerCol = null;
            debugModel = null;
            _apexZeroDone = false;
        }

        _lastPlatY = platY;
        _lastPlatVy = rawPlatVy;
    }

    bool TryDetectPlayer(out Rigidbody rb, out Model m, out Collider playerCol)
    {
        rb = null; m = null; playerCol = null;
        if (_col == null) return false;

        Bounds b = _col.bounds;
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
