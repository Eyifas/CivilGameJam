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

    private void Start()
    {
        UpdateInventoryUI();
    }

    private void Update()
    {
        UpdateInventoryUI();
    }

    private void UpdateInventoryUI()
    {
        List<string> items = InventoryManager.GetItems();

        if (itemIcons == null) return;

        // Hide all slots first
        for (int i = 0; i < itemIcons.Length; i++)
        {
            if (itemIcons[i] != null)
            {
                itemIcons[i].gameObject.SetActive(false);
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
            }
        }
    }

    private Sprite GetItemSprite(string itemName)
    {
        switch (itemName)
        {
            case "Vida":
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