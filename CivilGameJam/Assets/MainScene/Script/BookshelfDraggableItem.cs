using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attached to draggable tool items (Hammer and Nails) in BookshelfScene.
/// Allows the player to drag the tools and drop them onto the top of the bookshelf.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BookshelfDraggableItem : MonoBehaviour
{
    public enum ToolType
    {
        Hammer,
        Nails
    }

    [Header("Item Config")]
    [SerializeField] private ToolType toolType;
    [SerializeField] private string toolName = "Tool";
    [SerializeField] private Vector3 startPosition;
    [SerializeField] private Vector3 targetDropPosition;
    [SerializeField] private Vector3 normalScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField] private Vector3 placedScale = new Vector3(1.0f, 1.0f, 1f);

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private int idleSortingOrder = 20;
    [SerializeField] private int draggingSortingOrder = 30;
    [SerializeField] private int placedSortingOrder = 15;

    private Collider2D itemCollider;
    private BookshelfSceneController controller;
    private bool isDragging = false;
    private bool isPlaced = false;
    private Vector3 dragOffset;
    private Camera mainCamera;
    private Coroutine returnCoroutine;

    public bool IsPlaced => isPlaced;
    public ToolType Type => toolType;
    public string ToolName => toolName;

    public void Initialize(ToolType type, string name, Vector3 startPos, Vector3 dropPos, Sprite sprite, BookshelfSceneController ctrl)
    {
        toolType = type;
        toolName = name;
        startPosition = startPos;
        targetDropPosition = dropPos;
        controller = ctrl;
        mainCamera = Camera.main;

        transform.position = startPosition;
        transform.localScale = normalScale;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (sprite != null)
        {
            spriteRenderer.sprite = sprite;
        }

        spriteRenderer.sortingOrder = idleSortingOrder;

        itemCollider = GetComponent<Collider2D>();
        if (itemCollider == null)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            Vector2 s = box.size;
            if (s.x < 1.6f) s.x = 1.6f;
            if (s.y < 1.6f) s.y = 1.6f;
            box.size = s;
            itemCollider = box;
        }
    }

    private void Awake()
    {
        mainCamera = Camera.main;
        itemCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        if (isPlaced) return;

        Vector2 mouseWorld = GetMouseWorldPosition();

        // Mouse Down - Begin Drag
        if (WasLeftMouseButtonPressed())
        {
            if (itemCollider != null && itemCollider.OverlapPoint(mouseWorld))
            {
                StartDragging(mouseWorld);
            }
        }

        // Dragging
        if (isDragging)
        {
            if (IsLeftMouseButtonHeld())
            {
                transform.position = new Vector3(mouseWorld.x + dragOffset.x, mouseWorld.y + dragOffset.y, startPosition.z);
            }
            else
            {
                EndDragging();
            }
        }
    }

    private void StartDragging(Vector2 mouseWorld)
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        isDragging = true;
        dragOffset = transform.position - (Vector3)mouseWorld;
        dragOffset.z = 0f;

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = draggingSortingOrder;
        }

        transform.localScale = normalScale * 1.15f;
    }

    private void EndDragging()
    {
        isDragging = false;
        transform.localScale = normalScale;

        if (controller != null && controller.IsInsideDropZone(transform.position))
        {
            SnapToPlaced();
        }
        else
        {
            ReturnToStart();
        }
    }

    private void SnapToPlaced()
    {
        isPlaced = true;

        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        transform.position = targetDropPosition;
        transform.localScale = placedScale;

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = placedSortingOrder;
        }

        if (controller != null)
        {
            controller.OnToolPlaced(this);
        }
    }

    private void ReturnToStart()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = idleSortingOrder;
        }

        if (gameObject.activeInHierarchy)
        {
            returnCoroutine = StartCoroutine(SmoothReturnRoutine());
        }
        else
        {
            transform.position = startPosition;
        }
    }

    private IEnumerator SmoothReturnRoutine()
    {
        Vector3 from = transform.position;
        Vector3 to = startPosition;
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        transform.position = to;
        returnCoroutine = null;
    }

    public void ResetItem()
    {
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        isPlaced = false;
        isDragging = false;
        transform.position = startPosition;
        transform.localScale = normalScale;

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = idleSortingOrder;
        }
    }

    private Vector2 GetMouseWorldPosition()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return Vector2.zero;

        Vector2 screenPos = Vector2.zero;
        if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
        }
        else
        {
            screenPos = Input.mousePosition;
        }

        return mainCamera.ScreenToWorldPoint(screenPos);
    }

    private bool WasLeftMouseButtonPressed()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasPressedThisFrame;
        }
        return Input.GetMouseButtonDown(0);
    }

    private bool IsLeftMouseButtonHeld()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }
        return Input.GetMouseButton(0);
    }
}
