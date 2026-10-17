using UnityEngine;

public class DrawerController : MonoBehaviour
{
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite leftOpenSprite;
    [SerializeField] private Sprite rightOpenSprite;
    [SerializeField] private Sprite bothOpenSprite;

    [SerializeField] private GameObject hammerObject;

    private SpriteRenderer spriteRenderer;

    private bool leftOpen = false;
    private bool rightOpen = false;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Oyun başladığında çekici gizle
        if (hammerObject != null)
        {
            hammerObject.SetActive(false);
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
        UpdateHammer();
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

    private void UpdateHammer()
    {
        if (hammerObject == null)
        {
            return;
        }

        // Çekiç daha önce alınmışsa bir daha gösterme
        if (InventoryManager.HasItem("Hammer"))
        {
            hammerObject.SetActive(false);
            return;
        }

        // Sağ çekmece açıksa göster, kapalıysa gizle
        hammerObject.SetActive(rightOpen);
    }
}