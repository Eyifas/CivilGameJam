using UnityEngine;

/// <summary>
/// Attach this component to an interactive object (like the Wall 1 Bookshelf)
/// to trigger a Timing Bar QTE when the player presses interaction key in range.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BookshelfAnchorInteractable : MonoBehaviour
{
    [Header("QTE Settings")]
    [SerializeField] private int requiredHits = 3;
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private GameObject visualSecuredIndicator; // Optional visual change (e.g. bracket visible)

    private bool playerInRange = false;
    private bool isAnchored = false;
    private bool isQTEActive = false;

    private void Start()
    {
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
        if (visualSecuredIndicator != null) visualSecuredIndicator.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isAnchored) return;

        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (interactionPrompt != null) interactionPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerInRange && !isAnchored && !isQTEActive)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                TriggerAnchorQTE();
            }
        }
    }

    private void TriggerAnchorQTE()
    {
        if (QTEManager.Instance == null)
        {
            Debug.LogError("BookshelfAnchorInteractable: No QTEManager found in scene!");
            return;
        }

        isQTEActive = true;
        if (interactionPrompt != null) interactionPrompt.SetActive(false);

        QTEManager.Instance.StartTimingQTE(
            hitsNeeded: requiredHits,
            onComplete: OnAnchorSuccess,
            onFail: OnAnchorFail
        );
    }

    private void OnAnchorSuccess()
    {
        isQTEActive = false;
        isAnchored = true;
        Debug.Log("Bookshelf securely anchored to the wall!");

        if (visualSecuredIndicator != null) visualSecuredIndicator.SetActive(true);
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }

    private void OnAnchorFail()
    {
        isQTEActive = false;
        Debug.Log("Failed to anchor the bookshelf in time!");
        if (playerInRange && interactionPrompt != null) interactionPrompt.SetActive(true);
    }
}

