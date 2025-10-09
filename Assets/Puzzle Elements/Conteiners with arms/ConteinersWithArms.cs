using System.Collections.Generic;
using UnityEngine;

public class ConteinersWithArms : MonoBehaviour
{
    public enum PathMode { Loop, PingPong, Once }

    [Header("Trayectoria")]
    public List<Transform> waypoints = new List<Transform>();
    public PathMode mode = PathMode.Loop;
    public bool autoStart = true;
    public bool drawGizmos = true;

    [Header("Movimiento")]
    public float speed = 2f;
    public float arriveThreshold = 0.05f;
    public float waitAtWaypoint = 0f;    // segundos a esperar en cada punto
    public bool faceDirection = true;    // mirar hacia donde se mueve
    public float turnSpeed = 10f;        // velocidad de giro (slerp)

    // Opcional: pausa por “stasis” si el componente existe en el mismo GO
    StasisConteinerWithArms _stasisConteiner;

    int _index = 0;
    int _dir = 1;                        // usado en PingPong
    float _waitTimer = 0f;
    bool _isMoving;

    void Awake()
    {
        _stasisConteiner = GetComponent<StasisConteinerWithArms>();
    }

    void OnEnable()
    {
        _isMoving = autoStart && waypoints != null && waypoints.Count > 0;

        if (waypoints.Count > 0 && waypoints[0] != null)
        {
            transform.position = waypoints[0].position;

            if (faceDirection && waypoints.Count > 1 && waypoints[1] != null)
            {
                Vector3 dir = (waypoints[1].position - waypoints[0].position);
                if (dir.sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }
    }

    void FixedUpdate()
    {
        if (_stasisConteiner != null && _stasisConteiner.isFreezed) return;
        if (!_isMoving || waypoints == null || waypoints.Count == 0) return;

        Transform targetWp = waypoints[_index];
        if (targetWp == null) return;

        Vector3 toTarget = targetWp.position - transform.position;
        float dist = toTarget.magnitude;

        // Espera en el waypoint
        if (dist <= arriveThreshold)
        {
            if (waitAtWaypoint > 0f)
            {
                _waitTimer += Time.fixedDeltaTime;
                if (_waitTimer < waitAtWaypoint) return;
                _waitTimer = 0f;
            }

            AdvanceIndex();
            if (!_isMoving) return;

            targetWp = waypoints[_index];
            toTarget = targetWp.position - transform.position;
            dist = toTarget.magnitude;
        }

        // Movimiento con transform
        if (dist > 0.0001f)
        {
            Vector3 step = toTarget.normalized * speed * Time.fixedDeltaTime;
            transform.position += step;

            // Rotación opcional hacia la dirección de avance
            if (faceDirection && step.sqrMagnitude > 1e-6f)
            {
                Quaternion targetRot = Quaternion.LookRotation(step.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
            }
        }
    }

    void AdvanceIndex()
    {
        switch (mode)
        {
            case PathMode.Loop:
                _index = (_index + 1) % waypoints.Count;
                break;

            case PathMode.PingPong:
                _index += _dir;
                if (_index >= waypoints.Count)
                {
                    _index = waypoints.Count - 2;
                    _dir = -1;
                }
                else if (_index < 0)
                {
                    _index = 1;
                    _dir = 1;
                }
                break;

            case PathMode.Once:
                if (_index < waypoints.Count - 1)
                    _index++;
                else
                    _isMoving = false; // terminó
                break;
        }
    }

    // Helpers
    public void Play()
    {
        if (waypoints == null || waypoints.Count == 0) return;
        _isMoving = true;
    }

    public void Pause() => _isMoving = false;

    public void RestartFromBeginning()
    {
        if (waypoints == null || waypoints.Count == 0) return;
        _index = 0;
        _dir = 1;
        _waitTimer = 0f;
        transform.position = waypoints[0].position;
        _isMoving = true;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || waypoints == null) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Count; i++)
        {
            var wp = waypoints[i];
            if (wp == null) continue;

            Gizmos.DrawSphere(wp.position, 0.06f);

            var next = GetNextIndexForGizmos(i);
            if (next >= 0 && next < waypoints.Count && waypoints[next] != null)
            {
                Gizmos.DrawLine(wp.position, waypoints[next].position);
            }
        }
    }

    int GetNextIndexForGizmos(int i)
    {
        if (waypoints.Count <= 1) return -1;
        switch (mode)
        {
            case PathMode.Loop: return (i + 1) % waypoints.Count;
            case PathMode.PingPong: return (i < waypoints.Count - 1) ? i + 1 : -1;
            case PathMode.Once: return (i < waypoints.Count - 1) ? i + 1 : -1;
            default: return -1;
        }
    }
}
