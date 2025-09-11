using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Vault : IState
    {
        private readonly FSM _fsm;
        private readonly Model _m;
        
        private Rigidbody _rb;
        private Vector3 _startPos;
        private Vector3 _endPos;
        private float _duration;
        private float _t;
        private float _apexHeight;
        
        private const float BaseSpeed = 6.5f;
        private const float MinTime   = 0.20f;
        private const float MaxTime   = 0.50f;
        private const float ApexBoost = 0.35f;

        private bool _oldUseGravity;

        public S_Vault(FSM fsm, Model model)
        {
            _fsm = fsm;
            _m   = model;
        }

        public void OnEnter()
        {
            _rb = _m.rb;

            // Leemos el último probe
            var p = _m.probe;

            // Punto de salida (actual, pies/cuerpo)
            _startPos = _rb.position;

            // Punto de aterrizaje sugerido por scanner (ya garantizado con clearance)
            _endPos = p.vaultLandPoint;

            // Duración en base a la distancia horizontal
            float dist = Vector3.Distance(new Vector3(_startPos.x, 0f, _startPos.z),
                                          new Vector3(_endPos.x,   0f, _endPos.z));
            _duration = Mathf.Clamp(dist / Mathf.Max(0.1f, BaseSpeed), MinTime, MaxTime);

            // Altura del arco: en función de la altura del obstáculo + boost
            // Si agregaste playerRadius/height al probe, podés usar p.playerRadius aquí.
            _apexHeight = Mathf.Max(0.25f, p.obstacleHeight * 0.5f) + ApexBoost;

            // Preparar rigidbody
            _oldUseGravity = _rb.useGravity;
            _rb.useGravity = false;
            _rb.velocity   = Vector3.zero;

            // Rotación opcional hacia la dirección del vault
            Vector3 dir = (_endPos - _startPos); dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion face = Quaternion.LookRotation(dir.normalized, Vector3.up);
                _m.transform.rotation = face;
            }

            _m.canMove = false; // que no meta fuerzas el grounded
            _m.VaultStartEvent();
            // Opcional: _m.View?.PlayVaultAnim(p.obstacleHeight);
        }

        public void OnUpdate()
        {
            _t += Time.deltaTime / _duration;
            float u = Mathf.Clamp01(_t);

            // Trayectoria: interpolación cúbica simple con "apex"
            // posición horizontal: Lerp
            Vector3 horiz = Vector3.Lerp(_startPos, _endPos, EaseOutIn(u)); // easing suave
            // altura: parabólica con apex en la mitad
            float y0 = _startPos.y;
            float y1 = _endPos.y;
            float apex = Mathf.Max(y0, y1) + _apexHeight;

            float y = Parabola(u, y0, apex, y1);

            Vector3 target = new Vector3(horiz.x, y, horiz.z);

            // Movemos por posición (suave, sin golpes de física)
            _rb.MovePosition(target);

            // Fin
            if (u >= 1f)
            {
                ExitToGrounded();
            }
        }

        public void OnFixedUpdate() { /* no hace falta; todo en Update por MovePosition */ }

        public void OnExit()
        {
            // restaurar rigidbody
            _rb.useGravity = _oldUseGravity;
            _m.canMove     = true;
            _m.VaultEndEvent();
        }

        // ---------------- helpers ----------------

        // parabola con apex en t=0.5 (Bézier cuadrática equivalente)
        private float Parabola(float t, float y0, float apex, float y1)
        {
            // Q(t) = (1-2t+ t^2) y0 + (2t - 2t^2) apex + (t^2) y1
            float tt = t * t;
            float omt = 1f - t;
            float omt2 = omt * omt;
            return omt2 * y0 + 2f * omt * t * apex + tt * y1;
        }

        // Easing suave: sale rápido, llega suave
        private float EaseOutIn(float t)
        {
            // out-in cúbico sencillo
            if (t < 0.5f)
            {
                float x = t * 2f;
                return 0.5f * (1f - Mathf.Pow(1f - x, 3f));
            }
            else
            {
                float x = (t - 0.5f) * 2f;
                return 0.5f + 0.5f * (x * x * x);
            }
        }

        private void ExitToGrounded()
        {
            // Asegura una pequeña velocidad hacia adelante al salir (feel)
            Vector3 fwd = _m.cameraHolderTransform
                ? _m.cameraHolderTransform.forward
                : _m.transform.forward;
            fwd.y = 0f; fwd.Normalize();

            _rb.velocity = fwd * Mathf.Max(_m.walkingSpeed, 3.5f);

            // Opcional: pequeño snap al suelo si estamos muy cerca (evita caer 2-3 cm)
            if (Physics.Raycast(_rb.position + Vector3.up * 0.2f, Vector3.down,
                                out var down, 0.6f, _m.groundMask))
            {
                _rb.MovePosition(new Vector3(_rb.position.x, down.point.y, _rb.position.z));
            }

            _fsm.ChangeState(FSM.States.Grounded);
        }
    }
}