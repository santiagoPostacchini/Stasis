using System;
using UnityEngine;

namespace Managers.Game
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public LayerMask triggerLayers;
        
        private void Awake()
        {
            Instance = this;
        }
    }
}
