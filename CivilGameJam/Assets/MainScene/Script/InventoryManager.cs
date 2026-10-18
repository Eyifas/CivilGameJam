using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static inventory repository. Synchronizes collected items with
/// InventoryUI and GameTimelineManager.
/// </summary>
public static class InventoryManager
{
    private static List<string> items = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnSubsystemRegistration()
    {
        items.Clear();
        InventorySelection.ResetSelection();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ResetInventory()
    {
        items.Clear();
        InventorySelection.ResetSelection();
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

        // Check aliases for key / anahtar
        if (IsKey(itemName))
        {
            foreach (var it in items)
            {
                if (IsKey(it)) return true;
            }
        }

        return false;
    }

    public static bool RemoveItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return false;

        bool removedAny = false;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (string.Equals(items[i].Trim(), itemName.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                string removed = items[i];
                items.RemoveAt(i);
                Debug.Log($"[InventoryManager] Item removed: {removed}");
                removedAny = true;
            }
        }

        // Check aliases for nails / screw / vida
        if (IsNailsOrScrew(itemName))
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (IsNailsOrScrew(items[i]))
                {
                    string removed = items[i];
                    items.RemoveAt(i);
                    Debug.Log($"[InventoryManager] Item removed: {removed} (matched alias {itemName})");
                    removedAny = true;
                }
            }
        }

        // Check aliases for key / anahtar
        if (IsKey(itemName))
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (IsKey(items[i]))
                {
                    string removed = items[i];
                    items.RemoveAt(i);
                    Debug.Log($"[InventoryManager] Item removed: {removed} (matched alias {itemName})");
                    removedAny = true;
                }
            }
        }

        return removedAny;
    }

    private static bool IsNailsOrScrew(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Equals("Vida", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Screw", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Nail", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Nails", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKey(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Equals("Anahtar", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Key", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("anahtar1", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("DrawerKey", System.StringComparison.OrdinalIgnoreCase);
    }

    public static List<string> GetItems()
    {
        return items;
    }

    public static List<string> GetItemsCopy()
    {
        return new List<string>(items);
    }

    public static void SetItems(IEnumerable<string> newItems)
    {
        items.Clear();
        if (newItems != null)
        {
            foreach (var item in newItems)
            {
                if (!string.IsNullOrEmpty(item) && !items.Contains(item))
                {
                    items.Add(item);
                }
            }
        }
    }
}