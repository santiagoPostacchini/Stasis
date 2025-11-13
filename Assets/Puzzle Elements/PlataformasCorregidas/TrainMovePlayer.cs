using Player.Scripts.MovementFSM.MVC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrainMovePlayer : MonoBehaviour
{
    [SerializeField] private Model _player;

    public Rigidbody trainRb;
    public Rigidbody playerRb;

    [Header("Debug")]
    public Vector3 velocityTrain = Vector3.zero;
    public Vector3 velocityPlayer = Vector3.zero;

    // Posición anterior del tren (para delta)
    [SerializeField] private Vector3 _lastTrainPosition = Vector3.zero;
    [SerializeField] private bool _playerOnTrain = false;

    [Header("Umbral de movimiento (debug)")]
    [SerializeField] private float _startStopThreshold = 0.02f;

    [Header("Bloqueo frontal")]
    [Tooltip("Collider del frente del tren contra el que NO quiero que el player avance.")]
    public Collider frontCollider;

    [Tooltip("Origen del raycast (si es null, usa el centro de masa del player).")]
    public Transform rayOrigin;

    [Tooltip("Distancia del ray hacia adelante.")]
    public float rayDistance = 0.7f;

    [Tooltip("Layers a considerar para el raycast.")]
    public LayerMask rayMask = ~0;

    [Tooltip("Dibujar ray en la escena para debug.")]
    public bool drawRay = true;

    private void Start()
    {
        if (trainRb != null)
        {
            _lastTrainPosition = trainRb.position;
        }
    }

    private void FixedUpdate()
    {
        if (trainRb == null) return;

        // ==========================
        // 1) Cálculo delta del tren
        // ==========================
        Vector3 currentTrainPos = trainRb.position;
        Vector3 deltaTrain = currentTrainPos - _lastTrainPosition;

        float dt = Time.fixedDeltaTime;
        velocityTrain = (dt > 0f) ? deltaTrain / dt : Vector3.zero;

        // ==========================
        // 2) Movimiento del player con el tren
        // ==========================
        if (_playerOnTrain && playerRb != null)
        {
            velocityPlayer = playerRb.velocity;

            // --- Bloqueo frontal antes de moverlo con el tren ---
            HandleFrontBlock();

            // Arrastramos al player con el mismo delta que el tren
            playerRb.MovePosition(playerRb.position + deltaTrain);
        }

        // Actualizamos posición anterior del tren
        _lastTrainPosition = currentTrainPos;
    }

    /// <summary>
    /// Lanza un rayo en dirección de movimiento del player.
    /// Si golpea el collider frontal del tren, anula la velocidad hacia adelante.
    /// </summary>
    private void HandleFrontBlock()
    {
        if (playerRb == null || frontCollider == null) return;

        Vector3 v = playerRb.velocity;
        float speed = v.magnitude;
        if (speed <= 0.01f) return; // no se está moviendo, no hace falta ray

        Vector3 dir = v / speed;

        // Origen del ray
        Vector3 origin;
        if (rayOrigin != null)
            origin = rayOrigin.position;
        else
            origin = playerRb.worldCenterOfMass;

        // Debug visual
        if (drawRay)
        {
            Debug.DrawRay(origin, dir * rayDistance, Color.cyan);
        }

        // Raycast hacia la dirección de movimiento
        if (Physics.Raycast(origin, dir, out RaycastHit hit, rayDistance, rayMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == frontCollider)
            {
                // Proyección de la velocidad del player sobre la dirección de avance
                float forwardComp = Vector3.Dot(playerRb.velocity, dir);

                // Solo si está intentando avanzar hacia el frente
                if (forwardComp > 0f)
                {
                    // Restamos la componente hacia adelante:
                    // esto “aplana” la velocidad contra el vidrio
                    playerRb.velocity -= dir * forwardComp*3;
                    // Opcional: puedes agregar un pequeño empuje hacia atrás si querés que rebote un poquito
                    // playerRb.velocity += -dir * 0.2f;

                    // Debug
                    // Debug.Log("Bloqueado por vidrio frontal, velocidad hacia adelante anulada.");
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if (player != null)
        {
            _player = player;
            playerRb = player.GetComponent<Rigidbody>();
            _playerOnTrain = true;

            if (trainRb != null)
            {
                // Resetea referencia para evitar delta bruto al entrar
                _lastTrainPosition = trainRb.position;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if (player != null && player == _player)
        {
            _playerOnTrain = false;
            _player = null;
            playerRb = null;
        }
    }
}
