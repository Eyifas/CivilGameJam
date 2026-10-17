using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class ReturnToRoom : MonoBehaviour
{
    [SerializeField] private string returnSceneName;

    private bool canExit = false;

    private void Update()
    {
        // Don't allow exiting room if player is currently in the middle of a QTE
        if (QTEManager.Instance != null && QTEManager.Instance.IsAnyQTEActive)
        {
            return;
        }

        if (!canExit)
        {
            if (Keyboard.current != null && !Keyboard.current.eKey.isPressed)
            {
                canExit = true;
            }

            return;
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(returnSceneName);
        }
    }
}