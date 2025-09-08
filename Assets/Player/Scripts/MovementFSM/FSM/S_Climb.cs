using Player.Scripts.MovementFSM.MVC;

namespace Player.Scripts.MovementFSM
{
    public class S_Climb : IState
    {
        private FSM _fsm;
        private Model _model;

        public S_Climb(FSM fsm, Model model)
        {
            _fsm = fsm;
            _model = model;
        }

        public void OnEnter()
        {
            
        }
        
        public void OnUpdate()
        {

        }
        
        public void OnFixedUpdate()
        {
            
        }

        public void OnExit()
        {
        
        }
    }
}
