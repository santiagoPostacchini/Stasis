using Player.FullBody_Scripts.MovementFSM;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class Controller : IController
    {
        private Model _model;
        private View _view;
        
        public Controller(Model model, View view)
        {
            _model = model;
            _view = view;
            
            _model.OnLand += _view.OnLandEvent;
            _model.OnJump += _view.OnJumpEvent;
            _model.OnShoot += _view.OnShootEvent;
            _model.OnMove += _view.OnMove;
            _model.OnRun += _view.OnRun;
            _model.OnStop += _view.OnStop;
            _model.OnCrouchStart += _view.OnCrouchStartEvent;
            _model.OnCrouchEnd += _view.OnCrouchEndEvent;
            _model.OnVaultStart += _view.OnVaultStartEvent;
            _model.OnVaultEnd += _view.OnVaultEndEvent;
            _model.OnClimbStart += _view.OnClimbStartEvent;
            _model.OnClimbEnd += _view.OnClimbEndEvent;
            _model.OnSlideStart += _view.OnSlideStartEvent;
            _model.OnSlideEnd += _view.OnSlideEndEvent;
            _model.OnGetDamage += _view.OnDamageEvent;
            _model.OnDeath += _view.OnDeathEvent;
        }
        
        public void OnUpdate()
        {
            float xAxis = Mathf.Clamp(Input.GetAxis("Horizontal"), -1f, 1f);
            float zAxis = Mathf.Clamp(Input.GetAxis("Vertical"), -1f, 1f);
            
            float rawXAxis = Mathf.Clamp(Input.GetAxisRaw("Horizontal"), -1f, 1f);
            float rawZAxis = Mathf.Clamp(Input.GetAxisRaw("Vertical"), -1f, 1f);
            
            _model.UpdateAxisInput(xAxis, zAxis, rawXAxis, rawZAxis);

            _model.UpdateRunKey(Input.GetKey(_model.runningKey));

            if (Input.GetKeyDown(_model.jumpKey))
            {
                _model.RegisterJumpDownThisFrame(); // <— NUEVO
                _model.JumpInput();                  // dispara el evento (si lo usás)
                _model.BufferJumpNow();  
            }
            
            if (Input.GetKeyDown(_model.mouseLeft))
            {
                _model.ShootInput();
            }
            
            
        }
    }
}