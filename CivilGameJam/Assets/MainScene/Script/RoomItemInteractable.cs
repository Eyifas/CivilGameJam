using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum ItemPickupType
{
    Flashlight,
    FirstAidKit,
    RemovePainting
}

/// <summary>
/// Simple interactable component to place on items in the room:
/// - Flashlight (adds to InventoryManager & GameTimelineManager)
/// - First Aid Kit (adds to InventoryManager & GameTimelineManager)
/// - Heavy Painting (removes it from the wall safely to the floor)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RoomItemInteractable : MonoBehaviour
{
    [SerializeField] private ItemPickupType itemType;
    [SerializeField] private GameObject interactionPrompt; // e.g. "Press E to Pick Up / Remove"
    [SerializeField] private string promptLabel = "";
    [SerializeField] private GameObject itemVisualOnFloor;  // For painting: show it sitting safely on the floor

    public ItemPickupType ItemType => itemType;

    private bool playerInRange = false;
    private bool isCollectedOrRemoved = false;
    private bool isInteractable = true;

    private void Awake()
    {
        // Auto-find child prompt if not explicitly assigned
        if (interactionPrompt == null)
        {
            var child = transform.Find("InteractionPrompt");
            if (child == null) child = transform.Find("E Button");
            if (child != null) interactionPrompt = child.gameObject;
        }

        if (interactionPrompt != null)
        {
            var labelComp = interactionPrompt.GetComponent<InteractionPromptLabel>();
            if (labelComp == null) labelComp = interactionPrompt.AddComponent<InteractionPromptLabel>();
            if (!string.IsNullOrEmpty(promptLabel))
            {
                labelComp.SetLabel(promptLabel);
            }
            else
            {
                string def = itemType == ItemPickupType.Flashlight ? "Take Flashlight" :
                             itemType == ItemPickupType.FirstAidKit ? "Take First-Aid Kit" : "Remove Painting";
                promptLabel = def;
                labelComp.SetLabel(def);
            }

            if (interactionPrompt.TryGetComponent<Button>(out var btn))
            {
                btn.onClick.RemoveListener(PerformInteraction);
                btn.onClick.AddListener(PerformInteraction);
            }
            interactionPrompt.SetActive(false);
        }

        if (itemVisualOnFloor != null) itemVisualOnFloor.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollectedOrRemoved || !isInteractable) return;

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
        if (!isInteractable || isCollectedOrRemoved) return;

        // Stage guard: Only active during Stage 1
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.CurrentStage != GameStage.Stage1_Preparation)
        {
            SetInteractable(false);
            return;
        }

        if (playerInRange)
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                PerformInteraction();
            }
        }
    }

    public void PerformInteraction()
    {
        if (!isInteractable || isCollectedOrRemoved || !playerInRange) return;

        isCollectedOrRemoved = true;
        if (interactionPrompt != null) interactionPrompt.SetActive(false);

        switch (itemType)
        {
            case ItemPickupType.Flashlight:
                InventoryManager.AddItem("Flashlight");
                gameObject.SetActive(false); // Collected into backpack
                break;

            case ItemPickupType.FirstAidKit:
                InventoryManager.AddItem("FirstAidKit");
                gameObject.SetActive(false); // Collected into backpack
                break;

            case ItemPickupType.RemovePainting:
                if (GameTimelineManager.Instance != null)
                {
                    GameTimelineManager.Instance.SetPaintingRemoved(true);
                }
                // Hide the hanging painting, and optionally show it safely resting on floor
                if (TryGetComponent<SpriteRenderer>(out var sr)) sr.enabled = false;
                if (itemVisualOnFloor != null) itemVisualOnFloor.SetActive(true);
                break;
        }
    }

    public void SetInteractable(bool active)
    {
        isInteractable = active;
        if (!active)
        {
            playerInRange = false;
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }
    }

    // Called on timeline rewind to restore items to initial state
    public void ResetItem()
    {
        isCollectedOrRemoved = false;
        isInteractable = true;
        playerInRange = false;
        gameObject.SetActive(true);
        if (TryGetComponent<SpriteRenderer>(out var sr)) sr.enabled = true;
        if (itemVisualOnFloor != null) itemVisualOnFloor.SetActive(false);
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }

    public void RestoreState(bool collectedOrRemoved)
    {
        isCollectedOrRemoved = collectedOrRemoved;
        if (collectedOrRemoved)
        {
            if (itemType == ItemPickupType.RemovePainting)
            {
                if (TryGetComponent<SpriteRenderer>(out var sr)) sr.enabled = false;
                if (itemVisualOnFloor != null) itemVisualOnFloor.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }
        else
        {
            gameObject.SetActive(true);
            isInteractable = true;
            playerInRange = false;
            if (TryGetComponent<SpriteRenderer>(out var sr)) sr.enabled = true;
            if (itemVisualOnFloor != null) itemVisualOnFloor.SetActive(false);
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }
    }
}
