using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this component to an interactive object (like the Wall 1 Bookshelf)
/// to trigger the Target Click QTE when the player presses interaction key or clicks in range.
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
    [SerializeField] private bool requireHammer = true;
    [SerializeField] private bool requireNails = true;

    [Header("UI & Visuals")]
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private GameObject visualSecuredIndicator; // Optional visual change (e.g. bracket visible)

    private bool playerInRange = false;
    private bool isAnchored = false;
    private bool isQTEActive = false;
    private bool isInteractable = true;

    private void Awake()
    {
        FindPrompt();

        if (interactionPrompt != null)
        {
            if (interactionPrompt.TryGetComponent<Button>(out var btn))
            {
                btn.onClick.RemoveListener(TriggerAnchorQTE);
                btn.onClick.AddListener(TriggerAnchorQTE);
            }
            interactionPrompt.SetActive(false);
        }

        if (visualSecuredIndicator != null) visualSecuredIndicator.SetActive(false);
    }

    private void FindPrompt()
    {
        if (interactionPrompt != null) return;

        var child = transform.Find("InteractionPrompt");
        if (child == null) child = transform.Find("E Button");
        if (child != null)
        {
            interactionPrompt = child.gameObject;
            return;
        }

        if (transform.parent != null)
        {
            var sibling = transform.parent.Find("InteractionPrompt");
            if (sibling == null) sibling = transform.parent.Find("E Button");
            if (sibling != null)
            {
                interactionPrompt = sibling.gameObject;
                return;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isAnchored || !isInteractable) return;

        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.CurrentStage != GameStage.Stage1_Preparation)
            return;

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
        if (!isInteractable || isAnchored || isQTEActive) return;

        // Stage guard: Only active during Stage 1
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.CurrentStage != GameStage.Stage1_Preparation)
        {
            SetInteractable(false);
            return;
        }

        if (playerInRange)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                TriggerAnchorQTE();
            }
        }
    }

    public void TriggerAnchorQTE()
    {
        if (!isInteractable || isAnchored || isQTEActive || !playerInRange) return;

        bool hasHammer = !requireHammer || InventoryManager.HasItem("Hammer");
        bool hasNails = !requireNails || InventoryManager.HasItem("Vida") || InventoryManager.HasItem("Screw") || InventoryManager.HasItem("Nail") || InventoryManager.HasItem("Nails");

        if (!hasHammer || !hasNails)
        {
            Debug.Log("[Bookshelf] You need tools (Hammer and Nails) from the drawer to anchor this!");
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

        InventoryManager.RemoveItem("Hammer");
        InventoryManager.RemoveItem("Vida");
        InventoryManager.RemoveItem("Nails");
        InventoryManager.RemoveItem("Nail");
        InventoryManager.RemoveItem("Screw");

        var ui = FindAnyObjectByType<InventoryUI>();
        if (ui != null)
        {
            ui.UpdateInventoryUI();
        }

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
        if (playerInRange && isInteractable && interactionPrompt != null) interactionPrompt.SetActive(true);
    }

    public void SetInteractable(bool active)
    {
        isInteractable = active;
        if (!active)
        {
            playerInRange = false;
            isQTEActive = false;
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }
    }

    public void ResetState()
    {
        isAnchored = false;
        isQTEActive = false;
        isInteractable = true;
        playerInRange = false;
        if (visualSecuredIndicator != null) visualSecuredIndicator.SetActive(false);
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }

    public void RestoreState(bool anchored)
    {
        isAnchored = anchored;
        isQTEActive = false;
        isInteractable = !anchored;
        playerInRange = false;
        if (visualSecuredIndicator != null) visualSecuredIndicator.SetActive(anchored);
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }
}
