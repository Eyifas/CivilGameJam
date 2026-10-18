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
        EnsureValidFlashlightSprite();
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

    private void EnsureValidFlashlightSprite()
    {
        if (flashlightSprite == null || IsPlaceholderSprite(flashlightSprite))
        {
            var realFener = GetFlashlightSprite();
            if (realFener != null)
            {
                flashlightSprite = realFener;
            }
        }
    }

    private static bool IsPlaceholderSprite(Sprite spr)
    {
        if (spr == null) return true;
        string n = spr.name;
        return n.IndexOf("hammer", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               n.IndexOf("interact", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Sprite s_fenerSprite;
    public static Sprite GetFlashlightSprite()
    {
        if (s_fenerSprite != null) return s_fenerSprite;

        // 1. Try Resources.Load
        s_fenerSprite = Resources.Load<Sprite>("Flashlight") ?? Resources.Load<Sprite>("fener");
        if (s_fenerSprite != null) return s_fenerSprite;

        // 2. Try Resources.LoadAll
        var all = Resources.LoadAll<Sprite>("Flashlight");
        if (all != null && all.Length > 0 && all[0] != null)
        {
            s_fenerSprite = all[0];
            return s_fenerSprite;
        }
        all = Resources.LoadAll<Sprite>("fener");
        if (all != null && all.Length > 0 && all[0] != null)
        {
            s_fenerSprite = all[0];
            return s_fenerSprite;
        }

#if UNITY_EDITOR
        // 3. Try AssetDatabase in Editor
        string[] paths = new[]
        {
            "Assets/MainScene/Sprite/fener.png",
            "Assets/Resources/Flashlight.png",
            "Assets/Resources/fener.png"
        };
        foreach (var p in paths)
        {
            var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s != null && !IsPlaceholderSprite(s))
            {
                s_fenerSprite = s;
                return s_fenerSprite;
            }
            var allS = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p);
            if (allS != null)
            {
                foreach (var a in allS)
                {
                    if (a is Sprite spr && !IsPlaceholderSprite(spr))
                    {
                        s_fenerSprite = spr;
                        return s_fenerSprite;
                    }
                }
            }
        }
#endif

        // 4. File-system byte loader fallback (guaranteed to never fail if file exists on disk)
        string diskPath = System.IO.Path.Combine(Application.dataPath, "MainScene/Sprite/fener.png");
        if (!System.IO.File.Exists(diskPath))
        {
            diskPath = System.IO.Path.Combine(Application.dataPath, "Resources/Flashlight.png");
        }
        if (System.IO.File.Exists(diskPath))
        {
            try
            {
                byte[] fileData = System.IO.File.ReadAllBytes(diskPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(fileData))
                {
                    s_fenerSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    return s_fenerSprite;
                }
            }
            catch { }
        }

        return null;
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
            case "fener":
            case "Torch":
                if (flashlightSprite != null && !IsPlaceholderSprite(flashlightSprite))
                {
                    return flashlightSprite;
                }
                return GetFlashlightSprite() ?? flashlightSprite;

            case "FirstAidKit":
            case "First-Aid Kit":
            case "First Aid Kit":
                return firstAidKitSprite;

            default:
                return null;
        }
    }
}