using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Universal animated visual cue for interactive mini-games and game interactions.
/// Displays clear, animated player guidance:
/// - "TURN" badges with directional curved arrows (clockwise / counter-clockwise).
/// - "DRAG & DROP" cues with animated upward arrows and target zone frames.
/// - "CLICK" / "HOLD" cues with animated pointer arrows.
/// Automatically adapts to world-space targets via ScreenSpaceOverlay Canvas tracking.
/// </summary>
public class InteractionVisualCue : MonoBehaviour
{
    public enum CueType
    {
        TurnClockwise,
        TurnCounterClockwise,
        DragAndDrop,
        ClickPrompt,
        HoldPrompt
    }

    [Header("Core References")]
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image badgeBackground;
    [SerializeField] private TextMeshProUGUI mainActionText;
    [SerializeField] private TextMeshProUGUI subActionText;
    [SerializeField] private Image arrowImage;
    [SerializeField] private RectTransform arrowRect;
    [SerializeField] private Image targetFrameImage;
    [SerializeField] private RectTransform targetFrameRect;

    // Tracking & Animation State
    private CueType cueType = CueType.TurnClockwise;
    private bool isWorldTracking = false;
    private Vector3 targetWorldPos = Vector3.zero;
    private Vector2 screenOffset = Vector2.zero;
    private Camera targetCamera;
    private float baseArrowRotation = 0f;
    private Vector2 baseArrowAnchoredPos = Vector2.zero;
    private bool isDismissing = false;
    private float dismissTimer = 0f;

    // Cached Procedural Sprites
    private static Sprite s_clockwiseArrow;
    private static Sprite s_counterClockwiseArrow;
    private static Sprite s_straightUpArrow;
    private static Sprite s_straightDownArrow;
    private static Sprite s_targetFrame;
    private static Sprite s_pillBadge;

    #region --- FACTORY CREATION METHODS ---

    /// <summary>
    /// Spawns a "TURN" visual cue with an animated curved arrow signaling which way to turn.
    /// </summary>
    public static InteractionVisualCue CreateTurnCue(
        Transform parentCanvas,
        Vector3 targetWorldPos,
        bool clockwise,
        string mainText = "TURN",
        string subText = "Clockwise",
        Vector2? offset = null,
        Camera cam = null)
    {
        var cue = InstantiateCue(parentCanvas);
        cue.cueType = clockwise ? CueType.TurnClockwise : CueType.TurnCounterClockwise;
        cue.isWorldTracking = true;
        cue.targetWorldPos = targetWorldPos;
        cue.screenOffset = offset ?? (clockwise ? new Vector2(160f, 40f) : new Vector2(-160f, 40f));
        cue.targetCamera = cam != null ? cam : (Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>());

        cue.SetupTurnVisuals(mainText, subText, clockwise);
        cue.UpdatePosition();
        return cue;
    }

    /// <summary>
    /// Spawns a "DRAG & DROP" visual cue with a target zone frame and an animated directional arrow.
    /// </summary>
    public static InteractionVisualCue CreateDragAndDropCue(
        Transform parentCanvas,
        Vector3 targetWorldPos,
        Vector2 dropZoneScreenSize,
        string mainText = "DRAG & DROP",
        string subText = "From inventory to target",
        Vector2? offset = null,
        Camera cam = null)
    {
        var cue = InstantiateCue(parentCanvas);
        cue.cueType = CueType.DragAndDrop;
        cue.isWorldTracking = true;
        cue.targetWorldPos = targetWorldPos;
        cue.screenOffset = offset ?? new Vector2(0f, 85f);
        cue.targetCamera = cam != null ? cam : (Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>());

        cue.SetupDragVisuals(mainText, subText, dropZoneScreenSize);
        cue.UpdatePosition();
        return cue;
    }

    /// <summary>
    /// Spawns a point-and-click or hold prompt with an animated pointing arrow.
    /// </summary>
    public static InteractionVisualCue CreateClickCue(
        Transform parentCanvas,
        Vector3 targetWorldPos,
        string mainText = "CLICK",
        string subText = "",
        bool pointDown = true,
        Vector2? offset = null,
        Camera cam = null)
    {
        var cue = InstantiateCue(parentCanvas);
        cue.cueType = CueType.ClickPrompt;
        cue.isWorldTracking = true;
        cue.targetWorldPos = targetWorldPos;
        cue.screenOffset = offset ?? (pointDown ? new Vector2(0f, 90f) : new Vector2(0f, -90f));
        cue.targetCamera = cam != null ? cam : (Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>());

        cue.SetupClickVisuals(mainText, subText, pointDown);
        cue.UpdatePosition();
        return cue;
    }

