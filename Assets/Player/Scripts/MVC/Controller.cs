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
        }
        
        public void OnUpdate()
        {
            _h = Input.GetAxis("Horizontal");
            _v = Input.GetAxis("Vertical");
            
            _model.UpdateMoveInput(_v, _h);
            
            if (Input.GetKeyDown(_crouchKey))
            {
                _model.UpdateCrouchInput(true);
            }

            if (Input.GetKeyUp(_crouchKey))
            {
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
            if (Input.GetKey(KeyCode.LeftControl))
                _model.StateUpdater(Model.MovementState.Crouching);
            else if (_model.characterController.isGrounded)
                _model.StateUpdater(Model.MovementState.Moving);
            else
                _model.StateUpdater(Model.MovementState.Air);
        }
    }
}