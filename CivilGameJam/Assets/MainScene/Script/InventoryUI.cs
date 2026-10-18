using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Image[] itemIcons;

    [Header("Hotbar Panel Setup")]
    [SerializeField] private Image panelImage;
    [SerializeField] private Sprite panelSprite;

    [Header("Item Sprites")]
    [SerializeField] private Sprite hammerSprite;
    [SerializeField] private Sprite vidaSprite;
    [SerializeField] private Sprite flashlightSprite;
    [SerializeField] private Sprite firstAidKitSprite;
    [SerializeField] private Sprite keySprite;

    private int draggedSlotIndex = -1;
    private string draggedItemName = null;
    private GameObject dragGhost;
    private Image dragGhostImage;

    private void Awake()
    {
        EnsureValidFlashlightSprite();
        EnsureValidKeySprite();
        ConfigureHotbarLayout();
        SetupSlotDraggers();
    }

    private void Start()
    {
        ConfigureHotbarLayout();
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

        HandleItemDrop(itemToDrop, slotIndex, screenPos);
    }

    private void HandleItemDrop(string itemName, int slotIndex, Vector2 screenPos)
    {
        // 1. Flashlight: When dragged into the scene, equip it under character's armpit and turn it on!
        if (IsFlashlightItem(itemName))
        {
            bool droppedInScene = true;
            if (slotIndex >= 0 && slotIndex < itemIcons.Length && itemIcons[slotIndex] != null)
            {
                RectTransform slotRect = itemIcons[slotIndex].rectTransform;
                if (slotRect != null && RectTransformUtility.RectangleContainsScreenPoint(slotRect, screenPos))
                {
                    droppedInScene = false;
                }
            }

            if (droppedInScene)
            {
                PlayerMovement.SetFlashlightEquipped(true);
                AudioManager.PlayFlashlightToggle(true);
                Debug.Log("<color=yellow>[Flashlight] Flashlight dragged into scene! Equipped in hand.</color>");
            }

            // As explicitly requested by the user: Flashlight DOES NOT disappear when dragged into the scene!
            UpdateInventoryUI();
            return;
        }

        // 2. Bookshelf: Anchoring items
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

        // 3. Door: Key insertion
        var doorCtrl = DoorSceneController.Instance != null 
            ? DoorSceneController.Instance 
            : Object.FindFirstObjectByType<DoorSceneController>();

        if (doorCtrl != null)
        {
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
                worldPos.z = 0f;

                if (doorCtrl.IsInsideDropZone(worldPos))
                {
                    bool accepted = doorCtrl.TryDropKeyFromInventory(itemName, worldPos);
                    if (accepted)
                    {
                        // Key inserted into door lock: disappears from inventory!
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

    public static bool IsFlashlightItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return false;
        return itemName.Equals("Flashlight", System.StringComparison.OrdinalIgnoreCase) ||
               itemName.Equals("fener", System.StringComparison.OrdinalIgnoreCase) ||
               itemName.Equals("Torch", System.StringComparison.OrdinalIgnoreCase);
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

        // Visual Guidance Badge under dragged item
        var labelObj = new GameObject("DragLabel");
        labelObj.transform.SetParent(dragGhost.transform, false);
        var lRect = labelObj.AddComponent<RectTransform>();
        lRect.anchorMin = new Vector2(0.5f, 0f);
        lRect.anchorMax = new Vector2(0.5f, 0f);
        lRect.pivot = new Vector2(0.5f, 1f);
        lRect.anchoredPosition = new Vector2(0f, -8f);
        lRect.sizeDelta = new Vector2(160f, 28f);

        var bg = labelObj.AddComponent<Image>();
        bg.sprite = InteractionVisualCue.GetPillBadgeSprite();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        bg.raycastTarget = false;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(labelObj.transform, false);
        var tRect = textObj.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.sizeDelta = Vector2.zero;
        var txt = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        txt.fontSize = 13f;
        txt.fontStyle = TMPro.FontStyles.Bold;
        txt.alignment = TMPro.TextAlignmentOptions.Center;
        txt.color = new Color(1f, 0.85f, 0.25f, 1f);
        txt.text = "DROP ON TARGET";
        txt.raycastTarget = false;

        dragGhost.SetActive(false);
    }

    public void ConfigureHotbarLayout()
    {
        // 1. Configure panel background image
        if (panelImage == null) panelImage = GetComponent<Image>();
        if (panelImage != null)
        {
            Sprite spr = panelSprite ?? GetPanelSprite();
            if (spr != null)
            {
                panelImage.sprite = spr;
            }
            panelImage.color = Color.white;
            panelImage.preserveAspect = true;
            panelImage.type = Image.Type.Simple;
        }

        // 2. Position hotbar at center of very bottom of the screen (like Minecraft)
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(472f, 136f);
            rt.anchoredPosition = new Vector2(0f, 78f);
        }

        // 3. Ensure 5 slots exist
        float[] slotXOffsets = new float[] { -171f, -85f, 0f, 85f, 171f };
        if (itemIcons != null && itemIcons.Length == 4 && itemIcons[3] != null)
        {
            Transform existingSlot5 = transform.Find("Item5");
            GameObject slot5Obj = existingSlot5 != null ? existingSlot5.gameObject : Instantiate(itemIcons[3].gameObject, transform);
            slot5Obj.name = "Item5";
            slot5Obj.SetActive(false);
            Image slot5Img = slot5Obj.GetComponent<Image>();

            List<Image> iconList = new List<Image>(itemIcons);
            if (!iconList.Contains(slot5Img))
            {
                iconList.Add(slot5Img);
                itemIcons = iconList.ToArray();
            }

            InventorySelection invSel = GetComponent<InventorySelection>();
            if (invSel != null)
            {
                invSel.RegisterAdditionalSlot(slot5Obj.GetComponent<RectTransform>());
            }
        }

        // 4. Align all slots to match the wooden boxes of panel.png
        if (itemIcons != null)
        {
            for (int i = 0; i < itemIcons.Length && i < slotXOffsets.Length; i++)
            {
                if (itemIcons[i] == null) continue;
                RectTransform slotRt = itemIcons[i].rectTransform;
                if (slotRt != null)
                {
                    slotRt.anchorMin = new Vector2(0.5f, 0.5f);
                    slotRt.anchorMax = new Vector2(0.5f, 0.5f);
                    slotRt.pivot = new Vector2(0.5f, 0.5f);
                    slotRt.anchoredPosition = new Vector2(slotXOffsets[i], 0f);
                    slotRt.sizeDelta = new Vector2(52f, 52f);
                    slotRt.localScale = Vector3.one;
                }
                itemIcons[i].preserveAspect = true;
            }
        }
    }

    private static Sprite s_panelSprite;
    public static Sprite GetPanelSprite()
    {
        if (s_panelSprite != null) return s_panelSprite;

        // 1. Try Resources.Load
        s_panelSprite = Resources.Load<Sprite>("UI/panel") ?? Resources.Load<Sprite>("panel");
        if (s_panelSprite != null) return s_panelSprite;

        var allRes = Resources.LoadAll<Sprite>("UI/panel");
        if (allRes != null && allRes.Length > 0 && allRes[0] != null)
        {
            s_panelSprite = allRes[0];
            return s_panelSprite;
        }

#if UNITY_EDITOR
        // 2. Try AssetDatabase in Editor
        string[] paths = new[]
        {
            "Assets/MainScene/panel.png",
            "Assets/Resources/UI/panel.png",
            "Assets/Resources/panel.png"
        };
        foreach (var p in paths)
        {
            var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s != null)
            {
                s_panelSprite = s;
                return s_panelSprite;
            }
            var allS = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p);
            if (allS != null)
            {
                foreach (var a in allS)
                {
                    if (a is Sprite spr)
                    {
                        s_panelSprite = spr;
                        return s_panelSprite;
                    }
                }
            }
        }
#endif

        // 3. Fallback: load directly from disk bytes
        string diskPath = System.IO.Path.Combine(Application.dataPath, "MainScene/panel.png");
        if (!System.IO.File.Exists(diskPath))
        {
            diskPath = System.IO.Path.Combine(Application.dataPath, "Resources/UI/panel.png");
        }
        if (System.IO.File.Exists(diskPath))
        {
            try
            {
                byte[] data = System.IO.File.ReadAllBytes(diskPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                if (tex.LoadImage(data))
                {
                    Rect rect = (tex.width == 256 && tex.height == 256)
                        ? new Rect(10, 94, 236, 68)
                        : new Rect(0, 0, tex.width, tex.height);
                    s_panelSprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
                    return s_panelSprite;
                }
            }
            catch { }
        }

        return null;
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

    private void EnsureValidKeySprite()
    {
        if (keySprite == null)
        {
            keySprite = GetKeySprite();
        }
    }

    private static Sprite s_keySprite;
    public static Sprite GetKeySprite()
    {
        if (s_keySprite != null) return s_keySprite;

        // 1. Try Resources.Load
        s_keySprite = Resources.Load<Sprite>("Anahtar") ??
                      Resources.Load<Sprite>("Key") ??
                      Resources.Load<Sprite>("anahtar1");
        if (s_keySprite != null) return s_keySprite;

        var allRes = Resources.LoadAll<Sprite>("Anahtar");
        if (allRes != null && allRes.Length > 0 && allRes[0] != null)
        {
            s_keySprite = allRes[0];
            return s_keySprite;
        }

#if UNITY_EDITOR
        // 2. Try AssetDatabase in Editor
        string[] paths = new[]
        {
            "Assets/MainScene/Sprite/anahtar1.png",
            "Assets/Resources/Anahtar.png",
            "Assets/Resources/Key.png"
        };
        foreach (var p in paths)
        {
            var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s != null)
            {
                s_keySprite = s;
                return s_keySprite;
            }
            var allS = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p);
            if (allS != null)
            {
                foreach (var a in allS)
                {
                    if (a is Sprite spr)
                    {
                        s_keySprite = spr;
                        return s_keySprite;
                    }
                }
            }
        }
#endif

        // 3. Fallback: load directly from disk bytes
        string diskPath = System.IO.Path.Combine(Application.dataPath, "MainScene/Sprite/anahtar1.png");
        if (!System.IO.File.Exists(diskPath))
        {
            diskPath = System.IO.Path.Combine(Application.dataPath, "Resources/Anahtar.png");
        }
        if (System.IO.File.Exists(diskPath))
        {
            try
            {
                byte[] data = System.IO.File.ReadAllBytes(diskPath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                if (tex.LoadImage(data))
                {
                    Rect rect = (tex.width == 128 && tex.height == 128)
                        ? new Rect(30, 11, 94, 88)
                        : new Rect(0, 0, tex.width, tex.height);
                    s_keySprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
                    return s_keySprite;
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

            case "Anahtar":
            case "Key":
            case "anahtar1":
            case "DrawerKey":
                if (keySprite != null) return keySprite;
                return GetKeySprite();

            default:
                return null;
        }
    }
}