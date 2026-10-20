using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class DrawerClickArea : MonoBehaviour
{
    [SerializeField] private DrawerController drawerController;
    [SerializeField] private bool isLeftDrawer;

    [Header("Hitbox Sizes (Closed vs Open)")]
    [Tooltip("Size and offset of the collider when this drawer is closed")]
    [SerializeField] private Vector2 closedSize;
    [SerializeField] private Vector2 closedOffset;

    [Tooltip("Size and offset of the collider when this drawer is opened")]
    [SerializeField] private Vector2 openSize;
    [SerializeField] private Vector2 openOffset;

    private BoxCollider2D boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();

        // Set accurate default dimensions based on drawer side if not set in Inspector
        if (isLeftDrawer)
        {
            if (closedSize == Vector2.zero) closedSize = new Vector2(0.94f, 0.33f);
            if (closedOffset == Vector2.zero) closedOffset = new Vector2(0.03f, -0.067f);
            if (openSize == Vector2.zero) openSize = new Vector2(1.05f, 1.15f);
            if (openOffset == Vector2.zero) openOffset = new Vector2(0.03f, -0.45f);
        }
        else
        {
            if (closedSize == Vector2.zero) closedSize = new Vector2(0.93f, 0.30f);
            if (closedOffset == Vector2.zero) closedOffset = new Vector2(-0.03f, -0.027f);
            if (openSize == Vector2.zero) openSize = new Vector2(1.05f, 1.15f);
            if (openOffset == Vector2.zero) openOffset = new Vector2(-0.03f, -0.42f);
        }
    }

    private void Start()
    {
        if (drawerController == null)
        {
            drawerController = GetComponentInParent<DrawerController>();
        }
        UpdateHitboxState();
    }

    public void UpdateHitbox(bool isOpen)
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            boxCollider.size = isOpen ? openSize : closedSize;
            boxCollider.offset = isOpen ? openOffset : closedOffset;
        }
    }

    public void UpdateHitboxState()
    {
        if (drawerController != null)
        {
            bool isOpen = isLeftDrawer ? drawerController.IsLeftOpen : drawerController.IsRightOpen;
            UpdateHitbox(isOpen);
        }
        else
        {
            UpdateHitbox(false);
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

            if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider != null && boxCollider.OverlapPoint(mousePosition))
            {
                // Objects have priority over the drawer click area
                if (IsItemUnderMouse(mousePosition))
                {
                    return;
                }

                if (drawerController != null)
                {
                    if (isLeftDrawer)
                    {
                        drawerController.ToggleLeftDrawer();
                    }
                    else
                    {
                        drawerController.ToggleRightDrawer();
                    }
                }
            }
        }
    }

    private bool IsItemUnderMouse(Vector2 mousePosition)
    {
        if (Time.frameCount == PickupItem.LastItemClickedFrame)
        {
            return true;
        }

        var allPickups = FindObjectsByType<PickupItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var p in allPickups)
        {
            if (p != null && p.gameObject.activeInHierarchy)
            {
                var col = p.GetComponent<Collider2D>();
                if (col != null && col.OverlapPoint(mousePosition))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
