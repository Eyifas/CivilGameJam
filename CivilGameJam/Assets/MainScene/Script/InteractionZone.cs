using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class InteractionZone : MonoBehaviour
{
    [SerializeField] private string sceneName;

    private bool playerInside = false;

    private void Update()
    {
        if (playerInside && Keyboard.current.eKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;

            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.EnterZone();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;

            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.ExitZone();
            }
        }
    }
}