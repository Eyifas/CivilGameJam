using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller for the zoomed-in BookshelfScene.
/// Manages anchoring the bookshelf by dragging Hammer and Nails from the inventory UI,
/// dropping them onto the top of the bookshelf where they disappear, and initiating the QTE.
/// </summary>
public class BookshelfSceneController : MonoBehaviour
{
    public static BookshelfSceneController Instance { get; private set; }

    [Header("UI & Feedback")]
    [SerializeField] private TMP_Text instructionText;        // e.g. "Drag the Hammer and Nails from your inventory onto the bookshelf!"
    [SerializeField] private GameObject anchorPromptButton;   // Optional button to initiate anchoring
    [SerializeField] private GameObject visualBracketsSecured; // L-bracket visual indicator on the bookshelf

    [Header("QTE / Click Settings")]
    [SerializeField] private int requiredHits = 3;
    [SerializeField] private float timePerHit = 2.0f;
    [SerializeField] private bool requireHammer = true;
    [SerializeField] private bool requireNails = true;

    [Header("Drag & Drop Settings")]
    [SerializeField] private Vector2 dropZoneCenter = new Vector2(-0.23f, 2.0f);
    [SerializeField] private Vector2 dropZoneSize = new Vector2(11.0f, 4.5f);
    [SerializeField] private Sprite hammerSprite;
    [SerializeField] private Sprite nailsSprite;

    [Header("Return Settings")]
    [SerializeField] private string returnSceneName = "MainScene";
    [SerializeField] private float autoReturnDelay = 1.0f;

    // Instance flags: each visit to the scene starts fresh
    private bool isHammerPlaced = false;
    private bool isNailsPlaced = false;
    private bool isAnchored = false;
    private bool isQTERunning = false;
    private bool areToolsPlaced = false;
    private string currentInstruction = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    public static void ResetToolState()
    {
        if (Instance != null)
        {
            Instance.isHammerPlaced = false;
            Instance.isNailsPlaced = false;
            Instance.areToolsPlaced = false;
        }
    }

    private void Awake()
    {
        Instance = this;
        isHammerPlaced = false;
        isNailsPlaced = false;
        areToolsPlaced = false;
        isAnchored = false;
        isQTERunning = false;

        if (instructionText == null)
        {
            instructionText = FindFirstObjectByType<TMP_Text>();
        }

        if (visualBracketsSecured == null)
        {
            var heal = GameObject.Find("heal_3");
            if (heal != null) visualBracketsSecured = heal;
        }

        if (hammerSprite == null)
        {
            hammerSprite = Resources.Load<Sprite>("Hammer");
        }
        if (nailsSprite == null)
        {
            nailsSprite = Resources.Load<Sprite>("Nails");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        // Clean up any legacy tool objects from earlier revisions
        var oldHammer = GameObject.Find("BookshelfHammerTool");
        if (oldHammer != null) Destroy(oldHammer);
        var oldNails = GameObject.Find("BookshelfNailsTool");
        if (oldNails != null) Destroy(oldNails);

        isHammerPlaced = false;
        isNailsPlaced = false;
        areToolsPlaced = false;

        // Check if already anchored in this timeline run
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsBookshelfAnchored)
        {
            SetAlreadySecuredVisuals();
            return;
        }

        if (visualBracketsSecured != null)
        {
            visualBracketsSecured.SetActive(false);
        }

        // Check inventory for needed tools
        bool hasHammer = !requireHammer || InventoryManager.HasItem("Hammer");
        bool hasNails = !requireNails || HasNailsOrScrewInInventory();

        if (!hasHammer || !hasNails)
        {
            if (!hasHammer && !hasNails)
            {
                UpdateInstruction("<color=red>You need a Hammer and Nails from the drawer to anchor this! Press [E] to return.</color>");
            }
            else if (!hasHammer)
            {
                UpdateInstruction("<color=red>You need a Hammer from the drawer to anchor this! Press [E] to return.</color>");
            }
            else
            {
                UpdateInstruction("<color=red>You need Nails from the drawer to anchor this! Press [E] to return.</color>");
            }
            Debug.LogWarning("[BookshelfSceneController] Hammer and Nails are required to anchor the bookshelf! Pick them up in DrawerScene first.");
            return;
        }

        UpdateInstruction("Drag the Hammer and Nails from your inventory onto the top of the bookshelf!");
    }

    private void Update()
    {
        if (isAnchored || isQTERunning) return;

        // Allow retry with Space or left click once tools have already been placed
        if (areToolsPlaced)
        {
            if ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
            {
                StartAnchoring();
            }
        }
    }

