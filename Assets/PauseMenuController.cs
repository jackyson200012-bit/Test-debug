using UnityEngine;
using UnityEngine.InputSystem;

namespace JayFos.Runtime
{
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject canvasObject;
        private bool isPaused = false;

        private void Start()
        {
            if (canvasObject == null)
            {
                Canvas canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    canvasObject = canvas.gameObject;
                }
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (isPaused)
                {
                    ResumeGame();
                }
                else
                {
                    PauseGame();
                }
            }
        }

        public void PauseGame()
        {
            isPaused = true;
            Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (canvasObject != null) canvasObject.SetActive(true);
        }

        public void ResumeGame()
        {
            isPaused = false;
            Time.timeScale = 1f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (canvasObject != null) canvasObject.SetActive(false);
        }
    }
}
