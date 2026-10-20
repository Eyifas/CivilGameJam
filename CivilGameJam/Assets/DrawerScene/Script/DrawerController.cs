using UnityEngine;

public class DrawerController : MonoBehaviour
{
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite leftOpenSprite;
    [SerializeField] private Sprite rightOpenSprite;
    [SerializeField] private Sprite bothOpenSprite;

    [SerializeField] private GameObject hammerObject;
    [SerializeField] private GameObject vidaObject;
    [SerializeField] private GameObject flashlightObject;
    [SerializeField] private GameObject anahtarObject;

    private SpriteRenderer spriteRenderer;

    private bool leftOpen = false;
    private bool rightOpen = false;
    private InteractionVisualCue drawerCue;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (hammerObject != null)
        {
            hammerObject.SetActive(false);
        }

        if (vidaObject != null)
        {
            vidaObject.SetActive(false);
        }

        if (flashlightObject != null)
        {
            flashlightObject.SetActive(false);
        }

        if (anahtarObject != null)
        {
            anahtarObject.SetActive(false);
        }

        UpdateDrawerVisual();
    }

    public bool IsLeftOpen => leftOpen;
    public bool IsRightOpen => rightOpen;

    private void Start()
    {
        UpdateItems();
        UpdateHitboxes();
        TodoListUI.RegisterDrawerChecked();

        // Allow InteractionVisualCue to create or attach to a local active-scene canvas
        drawerCue = InteractionVisualCue.CreateClickCue(
            null,
            transform.position + new Vector3(0f, 1.2f, 0f),
            "CLICK DRAWERS",
            "Open & search drawers",
            true,
            new Vector2(0f, 110f),
            Camera.main
        );
    }

    private void OnDisable()
    {
        CleanupCue();
    }

    private void OnDestroy()
    {
        CleanupCue();
    }

    private void CleanupCue()
    {
        if (drawerCue != null)
        {
            if (drawerCue.gameObject != null)
            {
                Destroy(drawerCue.gameObject);
            }
            drawerCue = null;
        }
    }

    public void ToggleLeftDrawer()
    {
        leftOpen = !leftOpen;
        AudioManager.PlayDrawer(leftOpen);

        UpdateDrawerVisual();
        UpdateItems();
        UpdateHitboxes();
        TodoListUI.RegisterDrawerChecked();
    }

    public void ToggleRightDrawer()
    {
        rightOpen = !rightOpen;
        AudioManager.PlayDrawer(rightOpen);

        UpdateDrawerVisual();
        UpdateItems();
        UpdateHitboxes();
        TodoListUI.RegisterDrawerChecked();
    }

    public void UpdateHitboxes()
    {
        var clickAreas = GetComponentsInChildren<DrawerClickArea>(true);
        foreach (var ca in clickAreas)
        {
            ca.UpdateHitboxState();
        }
    }

    private void UpdateDrawerVisual()
    {
        if (leftOpen && rightOpen)
        {
            spriteRenderer.sprite = bothOpenSprite;
        }
        else if (leftOpen)
        {
            spriteRenderer.sprite = leftOpenSprite;
        }
        else if (rightOpen)
        {
            spriteRenderer.sprite = rightOpenSprite;
        }
        else
        {
            spriteRenderer.sprite = closedSprite;
        }
    }

    public void UpdateItems()
    {
        bool bookshelfAnchored = GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsBookshelfAnchored;

        // Çekiç - sağ çekmece
        if (hammerObject != null)
        {
            if (bookshelfAnchored || InventoryManager.HasItem("Hammer"))
            {
                hammerObject.SetActive(false);
            }
            else
            {
                hammerObject.SetActive(rightOpen);
            }
        }

        // Vida - sağ çekmece
        if (vidaObject != null)
        {
            if (bookshelfAnchored ||
                InventoryManager.HasItem("Vida") ||
                InventoryManager.HasItem("Screw") ||
                InventoryManager.HasItem("Nail") ||
                InventoryManager.HasItem("Nails"))
            {
                vidaObject.SetActive(false);
            }
            else
            {
                vidaObject.SetActive(rightOpen);
            }
        }

        // Fener - sol çekmece
        if (flashlightObject != null)
        {
            if (InventoryManager.HasItem("Flashlight"))
            {
                flashlightObject.SetActive(false);
            }
            else
            {
                flashlightObject.SetActive(leftOpen);
            }
        }

        if (anahtarObject != null)
        {
            if (InventoryManager.HasItem("Anahtar"))
            {
                anahtarObject.SetActive(false);
            }
            else
            {
                anahtarObject.SetActive(leftOpen);
            }
        }

        // Update guidance cue
        bool hasVisibleItems = (hammerObject != null && hammerObject.activeSelf) ||
                               (vidaObject != null && vidaObject.activeSelf) ||
                               (flashlightObject != null && flashlightObject.activeSelf) ||
                               (anahtarObject != null && anahtarObject.activeSelf);

        if (drawerCue != null)
        {
            if (hasVisibleItems)
            {
                drawerCue.UpdateText("CLICK ITEM", "Take into inventory");
            }
            else if (!leftOpen && !rightOpen)
            {
                drawerCue.UpdateText("CLICK DRAWERS", "Open & search drawers");
            }
            else
            {
                drawerCue.Dismiss();
                drawerCue = null;
            }
        }
    }
}