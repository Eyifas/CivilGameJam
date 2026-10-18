using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reusable hazard death visual & rewind controller.
/// Displays a hazard/death illustration on sorting order 20 (on top of everything),
/// hides the game UI (timer, inventory, HUD) for full screen focus,
/// holds the visual for dramatic effect, then initiates the time rewind loop.
/// 
/// Can be used:
/// 1. As an interactive trigger with [E] (e.g., light switch).
/// 2. As an automatic hazard trigger via code (e.g., candle fire, falling painting) using TriggerSequence().
/// 3. As a global static one-liner: SwitchInteraction.PlayHazardDeathVisual(...).
/// </summary>
public class SwitchInteraction : MonoBehaviour
{
    [Header("Visual Object")]
    [Tooltip("The death/failure illustration to show on screen")]
    [SerializeField] private GameObject switchImage;

    [Header("Trigger Mode")]
    [Tooltip("If true, requires player to enter 2D trigger and press [E]. If false, only triggers when called automatically via code.")]
    [SerializeField] private bool triggerOnInteract = true;

    [Header("Death Settings")]
    [Tooltip("How long the death image is shown alone before rewind begins")]
    [SerializeField] private float imageDuration = 2.2f;

    [Tooltip("The educational explanation displayed on the rewind time loop screen")]
    [TextArea(2, 4)]
    [SerializeField] private string educationalReason = "ELECTRICAL HAZARD: You used the light switch after the earthquake while a gas leak was present.";

    [Tooltip("How many seconds are added back to the timer upon rewind (default: 5s, or 15s for candle/prep)")]
    [SerializeField] private float rewindDuration = 5f;

    [Header("Sorting / Layer Settings")]
    [Tooltip("Sorting Order (Layer) to display switchImage on top of everything (Default: 20)")]
    [SerializeField] private int sortingOrder = 20;
    [SerializeField] private string sortingLayerName = "Default";

    [Header("Interaction Prompt")]
    [Tooltip("Visual hint showing E button and label text next to it")]
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private string promptLabel = "Flip the Switch";
    [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.1f, 0f);

    private bool playerInside = false;
    private bool triggered = false;

    public static bool IsAnyHazardActive { get; private set; } = false;

    private static Material s_unlitMaterial;
    public static Material GetUnlitMaterial()
    {
        if (s_unlitMaterial != null) return s_unlitMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");

        if (shader != null)
        {
            s_unlitMaterial = new Material(shader);
            s_unlitMaterial.name = "Hazard_Sprite_Unlit";
        }
#if UNITY_EDITOR
        if (s_unlitMaterial == null)
        {
            string unlitPath = UnityEditor.AssetDatabase.GUIDToAssetPath("9dfc825aed78fcd4ba02077103263b40");
            if (!string.IsNullOrEmpty(unlitPath))
            {
                var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(unlitPath);
                if (mat != null) s_unlitMaterial = new Material(mat);
            }
        }
#endif
        return s_unlitMaterial;
    }

    public bool IsTriggered => triggered;
    public string EducationalReason => educationalReason;
    public GameObject InteractionPrompt => interactionPrompt;
    public string PromptLabel => promptLabel;

    public void SetTriggerOnInteract(bool val) => triggerOnInteract = val;
    public void SetEducationalReason(string reason) => educationalReason = reason;
    public void SetRewindDuration(float duration) => rewindDuration = duration;
    public void SetSortingOrder(int order) => sortingOrder = order;
    public void SetImageDuration(float dur) => imageDuration = dur;
    public void SetSwitchImage(GameObject img) => switchImage = img;
    public void SetPromptLabel(string label)
    {
        promptLabel = label;
        if (interactionPrompt != null)
        {
            var labelComp = interactionPrompt.GetComponent<InteractionPromptLabel>();
            if (labelComp != null) labelComp.SetLabel(promptLabel);
        }
    }

    private readonly List<Canvas> disabledCanvases = new List<Canvas>();
    private readonly List<GameObject> disabledObjects = new List<GameObject>();

    private void Awake()
    {
        EnsureSwitchImageReference();
        ApplySortingOrder(switchImage);
    }

    private void Start()
    {
        EnsureSwitchImageReference();
        ApplySortingOrder(switchImage);

        if (switchImage != null)
        {
            switchImage.SetActive(false);
        }

        EnsureInteractionPrompt();
    }

