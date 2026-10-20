using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InteractionZone : MonoBehaviour
{
    [SerializeField] private string sceneName;
    [SerializeField] private GameObject interactionPrompt;

    [Header("Interaction Prompt Label")]
    [Tooltip("Label displayed to the right of the interaction prompt icon. Editable in Inspector.")]
    [SerializeField] private string promptLabel = "";
    [Tooltip("Offset of the label from the prompt icon center (X = right)")]
    [SerializeField] private Vector3 promptLabelOffset = new Vector3(0.85f, 0f, 0f);
    [Tooltip("Font size for the label")]
    [SerializeField] private float promptLabelFontSize = 3.5f;
    [Tooltip("Color of the label")]
    [SerializeField] private Color promptLabelColor = Color.white;

    [Header("Table & Painting Visual Sync")]
    [SerializeField] private Sprite litTableSprite;
    [SerializeField] private Sprite unlitTableSprite;
    [SerializeField] private Vector3 paintingWallPosition = new Vector3(-15.35f, 3.01f, 0f);
    [SerializeField] private Vector3 paintingFloorPosition = new Vector3(-15.35f, -2.3f, 0f);

    private bool playerInside = false;
    private bool isInteractable = true;

    private void Awake()
    {
        FindPrompt();
        SetupPromptLabel();

        if (interactionPrompt != null)
        {
            if (interactionPrompt.TryGetComponent<Button>(out var btn))
            {
                btn.onClick.RemoveListener(OnInteractTriggered);
                btn.onClick.AddListener(OnInteractTriggered);
            }
            interactionPrompt.SetActive(false);
        }
    }

    private void Start()
    {
        FindPrompt();
        SetupPromptLabel();
        CheckCompletionStatus();
    }

    private void OnEnable()
    {
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.OnStateUpdated += HandleStateUpdated;
        }
        CheckCompletionStatus();
    }

    private void OnDisable()
    {
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.OnStateUpdated -= HandleStateUpdated;
        }
    }

    private void HandleStateUpdated()
    {
        CheckCompletionStatus();
    }

    public void CheckCompletionStatus()
    {
        // 1. Sync Table & Painting visuals in MainScene
        if (IsTableZone())
        {
            SyncTableVisuals();
        }

        // 2. Disable zone if completed, or re-enable if undone
        if (IsZoneCompleted())
        {
            DisableZonePermanently();
        }
        else
        {
            EnableZone();
        }
    }

    public void EnableZone()
    {
        isInteractable = true;
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = true;
        }
    }

    public void SyncTableVisuals()
    {
        if (GameTimelineManager.Instance == null) return;

        bool candleOut = GameTimelineManager.Instance.IsCandleExtinguished;
        bool paintingSafe = GameTimelineManager.Instance.IsPaintingRemoved;

        // 1. Sync Table Sprite (lit vs unlit)
        Transform tableObj = transform.parent != null ? transform.parent : transform;
        if (tableObj != null && tableObj.TryGetComponent<SpriteRenderer>(out var tableSr))
        {
            if (litTableSprite == null && !candleOut)
            {
                litTableSprite = tableSr.sprite;
            }

            if (unlitTableSprite == null)
            {
                unlitTableSprite = Resources.Load<Sprite>("MasaUnlit");
                if (unlitTableSprite == null) unlitTableSprite = Resources.Load<Sprite>("çalisma masa unlit");
            }

            if (candleOut && unlitTableSprite != null)
            {
                tableSr.sprite = unlitTableSprite;
            }
            else if (!candleOut && litTableSprite != null)
            {
                tableSr.sprite = litTableSprite;
            }
        }

        // 2. Sync Painting Position & Sorting Order
        var tablo = GameObject.Find("Tablo");
        if (tablo == null) tablo = GameObject.Find("Painting");
        if (tablo != null)
        {
            if (paintingSafe)
            {
                tablo.transform.position = new Vector3(paintingFloorPosition.x, paintingFloorPosition.y, tablo.transform.position.z);
                if (tablo.TryGetComponent<SpriteRenderer>(out var tabloSr))
                {
                    tabloSr.sortingOrder = 7; // behind the desk (sorting order 8)
                }
            }
            else
            {
                tablo.transform.position = new Vector3(paintingWallPosition.x, paintingWallPosition.y, tablo.transform.position.z);
                if (tablo.TryGetComponent<SpriteRenderer>(out var tabloSr))
                {
                    tabloSr.sortingOrder = 6; // Original wall order
                }
            }
        }
    }

    public bool IsZoneCompleted()
    {
        if (GameTimelineManager.Instance == null) return false;

        if (IsBookshelfZone() && GameTimelineManager.Instance.IsBookshelfAnchored)
            return true;

        if (IsTableZone() && GameTimelineManager.Instance.IsCandleExtinguished && GameTimelineManager.Instance.IsPaintingRemoved)
            return true;

        if (IsPipeZone() && GameTimelineManager.Instance.IsGasShutOff)
            return true;

        if (IsDolapZone() && GameTimelineManager.Instance.IsDolapSecured)
            return true;

        if (IsDrawerZone() && TodoListUI.HasAllDrawerItems())
            return true;

        return false;
    }

    public bool IsDolapZone()
    {
        // Strictly matches the hallway wardrobe before the exit door (DolapScene / düşme dolap)
        // NEVER matches the kitchen cupboard (CupboardScene / dolap mutfak)
        if (!string.IsNullOrEmpty(sceneName) && sceneName.Equals("DolapScene", System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (transform.parent != null && 
            (transform.parent.name.IndexOf("düşme", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             transform.parent.name.IndexOf("dsme", System.StringComparison.OrdinalIgnoreCase) >= 0))
            return true;

        if (name.IndexOf("düşme", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("dsme", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    public bool IsDrawerZone()
    {
        if (!string.IsNullOrEmpty(sceneName) && sceneName.IndexOf("Drawer", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (transform.parent != null && 
            (transform.parent.name.IndexOf("Drawer", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             transform.parent.name.IndexOf("Cekmece", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             transform.parent.name.IndexOf("Çekmece", System.StringComparison.OrdinalIgnoreCase) >= 0))
            return true;

        if (name.IndexOf("Drawer", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Cekmece", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Çekmece", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    public bool IsTableZone()
    {
        if (!string.IsNullOrEmpty(sceneName) && 
            (sceneName.IndexOf("Table", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             sceneName.IndexOf("Desk", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             sceneName.IndexOf("Painting", System.StringComparison.OrdinalIgnoreCase) >= 0))
            return true;

        if (transform.parent != null && 
            (transform.parent.name.IndexOf("Masa", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             transform.parent.name.IndexOf("Tablo", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             transform.parent.name.IndexOf("Desk", System.StringComparison.OrdinalIgnoreCase) >= 0))
            return true;

        return false;
    }

    public bool IsBookshelfZone()
    {
        if (!string.IsNullOrEmpty(sceneName) && sceneName.IndexOf("Bookshelf", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (transform.parent != null && transform.parent.name.IndexOf("Bookshelf", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (name.IndexOf("Bookshelf", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    public bool IsPipeZone()
    {
        if (!string.IsNullOrEmpty(sceneName) && sceneName.IndexOf("Pipe", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (transform.parent != null && 
            (transform.parent.name.IndexOf("Pipe", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             transform.parent.name.IndexOf("boru", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             transform.parent.name.IndexOf("vana", System.StringComparison.OrdinalIgnoreCase) >= 0))
            return true;

        if (name.IndexOf("Pipe", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("boru", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("vana", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    public void DisableZonePermanently()
    {
        isInteractable = false;
        playerInside = false;

        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        if (interactionPrompt == null) FindPrompt();
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }

        if (transform.parent != null)
        {
            var siblingPrompt = transform.parent.Find("InteractionPrompt");
            if (siblingPrompt == null) siblingPrompt = transform.parent.Find("E Button");
            if (siblingPrompt != null)
            {
                siblingPrompt.gameObject.SetActive(false);
            }
        }

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.ExitZone(this);
        }
    }

    private void FindPrompt()
    {
        if (interactionPrompt != null) return;

        // 1. Check direct children
        var child = transform.Find("InteractionPrompt");
        if (child == null) child = transform.Find("E Button");
        if (child != null)
        {
            interactionPrompt = child.gameObject;
            return;
        }

        // 2. Check siblings under the same parent (e.g. Drawer -> InteractionZone + InteractionPrompt)
        if (transform.parent != null)
        {
            var sibling = transform.parent.Find("InteractionPrompt");
            if (sibling == null) sibling = transform.parent.Find("E Button");
            if (sibling != null)
            {
                interactionPrompt = sibling.gameObject;
                return;
            }
        }
    }

    public void SetupPromptLabel()
    {
        if (interactionPrompt == null) FindPrompt();
        if (interactionPrompt == null) return;

        var labelComp = interactionPrompt.GetComponent<InteractionPromptLabel>();
        if (labelComp == null)
        {
            labelComp = interactionPrompt.AddComponent<InteractionPromptLabel>();
        }

        if (labelComp != null)
        {
            if (!string.IsNullOrEmpty(promptLabel))
            {
                labelComp.SetLabel(promptLabel);
            }
            else if (!string.IsNullOrEmpty(labelComp.LabelText) && labelComp.LabelText != "Interact")
            {
                promptLabel = labelComp.LabelText;
            }
            else
            {
                promptLabel = GetDefaultLabel();
                labelComp.SetLabel(promptLabel);
            }

            labelComp.Offset = promptLabelOffset;
            labelComp.FontSize = promptLabelFontSize;
            labelComp.TextColor = promptLabelColor;
        }
    }

    private string GetDefaultLabel()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            if (sceneName.IndexOf("Drawer", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Search Drawer";
            if (sceneName.IndexOf("Bookshelf", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Anchor Bookshelf";
            if (sceneName.IndexOf("Table", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Secure Desk";
            if (sceneName.IndexOf("Pipe", System.StringComparison.OrdinalIgnoreCase) >= 0) return "Shut Off Gas";
            if (sceneName.Equals("DolapScene", System.StringComparison.OrdinalIgnoreCase)) return "Lift Wardrobe";
            return sceneName;
        }
        if (transform.parent != null)
        {
            if (transform.parent.name.IndexOf("boru", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                transform.parent.name.IndexOf("vana", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                transform.parent.name.IndexOf("pipe", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Shut Off Gas";

            if (transform.parent.name.IndexOf("düşme", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                transform.parent.name.IndexOf("dsme", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Lift Wardrobe";

            return transform.parent.name;
        }
        return "Interact";
    }

    public bool IsAllowedInCurrentStage()
    {
        if (GameTimelineManager.Instance == null) return true;

        var stage = GameTimelineManager.Instance.CurrentStage;

        if (IsPipeZone())
        {
            // Kitchen gas pipe is ONLY interactable after the earthquake during Stage 3 Utilities (until gas is shut off)
            return stage == GameStage.Stage3_Utilities && !GameTimelineManager.Instance.IsGasShutOff;
        }

        if (IsDolapZone())
        {
            // Hallway wardrobe before exit door is ONLY interactable after the earthquake during Stage 3 Utilities (until lifted)
            return stage == GameStage.Stage3_Utilities && !GameTimelineManager.Instance.IsDolapSecured;
        }

        if (IsDrawerZone())
        {
            // Drawer can be searched in Stage 1 Preparation AND Stage 3 Utilities (until all drawer items are collected)
            return (stage == GameStage.Stage1_Preparation || stage == GameStage.Stage3_Utilities) && !TodoListUI.HasAllDrawerItems();
        }

        // Room preparation zones (Bookshelf, Table/Painting) are only interactable during Stage 1 Preparation
        return stage == GameStage.Stage1_Preparation && !IsZoneCompleted();
    }

    private void Update()
    {
        bool allowed = IsAllowedInCurrentStage();
        if (isInteractable != allowed)
        {
            SetInteractable(allowed);
        }

        if (!isInteractable) return;

        if (IsZoneCompleted())
        {
            DisableZonePermanently();
            return;
        }

        if (playerInside && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            OnInteractTriggered();
        }
    }

    public void OnInteractTriggered()
    {
        if (!isInteractable || !playerInside) return;

        if (IsZoneCompleted())
        {
            DisableZonePermanently();
            return;
        }

        if (!string.IsNullOrEmpty(sceneName))
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                PlayerMovement.SavePosition(player.transform.position);
            }

            SceneManager.LoadScene(sceneName);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isInteractable || !IsAllowedInCurrentStage()) return;

        if (IsZoneCompleted())
        {
            DisableZonePermanently();
            return;
        }

        if (other.CompareTag("Player"))
        {
            playerInside = true;

            if (interactionPrompt == null) FindPrompt();
            SetupPromptLabel();

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }

            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.EnterZone(this);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;

            if (interactionPrompt == null) FindPrompt();

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }

            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.ExitZone(this);
            }
        }
    }

    public void SetInteractable(bool active)
    {
        isInteractable = active;
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = active;
        }

        if (active)
        {
            CheckCompletionStatus();

            // If player is already touching zone when it becomes active, show prompt
            if (col != null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null && player.TryGetComponent<Collider2D>(out var pCol) && col.IsTouching(pCol))
                {
                    playerInside = true;
                    if (interactionPrompt == null) FindPrompt();
                    SetupPromptLabel();
                    if (interactionPrompt != null) interactionPrompt.SetActive(true);
                    if (InteractionManager.Instance != null) InteractionManager.Instance.EnterZone(this);
                }
            }
        }
        else
        {
            playerInside = false;
            if (interactionPrompt == null) FindPrompt();

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }

            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.ExitZone(this);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(promptLabel))
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    SetupPromptLabel();
                }
            };
        }
    }
#endif
}