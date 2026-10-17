using UnityEngine;

public class DrawerController : MonoBehaviour
{
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite leftOpenSprite;
    [SerializeField] private Sprite rightOpenSprite;
    [SerializeField] private Sprite bothOpenSprite;

    [SerializeField] private GameObject hammerObject;
    [SerializeField] private GameObject vidaObject;

    private SpriteRenderer spriteRenderer;

    private bool leftOpen = false;
    private bool rightOpen = false;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Başlangıçta ikisi de görünmesin
        if (hammerObject != null)
        {
            hammerObject.SetActive(false);
        }

        if (vidaObject != null)
        {
            vidaObject.SetActive(false);
        }

        UpdateDrawerVisual();
    }

    public void ToggleLeftDrawer()
    {
        leftOpen = !leftOpen;
        UpdateDrawerVisual();
    }

    public void ToggleRightDrawer()
    {
        rightOpen = !rightOpen;

        UpdateDrawerVisual();
        UpdateItems();
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

    private void Start()
    {
        UpdateItems();
    }

    public void UpdateItems()
    {
        // Hammer
        if (hammerObject != null)
        {
            if (InventoryManager.HasItem("Hammer"))
            {
                hammerObject.SetActive(false);
            }
            else
            {
                hammerObject.SetActive(rightOpen);
            }
        }

        // Vida / Nails
        if (vidaObject != null)
        {
            if (InventoryManager.HasItem("Vida") || InventoryManager.HasItem("Screw") || InventoryManager.HasItem("Nail") || InventoryManager.HasItem("Nails"))
            {
                vidaObject.SetActive(false);
            }
            else
            {
                vidaObject.SetActive(rightOpen);
            }
        }
    }
}