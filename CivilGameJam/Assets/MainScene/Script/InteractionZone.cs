using UnityEngine;

public class InteractionZone : MonoBehaviour
{
    [SerializeField] private GameObject interactionPrompt;

    private void Start()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && interactionPrompt != null)
        {
            interactionPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }
}