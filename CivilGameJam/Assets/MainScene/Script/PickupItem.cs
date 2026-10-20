using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PickupItem : MonoBehaviour
{
    public static int LastItemClickedFrame = -1;

    [SerializeField] private string itemName;

    private Collider2D itemCollider;

    private void Awake()
    {
        itemCollider = GetComponent<Collider2D>();
        if (itemCollider is BoxCollider2D box)
        {
            // Ensure collider comfortably covers the item so players don't misclick the drawer behind it
            if (box.size.x < 1.2f || box.size.y < 1.2f)
            {
                box.size = new Vector2(Mathf.Max(box.size.x, 1.2f), Mathf.Max(box.size.y, 1.2f));
            }
        }
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
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (Camera.main == null) return;
            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            if (itemCollider != null && itemCollider.OverlapPoint(mousePosition))
            {
                LastItemClickedFrame = Time.frameCount;
                TakeItem();
            }
        }
    }

    private void TakeItem()
    {
        // Envantere ekle
        InventoryManager.AddItem(itemName);
        AudioManager.PlayItemPickup();

        Debug.Log(itemName + " envantere eklendi!");

        // Eşyayı sahneden kaldır
        gameObject.SetActive(false);

        // Çekmece içindeki eşya durumlarını güncelle
        DrawerController drawer = FindFirstObjectByType<DrawerController>();

        if (drawer != null)
        {
            drawer.UpdateItems();
        }

        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.CheckStage1EarlyCompletion();
        }
    }
}
