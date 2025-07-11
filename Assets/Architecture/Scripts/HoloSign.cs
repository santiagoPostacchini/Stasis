using UnityEngine;

namespace Architecture.Scripts
{
    public class HoloSign : MonoBehaviour
    {
        public Transform playerTransform;
    
        private void LateUpdate()
        {
            transform.LookAt(2 * transform.position - playerTransform.position);
        }
    }
}
