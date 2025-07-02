using UnityEngine;
using DG.Tweening;

namespace Player.Camera
{
    public class PlayerCam : MonoBehaviour
    {
        [Header("Sensibilidad")]
        public float sens = 700f;

        [Header("Referencias")]
        public Transform orientation;  // Para girar el cuerpo/player
        public Transform camHolder;    // Padre de la cámara

        float xRotation;
        float yRotation;
        private void Start()
        {
            LockCursor();
        }
        private void Update()
        {
            // ESC siempre libera el cursor (para abrir menú, etc.)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                UnlockCursor();
            }

            // Clic izquierdo para volver a bloquear (solo si NO estamos en pausa)
            if (!PauseMenuManager.IsGamePaused &&
                Input.GetMouseButtonDown(0) &&
                Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }

            // Rotación de cámara sólo cuando el cursor está bloqueado
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

        // Ejemplo de efecto de cambio de FOV con DOTween
        public void DoFov(float endValue)
        {
            GetComponent<UnityEngine.Camera>().DOFieldOfView(endValue, 0.25f);
        }

        // Ejemplo de tilt/cabeceo de cámara con DOTween
        public void DoTilt(float zTilt)
        {
            transform.DOLocalRotate(new Vector3(0, 0, zTilt), 0.25f);
        }
    }
}