    private void OnValidate()
    {
        ApplySortingOrder(switchImage);
        if (interactionPrompt != null)
        {
            var labelComp = interactionPrompt.GetComponent<InteractionPromptLabel>();
            if (labelComp != null)
            {
                labelComp.SetLabel(promptLabel);
            }
        }
    }

    private void OnDisable()
    {
        RestoreGameUI();
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
        if (triggered)
        {
            triggered = false;
            IsAnyHazardActive = false;
        }
    }

    private void OnDestroy()
    {
        if (triggered)
        {
            IsAnyHazardActive = false;
        }
    }

    private void EnsureSwitchImageReference()
    {
        if (switchImage == null)
        {
            if (transform.childCount > 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i).gameObject;
                    if (child.name != "InteractionPrompt" && child.name != "E Button")
                    {
                        switchImage = child;
                        break;
                    }
                }
            }
            if (switchImage == null)
            {
                var candidate = GameObject.Find("1000000106_0") ??
                                GameObject.Find("SwitchImage") ??
                                GameObject.Find("switchImage") ??
                                GameObject.Find("DeathImage") ??
                                GameObject.Find("Patlama");

                if (candidate == null)
                {
                    var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var t in allTransforms)
                    {
                        if (t != null && (t.name == "1000000106_0" || t.name == "SwitchImage" || t.name == "switchImage" || t.name == "Patlama"))
                        {
                            candidate = t.gameObject;
                            break;
                        }
                    }
                }

