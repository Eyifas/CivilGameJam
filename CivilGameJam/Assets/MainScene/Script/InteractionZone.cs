using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class InteractionZone : MonoBehaviour
{
    [SerializeField] private GameObject interactionPrompt;

    private bool playerInside = false;

    private void Start()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerInside && Keyboard.current.eKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene("DrawerScene");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }
    }
}