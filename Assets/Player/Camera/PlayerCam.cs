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
            LockCursor();
        }

        private void Update()
        {
            // Si presionás ESC siempre liberás el cursor (incluso si estás pausado)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                UnlockCursor();
            }

            // Solo bloqueás con clic izquierdo si NO estás en pausa
            if (!PauseMenuManager.IsGamePaused &&
                Input.GetMouseButtonDown(0) &&
                Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }

            // Rotación solo cuando el cursor está bloqueado
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sens;
                float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sens;

                yRotation += mouseX;
                xRotation -= mouseY;
                xRotation = Mathf.Clamp(xRotation, -75f, 75f);

                camHolder.rotation = Quaternion.Euler(xRotation, yRotation, 0);
                orientation.rotation = Quaternion.Euler(0, yRotation, 0);
            }
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
