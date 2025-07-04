namespace Player.Scripts.MVC
{
    public interface IController
    {
        void OnUpdate();

        bool IsCrouchPressed();
    }
}