    private static InteractionVisualCue InstantiateCue(Transform parentCanvas)
    {
        Transform canvasTransform = parentCanvas;
        if (canvasTransform == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) canvasTransform = canvas.transform;
            else
            {
                var newCanvasObj = new GameObject("InteractionCue_Canvas");
                var c = newCanvasObj.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                var cs = newCanvasObj.AddComponent<CanvasScaler>();
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);
                newCanvasObj.AddComponent<GraphicRaycaster>();
                canvasTransform = newCanvasObj.transform;
            }
        }

        GameObject cueObj = new GameObject("InteractionVisualCue");
        cueObj.transform.SetParent(canvasTransform, false);
        return cueObj.AddComponent<InteractionVisualCue>();
    }

    #endregion

    #region --- VISUAL SETUP ---

    private void Awake()
    {
        BuildUIHierarchy();
    }

    private void BuildUIHierarchy()
    {
        if (rootRect != null) return;

        rootRect = gameObject.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(280f, 100f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = false; // Never block player clicks/drags
        canvasGroup.interactable = false;

        // 1. Target Zone Reticle (Dashed/Bracket outline)
        var targetObj = new GameObject("TargetReticle");
        targetObj.transform.SetParent(transform, false);
        targetFrameRect = targetObj.AddComponent<RectTransform>();
        targetFrameRect.sizeDelta = new Vector2(160f, 160f);
        targetFrameRect.anchoredPosition = Vector2.zero;
        targetFrameImage = targetObj.AddComponent<Image>();
        targetFrameImage.sprite = GetTargetFrameSprite();
        targetFrameImage.color = new Color(1f, 0.85f, 0.25f, 0.85f); // Bright golden glow
        targetFrameImage.raycastTarget = false;
        targetObj.SetActive(false);

        // 2. Badge Container (Pill badge holding text)
        var badgeObj = new GameObject("BadgeBackground");
        badgeObj.transform.SetParent(transform, false);
        var badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.sizeDelta = new Vector2(240f, 62f);
        badgeRect.anchoredPosition = Vector2.zero;
        badgeBackground = badgeObj.AddComponent<Image>();
        badgeBackground.sprite = GetPillBadgeSprite();
        badgeBackground.color = new Color(0.08f, 0.08f, 0.10f, 0.92f);
        badgeBackground.raycastTarget = false;

        // 3. Main Action Text (Bold e.g. "TURN", "DRAG & DROP", "CLICK")
        var mainTextObj = new GameObject("MainActionText");
        mainTextObj.transform.SetParent(badgeObj.transform, false);
        var mainRect = mainTextObj.AddComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0f, 0.35f);
        mainRect.anchorMax = new Vector2(1f, 0.95f);
        mainRect.offsetMin = new Vector2(10f, 0f);
        mainRect.offsetMax = new Vector2(-10f, 0f);

        mainActionText = mainTextObj.AddComponent<TextMeshProUGUI>();
        mainActionText.fontSize = 24f;
        mainActionText.fontStyle = FontStyles.Bold;
        mainActionText.alignment = TextAlignmentOptions.Center;
        mainActionText.color = new Color(1.0f, 0.82f, 0.20f, 1f); // Vibrant amber gold
        mainActionText.raycastTarget = false;

        // 4. Subtitle Action Text (e.g. "Clockwise", "From inventory")
        var subTextObj = new GameObject("SubActionText");
        subTextObj.transform.SetParent(badgeObj.transform, false);
        var subRect = subTextObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0.05f);
        subRect.anchorMax = new Vector2(1f, 0.42f);
        subRect.offsetMin = new Vector2(10f, 0f);
        subRect.offsetMax = new Vector2(-10f, 0f);

        subActionText = subTextObj.AddComponent<TextMeshProUGUI>();
        subActionText.fontSize = 14f;
        subActionText.fontStyle = FontStyles.Normal;
        subActionText.alignment = TextAlignmentOptions.Center;
        subActionText.color = new Color(0.95f, 0.95f, 0.95f, 0.90f);
        subActionText.raycastTarget = false;

        // 5. Directional Arrow
        var arrowObj = new GameObject("DirectionalArrow");
        arrowObj.transform.SetParent(transform, false);
        arrowRect = arrowObj.AddComponent<RectTransform>();
        arrowRect.sizeDelta = new Vector2(90f, 90f);
        arrowRect.anchoredPosition = new Vector2(0f, -65f);
        arrowImage = arrowObj.AddComponent<Image>();
        arrowImage.color = new Color(1.0f, 0.85f, 0.25f, 1f);
        arrowImage.raycastTarget = false;
    }

    private void SetupTurnVisuals(string mainText, string subText, bool clockwise)
    {
        if (targetFrameImage != null) targetFrameImage.gameObject.SetActive(false);

        mainActionText.text = mainText;
        subActionText.text = subText;
        subActionText.gameObject.SetActive(!string.IsNullOrEmpty(subText));

        arrowImage.sprite = clockwise ? GetClockwiseArrowSprite() : GetCounterClockwiseArrowSprite();
        arrowImage.gameObject.SetActive(true);
        arrowRect.sizeDelta = new Vector2(90f, 90f);

        // Position arrow next to badge
        if (clockwise)
        {
            arrowRect.anchoredPosition = new Vector2(140f, 0f);
            baseArrowRotation = 0f;
        }
        else
        {
            arrowRect.anchoredPosition = new Vector2(-140f, 0f);
            baseArrowRotation = 0f;
        }
        baseArrowAnchoredPos = arrowRect.anchoredPosition;
    }

    private void SetupDragVisuals(string mainText, string subText, Vector2 dropZoneSize)
    {
        if (targetFrameImage != null)
        {
            targetFrameImage.gameObject.SetActive(true);
            targetFrameRect.sizeDelta = dropZoneSize;
            targetFrameRect.anchoredPosition = -screenOffset; // Align directly over the target center
        }

        mainActionText.text = mainText;
        subActionText.text = subText;
        subActionText.gameObject.SetActive(!string.IsNullOrEmpty(subText));

        arrowImage.sprite = GetStraightUpArrowSprite();
        arrowImage.gameObject.SetActive(true);
        arrowRect.sizeDelta = new Vector2(64f, 80f);
        arrowRect.anchoredPosition = new Vector2(0f, -75f);
        arrowRect.localRotation = Quaternion.identity;
        baseArrowAnchoredPos = arrowRect.anchoredPosition;
    }

    private void SetupClickVisuals(string mainText, string subText, bool pointDown)
    {
        if (targetFrameImage != null) targetFrameImage.gameObject.SetActive(false);

        mainActionText.text = mainText;
        subActionText.text = subText;
        subActionText.gameObject.SetActive(!string.IsNullOrEmpty(subText));

        arrowImage.sprite = pointDown ? GetStraightDownArrowSprite() : GetStraightUpArrowSprite();
        arrowImage.gameObject.SetActive(true);
        arrowRect.sizeDelta = new Vector2(58f, 72f);
        arrowRect.anchoredPosition = pointDown ? new Vector2(0f, -60f) : new Vector2(0f, 60f);
        arrowRect.localRotation = Quaternion.identity;
        baseArrowAnchoredPos = arrowRect.anchoredPosition;
    }

    #endregion

    #region --- RUNTIME ANIMATION & TRACKING ---

    private void Update()
    {
        if (isDismissing)
        {
            dismissTimer += Time.deltaTime;
            float t = dismissTimer / 0.25f;
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.8f, t);
            if (t >= 1f) Destroy(gameObject);
            return;
        }

        UpdatePosition();
        AnimateVisuals();
    }

    public void UpdatePosition()
    {
        if (!isWorldTracking) return;

        if (targetCamera == null)
        {
            targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }

        if (targetCamera != null)
        {
            Vector3 screenPt = targetCamera.WorldToScreenPoint(targetWorldPos);
            // Hide if behind camera
            if (screenPt.z < 0)
            {
                if (canvasGroup.alpha > 0f) canvasGroup.alpha = 0f;
                return;
            }

            if (canvasGroup.alpha < 1f && !isDismissing) canvasGroup.alpha = 1f;
            transform.position = screenPt + (Vector3)screenOffset;
        }
    }

    private void AnimateVisuals()
    {
        float time = Time.time;

        // Subtle gentle breathing scale on the badge
        float badgeScale = 1.0f + 0.035f * Mathf.Sin(time * 3.5f);
        badgeBackground.transform.localScale = new Vector3(badgeScale, badgeScale, 1f);

        // Animate Arrow depending on type
        switch (cueType)
        {
            case CueType.TurnClockwise:
                // Gentle rocking rotation in the clockwise direction to signal turning right
                float rotCw = baseArrowRotation - Mathf.Abs(Mathf.Sin(time * 5.0f)) * 22f;
                arrowRect.localRotation = Quaternion.Euler(0f, 0f, rotCw);
                float scaleCw = 1.0f + 0.08f * Mathf.Sin(time * 5.0f);
                arrowRect.localScale = new Vector3(scaleCw, scaleCw, 1f);
                break;

            case CueType.TurnCounterClockwise:
                // Rocking rotation in counter-clockwise direction to signal turning left
                float rotCcw = baseArrowRotation + Mathf.Abs(Mathf.Sin(time * 5.0f)) * 22f;
                arrowRect.localRotation = Quaternion.Euler(0f, 0f, rotCcw);
                float scaleCcw = 1.0f + 0.08f * Mathf.Sin(time * 5.0f);
                arrowRect.localScale = new Vector3(scaleCcw, scaleCcw, 1f);
                break;

            case CueType.DragAndDrop:
                // Upward bobbing motion directing player from inventory UP to target zone
                float bobUp = Mathf.Sin(time * 4.5f) * 12f;
                arrowRect.anchoredPosition = baseArrowAnchoredPos + new Vector2(0f, bobUp);
                // Target frame gentle breathing
                if (targetFrameRect != null && targetFrameRect.gameObject.activeSelf)
                {
                    float frameScale = 1.0f + 0.06f * Mathf.Sin(time * 3.0f);
                    targetFrameRect.localScale = new Vector3(frameScale, frameScale, 1f);
                }
                break;

            case CueType.ClickPrompt:
            case CueType.HoldPrompt:
                // Downward/Vertical bobbing towards click target
                float bobClick = Mathf.Sin(time * 4.5f) * 9f;
                arrowRect.anchoredPosition = baseArrowAnchoredPos + new Vector2(0f, bobClick);
                break;
        }
    }

    #endregion

    #region --- PUBLIC CONTROL METHODS ---

    public void SetTargetWorldPos(Vector3 pos)
    {
        targetWorldPos = pos;
        UpdatePosition();
    }

    public void SetScreenOffset(Vector2 offset)
    {
        screenOffset = offset;
        UpdatePosition();
    }

    public void UpdateText(string mainText, string subText = "")
    {
        if (mainActionText != null) mainActionText.text = mainText;
        if (subActionText != null)
        {
            subActionText.text = subText;
            subActionText.gameObject.SetActive(!string.IsNullOrEmpty(subText));
        }
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void Dismiss()
    {
        if (isDismissing) return;
        isDismissing = true;
        dismissTimer = 0f;
    }

    #endregion

    #region --- PROCEDURAL SPRITE GENERATION ---

    public static Sprite GetClockwiseArrowSprite()
    {
        if (s_clockwiseArrow != null) return s_clockwiseArrow;
        s_clockwiseArrow = GenerateCurvedArrow(true);
        return s_clockwiseArrow;
    }

    public static Sprite GetCounterClockwiseArrowSprite()
    {
        if (s_counterClockwiseArrow != null) return s_counterClockwiseArrow;
        s_counterClockwiseArrow = GenerateCurvedArrow(false);
        return s_counterClockwiseArrow;
    }

    public static Sprite GetStraightUpArrowSprite()
    {
        if (s_straightUpArrow != null) return s_straightUpArrow;
        s_straightUpArrow = GenerateStraightArrow(true);
        return s_straightUpArrow;
    }

    public static Sprite GetStraightDownArrowSprite()
    {
        if (s_straightDownArrow != null) return s_straightDownArrow;
        s_straightDownArrow = GenerateStraightArrow(false);
        return s_straightDownArrow;
    }

    public static Sprite GetTargetFrameSprite()
    {
        if (s_targetFrame != null) return s_targetFrame;
        s_targetFrame = GenerateTargetFrame();
        return s_targetFrame;
    }

    public static Sprite GetPillBadgeSprite()
    {
        if (s_pillBadge != null) return s_pillBadge;
        s_pillBadge = GeneratePillBadge();
        return s_pillBadge;
    }

    /// <summary>
    /// Generates a smooth, anti-aliased curved rotation arrow with a distinct arrowhead.
    /// </summary>
    private static Sprite GenerateCurvedArrow(bool clockwise)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.35f;
        float halfStroke = size * 0.055f;
        Color arrowColor = new Color(1.0f, 0.84f, 0.18f, 1f); // Rich gold
        Color outlineColor = new Color(0.12f, 0.08f, 0.02f, 0.95f); // Crisp dark rim

        // Clockwise arc from 170 deg (top-left) to 15 deg (right side pointing downwards)
        // Counter-clockwise arc from 10 deg (top-right) to 165 deg (left side pointing downwards)
        float startAngle = clockwise ? 175f : 5f;
        float endAngle = clockwise ? 20f : 160f;

        // Arrowhead tip and wings
        Vector2 tipPos, wingL, wingR;
        if (clockwise)
        {
            float tipRad = 5f * Mathf.Deg2Rad;
            tipPos = center + new Vector2(Mathf.Cos(tipRad), Mathf.Sin(tipRad)) * (radius - 2f);
            wingL = tipPos + new Vector2(-16f, 22f);
            wingR = tipPos + new Vector2(14f, 16f);
        }
        else
        {
            float tipRad = 175f * Mathf.Deg2Rad;
            tipPos = center + new Vector2(Mathf.Cos(tipRad), Mathf.Sin(tipRad)) * (radius - 2f);
            wingL = tipPos + new Vector2(16f, 22f);
            wingR = tipPos + new Vector2(-14f, 16f);
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
                float dist = Vector2.Distance(pt, center);
                float angle = Mathf.Atan2(pt.y - center.y, pt.x - center.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;

                // 1. Arc segment
                bool inAngle = false;
                if (clockwise)
                {
                    inAngle = (angle >= endAngle && angle <= startAngle);
                }
                else
                {
                    inAngle = (angle >= startAngle && angle <= endAngle);
                }

                float distToArc = Mathf.Abs(dist - radius);
                bool inArc = inAngle && distToArc <= (halfStroke + 2.5f);

                // 2. Arrowhead triangle
                bool inHead = IsPointInTriangle(pt, tipPos, wingL, wingR);

                if (inArc || inHead)
                {
                    // Check if it's on the border
                    bool isBorder = false;
                    if (inArc && distToArc >= (halfStroke - 1.2f)) isBorder = true;
                    if (inHead)
                    {
                        float dL = DistanceToSegment(pt, tipPos, wingL);
                        float dR = DistanceToSegment(pt, tipPos, wingR);
                        float dB = DistanceToSegment(pt, wingL, wingR);
                        if (Mathf.Min(dL, Mathf.Min(dR, dB)) <= 1.8f) isBorder = true;
                    }

                    pixels[idx] = isBorder ? outlineColor : arrowColor;
                }
                else
                {
                    pixels[idx] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Generates a bold straight arrow (Upward by default, or Downward).
    /// </summary>
    private static Sprite GenerateStraightArrow(bool pointingUp)
    {
        int w = 96;
        int h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];

        Color arrowColor = new Color(1.0f, 0.85f, 0.22f, 1f);
        Color outlineColor = new Color(0.12f, 0.08f, 0.02f, 0.95f);

        Vector2 tip = pointingUp ? new Vector2(w * 0.5f, h - 8f) : new Vector2(w * 0.5f, 8f);
        Vector2 wingL = pointingUp ? new Vector2(8f, h * 0.45f) : new Vector2(8f, h * 0.55f);
        Vector2 wingR = pointingUp ? new Vector2(w - 8f, h * 0.45f) : new Vector2(w - 8f, h * 0.55f);

        float shaftMinX = w * 0.5f - 14f;
        float shaftMaxX = w * 0.5f + 14f;
        float shaftMinY = pointingUp ? 8f : h * 0.48f;
        float shaftMaxY = pointingUp ? h * 0.52f : h - 8f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);

                bool inHead = IsPointInTriangle(pt, tip, wingL, wingR);
                bool inShaft = (pt.x >= shaftMinX && pt.x <= shaftMaxX && pt.y >= shaftMinY && pt.y <= shaftMaxY);

                if (inHead || inShaft)
                {
                    bool isBorder = false;
                    if (inShaft && (pt.x <= shaftMinX + 2.5f || pt.x >= shaftMaxX - 2.5f || (pointingUp && pt.y <= shaftMinY + 2.5f) || (!pointingUp && pt.y >= shaftMaxY - 2.5f)))
                    {
                        isBorder = true;
                    }
                    if (inHead)
                    {
                        float dL = DistanceToSegment(pt, tip, wingL);
                        float dR = DistanceToSegment(pt, tip, wingR);
                        float dB = DistanceToSegment(pt, wingL, wingR);
                        if (Mathf.Min(dL, Mathf.Min(dR, dB)) <= 2.2f) isBorder = true;
                    }

                    pixels[idx] = isBorder ? outlineColor : arrowColor;
                }
                else
                {
                    pixels[idx] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Generates a glowing dashed corner-bracket reticle for drop targets.
    /// </summary>
    private static Sprite GenerateTargetFrame()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        Color bracketColor = new Color(1.0f, 0.88f, 0.25f, 0.95f);
        Color glowColor = new Color(1.0f, 0.75f, 0.15f, 0.35f);

        int pad = 8;
        int len = 34; // Bracket arm length
        int thick = 5;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                bool isCornerBracket = false;

                // Top-Left
                if ((x >= pad && x <= pad + len && y >= size - pad - thick && y <= size - pad) ||
                    (x >= pad && x <= pad + thick && y >= size - pad - len && y <= size - pad))
                    isCornerBracket = true;

                // Top-Right
                if ((x >= size - pad - len && x <= size - pad && y >= size - pad - thick && y <= size - pad) ||
                    (x >= size - pad - thick && x <= size - pad && y >= size - pad - len && y <= size - pad))
                    isCornerBracket = true;

                // Bottom-Left
                if ((x >= pad && x <= pad + len && y >= pad && y <= pad + thick) ||
                    (x >= pad && x <= pad + thick && y >= pad && y <= pad + len))
                    isCornerBracket = true;

                // Bottom-Right
                if ((x >= size - pad - len && x <= size - pad && y >= pad && y <= pad + thick) ||
                    (x >= size - pad - thick && x <= size - pad && y >= pad && y <= pad + len))
                    isCornerBracket = true;

                if (isCornerBracket)
                {
                    pixels[idx] = bracketColor;
                }
                else
                {
                    // Subtle interior target dashed border or glow
                    bool isInnerDashed = false;
                    if ((x == pad + 4 || x == size - pad - 4 || y == pad + 4 || y == size - pad - 4) && ((x + y) / 8) % 2 == 0)
                    {
                        isInnerDashed = true;
                    }
                    pixels[idx] = isInnerDashed ? glowColor : Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Generates a clean rounded pill badge background texture.
    /// </summary>
    private static Sprite GeneratePillBadge()
    {
        int w = 256;
        int h = 72;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];

        Color fillColor = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        Color rimColor = new Color(1.0f, 0.82f, 0.22f, 0.95f);
        float radius = h * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                float dist = 0f;

                if (x < radius)
                {
                    dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius));
                }
                else if (x > w - radius)
                {
                    dist = Vector2.Distance(new Vector2(x, y), new Vector2(w - radius, radius));
                }
                else
                {
                    dist = Mathf.Abs(y - radius);
                }

                if (dist <= radius)
                {
                    if (dist >= radius - 2.5f)
                    {
                        pixels[idx] = rimColor;
                    }
                    else
                    {
                        pixels[idx] = fillColor;
                    }
                }
                else
                {
                    pixels[idx] = Color.clear;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        float lenSq = (b - a).sqrMagnitude;
        if (lenSq == 0f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / lenSq);
        Vector2 projection = a + t * (b - a);
        return Vector2.Distance(p, projection);
    }

    #endregion
}
