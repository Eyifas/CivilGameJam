using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static inventory repository. Synchronizes collected items with
/// InventoryUI and GameTimelineManager.
/// </summary>
public static class InventoryManager
{
    private static List<string> items = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ResetInventory()
    {
        items.Clear();
    }

    public static void AddItem(string itemName)
    {
        if (!items.Contains(itemName))
        {
            items.Add(itemName);
            Debug.Log($"[InventoryManager] Item added: {itemName}");

            // Automatically sync with GameTimelineManager
            if (GameTimelineManager.Instance != null)
            {
                if (itemName.Equals("Flashlight", System.StringComparison.OrdinalIgnoreCase))
                {
                    GameTimelineManager.Instance.CollectFlashlight();
                }
                else if (itemName.Equals("FirstAidKit", System.StringComparison.OrdinalIgnoreCase) ||
                         itemName.Equals("First-Aid Kit", System.StringComparison.OrdinalIgnoreCase) ||
                         itemName.Equals("First Aid Kit", System.StringComparison.OrdinalIgnoreCase))
                {
                    GameTimelineManager.Instance.CollectFirstAidKit();
                }
            }
        }
    }

    public static bool HasItem(string itemName)
    {
        if (items.Contains(itemName)) return true;

        // Check aliases for nails / screw / vida
        if (IsNailsOrScrew(itemName))
        {
            foreach (var it in items)
            {
                if (IsNailsOrScrew(it)) return true;
            }
        }

        return false;
    }

    private static bool IsNailsOrScrew(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Equals("Vida", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Screw", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Nail", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Nails", System.StringComparison.OrdinalIgnoreCase);
    }

    public static List<string> GetItems()
    {
        return items;
    }
}