    /// <summary>
    /// Checks whether the given world position falls within the bookshelf's top drop zone.
    /// </summary>
    public bool IsInsideDropZone(Vector3 worldPos)
    {
        return Mathf.Abs(worldPos.x - dropZoneCenter.x) <= dropZoneSize.x * 0.5f &&
               Mathf.Abs(worldPos.y - dropZoneCenter.y) <= dropZoneSize.y * 0.5f;
    }

    /// <summary>
    /// Called when an item is dragged from the inventory and dropped onto the drop zone.
    /// Returns true if accepted (which causes the item to be consumed from inventory).
    /// </summary>
    public bool TryDropItemFromInventory(string itemName, Vector3 worldPos)
    {
        if (isAnchored || isQTERunning)
        {
            return false;
        }

        if (IsHammer(itemName))
        {
            if (isHammerPlaced)
            {
                UpdateInstruction("<color=yellow>Hammer is already placed!</color> Drag the Nails from your inventory.");
                return false;
            }

            isHammerPlaced = true;
            InventoryManager.RemoveItem("Hammer");
            var ui = FindAnyObjectByType<InventoryUI>();
            if (ui != null) ui.UpdateInventoryUI();

            Debug.Log("[BookshelfScene] Hammer added to the top of the bookshelf!");
            StartCoroutine(PlayDisappearEffect(worldPos, itemName));

            if (isNailsPlaced)
            {
                OnBothToolsPlaced();
            }
            else
            {
                UpdateInstruction("<color=green>Hammer placed on the bookshelf!</color> Now drag the Nails from your inventory.");
            }
            return true;
        }
        else if (IsNailsOrScrew(itemName))
        {
            if (isNailsPlaced)
            {
                UpdateInstruction("<color=yellow>Nails are already placed!</color> Drag the Hammer from your inventory.");
                return false;
            }

            isNailsPlaced = true;
            InventoryManager.RemoveItem(itemName);
            var ui = FindAnyObjectByType<InventoryUI>();
            if (ui != null) ui.UpdateInventoryUI();

            Debug.Log("[BookshelfScene] Nails added to the top of the bookshelf!");
            StartCoroutine(PlayDisappearEffect(worldPos, itemName));

            if (isHammerPlaced)
            {
                OnBothToolsPlaced();
            }
            else
            {
                UpdateInstruction("<color=green>Nails placed on the bookshelf!</color> Now drag the Hammer from your inventory.");
            }
            return true;
        }
        else
        {
            UpdateInstruction($"<color=red>{itemName} cannot be used here! You need a Hammer and Nails.</color>");
            return false;
        }
    }

    /// <summary>
    /// Legacy compatibility hook for BookshelfDraggableItem if triggered.
    /// </summary>
    public void OnToolPlaced(BookshelfDraggableItem item)
    {
        if (item == null) return;
        TryDropItemFromInventory(item.ToolName, item.transform.position);
    }

