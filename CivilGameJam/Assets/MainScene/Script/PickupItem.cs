using UnityEngine;
using UnityEngine.InputSystem;

public class PickupItem : MonoBehaviour
{
    [SerializeField] private string itemName;

    private Collider2D itemCollider;

    private void Awake()
    {
        itemCollider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        // Eşya daha önce alındıysa tekrar görünmesin
        if (InventoryManager.HasItem(itemName))
        {
            gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition =
                Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            if (itemCollider != null && itemCollider.OverlapPoint(mousePosition))
            {
                TakeItem();
            }
        }
    }

    private void TakeItem()
    {
        // Envantere ekle
        InventoryManager.AddItem(itemName);

        Debug.Log(itemName + " envantere eklendi!");

        // Eşyayı sahneden kaldır
        gameObject.SetActive(false);

        // Çekmece içindeki eşya durumlarını güncelle
        DrawerController drawer = FindFirstObjectByType<DrawerController>();

        if (drawer != null)
        {
            drawer.UpdateItems();
        }
    }
}