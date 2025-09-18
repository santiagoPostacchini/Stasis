using UnityEngine;

namespace IKSuite
{
    // Tus scripts de bone pueden implementar esta interfaz
    // para recibir la referencia del "Stasis Tip Controller".
    public interface IStasisPartIK
    {
        void SetTipController(Component tipController);
    }
}