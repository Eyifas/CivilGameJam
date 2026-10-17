using UnityEngine;

/// <summary>
/// Attach this component to an interactive object (like the Wall 1 Bookshelf)
/// to trigger the Target Click QTE when the player presses interaction key in range.
/// Successfully completing the QTE anchors the bookshelf and informs GameTimelineManager.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BookshelfAnchorInteractable : MonoBehaviour
{
    [Header("QTE Settings")]
    [Tooltip("Number of anchor targets to hit in the QTE.")]
    [SerializeField] private int requiredHits = 3;
    [SerializeField] private float timePerTarget = 2.0f;

    [Header("Requirements (Optional)")]
    [SerializeField] private bool requireHammer = false;

    [Header("UI & Visuals")]
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
        if (requireHammer && !InventoryManager.HasItem("Hammer"))
        {
            Debug.Log("[Bookshelf] You need tools (Hammer) from the drawer to anchor this!");
            return;
        }

        if (QTEManager.Instance == null)
        {
            Debug.LogError("BookshelfAnchorInteractable: No QTEManager found in scene!");
            return;
        }

        isQTEActive = true;
        if (interactionPrompt != null) interactionPrompt.SetActive(false);

        // Uses the fading Target Click QTE (random positions and random keys)
        QTEManager.Instance.StartTargetClickQTE(
            targetCount: requiredHits,
            timeLimitPerTarget: timePerTarget,
            onComplete: OnAnchorSuccess,
            onFail: OnAnchorFail
        );
    }

    private void OnAnchorSuccess()
    {
        isQTEActive = false;
        isAnchored = true;
        Debug.Log("<color=green>[Bookshelf] Bookshelf securely anchored to the wall!</color>");

        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.SetBookshelfAnchored(true);
        }

        if (visualSecuredIndicator != null) visualSecuredIndicator.SetActive(true);
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }

    private void OnAnchorFail()
    {
        isQTEActive = false;
        Debug.Log("<color=red>[Bookshelf] Failed to anchor the bookshelf in time!</color>");
        if (playerInRange && interactionPrompt != null) interactionPrompt.SetActive(true);
    }

    public void ResetState()
    {
        isAnchored = false;
        isQTEActive = false;
        if (visualSecuredIndicator != null) visualSecuredIndicator.SetActive(false);
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }
}