                if (candidate != null)
                {
                    switchImage = candidate;
                }
            }
        }
    }

    public void EnsureInteractionPrompt()
    {
        if (!triggerOnInteract) return;

        if (interactionPrompt == null)
        {
            var child = transform.Find("InteractionPrompt") ?? transform.Find("E Button");
            if (child != null)
            {
                interactionPrompt = child.gameObject;
            }
        }

        if (interactionPrompt == null)
        {
            interactionPrompt = CreateInteractionPrompt();
        }

        if (interactionPrompt != null)
        {
            var labelComp = interactionPrompt.GetComponent<InteractionPromptLabel>();
            if (labelComp == null) labelComp = interactionPrompt.AddComponent<InteractionPromptLabel>();
            labelComp.SetLabel(promptLabel);
            labelComp.Offset = new Vector3(0.5f, 0f, 0f);
            labelComp.FontSize = 5f;
            labelComp.TextColor = Color.white;

            interactionPrompt.SetActive(false);
        }
    }

    private GameObject CreateInteractionPrompt()
    {
        GameObject promptObj = new GameObject("InteractionPrompt");
        promptObj.transform.SetParent(transform, false);
        promptObj.transform.localPosition = promptOffset;
        promptObj.transform.localScale = new Vector3(0.55f, 0.55f, 1f);

        var sr = promptObj.AddComponent<SpriteRenderer>();
        sr.sprite = GetEButtonSprite();
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = 10;

        var labelComp = promptObj.AddComponent<InteractionPromptLabel>();
        labelComp.SetLabel(promptLabel);
        labelComp.Offset = new Vector3(0.5f, 0f, 0f);
        labelComp.FontSize = 5f;
        labelComp.TextColor = Color.white;

        return promptObj;
    }

    private Sprite GetEButtonSprite()
    {
        var sprite = Resources.Load<Sprite>("EButton");
        if (sprite != null) return sprite;

        var tex = Resources.Load<Texture2D>("EButton");
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s != null && (s.name.IndexOf("E Button", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              s.name.IndexOf("EButton", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return s;
            }
        }
        return null;
    }

    [ContextMenu("Create Interaction Prompt in Editor")]
    public void ManualCreatePromptInEditor()
    {
        EnsureInteractionPrompt();
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(true);
        }
    }

    public void ApplySortingOrder(GameObject obj)
    {
        if (obj == null) return;

        var unlitMat = GetUnlitMaterial();

        // 1. SpriteRenderers
        var spriteRenderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in spriteRenderers)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                sr.sortingLayerName = sortingLayerName;
            }
            sr.sortingOrder = sortingOrder;

            // Make sure the hazard visual is 100% unlit (full bright independent of room lighting / cut power)
            if (unlitMat != null)
            {
                sr.material = unlitMat;
            }
        }

        // 2. Canvases (if UI)
        var canvases = obj.GetComponentsInChildren<Canvas>(true);
        foreach (var canvas in canvases)
        {
            canvas.overrideSorting = true;
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                canvas.sortingLayerName = sortingLayerName;
            }
            canvas.sortingOrder = sortingOrder;
        }

        // 3. If obj has a RectTransform inside a parent Canvas but has no Canvas component of its own
        if (obj.GetComponent<RectTransform>() != null && obj.GetComponent<Canvas>() == null)
        {
            var canvas = obj.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                canvas.sortingLayerName = sortingLayerName;
            }
            canvas.sortingOrder = sortingOrder;
        }

        // 4. SortingGroups
        var sortingGroups = obj.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>(true);
        foreach (var sg in sortingGroups)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                sg.sortingLayerName = sortingLayerName;
            }
            sg.sortingOrder = sortingOrder;
        }

        // 5. MeshRenderers
        var meshRenderers = obj.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var mr in meshRenderers)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                mr.sortingLayerName = sortingLayerName;
            }
            mr.sortingOrder = sortingOrder;
        }

        // Ensure Z position is in front of room sprites
        Vector3 pos = obj.transform.position;
        if (pos.z > -1f)
        {
            obj.transform.position = new Vector3(pos.x, pos.y, -1f);
        }
    }

    private void HideGameUI()
    {
        disabledCanvases.Clear();
        disabledObjects.Clear();

        // 1. Tell GameTimelineManager to hide timer & inventory text
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.SetHUDVisibility(false);
        }

        // 2. Hide PersistentCanvas (holds inventory and timer)
        if (PersistentCanvas.Instance != null)
        {
            var c = PersistentCanvas.Instance.GetComponent<Canvas>();
            if (c != null && c.enabled)
            {
                c.enabled = false;
                disabledCanvases.Add(c);
            }
        }

        // 3. Disable any other active screen-space canvases (except switchImage and TimeRewind)
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (switchImage != null && c.transform.IsChildOf(switchImage.transform)) continue;
            if (TimeRewindController.Instance != null && c.transform.IsChildOf(TimeRewindController.Instance.transform)) continue;
            if (c.name.IndexOf("TimeRewind", StringComparison.OrdinalIgnoreCase) >= 0) continue;

            if (c.enabled)
            {
                c.enabled = false;
                if (!disabledCanvases.Contains(c))
                {
                    disabledCanvases.Add(c);
                }
            }
        }

        // 4. Directly hide any standalone timer or inventory objects by common names
        string[] uiNames = new string[] { "InventoryPanel", "Inventory", "TimerText", "Timer", "QTE", "HUDStatusPanel" };
        foreach (var name in uiNames)
        {
            var go = GameObject.Find(name);
            if (go != null && go.activeSelf)
            {
                go.SetActive(false);
                disabledObjects.Add(go);
            }
        }
    }

    private void RestoreGameUI()
    {
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.SetHUDVisibility(true);
        }

        foreach (var c in disabledCanvases)
        {
            if (c != null) c.enabled = true;
        }
        disabledCanvases.Clear();

        foreach (var go in disabledObjects)
        {
            if (go != null) go.SetActive(true);
        }
        disabledObjects.Clear();

        if (PersistentCanvas.Instance != null)
        {
            var c = PersistentCanvas.Instance.GetComponent<Canvas>();
            if (c != null) c.enabled = true;
        }

        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(playerInside && triggerOnInteract && !triggered);
        }
    }

    private void Update()
    {
        if (!triggerOnInteract) return;

        // The kitchen light switch is only interactable in Stage 3 (after the earthquake)
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.CurrentStage != GameStage.Stage3_Utilities)
        {
            if (interactionPrompt != null && interactionPrompt.activeSelf)
            {
                interactionPrompt.SetActive(false);
            }
            return;
        }

        if (playerInside &&
            !triggered &&
            Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            TriggerSequence();
        }
    }

    /// <summary>
    /// Programmatically triggers the death sequence (for automatic events like candle fire or falling painting).
    /// </summary>
    public void TriggerSequence()
    {
        if (triggered) return;
        StartCoroutine(SwitchDeathSequence(educationalReason, rewindDuration, imageDuration));
    }

    /// <summary>
    /// Programmatically triggers the death sequence with custom message, rewind duration, and display time.
    /// </summary>
    public void TriggerSequence(string customReason, float customRewindDuration, float customImageDuration = 1.5f)
    {
        if (triggered) return;
        StartCoroutine(SwitchDeathSequence(customReason, customRewindDuration, customImageDuration));
    }

    /// <summary>
    /// Static one-liner to play any death visual from anywhere:
    /// SwitchInteraction.PlayHazardDeathVisual(fireImage, "APARTMENT FIRE...", 15f);
    /// </summary>
    public static void PlayHazardDeathVisual(GameObject imageObj, string reason, float rewindSeconds = 5f, float holdDuration = 1.5f, int order = 20)
    {
        if (imageObj == null)
        {
            if (TimeRewindController.Instance != null)
            {
                TimeRewindController.Instance.PlayTimeRewind(reason, rewindSeconds, () => {}, () => {});
            }
            return;
        }

        var comp = imageObj.GetComponent<SwitchInteraction>();
        if (comp == null)
        {
            comp = imageObj.AddComponent<SwitchInteraction>();
            comp.switchImage = imageObj;
            comp.triggerOnInteract = false;
            comp.sortingOrder = order;
        }
        comp.TriggerSequence(reason, rewindSeconds, holdDuration);
    }

    public void CenterOnCamera(GameObject obj)
    {
        if (obj == null) return;
        Camera cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            Vector3 camPos = cam.transform.position;
            obj.transform.position = new Vector3(camPos.x, camPos.y, -1f);
        }
    }

    private IEnumerator SwitchDeathSequence(string reason, float rewindSeconds, float holdDuration)
    {
        triggered = true;
        IsAnyHazardActive = true;

        // 1. FREEZE / STOP TIME
        Time.timeScale = 0f;
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.PauseTimer();
        }

        // Immediately turn off all player lights (flashlight, character light, etc.)
        var allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            p.TurnOffAllLights();
            p.enabled = false;
            var rb = p.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        var cam = FindFirstObjectByType<RoomCamera>();
        if (cam != null)
        {
            cam.StopEarthquake();
        }

        if (QTEManager.Instance != null)
        {
            QTEManager.Instance.CancelAll();
        }

        var choiceGroup = FindFirstObjectByType<WorldChoiceGroup>(FindObjectsInactive.Include);
        if (choiceGroup != null)
        {
            choiceGroup.gameObject.SetActive(false);
        }

        // 2. Hide UI so the death visual has full screen focus
        HideGameUI();
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }

        // 3. Center on camera & display on Layer 20
        EnsureSwitchImageReference();
        if (switchImage != null)
        {
            CenterOnCamera(switchImage);
            ApplySortingOrder(switchImage);
            switchImage.SetActive(true);
        }

        // 4. Hold visual for holdDuration seconds while time is frozen
        yield return new WaitForSecondsRealtime(holdDuration);

        // 5. Initiate rewind with visual open until eyelids close
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.TriggerRewind(
                reason,
                isFromVisual: true,
                onStateRestore: () =>
                {
                    if (switchImage != null)
                    {
                        switchImage.SetActive(false);
                    }

                    RestoreGameUI();
                    Time.timeScale = 1f;
                    if (GameTimelineManager.Instance != null)
                    {
                        GameTimelineManager.Instance.ResumeTimer();
                    }
                    triggered = false;
                    IsAnyHazardActive = false;
                },
                onComplete: () =>
                {
                    Time.timeScale = 1f;
                    RestoreGameUI();
                    triggered = false;
                    IsAnyHazardActive = false;
                    Debug.Log($"[HazardDeath] Rewind completed for: {reason}");
                }
            );
        }
        else if (TimeRewindController.Instance != null)
        {
            TimeRewindController.Instance.PlayTimeRewind(
                reason,
                rewindSeconds,

                onRewindStateRestore: () =>
                {
                    if (switchImage != null)
                    {
                        switchImage.SetActive(false);
                    }

                    RestoreGameUI();
                    Time.timeScale = 1f;
                    triggered = false;
                    IsAnyHazardActive = false;
                },

                onComplete: () =>
                {
                    Time.timeScale = 1f;
                    RestoreGameUI();
                    triggered = false;
                    IsAnyHazardActive = false;
                    Debug.Log($"[HazardDeath] Rewind completed for: {reason}");
                }
            );
        }
        else
        {
            if (switchImage != null)
            {
                switchImage.SetActive(false);
            }

            Time.timeScale = 1f;
            RestoreGameUI();
            triggered = false;
            IsAnyHazardActive = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && triggerOnInteract && !triggered)
        {
            playerInside = true;

            // Only show prompt in Stage 3 (after the earthquake)
            if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.CurrentStage != GameStage.Stage3_Utilities)
            {
                return;
            }

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && triggerOnInteract)
        {
            playerInside = false;
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }
    }
}