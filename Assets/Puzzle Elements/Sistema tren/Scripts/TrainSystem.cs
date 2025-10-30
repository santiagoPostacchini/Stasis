// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.Events;
//
// public class TrainSystem : MonoBehaviour
// {
//     [Header("Container")]
//     public HedronContainer container;
//
//     [Header("Elevator (Rigidbody)")]
//     public Rigidbody elevatorRb;
//     public Transform elevatorWP1;
//     public Transform elevatorWP2;
//     public float elevatorSpeed = 2f;
//     public float elevatorAcceleration = 20f;
//     public float elevatorWaitSeconds = 2f;
//     public float elevatorArriveThreshold = 0.02f;
//
//     [Header("Barricade (Rigidbody)")]
//     public Rigidbody barricadeRb;
//     public Transform barricadeWP1;
//     public Transform barricadeWP2;
//     public float barricadeSpeed = 2.5f;
//     public float barricadeArriveThreshold = 0.02f;
//
//     public enum TrainPathMode { Loop, PingPong }
//
//     [Header("Train (Rigidbody + Waypoints)")]
//     public Rigidbody trainRb;
//     public List<Transform> trainWaypoints = new List<Transform>();
//     public Transform departure;
//     public float trainSpeed = 4f;
//     public float trainArriveThreshold = 0.03f;
//     [Tooltip("Tiempo de espera en el extremo. En Loop también se usa antes del teletransporte al primero.")]
//     public float trainTeleportWaitSeconds = 2f;
//     [Tooltip("Loop = salta del último al primero; PingPong = va y vuelve sin teletransportar.")]
//     public TrainPathMode trainPathMode = TrainPathMode.Loop;
//     public float trainRotationSpeed = 180f; // legacy, no usado
//
//     [Header("Events")]
//     public UnityEvent onTrainStarted;
//     public UnityEvent onTrainStoppedAtDeparture;
//
//     private bool systemEnabled;
//     private bool trainRunRequested;
//     private bool trainHaltAtDeparture;
//
//     private Transform elevatorLow;
//     private Transform elevatorHigh;
//     private Transform barricadeDown;
//     private Transform barricadeUp;
//
//     private enum ElevatorState { Idle, ToA, WaitAtA, ToB, WaitAtB }
//     private ElevatorState elevState = ElevatorState.Idle;
//     private Vector3 elevTarget;
//     private float elevCurrentSpeed;
//     private float elevWaitUntil;
//     private bool elevConfigured;
//
//     private enum BarricadeState { Idle, ToDown, ToUp, HoldUp, HoldDown }
//     private BarricadeState barrState = BarricadeState.Idle;
//     private Vector3 barrTarget;
//     private bool barrConfigured;
//     private bool barricadeForceDown;
//
//     // --- Tren (Loop / PingPong) ---
//     private bool trainConfigured;
//     private int trainFirstIdx;
//     private int trainLastIdx;
//     private int trainDepIdx;
//     private int trainCurrentIdx;
//     private int trainDir = +1; // +1 adelante, -1 atrás
//     private float trainWaitUntil;
//     private enum TrainState { IdleAtDeparture, Running, PauseAtEnd }
//     private TrainState trainState = TrainState.IdleAtDeparture;
//     private int trainPauseNextIdx;
//     private bool trainPauseDoTeleport;
//
//     void Awake()
//     {
//         ResolveHeights();
//         HookContainer(true);
//         PrepareSystems();
//     }
//
//     void OnEnable()
//     {
//         HookContainer(true);
//         PrepareSystems();
//     }
//
//     void OnDisable()
//     {
//         HookContainer(false);
//         systemEnabled = false;
//     }
//
//     // API
//     public void StartAllMovement()
//     {
//         systemEnabled = true;
//         trainHaltAtDeparture = false;
//         barricadeForceDown = false;
//         ResolveHeights();
//         PrepareSystems(true);
//     }
//
//     public void EnableSystem() => StartAllMovement();
//
//     public void RequestTrainHaltAtDeparture()
//     {
//         if (!trainConfigured) return;
//         trainHaltAtDeparture = true;
//         if (trainState == TrainState.IdleAtDeparture)
//             trainRunRequested = true;
//     }
//
//     public void DisableSystem()
//     {
//         RequestTrainHaltAtDeparture();
//         barricadeForceDown = true;
//         if (barrConfigured && barricadeRb != null && barricadeDown != null)
//         {
//             barrState = BarricadeState.ToDown;
//             barrTarget = barricadeDown.position;
//         }
//     }
//
//     public void HardStopAll()
//     {
//         systemEnabled = false;
//     }
//
//     void FixedUpdate()
//     {
//         elevConfigured = IsElevatorConfigured();
//         barrConfigured = IsBarricadeConfigured();
//         trainConfigured = ValidateTrainSetup();
//
//         TickElevator();
//         TickBarricade();
//         TickTrain();
//     }
//
//     // Setup
//     private void HookContainer(bool add)
//     {
//         if (container == null) return;
//         if (add)
//         {
//             container.onPlaced.AddListener(EnableSystem);
//             container.onRemoved.AddListener(RequestTrainHaltAtDeparture);
//         }
//         else
//         {
//             container.onPlaced.RemoveListener(EnableSystem);
//             container.onRemoved.RemoveListener(RequestTrainHaltAtDeparture);
//         }
//     }
//
//     private void PrepareSystems(bool resetPositions = false)
//     {
//         // Elevator
//         elevCurrentSpeed = 0f;
//         if (elevConfigured)
//         {
//             Vector3 p = elevatorRb.position;
//             float dLow = Vector3.Distance(p, elevatorLow.position);
//             float dHigh = Vector3.Distance(p, elevatorHigh.position);
//             Transform first = (dLow <= dHigh) ? elevatorHigh : elevatorLow;
//             elevTarget = first.position;
//             elevState = ElevatorState.ToA;
//             elevWaitUntil = 0f;
//         }
//         else elevState = ElevatorState.Idle;
//
//         // Barricade
//         if (barrConfigured)
//         {
//             if (barricadeForceDown)
//             {
//                 barrState = BarricadeState.ToDown;
//                 barrTarget = barricadeDown.position;
//             }
//             else
//             {
//                 barrState = BarricadeState.ToUp;
//                 barrTarget = barricadeUp.position;
//             }
//         }
//         else barrState = BarricadeState.Idle;
//
//         // Train
//         if (ValidateTrainSetup())
//         {
//             trainFirstIdx = 0;
//             trainLastIdx = trainWaypoints.Count - 1;
//             trainDepIdx = trainWaypoints.IndexOf(departure);
//
//             if (resetPositions && !IsNear(trainRb.position, trainWaypoints[trainDepIdx].position, trainArriveThreshold))
//                 TeleportBodyTo(trainRb, trainWaypoints[trainDepIdx].position);
//
//             trainState = TrainState.IdleAtDeparture;
//             trainRunRequested = false;
//             trainDir = +1;
//             trainCurrentIdx = NextIndexFrom(trainDepIdx, +1);
//         }
//         else trainState = TrainState.IdleAtDeparture;
//     }
//
//     private void ResolveHeights()
//     {
//         if (elevatorWP1 != null && elevatorWP2 != null)
//         {
//             if (elevatorWP1.position.y <= elevatorWP2.position.y) { elevatorLow = elevatorWP1; elevatorHigh = elevatorWP2; }
//             else { elevatorLow = elevatorWP2; elevatorHigh = elevatorWP1; }
//         }
//         else { elevatorLow = null; elevatorHigh = null; }
//
//         if (barricadeWP1 != null && barricadeWP2 != null)
//         {
//             if (barricadeWP1.position.y <= barricadeWP2.position.y) { barricadeDown = barricadeWP1; barricadeUp = barricadeWP2; }
//             else { barricadeDown = barricadeWP2; barricadeUp = barricadeWP1; }
//         }
//         else { barricadeDown = null; barricadeUp = null; }
//     }
//
//     private bool IsElevatorConfigured() =>
//         elevatorRb != null && elevatorLow != null && elevatorHigh != null;
//
//     private bool IsBarricadeConfigured() =>
//         barricadeRb != null && barricadeDown != null && barricadeUp != null;
//
//     private bool ValidateTrainSetup()
//     {
//         if (trainRb == null) return false;
//         if (trainWaypoints == null || trainWaypoints.Count < 2) return false;
//         if (departure == null) return false;
//         int dep = trainWaypoints.IndexOf(departure);
//         return dep >= 0 && dep < trainWaypoints.Count;
//     }
//
//     // Elevator
//     private void TickElevator()
//     {
//         if (!elevConfigured) return;
//
//         switch (elevState)
//         {
//             case ElevatorState.ToA:
//                 {
//                     bool reached = MoveElevatorAcc(elevTarget);
//                     if (reached)
//                     {
//                         elevCurrentSpeed = 0f;
//                         elevWaitUntil = Time.fixedUnscaledTime + elevatorWaitSeconds;
//                         elevState = ElevatorState.WaitAtA;
//                     }
//                     break;
//                 }
//             case ElevatorState.WaitAtA:
//                 {
//                     if (Time.fixedUnscaledTime >= elevWaitUntil)
//                     {
//                         elevTarget = (IsNear(elevTarget, elevatorHigh.position, 1e-4f)) ? elevatorLow.position : elevatorHigh.position;
//                         elevState = ElevatorState.ToB;
//                     }
//                     break;
//                 }
//             case ElevatorState.ToB:
//                 {
//                     bool reached = MoveElevatorAcc(elevTarget);
//                     if (reached)
//                     {
//                         elevCurrentSpeed = 0f;
//                         elevWaitUntil = Time.fixedUnscaledTime + elevatorWaitSeconds;
//                         elevState = ElevatorState.WaitAtB;
//                     }
//                     break;
//                 }
//             case ElevatorState.WaitAtB:
//                 {
//                     if (Time.fixedUnscaledTime >= elevWaitUntil)
//                     {
//                         elevTarget = (IsNear(elevTarget, elevatorHigh.position, 1e-4f)) ? elevatorLow.position : elevatorHigh.position;
//                         elevState = ElevatorState.ToA;
//                     }
//                     break;
//                 }
//         }
//     }
//
//     private bool MoveElevatorAcc(Vector3 targetPos)
//     {
//         Vector3 toTarget = targetPos - elevatorRb.position;
//         float distance = toTarget.magnitude;
//         if (distance <= elevatorArriveThreshold)
//         {
//             elevatorRb.MovePosition(targetPos);
//             return true;
//         }
//
//         float maxSpeed = Mathf.Max(0f, elevatorSpeed);
//         float accel = Mathf.Max(0f, elevatorAcceleration);
//         float maxSpeedForStop = Mathf.Sqrt(Mathf.Max(0f, 2f * accel * distance));
//         float desiredSpeed = Mathf.Min(maxSpeed, maxSpeedForStop);
//         elevCurrentSpeed = Mathf.MoveTowards(elevCurrentSpeed, desiredSpeed, accel * Time.fixedDeltaTime);
//
//         float step = elevCurrentSpeed * Time.fixedDeltaTime;
//         Vector3 dir = (distance > 1e-5f) ? (toTarget / distance) : Vector3.zero;
//         Vector3 next = (step >= distance) ? targetPos : elevatorRb.position + dir * step;
//
//         elevatorRb.MovePosition(next);
//         return false;
//     }
//
//     // Barricade
//     private void TickBarricade()
//     {
//         if (!barrConfigured) return;
//
//         if (barricadeForceDown && barrState != BarricadeState.HoldDown)
//         {
//             barrState = BarricadeState.ToDown;
//             barrTarget = barricadeDown.position;
//         }
//
//         switch (barrState)
//         {
//             case BarricadeState.ToDown:
//                 {
//                     barrTarget = barricadeDown.position;
//                     bool reached = MoveBodyLinear(barricadeRb, barrTarget, barricadeSpeed, barricadeArriveThreshold);
//                     if (reached)
//                     {
//                         barrState = barricadeForceDown ? BarricadeState.HoldDown : BarricadeState.ToUp;
//                     }
//                     break;
//                 }
//             case BarricadeState.ToUp:
//                 {
//                     if (barricadeForceDown)
//                     {
//                         barrState = BarricadeState.ToDown;
//                         break;
//                     }
//
//                     barrTarget = barricadeUp.position;
//                     bool reached = MoveBodyLinear(barricadeRb, barrTarget, barricadeSpeed, barricadeArriveThreshold);
//                     if (reached)
//                     {
//                         RequestStartTrain();
//                         barrState = BarricadeState.HoldUp;
//                     }
//                     break;
//                 }
//             case BarricadeState.HoldUp:
//                 break;
//             case BarricadeState.HoldDown:
//                 break;
//             case BarricadeState.Idle:
//                 break;
//         }
//     }
//
//     private void RequestStartTrain()
//     {
//         if (!systemEnabled || !trainConfigured) return;
//         trainRunRequested = true;
//         onTrainStarted?.Invoke();
//     }
//
//     // Train (Loop / PingPong)
//     private void TickTrain()
//     {
//         if (!trainConfigured) return;
//
//         switch (trainState)
//         {
//             case TrainState.IdleAtDeparture:
//                 {
//                     Vector3 depPos = trainWaypoints[trainDepIdx].position;
//                     if (!IsNear(trainRb.position, depPos, trainArriveThreshold))
//                         TeleportBodyTo(trainRb, depPos);
//
//                     if (trainRunRequested)
//                     {
//                         trainDir = +1;
//                         trainCurrentIdx = NextIndexFrom(trainDepIdx, trainDir);
//                         trainState = TrainState.Running;
//                     }
//                     break;
//                 }
//
//             case TrainState.Running:
//                 {
//                     Vector3 target = trainWaypoints[trainCurrentIdx].position;
//                     bool reached = MoveBodyLinear(trainRb, target, trainSpeed, trainArriveThreshold);
//
//                     if (!reached) break;
//
//                     if (trainHaltAtDeparture && trainCurrentIdx == trainDepIdx)
//                     {
//                         trainRunRequested = false;
//                         onTrainStoppedAtDeparture?.Invoke();
//                         trainHaltAtDeparture = false;
//                         trainState = TrainState.IdleAtDeparture;
//                         break;
//                     }
//
//                     if (trainPathMode == TrainPathMode.Loop)
//                     {
//                         int next = trainCurrentIdx + 1;
//                         if (next > trainLastIdx)
//                         {
//                             trainWaitUntil = Time.fixedUnscaledTime + trainTeleportWaitSeconds;
//                             trainPauseDoTeleport = true;
//                             trainPauseNextIdx = Mathf.Clamp(trainFirstIdx + 1, trainFirstIdx, trainLastIdx);
//                             trainState = TrainState.PauseAtEnd;
//                         }
//                         else
//                         {
//                             trainCurrentIdx = next;
//                         }
//                     }
//                     else // PingPong
//                     {
//                         int next = trainCurrentIdx + trainDir;
//
//                         if (next > trainLastIdx || next < trainFirstIdx)
//                         {
//                             trainDir *= -1;
//                             trainWaitUntil = Time.fixedUnscaledTime + trainTeleportWaitSeconds;
//                             trainPauseDoTeleport = false; // no teletransporte en ping-pong
//                             trainPauseNextIdx = Mathf.Clamp(trainCurrentIdx + trainDir, trainFirstIdx, trainLastIdx);
//                             trainState = TrainState.PauseAtEnd;
//                         }
//                         else
//                         {
//                             trainCurrentIdx = next;
//                         }
//                     }
//                     break;
//                 }
//
//             case TrainState.PauseAtEnd:
//                 {
//                     if (Time.fixedUnscaledTime < trainWaitUntil) break;
//
//                     if (trainPauseDoTeleport)
//                     {
//                         TeleportBodyTo(trainRb, trainWaypoints[trainFirstIdx].position);
//                     }
//
//                     trainCurrentIdx = trainPauseNextIdx;
//
//                     if (trainHaltAtDeparture && trainCurrentIdx == trainDepIdx)
//                     {
//                         trainRunRequested = false;
//                         onTrainStoppedAtDeparture?.Invoke();
//                         trainHaltAtDeparture = false;
//                         trainState = TrainState.IdleAtDeparture;
//                     }
//                     else
//                     {
//                         trainState = TrainState.Running;
//                     }
//                     break;
//                 }
//         }
//     }
//
//     private int NextIndexFrom(int idx, int dir)
//     {
//         int next = idx + Mathf.Clamp(dir, -1, 1);
//         return Mathf.Clamp(next, 0, (trainWaypoints.Count - 1));
//     }
//
//     // Helpers
//     private bool MoveBodyLinear(Rigidbody rb, Vector3 targetPos, float speed, float arriveThreshold)
//     {
//         Vector3 toTarget = targetPos - rb.position;
//         float dist = toTarget.magnitude;
//         if (dist <= arriveThreshold)
//         {
//             rb.MovePosition(targetPos);
//             return true;
//         }
//
//         float s = Mathf.Max(0f, speed);
//         float step = s * Time.fixedDeltaTime;
//         Vector3 dir = (dist > 1e-5f) ? (toTarget / dist) : Vector3.zero;
//         Vector3 next = (step >= dist) ? targetPos : rb.position + dir * step;
//
//         rb.MovePosition(next);
//         return false;
//     }
//
//     private static bool IsNear(Vector3 a, Vector3 b, float eps) =>
//         (a - b).sqrMagnitude <= eps * eps;
//
//     private void TeleportBodyTo(Rigidbody rb, Vector3 pos)
//     {
//         if (rb == null) return;
// #if UNITY_2022_1_OR_NEWER
//         rb.position = pos;
// #else
//         rb.MovePosition(pos);
// #endif
//     }
//
// #if UNITY_EDITOR
//     void OnDrawGizmosSelected()
//     {
//         if (elevatorWP1 != null && elevatorWP2 != null)
//         {
//             Transform low = (elevatorWP1.position.y <= elevatorWP2.position.y) ? elevatorWP1 : elevatorWP2;
//             Transform high = (low == elevatorWP1) ? elevatorWP2 : elevatorWP1;
//             Gizmos.color = Color.cyan;
//             Gizmos.DrawSphere(low.position, 0.08f);
//             Gizmos.color = Color.magenta;
//             Gizmos.DrawCube(high.position, Vector3.one * 0.14f);
//         }
//
//         if (barricadeWP1 != null && barricadeWP2 != null)
//         {
//             Transform down = (barricadeWP1.position.y <= barricadeWP2.position.y) ? barricadeWP1 : barricadeWP2;
//             Transform up = (down == barricadeWP1) ? barricadeWP2 : barricadeWP1;
//             Gizmos.color = Color.green;
//             Gizmos.DrawSphere(down.position, 0.08f);
//             Gizmos.color = Color.red;
//             Gizmos.DrawCube(up.position, 0.14f * Vector3.one);
//         }
//     }
// #endif
// }
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla un pequeño sistema "estación":
/// - Un ascensor (elevator) que sube/baja entre 2 waypoints con aceleración suave.
/// - Una barricada que sube/baja y dispara el inicio del tren al llegar arriba.
/// - Un tren que recorre waypoints en Loop o PingPong, con pausa configurable en extremos.
/// Se integra con un HedronContainer (opcional) que habilita/deshabilita el sistema.
/// </summary>
[AddComponentMenu("TESIS/Train System")]
[HelpURL("https://your-docs-or-ticketing-url.example/TrainSystem")]
public class TrainSystem : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // CONTAINER / GATE
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Container (Llave / Gate)")]
    [Tooltip("Contenedor que actúa como 'llave'. onPlaced => EnableSystem, onRemoved => RequestTrainHaltAtDeparture.")]
    public HedronContainer container;

    // ─────────────────────────────────────────────────────────────────────────────
    // ELEVATOR
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Elevator (Rigidbody + Waypoints)")]
    [Tooltip("Rigidbody del ascensor. Se mueve mediante MovePosition en FixedUpdate.")]
    public Rigidbody elevatorRb;

    [Tooltip("Waypoint A (uno de los extremos). Cualquiera puede ser alto/bajo; se resuelve automáticamente por altura.")]
    public Transform elevatorWP1;

    [Tooltip("Waypoint B (otro extremo). Se asigna automáticamente como alto o bajo según Y.")]
    public Transform elevatorWP2;

    [Tooltip("Velocidad objetivo (m/s) del ascensor. Se respeta aceleración y frenado para detenerse justo en el destino.")]
    [Min(0f)]
    public float elevatorSpeed = 2f;

    [Tooltip("Aceleración (m/s²) usada para acelerar/frenar y calcular velocidad máxima que permita detenerse a tiempo.")]
    [Min(0f)]
    public float elevatorAcceleration = 20f;

    [Tooltip("Tiempo (seg) detenido en cada extremo antes de invertir el sentido.")]
    [Min(0f)]
    public float elevatorWaitSeconds = 2f;

    [Tooltip("Umbral de llegada (m) para 'encajar' en el waypoint y considerar alcanzado el destino.")]
    [Range(0.001f, 0.25f)]
    public float elevatorArriveThreshold = 0.02f;

    // ─────────────────────────────────────────────────────────────────────────────
    // BARRICADE
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Barricade (Rigidbody + Waypoints)")]
    [Tooltip("Rigidbody de la barricada que sube/baja.")]
    public Rigidbody barricadeRb;

    [Tooltip("Waypoint 1 (uno de los extremos). El más bajo será 'Down'.")]
    public Transform barricadeWP1;

    [Tooltip("Waypoint 2 (otro extremo). El más alto será 'Up'.")]
    public Transform barricadeWP2;

    [Tooltip("Velocidad lineal (m/s) de la barricada.")]
    [Min(0f)]
    public float barricadeSpeed = 2.5f;

    [Tooltip("Umbral de llegada (m) para la barricada.")]
    [Range(0.001f, 0.25f)]
    public float barricadeArriveThreshold = 0.02f;

    // ─────────────────────────────────────────────────────────────────────────────
    // TRAIN
    // ─────────────────────────────────────────────────────────────────────────────
    public enum TrainPathMode { Loop, PingPong }

    [Header("Train (Rigidbody + Waypoints)")]
    [Tooltip("Rigidbody del tren.")]
    public Rigidbody trainRb;

    [Tooltip("Waypoints (mínimo 2). El tren recorrerá estos puntos según el modo Path.")]
    public List<Transform> trainWaypoints = new List<Transform>();

    [Tooltip("Punto de partida / estación. Debe ser un elemento dentro de 'trainWaypoints'.")]
    public Transform departure;

    [Tooltip("Velocidad lineal (m/s) del tren.")]
    [Min(0f)]
    public float trainSpeed = 4f;

    [Tooltip("Umbral de llegada (m) del tren.")]
    [Range(0.001f, 0.25f)]
    public float trainArriveThreshold = 0.03f;

    [Tooltip("Tiempo (seg) de espera en el extremo. En Loop también se usa antes del teletransporte al primer punto.")]
    [Min(0f)]
    public float trainTeleportWaitSeconds = 2f;

    [Tooltip("Loop: salta del último al primero con una pausa. PingPong: va y vuelve sin teletransportarse.")]
    public TrainPathMode trainPathMode = TrainPathMode.Loop;

    [Tooltip("(Legacy, no usado) Velocidad de rotación (deg/s).")]
    public float trainRotationSpeed = 180f;

    // ─────────────────────────────────────────────────────────────────────────────
    // EVENTS
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Events")]
    [Tooltip("Se invoca cuando el tren comienza a moverse (tras levantar barricada y solicitud válida).")]
    public UnityEvent onTrainStarted;

    [Tooltip("Se invoca cuando el tren se detiene en 'departure' por una petición de HaltAtDeparture.")]
    public UnityEvent onTrainStoppedAtDeparture;

    // ─────────────────────────────────────────────────────────────────────────────
    // RUNTIME STATE (solo lectura en Inspector)
    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Debug / Estado (Runtime)")]
    [Tooltip("Indica si el sistema está habilitado (p. ej. por el Container).")]
    [SerializeField] private bool systemEnabled;

    [Tooltip("True si se solicitó correr el tren.")]
    [SerializeField] private bool trainRunRequested;

    [Tooltip("True si se pidió detener en 'departure' en el próximo paso por la estación.")]
    [SerializeField] private bool trainHaltAtDeparture;

    private Transform elevatorLow;
    private Transform elevatorHigh;
    private Transform barricadeDown;
    private Transform barricadeUp;

    private enum ElevatorState { Idle, ToA, WaitAtA, ToB, WaitAtB }
    [SerializeField] private ElevatorState elevState = ElevatorState.Idle;
    private Vector3 elevTarget;
    private float elevCurrentSpeed;
    private float elevWaitUntil;
    private bool elevConfigured;

    private enum BarricadeState { Idle, ToDown, ToUp, HoldUp, HoldDown }
    [SerializeField] private BarricadeState barrState = BarricadeState.Idle;
    private Vector3 barrTarget;
    private bool barrConfigured;
    private bool barricadeForceDown;

    private bool trainConfigured;
    private int trainFirstIdx;
    private int trainLastIdx;
    private int trainDepIdx;
    private int trainCurrentIdx;
    private int trainDir = +1; // +1 adelante, -1 atrás
    private float trainWaitUntil;
    private enum TrainState { IdleAtDeparture, Running, PauseAtEnd }
    [SerializeField] private TrainState trainState = TrainState.IdleAtDeparture;
    private int trainPauseNextIdx;
    private bool trainPauseDoTeleport;

    // ─────────────────────────────────────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        ResolveHeights();
        HookContainer(true);
        PrepareSystems();
    }

    private void OnEnable()
    {
        HookContainer(true);
        PrepareSystems();
    }

    private void OnDisable()
    {
        HookContainer(false);
        systemEnabled = false;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // API PÚBLICA
    // ─────────────────────────────────────────────────────────────────────────────
    /// <summary>Habilita todo el sistema y resetea estados de movimiento.</summary>
    [ContextMenu("▶ Start All Movement")]
    public void StartAllMovement()
    {
        systemEnabled = true;
        trainHaltAtDeparture = false;
        barricadeForceDown = false;
        ResolveHeights();
        PrepareSystems(true);
    }

    /// <summary>Alias de StartAllMovement().</summary>
    public void EnableSystem() => StartAllMovement();

    /// <summary>Solicita que el tren se detenga la próxima vez que llegue a 'departure'.</summary>
    [ContextMenu("☒ Request Train Halt At Departure")]
    public void RequestTrainHaltAtDeparture()
    {
        if (!trainConfigured) return;
        trainHaltAtDeparture = true;
        if (trainState == TrainState.IdleAtDeparture)
            trainRunRequested = true; // disparar la salida si está en la estación
    }

    /// <summary>Fuerza barricada hacia abajo y pide detener el tren en 'departure'.</summary>
    [ContextMenu("⏹ Disable System (Barricade Down + Halt)")]
    public void DisableSystem()
    {
        RequestTrainHaltAtDeparture();
        barricadeForceDown = true;
        if (barrConfigured && barricadeRb != null && barricadeDown != null)
        {
            barrState = BarricadeState.ToDown;
            barrTarget = barricadeDown.position;
        }
    }

    /// <summary>Detiene todo hard (no mueve nada en curso).</summary>
    [ContextMenu("⛔ Hard Stop All (Immediate)")]
    public void HardStopAll()
    {
        systemEnabled = false;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // UPDATE LOOP
    // ─────────────────────────────────────────────────────────────────────────────
    private void FixedUpdate()
    {
        elevConfigured = IsElevatorConfigured();
        barrConfigured = IsBarricadeConfigured();
        trainConfigured = ValidateTrainSetup();

        TickElevator();
        TickBarricade();
        TickTrain();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SETUP
    // ─────────────────────────────────────────────────────────────────────────────
    private void HookContainer(bool add)
    {
        if (container == null) return;
        if (add)
        {
            container.onPlaced.AddListener(EnableSystem);
            container.onRemoved.AddListener(RequestTrainHaltAtDeparture);
        }
        else
        {
            container.onPlaced.RemoveListener(EnableSystem);
            container.onRemoved.RemoveListener(RequestTrainHaltAtDeparture);
        }
    }

    /// <summary>Inicializa estados de cada subsistema. Si resetPositions = true, teletransporta el tren a departure si está lejos.</summary>
    private void PrepareSystems(bool resetPositions = false)
    {
        // Elevator
        elevCurrentSpeed = 0f;
        if (elevConfigured)
        {
            Vector3 p = elevatorRb.position;
            float dLow = Vector3.Distance(p, elevatorLow.position);
            float dHigh = Vector3.Distance(p, elevatorHigh.position);
            Transform first = (dLow <= dHigh) ? elevatorHigh : elevatorLow; // primero el más lejano, para que siempre vaya al opuesto
            elevTarget = first.position;
            elevState = ElevatorState.ToA;
            elevWaitUntil = 0f;
        }
        else elevState = ElevatorState.Idle;

        // Barricade
        if (barrConfigured)
        {
            if (barricadeForceDown)
            {
                barrState = BarricadeState.ToDown;
                barrTarget = barricadeDown.position;
            }
            else
            {
                barrState = BarricadeState.ToUp;
                barrTarget = barricadeUp.position;
            }
        }
        else barrState = BarricadeState.Idle;

        // Train
        if (ValidateTrainSetup())
        {
            trainFirstIdx = 0;
            trainLastIdx = trainWaypoints.Count - 1;
            trainDepIdx = trainWaypoints.IndexOf(departure);

            if (resetPositions && !IsNear(trainRb.position, trainWaypoints[trainDepIdx].position, trainArriveThreshold))
                TeleportBodyTo(trainRb, trainWaypoints[trainDepIdx].position);

            trainState = TrainState.IdleAtDeparture;
            trainRunRequested = false;
            trainDir = +1;
            trainCurrentIdx = NextIndexFrom(trainDepIdx, +1);
        }
        else trainState = TrainState.IdleAtDeparture;
    }

    private void ResolveHeights()
    {
        if (elevatorWP1 != null && elevatorWP2 != null)
        {
            if (elevatorWP1.position.y <= elevatorWP2.position.y) { elevatorLow = elevatorWP1; elevatorHigh = elevatorWP2; }
            else { elevatorLow = elevatorWP2; elevatorHigh = elevatorWP1; }
        }
        else { elevatorLow = null; elevatorHigh = null; }

        if (barricadeWP1 != null && barricadeWP2 != null)
        {
            if (barricadeWP1.position.y <= barricadeWP2.position.y) { barricadeDown = barricadeWP1; barricadeUp = barricadeWP2; }
            else { barricadeDown = barricadeWP2; barricadeUp = barricadeWP1; }
        }
        else { barricadeDown = null; barricadeUp = null; }
    }

    private bool IsElevatorConfigured() =>
        elevatorRb != null && elevatorLow != null && elevatorHigh != null;

    private bool IsBarricadeConfigured() =>
        barricadeRb != null && barricadeDown != null && barricadeUp != null;

    private bool ValidateTrainSetup()
    {
        if (trainRb == null) return false;
        if (trainWaypoints == null || trainWaypoints.Count < 2) return false;
        if (departure == null) return false;
        int dep = trainWaypoints.IndexOf(departure);
        return dep >= 0 && dep < trainWaypoints.Count;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ELEVATOR
    // ─────────────────────────────────────────────────────────────────────────────
    private void TickElevator()
    {
        if (!elevConfigured) return;

        switch (elevState)
        {
            case ElevatorState.ToA:
            {
                bool reached = MoveElevatorAcc(elevTarget);
                if (reached)
                {
                    elevCurrentSpeed = 0f;
                    elevWaitUntil = Time.fixedUnscaledTime + elevatorWaitSeconds;
                    elevState = ElevatorState.WaitAtA;
                }
                break;
            }
            case ElevatorState.WaitAtA:
            {
                if (Time.fixedUnscaledTime >= elevWaitUntil)
                {
                    elevTarget = (IsNear(elevTarget, elevatorHigh.position, 1e-4f)) ? elevatorLow.position : elevatorHigh.position;
                    elevState = ElevatorState.ToB;
                }
                break;
            }
            case ElevatorState.ToB:
            {
                bool reached = MoveElevatorAcc(elevTarget);
                if (reached)
                {
                    elevCurrentSpeed = 0f;
                    elevWaitUntil = Time.fixedUnscaledTime + elevatorWaitSeconds;
                    elevState = ElevatorState.WaitAtB;
                }
                break;
            }
            case ElevatorState.WaitAtB:
            {
                if (Time.fixedUnscaledTime >= elevWaitUntil)
                {
                    elevTarget = (IsNear(elevTarget, elevatorHigh.position, 1e-4f)) ? elevatorLow.position : elevatorHigh.position;
                    elevState = ElevatorState.ToA;
                }
                break;
            }
        }
    }

    private bool MoveElevatorAcc(Vector3 targetPos)
    {
        Vector3 toTarget = targetPos - elevatorRb.position;
        float distance = toTarget.magnitude;
        if (distance <= elevatorArriveThreshold)
        {
            elevatorRb.MovePosition(targetPos);
            return true;
        }

        float maxSpeed = Mathf.Max(0f, elevatorSpeed);
        float accel = Mathf.Max(0f, elevatorAcceleration);
        float maxSpeedForStop = Mathf.Sqrt(Mathf.Max(0f, 2f * accel * distance)); // v = sqrt(2*a*s)
        float desiredSpeed = Mathf.Min(maxSpeed, maxSpeedForStop);
        elevCurrentSpeed = Mathf.MoveTowards(elevCurrentSpeed, desiredSpeed, accel * Time.fixedDeltaTime);

        float step = elevCurrentSpeed * Time.fixedDeltaTime;
        Vector3 dir = (distance > 1e-5f) ? (toTarget / distance) : Vector3.zero;
        Vector3 next = (step >= distance) ? targetPos : elevatorRb.position + dir * step;

        elevatorRb.MovePosition(next);
        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // BARRICADE
    // ─────────────────────────────────────────────────────────────────────────────
    private void TickBarricade()
    {
        if (!barrConfigured) return;

        if (barricadeForceDown && barrState != BarricadeState.HoldDown)
        {
            barrState = BarricadeState.ToDown;
            barrTarget = barricadeDown.position;
        }

        switch (barrState)
        {
            case BarricadeState.ToDown:
            {
                barrTarget = barricadeDown.position;
                bool reached = MoveBodyLinear(barricadeRb, barrTarget, barricadeSpeed, barricadeArriveThreshold);
                if (reached)
                {
                    barrState = barricadeForceDown ? BarricadeState.HoldDown : BarricadeState.ToUp;
                }
                break;
            }
            case BarricadeState.ToUp:
            {
                if (barricadeForceDown)
                {
                    barrState = BarricadeState.ToDown;
                    break;
                }

                barrTarget = barricadeUp.position;
                bool reached = MoveBodyLinear(barricadeRb, barrTarget, barricadeSpeed, barricadeArriveThreshold);
                if (reached)
                {
                    RequestStartTrain();
                    barrState = BarricadeState.HoldUp;
                }
                break;
            }
            case BarricadeState.HoldUp:
                break;
            case BarricadeState.HoldDown:
                break;
            case BarricadeState.Idle:
                break;
        }
    }

    private void RequestStartTrain()
    {
        if (!systemEnabled || !trainConfigured) return;
        trainRunRequested = true;
        onTrainStarted?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TRAIN
    // ─────────────────────────────────────────────────────────────────────────────
    private void TickTrain()
    {
        if (!trainConfigured) return;

        switch (trainState)
        {
            case TrainState.IdleAtDeparture:
            {
                Vector3 depPos = trainWaypoints[trainDepIdx].position;
                if (!IsNear(trainRb.position, depPos, trainArriveThreshold))
                    TeleportBodyTo(trainRb, depPos);

                if (trainRunRequested)
                {
                    trainDir = +1;
                    trainCurrentIdx = NextIndexFrom(trainDepIdx, trainDir);
                    trainState = TrainState.Running;
                }
                break;
            }

            case TrainState.Running:
            {
                Vector3 target = trainWaypoints[trainCurrentIdx].position;
                bool reached = MoveBodyLinear(trainRb, target, trainSpeed, trainArriveThreshold);

                if (!reached) break;

                if (trainHaltAtDeparture && trainCurrentIdx == trainDepIdx)
                {
                    trainRunRequested = false;
                    onTrainStoppedAtDeparture?.Invoke();
                    trainHaltAtDeparture = false;
                    trainState = TrainState.IdleAtDeparture;
                    break;
                }

                if (trainPathMode == TrainPathMode.Loop)
                {
                    int next = trainCurrentIdx + 1;
                    if (next > trainLastIdx)
                    {
                        trainWaitUntil = Time.fixedUnscaledTime + trainTeleportWaitSeconds;
                        trainPauseDoTeleport = true;
                        trainPauseNextIdx = Mathf.Clamp(trainFirstIdx + 1, trainFirstIdx, trainLastIdx);
                        trainState = TrainState.PauseAtEnd;
                    }
                    else
                    {
                        trainCurrentIdx = next;
                    }
                }
                else // PingPong
                {
                    int next = trainCurrentIdx + trainDir;

                    if (next > trainLastIdx || next < trainFirstIdx)
                    {
                        trainDir *= -1;
                        trainWaitUntil = Time.fixedUnscaledTime + trainTeleportWaitSeconds;
                        trainPauseDoTeleport = false; // no teletransporte en ping-pong
                        trainPauseNextIdx = Mathf.Clamp(trainCurrentIdx + trainDir, trainFirstIdx, trainLastIdx);
                        trainState = TrainState.PauseAtEnd;
                    }
                    else
                    {
                        trainCurrentIdx = next;
                    }
                }
                break;
            }

            case TrainState.PauseAtEnd:
            {
                if (Time.fixedUnscaledTime < trainWaitUntil) break;

                if (trainPauseDoTeleport)
                {
                    TeleportBodyTo(trainRb, trainWaypoints[trainFirstIdx].position);
                }

                trainCurrentIdx = trainPauseNextIdx;

                if (trainHaltAtDeparture && trainCurrentIdx == trainDepIdx)
                {
                    trainRunRequested = false;
                    onTrainStoppedAtDeparture?.Invoke();
                    trainHaltAtDeparture = false;
                    trainState = TrainState.IdleAtDeparture;
                }
                else
                {
                    trainState = TrainState.Running;
                }
                break;
            }
        }
    }

    private int NextIndexFrom(int idx, int dir)
    {
        int next = idx + Mathf.Clamp(dir, -1, 1);
        return Mathf.Clamp(next, 0, (trainWaypoints.Count - 1));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────────
    private bool MoveBodyLinear(Rigidbody rb, Vector3 targetPos, float speed, float arriveThreshold)
    {
        Vector3 toTarget = targetPos - rb.position;
        float dist = toTarget.magnitude;
        if (dist <= arriveThreshold)
        {
            rb.MovePosition(targetPos);
            return true;
        }

        float s = Mathf.Max(0f, speed);
        float step = s * Time.fixedDeltaTime;
        Vector3 dir = (dist > 1e-5f) ? (toTarget / dist) : Vector3.zero;
        Vector3 next = (step >= dist) ? targetPos : rb.position + dir * step;

        rb.MovePosition(next);
        return false;
    }

    private static bool IsNear(Vector3 a, Vector3 b, float eps) =>
        (a - b).sqrMagnitude <= eps * eps;

    private void TeleportBodyTo(Rigidbody rb, Vector3 pos)
    {
        if (rb == null) return;
#if UNITY_2022_1_OR_NEWER
        rb.position = pos;
#else
        rb.MovePosition(pos);
#endif
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (elevatorWP1 != null && elevatorWP2 != null)
        {
            Transform low = (elevatorWP1.position.y <= elevatorWP2.position.y) ? elevatorWP1 : elevatorWP2;
            Transform high = (low == elevatorWP1) ? elevatorWP2 : elevatorWP1;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(low.position, 0.08f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawCube(high.position, Vector3.one * 0.14f);
        }

        if (barricadeWP1 != null && barricadeWP2 != null)
        {
            Transform down = (barricadeWP1.position.y <= barricadeWP2.position.y) ? barricadeWP1 : barricadeWP2;
            Transform up = (down == barricadeWP1) ? barricadeWP2 : barricadeWP1;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(down.position, 0.08f);
            Gizmos.color = Color.red;
            Gizmos.DrawCube(up.position, 0.14f * Vector3.one);
        }
    }
#endif
}
