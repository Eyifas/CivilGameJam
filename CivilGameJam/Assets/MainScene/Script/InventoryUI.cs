using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Image[] itemIcons;

    [Header("Item Sprites")]
    [SerializeField] private Sprite hammerSprite;
    [SerializeField] private Sprite vidaSprite;
    [SerializeField] private Sprite flashlightSprite;
    [SerializeField] private Sprite firstAidKitSprite;

    private int draggedSlotIndex = -1;
    private string draggedItemName = null;
    private GameObject dragGhost;
    private Image dragGhostImage;

    private void Awake()
    {
        SetupSlotDraggers();
    }

    private void Start()
    {
        SetupSlotDraggers();
        UpdateInventoryUI();
    }

    private void SetupSlotDraggers()
    {
        if (itemIcons == null) return;

        for (int i = 0; i < itemIcons.Length; i++)
        {
            if (itemIcons[i] == null) continue;

            var dragger = itemIcons[i].GetComponent<InventorySlotDrag>();
            if (dragger == null)
            {
                dragger = itemIcons[i].gameObject.AddComponent<InventorySlotDrag>();
            }
            dragger.Initialize(this, i);
        }
    }

    private void Update()
    {
        UpdateInventoryUI();
    }

    public void UpdateInventoryUI()
    {
        List<string> items = InventoryManager.GetItems();

        if (itemIcons == null) return;

        // Hide all slots first
        for (int i = 0; i < itemIcons.Length; i++)
        {
            if (itemIcons[i] != null)
            {
                // If this slot is currently being dragged, don't abruptly deactivate it
                if (i != draggedSlotIndex || i >= items.Count)
                {
                    itemIcons[i].gameObject.SetActive(false);
                }
            }
        }

        // Display collected items in order
        for (int i = 0; i < items.Count && i < itemIcons.Length; i++)
        {
            if (itemIcons[i] == null) continue;

            Sprite itemSprite = GetItemSprite(items[i]);
            if (itemSprite != null)
            {
                itemIcons[i].sprite = itemSprite;
                itemIcons[i].gameObject.SetActive(true);

                // Preserve dimming if currently dragging this slot
                if (i == draggedSlotIndex)
                {
                    itemIcons[i].color = new Color(1f, 1f, 1f, 0.4f);
                }
                else
                {
                    itemIcons[i].color = Color.white;
                }
            }
        }
    }

    public void OnSlotBeginDrag(int slotIndex, Vector2 screenPos)
    {
        List<string> items = InventoryManager.GetItems();
        if (slotIndex < 0 || slotIndex >= items.Count) return;

        draggedSlotIndex = slotIndex;
        draggedItemName = items[slotIndex];

        EnsureDragGhost();

        if (dragGhost != null && dragGhostImage != null)
        {
            Sprite sprite = GetItemSprite(draggedItemName);
            if (sprite != null)
            {
                dragGhostImage.sprite = sprite;
                dragGhost.transform.position = screenPos;
                dragGhost.SetActive(true);
                dragGhost.transform.SetAsLastSibling();
            }
        }

        if (slotIndex < itemIcons.Length && itemIcons[slotIndex] != null)
        {
            itemIcons[slotIndex].color = new Color(1f, 1f, 1f, 0.4f);
        }
    }

    public void OnSlotDrag(Vector2 screenPos)
    {
        if (dragGhost != null && dragGhost.activeSelf)
        {
            dragGhost.transform.position = screenPos;
        }
    }

    public void OnSlotEndDrag(int slotIndex, Vector2 screenPos)
    {
        if (dragGhost != null)
        {
            dragGhost.SetActive(false);
        }

        if (slotIndex >= 0 && slotIndex < itemIcons.Length && itemIcons[slotIndex] != null)
        {
            itemIcons[slotIndex].color = Color.white;
        }

        if (string.IsNullOrEmpty(draggedItemName))
        {
            draggedSlotIndex = -1;
            return;
        }

        string itemToDrop = draggedItemName;
        draggedItemName = null;
        draggedSlotIndex = -1;

        HandleItemDrop(itemToDrop, screenPos);
    }

    private void HandleItemDrop(string itemName, Vector2 screenPos)
    {
        // Check if BookshelfSceneController is present in the active scene
        var bookshelfCtrl = BookshelfSceneController.Instance != null 
            ? BookshelfSceneController.Instance 
            : Object.FindFirstObjectByType<BookshelfSceneController>();

        if (bookshelfCtrl != null)
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
                worldPos.z = 0f;

                if (bookshelfCtrl.IsInsideDropZone(worldPos))
                {
                    bool accepted = bookshelfCtrl.TryDropItemFromInventory(itemName, worldPos);
                    if (accepted)
                    {
                        // Item dropped onto the bookshelf: it disappears from the inventory!
                        InventoryManager.RemoveItem(itemName);
                        UpdateInventoryUI();
                        return;
                    }
                }
            }
        }

        // Outside valid drop zone or rejected: slot will restore automatically on UpdateInventoryUI
        UpdateInventoryUI();
    }

    private void EnsureDragGhost()
    {
        if (dragGhost != null) return;

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) rootCanvas = Object.FindFirstObjectByType<Canvas>();
        if (rootCanvas == null) return;

        dragGhost = new GameObject("InventoryDragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragGhost.transform.SetParent(rootCanvas.transform, false);
        dragGhost.transform.SetAsLastSibling();

        dragGhostImage = dragGhost.GetComponent<Image>();
        dragGhostImage.raycastTarget = false;
        dragGhostImage.preserveAspect = true;
        dragGhostImage.color = new Color(1f, 1f, 1f, 0.9f);

        RectTransform rt = dragGhost.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(90f, 90f);

        dragGhost.SetActive(false);
    }

    private Sprite GetItemSprite(string itemName)
    {
        switch (itemName)
        {
            case "Vida":
            case "Screw":
            case "Nail":
            case "Nails":
                 return vidaSprite;

            case "Hammer":
                return hammerSprite;

            case "Flashlight":
                return flashlightSprite;

            case "FirstAidKit":
            case "First-Aid Kit":
            case "First Aid Kit":
                return firstAidKitSprite;

            default:
                return null;
        }
    }
}