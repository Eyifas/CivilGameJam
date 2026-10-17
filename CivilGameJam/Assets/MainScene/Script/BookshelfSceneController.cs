using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller for the zoomed-in BookshelfScene.
/// Manages anchoring the bookshelf with L-brackets, visual updates,
/// and synchronizing with GameTimelineManager.
/// </summary>
public class BookshelfSceneController : MonoBehaviour
{
    [Header("UI & Feedback")]
    [SerializeField] private TMP_Text instructionText;        // e.g. "Click the brackets to anchor bookshelf!"
    [SerializeField] private GameObject anchorPromptButton;   // Optional button to initiate anchoring
    [SerializeField] private GameObject visualBracketsSecured; // L-bracket visual indicator on the bookshelf

    [Header("QTE / Click Settings")]
    [SerializeField] private int requiredHits = 3;
    [SerializeField] private float timePerHit = 2.0f;
    [SerializeField] private bool requireHammer = true;
    [SerializeField] private bool requireNails = true;

    private bool isAnchored = false;
    private bool isQTERunning = false;
    private string currentInstruction = "";

    private void Awake()
    {
        if (instructionText == null)
        {
            instructionText = FindFirstObjectByType<TMP_Text>();
        }

        if (visualBracketsSecured == null)
        {
            var heal = GameObject.Find("heal_3");
            if (heal != null) visualBracketsSecured = heal;
        }
    }

    private void Start()
    {
        // Check if already anchored in this time-loop run
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsBookshelfAnchored)
        {
            SetAlreadySecuredVisuals();
            return;
        }

        if (visualBracketsSecured != null)
        {
            visualBracketsSecured.SetActive(false);
        }

        // Start QTE automatically after a brief moment for the scene transition to complete
        StartCoroutine(AutoStartRoutine());
    }

    private System.Collections.IEnumerator AutoStartRoutine()
    {
        yield return new WaitForSeconds(0.25f);

        if (!isAnchored && !isQTERunning)
        {
            StartAnchoring();
        }
    }

    private void Update()
    {
        if (isAnchored || isQTERunning) return;

        // Allow retry with Space or left click if it failed or was waiting
        if ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
        {
            StartAnchoring();
        }
    }

    /// <summary>
    /// Starts the bookshelf anchoring target-click QTE.
    /// </summary>
    public void StartAnchoring()
    {
        if (isAnchored || isQTERunning) return;

        bool hasHammer = !requireHammer || InventoryManager.HasItem("Hammer");
        bool hasNails = !requireNails || InventoryManager.HasItem("Vida") || InventoryManager.HasItem("Screw") || InventoryManager.HasItem("Nail") || InventoryManager.HasItem("Nails");

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

    [Header("Return Settings")]
    [SerializeField] private string returnSceneName = "MainScene";
    [SerializeField] private float autoReturnDelay = 1.0f;

    private void OnAnchoringSuccess()
    {
        isAnchored = true;
        isQTERunning = false;

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

    private System.Collections.IEnumerator ReturnToRoomRoutine()
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
