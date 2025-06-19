using UnityEngine;

namespace Player.Scripts.MVC
{
    public class Controller
    {
        private Model _model;
        private View _view;

        public void Init(Model model, View view)
        {
            this._model = model;
            this._view = view;
        }

        void Update()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _model.SetInput(h, v);

            if (Input.GetKeyDown(KeyCode.LeftControl))
                _model.TryCrouch(true);
            else if (Input.GetKeyUp(KeyCode.LeftControl))
                _model.TryCrouch(false);

            if (Input.GetKeyDown(KeyCode.Space))
                _model.TryJump();

            if (Input.GetKeyDown(KeyCode.Mouse0))
                _model.Shot();
            if (Input.GetKeyDown(KeyCode.E))
                _model.Grab();
            if (Input.GetKeyDown(KeyCode.Q))
                _model.Drop();
            if (Input.GetKeyDown(KeyCode.R))
                _model.Throw();

            _view.UpdateCameraHeight(
                _model.IsCrouching ? _model.CrouchCenterY : _model.StandCenterY,
                _model.EyeOffset
            );
        }
    }
}