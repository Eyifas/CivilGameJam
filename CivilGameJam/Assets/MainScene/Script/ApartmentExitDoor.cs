using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Controls the apartment exit door at the end of the hallway (x = 16.8f).
/// - Stage 1: Closed (must prepare room).
/// - Stage 2: Locked / dangerous (running during tremor causes DoorRush fall).
/// - Stage 3:
///     - Before gas shut off: Warns player to shut off kitchen gas first.
///     - After gas shut off: Player can open the door and evacuate into the street!
///       Opening the door completes the game and triggers Victory (Comic Panel 4 cutscene).
/// </summary>
public class ApartmentExitDoor : MonoBehaviour
{
    public static ApartmentExitDoor Instance { get; private set; }

    [Header("Door Settings")]
    [SerializeField] private Vector3 doorPosition = new Vector3(16.8f, -1.8f, 0f);
    [SerializeField] private Vector2 triggerSize = new Vector2(3.0f, 4.0f);

    [Header("Prompt Settings")]
    [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.6f, 0f);

    private GameObject promptObj;
    private TextMeshPro promptLabelText;
    private bool playerInside = false;
    private bool isEvacuated = false;

    public bool IsEvacuated => isEvacuated;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        transform.position = doorPosition;
        EnsureTriggerCollider();
        BuildDoorPrompt();
    }

    private void EnsureTriggerCollider()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = triggerSize;
        col.offset = Vector2.zero;
    }

    private void BuildDoorPrompt()
    {
        if (promptObj != null) return;

        promptObj = new GameObject("ExitDoorPrompt");
        promptObj.transform.SetParent(transform, false);
        promptObj.transform.localPosition = promptOffset;

        // Button Icon (using QTE E Button sprite)
        var iconObj = new GameObject("ButtonIcon");
        iconObj.transform.SetParent(promptObj.transform, false);
        var sr = iconObj.AddComponent<SpriteRenderer>();
        sr.sprite = HazardVisualManager.LoadHazardSprite("UI/QTE_Button_E") ??
                    Resources.Load<Sprite>("UI/QTE_Button_E") ??
                    Resources.Load<Sprite>("EButton");
        sr.sortingOrder = 15;
        var unlitMat = SwitchInteraction.GetUnlitMaterial();
        if (unlitMat != null) sr.material = unlitMat;

        // Scale to a clean readable size
        float targetSize = 1.0f;
        if (sr.sprite != null && sr.sprite.rect.height > 0)
        {
            float curH = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;
            float s = targetSize / curH;
            iconObj.transform.localScale = new Vector3(s, s, 1f);
        }
        else
        {
            iconObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        }

        // Label Text
        var textObj = new GameObject("PromptLabel");
        textObj.transform.SetParent(promptObj.transform, false);
        textObj.transform.localPosition = new Vector3(1.1f, 0f, 0f);

        promptLabelText = textObj.AddComponent<TextMeshPro>();
        promptLabelText.text = "Open Door & Evacuate";
        promptLabelText.fontSize = 4.2f;
        promptLabelText.fontStyle = FontStyles.Bold;
        promptLabelText.alignment = TextAlignmentOptions.MidlineLeft;
        promptLabelText.color = Color.white;
        promptLabelText.sortingOrder = 16;
        promptLabelText.rectTransform.sizeDelta = new Vector2(8f, 1.5f);

        promptObj.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") || collision.GetComponent<PlayerMovement>() != null)
        {
            playerInside = true;
            UpdatePromptVisibility();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") || collision.GetComponent<PlayerMovement>() != null)
        {
            playerInside = false;
            UpdatePromptVisibility();
        }
    }

    private void UpdatePromptVisibility()
    {
        if (promptObj == null) return;

        if (!playerInside || isEvacuated)
        {
            promptObj.SetActive(false);
            return;
        }

        var timeline = GameTimelineManager.Instance;
        if (timeline == null)
        {
            promptObj.SetActive(false);
            return;
        }

        // In Stage 3:
        if (timeline.CurrentStage == GameStage.Stage3_Utilities)
        {
            if (!timeline.IsGasShutOff)
            {
                promptLabelText.text = "<color=#E74C3C>Gas Leaking! Shut valve first!</color>";
            }
            else if (!timeline.IsDolapSecured)
            {
                promptLabelText.text = "<color=#E67E22>Fallen Wardrobe Blocks Exit!</color>";
            }
            else
            {
                promptLabelText.text = "<color=#27AE60>Open Door [E]</color>";
            }
            promptObj.SetActive(true);
        }
        else if (timeline.CurrentStage == GameStage.Stage1_Preparation)
        {
            promptLabelText.text = "<color=#F39C12>Prepare room before earthquake!</color>";
            promptObj.SetActive(true);
        }
        else
        {
            promptObj.SetActive(false);
        }
    }

    private void Update()
    {
        if (isEvacuated)
        {
            if (promptObj != null && promptObj.activeSelf) promptObj.SetActive(false);
            return;
        }

        if (!playerInside) return;

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null && Vector2.Distance(playerObj.transform.position, transform.position) > 4.5f)
        {
            playerInside = false;
            UpdatePromptVisibility();
            return;
        }

        UpdatePromptVisibility();

        var timeline = GameTimelineManager.Instance;
        if (timeline == null) return;

        // Check [E] key or mouse click on prompt while standing near the door
        bool ePressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool clickPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (ePressed || clickPressed)
        {
            InteractWithDoor();
        }
    }

    public void InteractWithDoor()
    {
        if (isEvacuated) return;
        var timeline = GameTimelineManager.Instance;
        if (timeline == null) return;

        if (timeline.CurrentStage == GameStage.Stage1_Preparation)
        {
            AudioManager.PlayDoorLocked();
            Debug.Log("<color=yellow>[ExitDoor] Cannot leave yet — must prepare apartment for earthquake!</color>");
            timeline.ShowUrgentMessage("Cannot leave yet! Secure the apartment before the earthquake strikes!");
            return;
        }

        if (timeline.CurrentStage == GameStage.Stage2_Tremor)
        {
            AudioManager.PlayDoorLocked();
            // Trying to run out during active tremor
            timeline.TriggerRewind("FATAL FALL: Violent lateral forces threw you into walls and falling debris! Never run while the building is shaking—Drop, Cover, and Hold On!");
            return;
        }

        if (timeline.CurrentStage == GameStage.Stage3_Utilities)
        {
            if (!timeline.IsGasShutOff)
            {
                AudioManager.PlayDoorLocked();
                Debug.LogWarning("<color=red>[ExitDoor] Cannot leave while gas is freely leaking in kitchen!</color>");
                timeline.ShowUrgentMessage("DANGER: Leaking gas in kitchen! Shut off the gas valve before evacuating!");
                return;
            }

            if (!timeline.IsDolapSecured)
            {
                AudioManager.PlayDoorLocked();
                Debug.LogWarning("<color=orange>[ExitDoor] Hallway wardrobe fell and blocks exit!</color>");
                timeline.ShowUrgentMessage("OBSTACLE: The fallen wardrobe is blocking the exit! Lift it back up!");
                return;
            }

            // Transition to DoorScene for key insertion and deadbolt rotation QTE
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                PlayerMovement.SavePosition(player.transform.position);
            }

            timeline.RecordActionSnapshot("Reached Exit Door - Ready to Unlock");
            AudioManager.PlayDoorLocked();
            Debug.Log("<color=cyan>[ExitDoor] Opening DoorScene to unlock the front door!</color>");
            UnityEngine.SceneManagement.SceneManager.LoadScene("DoorScene");
        }
    }

    public void ResetDoor()
    {
        isEvacuated = false;
        UpdatePromptVisibility();
    }
}
