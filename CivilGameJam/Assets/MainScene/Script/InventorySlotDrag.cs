using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Attached to each inventory slot UI element.
/// Handles dragging items out of the inventory UI and dropping them in the scene.
/// </summary>
public class InventorySlotDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    private InventoryUI inventoryUI;
    private int slotIndex = -1;
    private bool isDragging = false;
    private bool isPointerDown = false;
    private Vector2 pointerDownPos;

    public void Initialize(InventoryUI ui, int index)
    {
        inventoryUI = ui;
        slotIndex = index;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        pointerDownPos = eventData.position;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inventoryUI == null) return;
        if (!isDragging)
        {
            isDragging = true;
            inventoryUI.OnSlotBeginDrag(slotIndex, eventData.position);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (inventoryUI == null || !isDragging) return;
        inventoryUI.OnSlotDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (inventoryUI == null || !isDragging) return;
        isDragging = false;
        isPointerDown = false;
        inventoryUI.OnSlotEndDrag(slotIndex, eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        if (isDragging)
        {
            isDragging = false;
            if (inventoryUI != null)
            {
                inventoryUI.OnSlotEndDrag(slotIndex, eventData.position);
            }
        }
    }

    private void Update()
    {
        Vector2 currentMousePos = GetCurrentMousePosition();
        bool isMouseHeld = IsMouseButtonHeld();

        // 1. Trigger drag if pointer is held and moved past threshold
        if (isPointerDown && !isDragging)
        {
            if (!isMouseHeld)
            {
                isPointerDown = false;
            }
            else if (Vector2.Distance(currentMousePos, pointerDownPos) > 6f)
            {
                isDragging = true;
                if (inventoryUI != null)
                {
                    inventoryUI.OnSlotBeginDrag(slotIndex, currentMousePos);
                }
            }
        }

        // 2. Failsafe: if dragging and mouse was released, end drag
        if (isDragging)
        {
            if (!isMouseHeld)
            {
                isDragging = false;
                isPointerDown = false;
                if (inventoryUI != null)
                {
                    inventoryUI.OnSlotEndDrag(slotIndex, currentMousePos);
                }
            }
            else
            {
                if (inventoryUI != null)
                {
                    inventoryUI.OnSlotDrag(currentMousePos);
                }
            }
        }
    }

    private Vector2 GetCurrentMousePosition()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
        return Input.mousePosition;
    }

    private bool IsMouseButtonHeld()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }
        return Input.GetMouseButton(0);
    }
}
