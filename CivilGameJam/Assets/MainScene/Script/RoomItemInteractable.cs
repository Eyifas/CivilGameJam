using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private GameObject itemVisualOnFloor;  // For painting: show it sitting safely on the floor

    private bool playerInRange = false;
    private bool isCollectedOrRemoved = false;

    private void Start()
    {
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
        if (itemVisualOnFloor != null) itemVisualOnFloor.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollectedOrRemoved) return;

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
        if (playerInRange && !isCollectedOrRemoved)
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                PerformInteraction();
            }
        }
    }

    private void PerformInteraction()
    {
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

    // Called on timeline rewind to restore items to initial state
    public void ResetItem()
    {
        isCollectedOrRemoved = false;
        gameObject.SetActive(true);
        if (TryGetComponent<SpriteRenderer>(out var sr)) sr.enabled = true;
        if (itemVisualOnFloor != null) itemVisualOnFloor.SetActive(false);
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }
}
