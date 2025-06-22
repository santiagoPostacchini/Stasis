using UnityEngine;
using DG.Tweening;

namespace Player.Camera
{
    public class PlayerCam : MonoBehaviour
    {
        public float sens = 700f;

        public Transform orientation;
        public Transform camHolder;
        

        float xRotation;
        float yRotation;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }


        private void Update()
        {
            float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sens;
            float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sens;

            yRotation += mouseX;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -75f, 75f);

            camHolder.rotation = Quaternion.Euler(xRotation, yRotation, 0);
            orientation.rotation = Quaternion.Euler(0, yRotation, 0);
        }
        
        public void DoFov(float endValue)
        {
            GetComponent<UnityEngine.Camera>().DOFieldOfView(endValue, 0.25f);
        }

        public void DoTilt(float zTilt)
        {
            transform.DOLocalRotate(new Vector3(0, 0, zTilt), 0.25f);
        }
    }
}
