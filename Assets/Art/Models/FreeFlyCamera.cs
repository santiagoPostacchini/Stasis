using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Camera/Free Fly Camera")]
public class FreeFlyCamera : MonoBehaviour
{
    [Header("Mira")]
    public bool lockCursorOnStart = true;          // Bloquear cursor al iniciar
    public bool requireMouseButtonToLook = false;  // Si querés mirar solo al mantener un botón
    public int lookMouseButton = 1;                // 0=Izq, 1=Der, 2=Medio
    [Tooltip("Grados por píxel aprox. (se combina con dt)")]
    public float mouseSensitivity = 0.12f;
    [Range(0f, 1f)] public float lookSmoothing = 0.1f; // 0 = crudo, 1 = muy suave

    [Header("Movimiento")]
    public float moveSpeed = 6f;       // Velocidad base
    public float slowSpeed = 2f;       // LCtrl
    public float fastSpeed = 24f;      // LShift
    public float acceleration = 12f;   // Qué tan rápido alcanza la velocidad objetivo
    public bool useUnscaledTime = true; // Para moverte aunque Time.timeScale = 0

    [Header("Teclas")]
    public KeyCode boostKey = KeyCode.LeftShift;
    public KeyCode slowKey  = KeyCode.LeftControl;
    public KeyCode upKey    = KeyCode.E;        // Subir
    public KeyCode downKey  = KeyCode.Q;        // Bajar
    public KeyCode toggleCursorKey = KeyCode.F1;

    private float yaw, pitch;
    private Vector2 smoothedDelta;
    private Vector3 velocity;

    float Dt => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    void Start()
    {
        var e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;

        if (lockCursorOnStart) SetCursor(true);
    }

    void Update()
    {
        HandleCursorToggle();

        bool canLook = !requireMouseButtonToLook || Input.GetMouseButton(lookMouseButton);
        if (canLook && Cursor.lockState == CursorLockMode.Locked)
            Look();

        Move();
    }

    void HandleCursorToggle()
    {
        if (Input.GetKeyDown(toggleCursorKey))
            SetCursor(Cursor.lockState != CursorLockMode.Locked);
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursor(false);
    }

    void SetCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void Look()
    {
        Vector2 raw = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        // Suavizado exponencial continuo (independiente del framerate)
        float k = 1f - Mathf.Exp(-Dt / Mathf.Max(0.0001f, lookSmoothing));
        smoothedDelta = Vector2.Lerp(smoothedDelta, raw, k);

        yaw   += smoothedDelta.x * mouseSensitivity * 360f * Dt;
        pitch -= smoothedDelta.y * mouseSensitivity * 360f * Dt;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void Move()
    {
        // Rueda del mouse ajusta la velocidad base
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
            moveSpeed = Mathf.Max(0.1f, moveSpeed * (1f + scroll * 0.15f));

        float currentSpeed = moveSpeed;
        if (Input.GetKey(boostKey)) currentSpeed = fastSpeed;
        else if (Input.GetKey(slowKey)) currentSpeed = slowSpeed;

        // WASD + (E/Space) subir, (Q/LeftCtrl) bajar
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        float y = (Input.GetKey(upKey) || Input.GetKey(KeyCode.Space) ? 1f : 0f)
                - (Input.GetKey(downKey) || Input.GetKey(KeyCode.LeftControl) ? 1f : 0f);

        Vector3 input = Vector3.ClampMagnitude(new Vector3(x, y, z), 1f);
        Vector3 targetVel = transform.TransformDirection(input) * currentSpeed;

        // Aceleración/damping suaves
        float a = 1f - Mathf.Exp(-acceleration * Dt);
        velocity = Vector3.Lerp(velocity, targetVel, a);

        transform.position += velocity * Dt;
    }

    void OnDisable()
    {
        SetCursor(false);
    }
}
