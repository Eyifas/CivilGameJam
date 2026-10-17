using UnityEngine;
using UnityEngine.InputSystem;

public class DrawerClickArea : MonoBehaviour
{
    [SerializeField] private DrawerController drawerController;
    [SerializeField] private bool isLeftDrawer;

    private Collider2D clickArea;

    private void Awake()
    {
        clickArea = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition =
                Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            if (clickArea.OverlapPoint(mousePosition))
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