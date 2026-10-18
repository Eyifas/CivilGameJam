using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InventorySelection : MonoBehaviour
{
    private static InventorySelection instance;

    [SerializeField] private RectTransform selectionFrame;
    [SerializeField] private RectTransform[] itemSlots;

    private int selectedIndex = 0;

    public static string SelectedItem { get; private set; } = "";
    public static int SelectedIndex => instance != null ? instance.selectedIndex : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticData()
    {
        SelectedItem = "";
        instance = null;
    }

    private void Awake()
    {
        instance = this;
        ConfigureSelectionFrame();
        ResetSelection();
    }

    private void ConfigureSelectionFrame()
    {
        if (selectionFrame != null)
        {
            selectionFrame.anchorMin = new Vector2(0.5f, 0.5f);
            selectionFrame.anchorMax = new Vector2(0.5f, 0.5f);
            selectionFrame.pivot = new Vector2(0.5f, 0.5f);
            selectionFrame.sizeDelta = new Vector2(64f, 62f);
            selectionFrame.localScale = Vector3.one;

            var img = selectionFrame.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.raycastTarget = false;
            }

            selectionFrame.gameObject.SetActive(false);
        }
    }

    public void RegisterAdditionalSlot(RectTransform slot)
    {
        if (slot == null) return;
        List<RectTransform> list = itemSlots != null ? new List<RectTransform>(itemSlots) : new List<RectTransform>();
        if (!list.Contains(slot))
        {
            list.Add(slot);
            itemSlots = list.ToArray();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static void ResetSelection()
    {
        SelectedItem = "";
        if (instance != null)
        {
            instance.selectedIndex = 0;
            if (instance.selectionFrame != null)
            {
                instance.selectionFrame.gameObject.SetActive(false);
            }
        }
    }

    public static void SelectSlot(int index)
    {
        if (instance == null) return;
        List<string> items = InventoryManager.GetItems();
        if (index >= 0 && index < items.Count)
        {
            instance.selectedIndex = index;
            SelectedItem = items[index];
            instance.UpdateFramePosition();
        }
    }

    public static bool IsItemSelected(string itemName)
    {
        if (string.IsNullOrEmpty(SelectedItem) || string.IsNullOrEmpty(itemName)) return false;
        return SelectedItem.Equals(itemName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void Update()
    {
        List<string> items = InventoryManager.GetItems();

        if (items.Count == 0)
        {
            if (selectionFrame != null && selectionFrame.gameObject.activeSelf)
            {
                selectionFrame.gameObject.SetActive(false);
            }
            SelectedItem = "";
            return;
        }

        // Minecraft hotbar style navigation: Left/Right, Up/Down, Number keys 1-5, and Mouse Scroll Wheel
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex + 1) % items.Count;
            }
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
            }

            // Direct slot selection with numbers 1-5 (Minecraft hotbar)
            if (Keyboard.current.digit1Key.wasPressedThisFrame && items.Count > 0) selectedIndex = 0;
            else if (Keyboard.current.digit2Key.wasPressedThisFrame && items.Count > 1) selectedIndex = 1;
            else if (Keyboard.current.digit3Key.wasPressedThisFrame && items.Count > 2) selectedIndex = 2;
            else if (Keyboard.current.digit4Key.wasPressedThisFrame && items.Count > 3) selectedIndex = 3;
            else if (Keyboard.current.digit5Key.wasPressedThisFrame && items.Count > 4) selectedIndex = 4;
        }

        // Mouse scroll wheel support (Minecraft hotbar)
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll < -0.1f)
            {
                selectedIndex = (selectedIndex + 1) % items.Count;
            }
            else if (scroll > 0.1f)
            {
                selectedIndex = (selectedIndex - 1 + items.Count) % items.Count;
            }
        }

        if (selectedIndex >= items.Count)
        {
            selectedIndex = items.Count - 1;
        }

        SelectedItem = items[selectedIndex];
        UpdateFramePosition();
    }

    private void UpdateFramePosition()
    {
        if (selectionFrame == null) return;

        if (selectedIndex >= 0 && itemSlots != null && selectedIndex < itemSlots.Length && itemSlots[selectedIndex] != null)
        {
            selectionFrame.gameObject.SetActive(true);
            selectionFrame.anchoredPosition = itemSlots[selectedIndex].anchoredPosition;
        }
        else
        {
            selectionFrame.gameObject.SetActive(false);
        }
    }
}