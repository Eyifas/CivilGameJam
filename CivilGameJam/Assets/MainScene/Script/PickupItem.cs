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
        bool bookshelfAnchored = GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsBookshelfAnchored;
        if (bookshelfAnchored && (itemName == "Hammer" || itemName == "Vida" || itemName == "Nails"))
        {
            gameObject.SetActive(false);
            return;
        }

        // Bu eşya daha önce alındıysa tekrar görünmesin
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

            if (itemCollider.OverlapPoint(mousePosition))
            {
                InventoryManager.AddItem(itemName);

                // Eşyayı sahneden kaldır
                gameObject.SetActive(false);

                var drawer = FindFirstObjectByType<DrawerController>();
                if (drawer != null)
                {
                    drawer.UpdateItems();
                }

                Debug.Log(itemName + " envantere eklendi!");
            }
        }
    }
}