using UnityEngine;

namespace Player.Scripts.MVC
{
    public class Controller : IController
    {
        private Model _model;
        private View _view;

        private KeyCode _crouchKey;
        private KeyCode _jumpKey;

        private float _v, _h;

        public Controller(Model model, View view, KeyCode crouchKey, KeyCode jumpKey)
        {
            _model = model;
            _view = view;
            
            _crouchKey = crouchKey;
            _jumpKey = jumpKey;

            _model.OnCrouch += _view.OnCrouchEvent;
            _model.OnJump += _view.OnJumpEvent;
            _model.OnLand += _view.OnLandEvent;
            _model.OnMove += _view.OnMoveEvent;
            _model.OnGetDamage += _view.OnDamageEvent;
            _model.OnVaultStart += _view.OnVaultStartEvent;
            _model.OnVaultEnd += _view.OnVaultEndEvent;
            _model.OnSpeedChange += _view.OnSpeedChangeEvent;
            _model.OnClimb += _view.OnClimbEvent;
        }
        
        public void OnUpdate()
        {
            _h = Input.GetAxis("Horizontal");
            _v = Input.GetAxis("Vertical");

            
            

            if (_model.isVaulting)
            {
                StateHandler();
                return;
            }

            _model.StateMachine(_v, Input.GetKey(_jumpKey));
            
            _model.UpdateMoveInput(_v, _h);
            
            if (Input.GetKeyDown(_crouchKey))
            {
                _model.UpdateCrouchInput(true);
            }

            if (Input.GetKeyUp(_crouchKey))
            {
                if (!_model.CanGetUp()) return;
                _model.UpdateCrouchInput(false);
            }

            _model.UpdateCrouchPosition();

            if (Input.GetKeyDown(_jumpKey))
            {
                _model.UpdateJumpInput();
            }
            
            StateHandler();
        }
        
        private void StateHandler()
        {
            if (_model.isVaulting)
                _model.StateUpdater(Model.MovementState.Vaulting);
            else if (Input.GetKey(KeyCode.LeftControl) || _model.iAmCrouching)
                _model.StateUpdater(Model.MovementState.Crouching);
            else if (_model.characterController.isGrounded)
                _model.StateUpdater(Model.MovementState.Moving);
            else if ((_model.wallLeft || _model.wallRight) && _v > 0 && _model.AboveGround() && !_model.exitingWall)
                _model.StateUpdater(Model.MovementState.Wallrunning);
            else
                _model.StateUpdater(Model.MovementState.Air);
                
        }
    }
}