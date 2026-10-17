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
        return items.Contains(itemName);
    }

    public static List<string> GetItems()
    {
        return items;
    }
}