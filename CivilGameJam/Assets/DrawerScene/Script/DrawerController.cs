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

    private void Start()
    {
        UpdateItems();
        TodoListUI.RegisterDrawerChecked();
    }

    public void ToggleLeftDrawer()
    {
        leftOpen = !leftOpen;
        AudioManager.PlayDrawer(leftOpen);

        UpdateDrawerVisual();
        UpdateItems();
        TodoListUI.RegisterDrawerChecked();
    }

    public void ToggleRightDrawer()
    {
        rightOpen = !rightOpen;
        AudioManager.PlayDrawer(rightOpen);

        UpdateDrawerVisual();
        UpdateItems();
        TodoListUI.RegisterDrawerChecked();
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
    }
}