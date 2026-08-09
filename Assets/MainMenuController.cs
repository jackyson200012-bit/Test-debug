using UnityEngine;
using UnityEngine.SceneManagement;

namespace JayFos.Runtime
{
    public class MainMenuController : MonoBehaviour
    {
        public void StartGame()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
            }

            SceneManager.LoadScene(1);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            Application.Quit();
            UnityEditor.EditorApplication.isPlaying = false;
#else
            if (Application.isEditor)
            {
                UnityEditor.EditorApplication.isPlaying = false;
            }
            else
            {
                Application.Quit();
            }
#endif
        }
    }
}
