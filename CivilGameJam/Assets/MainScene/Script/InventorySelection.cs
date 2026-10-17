using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InventorySelection : MonoBehaviour
{
    [SerializeField] private RectTransform selectionFrame;
    [SerializeField] private RectTransform[] itemSlots;

    private int selectedIndex = 0;

    public static string SelectedItem { get; private set; } = "";

    private void Update()
    {
        List<string> items = InventoryManager.GetItems();

        // Envanter boşsa seçim karesini gösterme
        if (items.Count == 0)
        {
            selectionFrame.gameObject.SetActive(false);
            SelectedItem = "";
            return;
        }

        selectionFrame.gameObject.SetActive(true);

        // Seçim, mevcut eşya sayısının dışına çıkmasın
        if (selectedIndex >= items.Count)
        {
            selectedIndex = items.Count - 1;
        }

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            if (selectedIndex < items.Count - 1)
            {
                selectedIndex++;
            }
        }

        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            if (selectedIndex > 0)
            {
                selectedIndex--;
            }
        }

        // Işıklı kareyi seçilen slotun üzerine taşı
        selectionFrame.anchoredPosition =
            itemSlots[selectedIndex].anchoredPosition;

        // Şu anda kullanılan eşya
        SelectedItem = items[selectedIndex];
    }
}