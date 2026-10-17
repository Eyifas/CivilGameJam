using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class ReturnToRoom : MonoBehaviour
{
    [SerializeField] private string returnSceneName;

    private bool canExit = false;

    private void Update()
    {
        if (!canExit)
        {
            if (!Keyboard.current.eKey.isPressed)
            {
                canExit = true;
            }

            return;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(returnSceneName);
        }
    }
}