    /// <summary>
    /// Plays a brief visual effect where the dropped tool shrinks and fades out into the bookshelf.
    /// </summary>
    private IEnumerator PlayDisappearEffect(Vector3 worldPos, string itemName)
    {
        Sprite sprite = IsHammer(itemName) ? hammerSprite : nailsSprite;
        if (sprite == null)
        {
            sprite = Resources.Load<Sprite>(IsHammer(itemName) ? "Hammer" : "Nails");
        }

        if (sprite != null)
        {
            GameObject ghost = new GameObject("ToolDisappearEffect");
            ghost.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
            ghost.transform.localScale = Vector3.one * 1.15f;

            var sr = ghost.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 25;

            float duration = 0.35f;
            float elapsed = 0f;
            Vector3 startScale = ghost.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (ghost != null)
                {
                    ghost.transform.localScale = Vector3.Lerp(startScale, startScale * 0.35f, t);
                    sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t));
                }
                yield return null;
            }

            if (ghost != null)
            {
                Destroy(ghost);
            }
        }
    }

    private void OnBothToolsPlaced()
    {
        areToolsPlaced = true;
        UpdateInstruction("<color=green>Both tools placed on the bookshelf! Starting anchoring...</color>");
        StartCoroutine(StartQTEAfterToolsPlacedRoutine());
    }

    private IEnumerator StartQTEAfterToolsPlacedRoutine()
    {
        yield return new WaitForSeconds(0.6f);
        StartAnchoring();
    }

    /// <summary>
    /// Starts the bookshelf anchoring target-click QTE.
    /// </summary>
    public void StartAnchoring()
    {
        if (isAnchored || isQTERunning) return;

        if (anchorPromptButton != null)
        {
            anchorPromptButton.SetActive(false);
        }

        isQTERunning = true;
        UpdateInstruction("Hit the targets or press the keys before they fade out!");

        // Try QTEManager.Instance first
        if (QTEManager.Instance != null)
        {
            QTEManager.Instance.StartTargetClickQTE(
                targetCount: requiredHits,
                timeLimitPerTarget: timePerHit,
                onComplete: OnAnchoringSuccess,
                onFail: OnAnchoringFail
            );
            return;
        }

        // Fallback: Check if QTETargetClick is on the persistent Canvas
        var directTargetClick = FindFirstObjectByType<QTETargetClick>(FindObjectsInactive.Include);
        if (directTargetClick != null)
        {
            directTargetClick.StartQTE(
                targetCount: requiredHits,
                timeLimitPerTarget: timePerHit,
                onComplete: OnAnchoringSuccess,
                onFail: OnAnchoringFail
            );
            return;
        }

        isQTERunning = false;
        Debug.LogError("[BookshelfSceneController] No QTEManager or QTETargetClick found! Make sure QTEManager exists in MainScene (it persists with DontDestroyOnLoad).");
    }

    private void OnAnchoringSuccess()
    {
        isAnchored = true;
        isQTERunning = false;

        // Permanently remove Hammer and Nails/Vida from inventory upon successful use!
        InventoryManager.RemoveItem("Hammer");
        InventoryManager.RemoveItem("Vida");
        InventoryManager.RemoveItem("Nails");
        InventoryManager.RemoveItem("Nail");
        InventoryManager.RemoveItem("Screw");

        var ui = FindAnyObjectByType<InventoryUI>();
        if (ui != null)
        {
            ui.UpdateInventoryUI();
        }

        if (visualBracketsSecured != null)
        {
            visualBracketsSecured.SetActive(true);
        }

        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.SetBookshelfAnchored(true);
        }

        UpdateInstruction("<color=green>Bookshelf securely anchored! Returning to room...</color>");

        StartCoroutine(ReturnToRoomRoutine());
    }

    private IEnumerator ReturnToRoomRoutine()
    {
        yield return new WaitForSeconds(autoReturnDelay);
        SceneManager.LoadScene(returnSceneName);
    }

    private void OnAnchoringFail()
    {
        isQTERunning = false;
        UpdateInstruction("<color=red>Failed to secure brackets in time! Click or press SPACE to try again.</color>");

        if (anchorPromptButton != null)
        {
            anchorPromptButton.SetActive(true);
        }
    }

    private void SetAlreadySecuredVisuals()
    {
        isAnchored = true;
        isQTERunning = false;
        if (visualBracketsSecured != null) visualBracketsSecured.SetActive(true);
        if (anchorPromptButton != null) anchorPromptButton.SetActive(false);
        UpdateInstruction("<color=green>Bookshelf is already safely anchored! Returning...</color>");
        StartCoroutine(ReturnToRoomRoutine());
    }

    private void UpdateInstruction(string message)
    {
        currentInstruction = message;
        if (instructionText != null)
        {
            instructionText.text = message;
        }
        Debug.Log("[BookshelfScene] " + message);
    }

    private bool IsHammer(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Equals("Hammer", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsNailsOrScrew(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Equals("Vida", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Screw", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Nail", System.StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Nails", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool HasNailsOrScrewInInventory()
    {
        return InventoryManager.HasItem("Vida") ||
               InventoryManager.HasItem("Screw") ||
               InventoryManager.HasItem("Nail") ||
               InventoryManager.HasItem("Nails");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
        Gizmos.DrawCube(new Vector3(dropZoneCenter.x, dropZoneCenter.y, 0f), new Vector3(dropZoneSize.x, dropZoneSize.y, 0.1f));
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(dropZoneCenter.x, dropZoneCenter.y, 0f), new Vector3(dropZoneSize.x, dropZoneSize.y, 0.1f));
    }

    private void OnGUI()
    {
        if (instructionText != null || string.IsNullOrEmpty(currentInstruction)) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 18;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;
        style.richText = true;

        float w = Mathf.Min(Screen.width * 0.85f, 750f);
        float h = 44f;
        float x = (Screen.width - w) * 0.5f;
        float y = 20f;

        GUI.Box(new Rect(x, y, w, h), currentInstruction, style);
    }
}
