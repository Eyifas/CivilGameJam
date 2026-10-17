using System.Collections.Generic;

public static class InventoryManager
{
    private static List<string> items = new List<string>();

    public static void AddItem(string itemName)
    {
        if (!items.Contains(itemName))
        {
            items.Add(itemName);
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