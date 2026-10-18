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

    public static bool IsAnyHazardActive { get; set; } = false;

    private static Material s_unlitArtworkMaterial;
    private static Material s_unlitBackdropMaterial;

    public static Material GetUnlitArtworkMaterial()
    {
        if (s_unlitArtworkMaterial != null) return s_unlitArtworkMaterial;
        s_unlitArtworkMaterial = CreateUnlitSpriteMaterial("Hazard_Artwork_Unlit", Color.white);
        return s_unlitArtworkMaterial;
    }

    public static Material GetUnlitBackdropMaterial()
    {
        if (s_unlitBackdropMaterial != null) return s_unlitBackdropMaterial;
        s_unlitBackdropMaterial = CreateUnlitSpriteMaterial("Hazard_Backdrop_Unlit", Color.black);
        return s_unlitBackdropMaterial;
    }

    public static Material GetUnlitMaterial() => GetUnlitArtworkMaterial();

    private static Material CreateUnlitSpriteMaterial(string matName, Color tintColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

#if UNITY_EDITOR
        if (shader == null)
        {
            var sourceMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (sourceMat != null)
            {
                var copy = new Material(sourceMat);
                copy.name = matName;
                copy.color = tintColor;
                return copy;
            }
        }
#endif

        if (shader != null)
        {
            var mat = new Material(shader);
            mat.name = matName;
            mat.color = tintColor;
            return mat;
        }

        return null;
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

        var artworkMat = GetUnlitArtworkMaterial();
        var backdropMat = GetUnlitBackdropMaterial();

        // 1. SpriteRenderers
        var spriteRenderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in spriteRenderers)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                sr.sortingLayerName = sortingLayerName;
            }

            if (sr.gameObject.name == "Backdrop")
            {
                sr.sortingOrder = sortingOrder - 2; // Always 18, strictly behind the comic panel (20)
                sr.color = Color.black;
                if (backdropMat != null) sr.sharedMaterial = backdropMat;
            }
            else
            {
                sr.sortingOrder = sortingOrder; // 20
                sr.color = Color.white;
                if (artworkMat != null) sr.sharedMaterial = artworkMat;
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

        // 4. TextMeshPro components (for placeholders)
        var tmps = obj.GetComponentsInChildren<TMPro.TextMeshPro>(true);
        foreach (var tmp in tmps)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
            {
                tmp.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            }
            tmp.sortingOrder = sortingOrder + 2; // 22, always visible in front
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

        // Dynamic prompt label
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsGasShutOff)
        {
            SetPromptLabel(GameTimelineManager.Instance.IsElectricityRestored ? "Turn Off Electricity" : "Turn On Electricity");
        }

        bool ePressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool clickPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (playerInside && !triggered && (ePressed || clickPressed))
        {
            // If gas is NOT shut off -> EXPLOSION!
            if (GameTimelineManager.Instance != null && !GameTimelineManager.Instance.IsGasShutOff)
            {
                TriggerSequence();
            }
            else
            {
                // Gas IS shut off -> Safe to flip switch and turn on house electricity!
                ToggleSafeLight();
            }
        }
    }

    private bool isSafeLightOn = false;
    private GameObject safeRoomLightObj;

    private void ToggleSafeLight()
    {
        var timeline = GameTimelineManager.Instance;
        if (timeline != null)
        {
            bool newState = !timeline.IsElectricityRestored;
            timeline.SetElectricityRestored(newState);
            AudioManager.PlaySwitchToggle(newState);
            SetPromptLabel(newState ? "Turn Off Electricity" : "Turn On Electricity");
            Debug.Log($"<color=yellow>[LightSwitch] House electricity switched {(newState ? "ON" : "OFF")}. Safe operation because gas valve was turned off.</color>");
        }
        else
        {
            isSafeLightOn = !isSafeLightOn;
            AudioManager.PlaySwitchToggle(isSafeLightOn);
            if (safeRoomLightObj == null)
            {
                safeRoomLightObj = new GameObject("KitchenSafeRoomLight");
                safeRoomLightObj.transform.position = transform.position + new Vector3(0f, 1f, 0f);
                var light2D = safeRoomLightObj.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
                light2D.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
                light2D.color = new Color(1f, 0.95f, 0.8f, 1f);
                light2D.intensity = 1.4f;
                light2D.pointLightOuterRadius = 6.5f;
                light2D.pointLightInnerRadius = 1.2f;
            }
            if (safeRoomLightObj != null) safeRoomLightObj.SetActive(isSafeLightOn);
            SetPromptLabel(isSafeLightOn ? "Turn Off Light" : "Turn On Light");
        }
    }

    public static void ResetAllHazardStates()
    {
        IsAnyHazardActive = false;
        var allSwitches = FindObjectsByType<SwitchInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var s in allSwitches)
        {
            if (s != null) s.triggered = false;
        }
    }

    public void HideVisual()
    {
        if (switchImage != null)
        {
            switchImage.SetActive(false);
        }
        triggered = false;
        IsAnyHazardActive = false;
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
    public void TriggerSequence(string customReason, float customRewindDuration, float customImageDuration = 2.2f, Action onDone = null)
    {
        if (triggered) return;
        StartCoroutine(SwitchDeathSequence(customReason, customRewindDuration, customImageDuration, onDone));
    }

    /// <summary>
    /// Static one-liner to play any death visual from anywhere:
    /// SwitchInteraction.PlayHazardDeathVisual(fireImage, "APARTMENT FIRE...", 15f);
    /// </summary>
    public static void PlayHazardDeathVisual(GameObject imageObj, string reason, float rewindSeconds = 5f, float holdDuration = 2.2f, int order = 20)
    {
        if (imageObj != null)
        {
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
        else
        {
            if (HazardVisualManager.Instance != null && HazardVisualManager.Instance.TriggerHazard(reason))
            {
                return;
            }

            if (GameTimelineManager.Instance != null)
            {
                GameTimelineManager.Instance.TriggerRewind(reason);
            }
            else if (TimeRewindController.Instance != null)
            {
                TimeRewindController.Instance.PlayTimeRewind(reason, rewindSeconds, () => {}, () => {});
            }
        }
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
            FitVisualToCamera(obj, cam);
        }
    }

    private void FitVisualToCamera(GameObject obj, Camera cam)
    {
        if (obj == null || cam == null) return;

        float camHeight, camWidth;
        if (cam.orthographic)
        {
            camHeight = cam.orthographicSize * 2f;
            camWidth = camHeight * cam.aspect;
        }
        else
        {
            float dist = Mathf.Abs(cam.transform.position.z - obj.transform.position.z);
            camHeight = 2.0f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            camWidth = camHeight * cam.aspect;
        }

        // Coverage with 50% safety margin so absolutely no room/apartment pixels bleed through
        float targetWorldW = camWidth * 1.5f;
        float targetWorldH = camHeight * 1.5f;

        var sr = obj.GetComponent<SpriteRenderer>();
        var backdrop = obj.transform.Find("Backdrop");
        var placeholder = obj.transform.Find("PlaceholderLabel") ?? obj.transform.Find("TempLabel");

        var bdSr = backdrop != null ? backdrop.GetComponent<SpriteRenderer>() : null;
        float bdUnitW = (bdSr != null && bdSr.sprite != null) ? (bdSr.sprite.rect.width / bdSr.sprite.pixelsPerUnit) : 2f;
        float bdUnitH = (bdSr != null && bdSr.sprite != null) ? (bdSr.sprite.rect.height / bdSr.sprite.pixelsPerUnit) : 2f;
        if (bdUnitW < 0.001f) bdUnitW = 2f;
        if (bdUnitH < 0.001f) bdUnitH = 2f;

        if (sr != null && sr.sprite != null)
        {
            float spriteW = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
            float spriteH = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;

            if (spriteW > 0.001f && spriteH > 0.001f)
            {
                // Fit comic panel nicely inside camera viewport preserving its exact aspect ratio
                float scale = Mathf.Min(camHeight / spriteH, camWidth / spriteW);
                obj.transform.localScale = new Vector3(scale, scale, 1f);
                sr.enabled = true;
                sr.color = Color.white;

                // Black underlay fills the entire screen behind the artwork
                if (backdrop != null)
                {
                    float bdLocalScaleX = targetWorldW / (scale * bdUnitW);
                    float bdLocalScaleY = targetWorldH / (scale * bdUnitH);
                    backdrop.localScale = new Vector3(bdLocalScaleX, bdLocalScaleY, 1f);
                    backdrop.localPosition = new Vector3(0f, 0f, 0.5f);
                    backdrop.gameObject.SetActive(true);
                }

                if (placeholder != null)
                {
                    placeholder.gameObject.SetActive(false);
                }
                return;
            }
        }

        // Placeholder fallback: artwork not yet added
        if (sr != null)
        {
            sr.enabled = false;
        }
        obj.transform.localScale = Vector3.one;

        if (backdrop != null)
        {
            float bdLocalScaleX = targetWorldW / bdUnitW;
            float bdLocalScaleY = targetWorldH / bdUnitH;
            backdrop.localScale = new Vector3(bdLocalScaleX, bdLocalScaleY, 1f);
            backdrop.localPosition = new Vector3(0f, 0f, 0.5f);
            backdrop.gameObject.SetActive(true);
        }

        if (placeholder != null)
        {
            placeholder.gameObject.SetActive(true);
            placeholder.localPosition = new Vector3(0f, 0f, -0.2f);
            placeholder.localScale = Vector3.one;
        }
    }

    private void PlayHazardSound(string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            AudioManager.PlayExplosion();
            return;
        }

        if (reason.IndexOf("explosion", StringComparison.OrdinalIgnoreCase) >= 0 ||
            reason.IndexOf("spark", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            AudioManager.PlayExplosion();
        }
        else if (reason.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 reason.IndexOf("candle", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            AudioManager.PlayExplosion();
        }
        else if (reason.IndexOf("painting", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 reason.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            AudioManager.PlayPaintingFall();
        }
        else if (reason.IndexOf("wardrobe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 reason.IndexOf("dolap", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            AudioManager.PlayWardrobeCrash();
        }
        else if (reason.IndexOf("cabinet", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            AudioManager.PlayCabinetCrash();
        }
        else if (reason.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 reason.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 reason.IndexOf("lateral", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 reason.IndexOf("fall", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            AudioManager.PlayDoorRushFall();
        }
        else if (reason.IndexOf("gas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 reason.IndexOf("suffocation", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            AudioManager.PlayGasSuffocation();
        }
        else
        {
            AudioManager.PlayExplosion();
        }
    }

    private IEnumerator SwitchDeathSequence(string reason, float rewindSeconds, float holdDuration, Action onFinishedDisplay = null)
    {
        triggered = true;
        IsAnyHazardActive = true;
        PlayHazardSound(reason);

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
        if (HazardVisualManager.Instance != null)
        {
            var hazardComp = HazardVisualManager.Instance.GetHazard(reason);
            if (hazardComp != null && hazardComp.switchImage != null)
            {
                switchImage = hazardComp.switchImage;
            }
        }

        if (switchImage != null)
        {
            CenterOnCamera(switchImage);
            ApplySortingOrder(switchImage);
            switchImage.SetActive(true);
        }

        // 4. Hold visual for holdDuration seconds while time is frozen, allowing skip on Space/Enter/Click after 0.4s
        float elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > 0.4f)
            {
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
                {
                    break;
                }
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    break;
                }
            }
            yield return null;
        }

        onFinishedDisplay?.Invoke();

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