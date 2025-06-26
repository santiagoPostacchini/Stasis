using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Player.Camera
{
    public class PauseMenuManager : MonoBehaviour
    {
        public static bool IsGamePaused = false;
        public GameObject pauseMenuUI;
        public GameObject confirmQuitUI;  // Segundo panel: confirmación

        void Awake()
        {
            confirmQuitUI.SetActive(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsGamePaused) Resume();
                else Pause();
            }
        }

        public void Resume()
        {
            pauseMenuUI.SetActive(false);
            confirmQuitUI.SetActive(false);
            Time.timeScale = 1f;
            IsGamePaused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Pause()
        {
            pauseMenuUI.SetActive(true);
            confirmQuitUI.SetActive(false);
            Time.timeScale = 0f;
            IsGamePaused = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ------------- Métodos para Quit -------------

        public void OnQuitPressed()
        {
            // Abre el panel de confirmación, cierra el de pausa
            confirmQuitUI.SetActive(true);
            pauseMenuUI.SetActive(false);
        }

        public void OnQuitYes()
        {
            #if UNITY_STANDALONE
                        Application.Quit();
            #endif

            #if UNITY_EDITOR
                        EditorApplication.isPlaying = false;
            #endif
        }

        public void OnQuitNo()
        {
            // Vuelve al menú de pausa
            confirmQuitUI.SetActive(false);
            pauseMenuUI.SetActive(true);
        }
    }
}
