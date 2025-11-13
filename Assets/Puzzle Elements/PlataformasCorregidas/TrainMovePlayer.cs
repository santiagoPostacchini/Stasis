using Player.Scripts.MovementFSM.MVC;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrainMovePlayer : MonoBehaviour
{
    [SerializeField] private Model _player;
    public Vector3 velocityTrain = Vector3.zero;
    public Vector3 velocityPlayer = Vector3.zero;

    public Rigidbody trainRb;
    public Rigidbody playerRb;

    // Nueva variable para guardar la velocidad previa del tren
    [SerializeField] private Vector3 _currentVelocityTrain = Vector3.zero;

    // Umbral para considerar que el tren está en movimiento/parado
    [SerializeField] private float _startStopThreshold = 0.02f;

    private void Start()
    {
        _currentVelocityTrain = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (trainRb != null && playerRb != null)
        {
            // Debug / inspección
            velocityTrain = trainRb.velocity;
            velocityPlayer = playerRb.velocity;

            // --- DETECCIÓN DE CAMBIO DE ESTADO DEL TREN ---
            Vector3 previousVelocity = _currentVelocityTrain;
            Vector3 currentVelocity = trainRb.velocity;

            float prevSpeed = previousVelocity.magnitude;
            float currSpeed = currentVelocity.magnitude;

            // Tren estaba quieto (<= threshold) y ahora se mueve (> threshold)
            if (prevSpeed <= _startStopThreshold && currSpeed > _startStopThreshold)
            {
                // Aplica el offset UNA SOLA VEZ al arrancar
                if (playerRb != null)
                {
                    playerRb.gameObject.transform.position += new Vector3(2.3f, 0f, 0f);
                    Debug.Log("Aumento");
                }
            }

            // Tren estaba en movimiento (> threshold) y ahora se detiene (<= threshold)
            if (prevSpeed > _startStopThreshold && currSpeed <= _startStopThreshold)
            {
                // Aplica el offset inverso UNA SOLA VEZ al frenar
                if (playerRb != null)
                {
                    playerRb.gameObject.transform.position += new Vector3(-2.3f, 0f, 0f);
                    Debug.Log("disminuyo");
                }
            }

            // Actualizamos la velocidad "actual" para la próxima frame
            _currentVelocityTrain = currentVelocity;
        }

        // Lógica original de arrastrar al jugador con el tren
        if (_player != null && trainRb != null && playerRb != null)
        {
            playerRb.position += trainRb.velocity * Time.fixedDeltaTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if (player != null)
        {
            _player = player;
            playerRb = player.GetComponent<Rigidbody>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if (player != null)
        {
            _player = null;
            playerRb = null;
        }
    }
}
