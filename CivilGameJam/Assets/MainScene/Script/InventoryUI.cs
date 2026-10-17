using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Image[] itemIcons;

    [Header("Item Sprites")]
    [SerializeField] private Sprite hammerSprite;
    [SerializeField] private Sprite nailSprite;

    private void Start()
    {
        UpdateInventoryUI();
    }

    private void UpdateInventoryUI()
    {
        List<string> items = InventoryManager.GetItems();

        // Önce bütün slotları gizle
        foreach (Image icon in itemIcons)
        {
            icon.gameObject.SetActive(false);
        }

        // Eşyaları alındıkları sıraya göre yerleştir
        for (int i = 0; i < items.Count && i < itemIcons.Length; i++)
        {
            Sprite itemSprite = GetItemSprite(items[i]);

            if (itemSprite != null)
            {
                itemIcons[i].sprite = itemSprite;
                itemIcons[i].gameObject.SetActive(true);
            }
        }
    }

    private Sprite GetItemSprite(string itemName)
    {
        switch (itemName)
        {
            case "Hammer":
                return hammerSprite;

            case "Nail":
                return nailSprite;

            default:
                return null;
        }
    }
}