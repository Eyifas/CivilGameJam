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
        ResetSelection();
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

        // Ok tuşları ile gezinme (Up/Down)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                if (selectedIndex < items.Count - 1)
                {
                    selectedIndex++;
                }
            }
            else if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                if (selectedIndex > 0)
                {
                    selectedIndex--;
                }
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