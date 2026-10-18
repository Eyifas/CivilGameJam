using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the cinematic "Going Back in Time" death/failure rewind mechanism.
/// Features authentic hand-drawn clock artwork (saadut.png, saadurt.png, saaut.png),
/// smooth eyelid blink transitions, counter-clockwise hand rotation, reverse ticking
/// & time-warp audio, concise high-impact Cause of Death presentation, and dynamic
/// time extension (+15s / +5s).
/// </summary>
public class TimeRewindController : MonoBehaviour
{
    public static TimeRewindController Instance { get; private set; }

    [Header("Clock Sprites (Optional Inspector Overrides)")]
    [SerializeField] private Sprite customClockBackground;
    [SerializeField] private Sprite customMinuteHand;
    [SerializeField] private Sprite customHourHand;

    [Header("Audio Settings (Optional Inspector Overrides)")]
    [SerializeField] private AudioClip customRewindWhoosh;
    [SerializeField] private AudioClip customReverseTick;
    [SerializeField] private AudioClip customTimeLockChime;
    [SerializeField] private AudioClip customEyelidThump;

    [Header("Animation Durations")]
    [SerializeField] private float eyeCloseDuration = 0.65f;
    [SerializeField] private float eyeOpenDuration = 0.5f;
    [SerializeField] private float rewindSpinDuration = 2.4f;
    [Tooltip("How long the educational reason and restored time remain on screen for the player to read before returning to gameplay")]
    [SerializeField] private float educationalReasonHoldDuration = 3.5f;

    // UI Structure
    private Canvas overlayCanvas;
    private RectTransform canvasRect;
    private GameObject eyelidsRoot;
    private RectTransform topEyelid;
    private RectTransform bottomEyelid;
    private Image topEyelidImage;
    private Image bottomEyelidImage;
    private Image centerVignette;
    private Image fullBlackOverlay;

    // Clock UI Structure
    private GameObject clockRoot;
    private RectTransform clockPanel;
    private Image clockBackdropImage;
    private RectTransform hourHand;
    private RectTransform minuteHand;
    private Image hourHandImage;
    private Image minuteHandImage;
    private Image clockAura;

    // Text Display Structure (Polished, punchy hierarchy)
    private TMP_Text categoryTagText;
    private TMP_Text primaryTitleText;
    private TMP_Text survivalRuleText;
    private TMP_Text bonusBadgeText;
    private TMP_Text promptText;
    private TMP_Text digitalClockText;

    // Audio
    private AudioSource audioSource;
    private AudioClip synthRewindWhoosh;
    private AudioClip synthReverseTick;
    private AudioClip synthTimeLockChime;
    private AudioClip synthEyelidThump;

    private bool isRewinding = false;

    // Normalized pivot of the clock hands on the 1920x1080 canvas
    // Nut center in saadut/saaut: X = 957.53, Y = 527.63 (from top-left)
    // Normalized in Unity UI (bottom-left origin): (957.53 / 1920, (1080 - 527.63) / 1080)
    private readonly Vector2 clockCenterPivot = new Vector2(0.49871f, 0.51145f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureAudioSource();
        GenerateProceduralAudio();
        BuildRewindOverlayUI();
        SetEyeOpenImmediate(1f); // Eyes fully open initially
    }

    private void EnsureAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D Sound
        audioSource.ignoreListenerPause = true;
    }

    #region --- SPRITE RESOLUTION ---

    /// <summary>
    /// Loads a clock sprite with high-reliability multi-tier fallbacks:
    /// 1. Inspector override
    /// 2. Resources.Load
    /// 3. Resources.LoadAll (for sliced sprite sheets)
    /// 4. AssetDatabase (Editor)
    /// 5. Raw file byte loader (Runtime / disk fallback)
    /// </summary>
    private Sprite EnsureFullScreenSprite(Sprite sprite, Vector2 pivot)
    {
        if (sprite == null) return null;
        if (sprite.texture != null && (sprite.rect.width < 1920 || sprite.rect.height < 1080))
        {
            return Sprite.Create(sprite.texture, new Rect(0, 0, sprite.texture.width, sprite.texture.height), pivot, 100f);
        }
        return sprite;
    }

    private Sprite ResolveClockSprite(Sprite inspectorOverride, string fileName, Vector2 pivot)
    {
        if (inspectorOverride != null) return EnsureFullScreenSprite(inspectorOverride, pivot);

        string baseName = Path.GetFileNameWithoutExtension(fileName);

        // 1. Try Resources.Load
        Sprite res = Resources.Load<Sprite>("TimeRewind/" + baseName);
        if (res == null) res = Resources.Load<Sprite>(baseName);
        if (res != null) return EnsureFullScreenSprite(res, pivot);

        // 2. Try Resources.LoadAll (for multiple sprite sheets)
        Sprite[] allRes = Resources.LoadAll<Sprite>("TimeRewind/" + baseName);
        if (allRes != null && allRes.Length > 0) return EnsureFullScreenSprite(allRes[0], pivot);

#if UNITY_EDITOR
        // 3. Try AssetDatabase in Editor
        string assetPath = "Assets/MainScene/Sprite/" + fileName;
        Sprite adSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (adSprite != null) return EnsureFullScreenSprite(adSprite, pivot);

        UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (assets != null)
        {
            foreach (var a in assets)
            {
                if (a is Sprite s) return EnsureFullScreenSprite(s, pivot);
            }
        }
#endif

        // 4. Runtime raw PNG file fallback
        string[] candidatePaths = new string[]
        {
            Path.Combine(Application.dataPath, "MainScene/Sprite", fileName),
            Path.Combine(Application.dataPath, "Resources/TimeRewind", fileName),
            Path.Combine(Application.dataPath, "Resources", fileName)
        };

        foreach (string path in candidatePaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, 100f);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TimeRewind] Could not load sprite from {path}: {ex.Message}");
                }
            }
        }

        return null;
    }

    #endregion

    #region --- REWIND OVERLAY & UI CONSTRUCTION ---

    private void BuildRewindOverlayUI()
    {
        // 1. Overlay Canvas (ScreenSpaceOverlay with highest sortingOrder)
        GameObject canvasObj = new GameObject("TimeRewindOverlayCanvas");
        canvasObj.transform.SetParent(transform, false);

        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 9999; // Top of everything

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        canvasRect = canvasObj.GetComponent<RectTransform>();

        // 2. Clock Root (Behind the eyelids)
        BuildClockUI(canvasObj);

        // 3. Eyelids Root (On top of Clock so eyelids close over it)
        BuildEyelidsUI(canvasObj);
    }

    private void BuildClockUI(GameObject parent)
    {
        clockRoot = new GameObject("ClockRoot");
        clockRoot.transform.SetParent(parent.transform, false);
        RectTransform rootRect = clockRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        // Dark ambient backdrop (behind clock tiles)
        GameObject bgObj = new GameObject("DarkBackdrop");
        bgObj.transform.SetParent(clockRoot.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.02f, 0.02f, 0.03f, 1f);
        bgImg.raycastTarget = false;

        // Clock Panel Container (1920x1080 full screen)
        GameObject panelObj = new GameObject("ClockPanel");
        panelObj.transform.SetParent(clockRoot.transform, false);
        clockPanel = panelObj.AddComponent<RectTransform>();
        clockPanel.anchorMin = new Vector2(0.5f, 0.5f);
        clockPanel.anchorMax = new Vector2(0.5f, 0.5f);
        clockPanel.pivot = new Vector2(0.5f, 0.5f);
        clockPanel.sizeDelta = new Vector2(1920f, 1080f);

        // Main Clock Background (saadut.png: clock on tiled wall)
        Sprite bgSprite = ResolveClockSprite(customClockBackground, "saadut.png", new Vector2(0.5f, 0.5f));
        GameObject clockBgObj = new GameObject("ClockBackground");
        clockBgObj.transform.SetParent(clockPanel, false);
        RectTransform clockBgRect = clockBgObj.AddComponent<RectTransform>();
        clockBgRect.anchorMin = Vector2.zero;
        clockBgRect.anchorMax = Vector2.one;
        clockBgRect.sizeDelta = Vector2.zero;
        clockBackdropImage = clockBgObj.AddComponent<Image>();
        if (bgSprite != null)
        {
            clockBackdropImage.sprite = bgSprite;
            clockBackdropImage.preserveAspect = true;
        }
        else
        {
            clockBackdropImage.sprite = GenerateClockFaceSprite();
        }
        clockBackdropImage.color = Color.white;
        clockBackdropImage.raycastTarget = false;

        // Subtle Radial Aura Pulse (surrounds clock center)
        GameObject auraObj = new GameObject("ClockAura");
        auraObj.transform.SetParent(clockPanel, false);
        RectTransform auraRect = auraObj.AddComponent<RectTransform>();
        auraRect.anchorMin = new Vector2(0.5f, 0.5f);
        auraRect.anchorMax = new Vector2(0.5f, 0.5f);
        auraRect.pivot = new Vector2(0.5f, 0.5f);
        auraRect.anchoredPosition = new Vector2(-2.5f, 12.4f);
        auraRect.sizeDelta = new Vector2(620f, 620f);
        clockAura = auraObj.AddComponent<Image>();
        clockAura.sprite = GenerateCircleSprite(256, 128);
        clockAura.color = new Color(0.2f, 0.75f, 1f, 0.25f);
        clockAura.raycastTarget = false;

        // Minute Hand (saadurt.png)
        Sprite minuteSprite = ResolveClockSprite(customMinuteHand, "saadurt.png", clockCenterPivot);
        GameObject minObj = new GameObject("MinuteHand");
        minObj.transform.SetParent(clockPanel, false);
        minuteHand = minObj.AddComponent<RectTransform>();
        minuteHand.anchorMin = new Vector2(0.5f, 0.5f);
        minuteHand.anchorMax = new Vector2(0.5f, 0.5f);
        minuteHand.pivot = clockCenterPivot;
        minuteHand.sizeDelta = new Vector2(1920f, 1080f);
        minuteHand.anchoredPosition = Vector2.zero;
        minuteHandImage = minObj.AddComponent<Image>();
        if (minuteSprite != null)
        {
            minuteHandImage.sprite = minuteSprite;
            minuteHandImage.preserveAspect = true;
        }
        else
        {
            minuteHandImage.sprite = GenerateHandSprite(20, 200, new Color(0.95f, 0.88f, 0.65f, 1f));
        }
        minuteHandImage.raycastTarget = false;

        // Hour Hand (saaut.png with center circular nut)
        Sprite hourSprite = ResolveClockSprite(customHourHand, "saaut.png", clockCenterPivot);
        GameObject hourObj = new GameObject("HourHand");
        hourObj.transform.SetParent(clockPanel, false);
        hourHand = hourObj.AddComponent<RectTransform>();
        hourHand.anchorMin = new Vector2(0.5f, 0.5f);
        hourHand.anchorMax = new Vector2(0.5f, 0.5f);
        hourHand.pivot = clockCenterPivot;
        hourHand.sizeDelta = new Vector2(1920f, 1080f);
        hourHand.anchoredPosition = Vector2.zero;
        hourHandImage = hourObj.AddComponent<Image>();
        if (hourSprite != null)
        {
            hourHandImage.sprite = hourSprite;
            hourHandImage.preserveAspect = true;
        }
        else
        {
            hourHandImage.sprite = GenerateHandSprite(26, 140, new Color(0.88f, 0.75f, 0.35f, 1f));
        }
        hourHandImage.raycastTarget = false;

        // Ambient Screen Vignette (softly shades the edges so text stands out sharply)
        GameObject vigObj = new GameObject("VignetteFraming");
        vigObj.transform.SetParent(clockRoot.transform, false);
        RectTransform vigRect = vigObj.AddComponent<RectTransform>();
        vigRect.anchorMin = Vector2.zero;
        vigRect.anchorMax = Vector2.one;
        vigRect.sizeDelta = Vector2.zero;
        Image vigImg = vigObj.AddComponent<Image>();
        vigImg.sprite = GenerateVignetteSprite();
        vigImg.color = new Color(0f, 0f, 0f, 0.35f);
        vigImg.raycastTarget = false;

        // Digital Rolling Time Text (below the clock dial)
        GameObject digObj = new GameObject("DigitalClockText");
        digObj.transform.SetParent(clockRoot.transform, false);
        RectTransform digRect = digObj.AddComponent<RectTransform>();
        digRect.anchorMin = new Vector2(0.5f, 0.5f);
        digRect.anchorMax = new Vector2(0.5f, 0.5f);
        digRect.pivot = new Vector2(0.5f, 0.5f);
        digRect.anchoredPosition = new Vector2(0f, -320f);
        digRect.sizeDelta = new Vector2(400f, 50f);
        digitalClockText = digObj.AddComponent<TextMeshProUGUI>();
        digitalClockText.text = "00:00";
        digitalClockText.fontSize = 32;
        digitalClockText.fontStyle = FontStyles.Bold;
        digitalClockText.alignment = TextAlignmentOptions.Center;
        digitalClockText.color = new Color(1f, 0.9f, 0.45f, 0.95f);

        // -------------------------------------------------------------
        // TOP CARD: Category Tag & Primary Cause / Objective
        // -------------------------------------------------------------
        GameObject topCardObj = new GameObject("TopCard");
        topCardObj.transform.SetParent(clockRoot.transform, false);
        RectTransform topCardRect = topCardObj.AddComponent<RectTransform>();
        topCardRect.anchorMin = new Vector2(0.5f, 1f);
        topCardRect.anchorMax = new Vector2(0.5f, 1f);
        topCardRect.pivot = new Vector2(0.5f, 1f);
        topCardRect.anchoredPosition = new Vector2(0f, -15f);
        topCardRect.sizeDelta = new Vector2(1460f, 130f);

        Image topCardBg = topCardObj.AddComponent<Image>();
        topCardBg.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);
        topCardBg.raycastTarget = false;

        // Category Tag ("CAUSE OF DEATH" / "TIMELINE RESET")
        GameObject catObj = new GameObject("CategoryTagText");
        catObj.transform.SetParent(topCardObj.transform, false);
        RectTransform catRect = catObj.AddComponent<RectTransform>();
        catRect.anchorMin = new Vector2(0.5f, 1f);
        catRect.anchorMax = new Vector2(0.5f, 1f);
        catRect.pivot = new Vector2(0.5f, 1f);
        catRect.anchoredPosition = new Vector2(0f, -12f);
        catRect.sizeDelta = new Vector2(1400f, 32f);
        categoryTagText = catObj.AddComponent<TextMeshProUGUI>();
        categoryTagText.text = "CAUSE OF DEATH";
        categoryTagText.fontSize = 20;
        categoryTagText.fontStyle = FontStyles.Bold;
        categoryTagText.characterSpacing = 4f;
        categoryTagText.alignment = TextAlignmentOptions.Center;
        categoryTagText.color = new Color(1f, 0.35f, 0.35f, 1f);

        // Primary Title ("IGNITED NATURAL GAS LEAK" / "60 SECONDS BEFORE EARTHQUAKE — BE READY!")
        GameObject titleObj = new GameObject("PrimaryTitleText");
        titleObj.transform.SetParent(topCardObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, -14f);
        titleRect.sizeDelta = new Vector2(1400f, 65f);
        primaryTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        primaryTitleText.text = "FATAL HAZARD";
        primaryTitleText.fontSize = 38;
        primaryTitleText.fontStyle = FontStyles.Bold;
        primaryTitleText.alignment = TextAlignmentOptions.Center;
        primaryTitleText.color = Color.white;

        // -------------------------------------------------------------
        // BOTTOM CARD: Survival Rule, Restored Time Badge, Continue Prompt
        // -------------------------------------------------------------
        GameObject botCardObj = new GameObject("BottomCard");
        botCardObj.transform.SetParent(clockRoot.transform, false);
        RectTransform botCardRect = botCardObj.AddComponent<RectTransform>();
        botCardRect.anchorMin = new Vector2(0.5f, 0f);
        botCardRect.anchorMax = new Vector2(0.5f, 0f);
        botCardRect.pivot = new Vector2(0.5f, 0f);
        botCardRect.anchoredPosition = new Vector2(0f, 15f);
        botCardRect.sizeDelta = new Vector2(1460f, 140f);

        Image botCardBg = botCardObj.AddComponent<Image>();
        botCardBg.color = new Color(0.04f, 0.05f, 0.08f, 0.90f);
        botCardBg.raycastTarget = false;

        // Survival Rule ("NEVER flip light switches when smelling gas!")
        GameObject ruleObj = new GameObject("SurvivalRuleText");
        ruleObj.transform.SetParent(botCardObj.transform, false);
        RectTransform ruleRect = ruleObj.AddComponent<RectTransform>();
        ruleRect.anchorMin = new Vector2(0.5f, 1f);
        ruleRect.anchorMax = new Vector2(0.5f, 1f);
        ruleRect.pivot = new Vector2(0.5f, 1f);
        ruleRect.anchoredPosition = new Vector2(0f, -12f);
        ruleRect.sizeDelta = new Vector2(1400f, 36f);
        survivalRuleText = ruleObj.AddComponent<TextMeshProUGUI>();
        survivalRuleText.text = "";
        survivalRuleText.fontSize = 24;
        survivalRuleText.fontStyle = FontStyles.Bold;
        survivalRuleText.alignment = TextAlignmentOptions.Center;
        survivalRuleText.color = new Color(1f, 0.88f, 0.42f, 1f);

        // Bonus Time / Loops Badge ("+15s ADDED TO TIMELINE    (LOOPS: 2/3)")
        GameObject badgeObj = new GameObject("BonusBadgeText");
        badgeObj.transform.SetParent(botCardObj.transform, false);
        RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
        badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(0f, -5f);
        badgeRect.sizeDelta = new Vector2(1400f, 40f);
        bonusBadgeText = badgeObj.AddComponent<TextMeshProUGUI>();
        bonusBadgeText.text = "+15s ADDED TO TIMELINE";
        bonusBadgeText.fontSize = 28;
        bonusBadgeText.fontStyle = FontStyles.Bold;
        bonusBadgeText.alignment = TextAlignmentOptions.Center;
        bonusBadgeText.color = new Color(0.15f, 1f, 0.65f, 1f);

        // Prompt Text ("[ Press SPACE or Click to Continue ]")
        GameObject promptObj = new GameObject("PromptText");
        promptObj.transform.SetParent(botCardObj.transform, false);
        RectTransform promptRect = promptObj.AddComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0f);
        promptRect.anchorMax = new Vector2(0.5f, 0f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.anchoredPosition = new Vector2(0f, 10f);
        promptRect.sizeDelta = new Vector2(1400f, 26f);
        promptText = promptObj.AddComponent<TextMeshProUGUI>();
        promptText.text = "[ Press SPACE / E or Click to Continue ]";
        promptText.fontSize = 17;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = new Color(0.72f, 0.76f, 0.82f, 0.85f);

        clockRoot.SetActive(false);
    }

    private void BuildEyelidsUI(GameObject parent)
    {
        eyelidsRoot = new GameObject("EyelidsRoot");
        eyelidsRoot.transform.SetParent(parent.transform, false);
        RectTransform rootRect = eyelidsRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        Sprite topEyelidSprite = GenerateTopEyelidSprite();
        Sprite bottomEyelidSprite = GenerateBottomEyelidSprite();

        // Top Eyelid
        GameObject topObj = new GameObject("TopEyelid");
        topObj.transform.SetParent(eyelidsRoot.transform, false);
        topEyelid = topObj.AddComponent<RectTransform>();
        topEyelid.anchorMin = new Vector2(0f, 0.5f);
        topEyelid.anchorMax = new Vector2(1f, 1f);
        topEyelid.pivot = new Vector2(0.5f, 0f);
        topEyelid.sizeDelta = new Vector2(400f, 650f);
        topEyelidImage = topObj.AddComponent<Image>();
        topEyelidImage.sprite = topEyelidSprite;
        topEyelidImage.type = Image.Type.Simple;
        topEyelidImage.color = Color.black;
        topEyelidImage.raycastTarget = false;

        // Bottom Eyelid
        GameObject bottomObj = new GameObject("BottomEyelid");
        bottomObj.transform.SetParent(eyelidsRoot.transform, false);
        bottomEyelid = bottomObj.AddComponent<RectTransform>();
        bottomEyelid.anchorMin = new Vector2(0f, 0f);
        bottomEyelid.anchorMax = new Vector2(1f, 0.5f);
        bottomEyelid.pivot = new Vector2(0.5f, 1f);
        bottomEyelid.sizeDelta = new Vector2(400f, 650f);
        bottomEyelidImage = bottomObj.AddComponent<Image>();
        bottomEyelidImage.sprite = bottomEyelidSprite;
        bottomEyelidImage.type = Image.Type.Simple;
        bottomEyelidImage.color = Color.black;
        bottomEyelidImage.raycastTarget = false;

        // Center Slit Vignette
        GameObject vigObj = new GameObject("EyeVignette");
        vigObj.transform.SetParent(eyelidsRoot.transform, false);
        RectTransform vigRect = vigObj.AddComponent<RectTransform>();
        vigRect.anchorMin = Vector2.zero;
        vigRect.anchorMax = Vector2.one;
        vigRect.sizeDelta = Vector2.zero;
        centerVignette = vigObj.AddComponent<Image>();
        centerVignette.sprite = GenerateVignetteSprite();
        centerVignette.color = new Color(0f, 0f, 0f, 0f);
        centerVignette.raycastTarget = false;

        // Full Black Overlay (guarantees 100% solid pitch black when eye closes)
        GameObject blackObj = new GameObject("FullBlackOverlay");
        blackObj.transform.SetParent(eyelidsRoot.transform, false);
        RectTransform blackRect = blackObj.AddComponent<RectTransform>();
        blackRect.anchorMin = Vector2.zero;
        blackRect.anchorMax = Vector2.one;
        blackRect.sizeDelta = Vector2.zero;
        fullBlackOverlay = blackObj.AddComponent<Image>();
        fullBlackOverlay.color = new Color(0f, 0f, 0f, 0f);
        fullBlackOverlay.raycastTarget = false;
    }

    #endregion

    #region --- REASON PARSING & POLISHED PRESENTATION ---

    public struct FormattedRewindReason
    {
        public string categoryTag;
        public string primaryTitle;
        public string survivalRule;
        public Color tagColor;
        public Color titleColor;
    }

    /// <summary>
    /// Parses educational reasons into punchy, high-importance headings & 1-line takeaways:
    /// "less text, more importance" focusing on the Cause of Death.
    /// </summary>
    public static FormattedRewindReason FormatReason(string rawReason)
    {
        if (string.IsNullOrEmpty(rawReason))
        {
            return new FormattedRewindReason
            {
                categoryTag = "CAUSE OF DEATH",
                primaryTitle = "FATAL MISTAKE",
                survivalRule = "Stay alert and follow earthquake safety procedures!",
                tagColor = new Color(1f, 0.35f, 0.35f, 1f),
                titleColor = Color.white
            };
        }

        string lower = rawReason.ToLowerInvariant();

        // 1. Gas Explosion from Light Switch or Sparks
        if (lower.Contains("switch") || lower.Contains("spark") || lower.Contains("explosion") || lower.Contains("gas"))
        {
            if (lower.Contains("valve") || lower.Contains("broken fixtures") || lower.Contains("lingered"))
            {
                return new FormattedRewindReason
                {
                    categoryTag = "CAUSE OF DEATH",
                    primaryTitle = "LINGERED IN LEAKING GAS",
                    survivalRule = "Evacuate immediately if you cannot safely shut off the main gas valve!",
                    tagColor = new Color(1f, 0.35f, 0.35f, 1f),
                    titleColor = new Color(1f, 0.45f, 0.45f, 1f)
                };
            }
            return new FormattedRewindReason
            {
                categoryTag = "CAUSE OF DEATH",
                primaryTitle = "ELECTRICAL SPARK IGNITED GAS LEAK",
                survivalRule = "NEVER flip light switches or create electrical sparks when you smell gas!",
                tagColor = new Color(1f, 0.35f, 0.35f, 1f),
                titleColor = new Color(1f, 0.4f, 0.4f, 1f)
            };
        }

        // 2. Heavy Wall Painting / Falling Frame
        if (lower.Contains("painting") || lower.Contains("wall hangings") || lower.Contains("shattered glass"))
        {
            return new FormattedRewindReason
            {
                categoryTag = "CAUSE OF DEATH",
                primaryTitle = "UNSECURED WALL HANGING FELL",
                survivalRule = "Always remove or securely anchor heavy frames & mirrors above desks and beds!",
                tagColor = new Color(1f, 0.35f, 0.35f, 1f),
                titleColor = new Color(1f, 0.45f, 0.45f, 1f)
            };
        }

        // 3. Candle Fire
        if (lower.Contains("candle") || lower.Contains("fire") || lower.Contains("open flames") || lower.Contains("igniting"))
        {
            return new FormattedRewindReason
            {
                categoryTag = "CAUSE OF DEATH",
                primaryTitle = "TOPPLED CANDLE IGNITED APARTMENT FIRE",
                survivalRule = "Always extinguish open flames and candles before an earthquake strikes!",
                tagColor = new Color(1f, 0.45f, 0.2f, 1f),
                titleColor = new Color(1f, 0.55f, 0.25f, 1f)
            };
        }

        // 4. Running during tremor
        if (lower.Contains("lateral") || lower.Contains("never run") || lower.Contains("run while"))
        {
            return new FormattedRewindReason
            {
                categoryTag = "CAUSE OF DEATH",
                primaryTitle = "RAN DURING VIOLENT SHAKING",
                survivalRule = "Never run during shaking — Drop, Cover, and Hold On immediately!",
                tagColor = new Color(1f, 0.35f, 0.35f, 1f),
                titleColor = new Color(1f, 0.45f, 0.45f, 1f)
            };
        }

        // 5. Freeze in panic / Timeout during tremor
        if (lower.Contains("frozen in panic") || lower.Contains("drop under sturdy") || lower.Contains("knocks you down"))
        {
            return new FormattedRewindReason
            {
                categoryTag = "CAUSE OF DEATH",
                primaryTitle = "FROZEN IN PANIC",
                survivalRule = "Drop under sturdy furniture within seconds before violent shaking knocks you down!",
                tagColor = new Color(1f, 0.35f, 0.35f, 1f),
                titleColor = new Color(1f, 0.45f, 0.45f, 1f)
            };
        }

        // 6. Shifting Cabinet / Aftershock
        if (lower.Contains("cabinet") || lower.Contains("aftershock") || lower.Contains("overhead obstacles"))
        {
            return new FormattedRewindReason
            {
                categoryTag = "CAUSE OF DEATH",
                primaryTitle = "CABINET COLLAPSE INJURY",
                survivalRule = "React quickly to avoid shifting heavy furniture during aftershocks!",
                tagColor = new Color(1f, 0.35f, 0.35f, 1f),
                titleColor = new Color(1f, 0.45f, 0.45f, 1f)
            };
        }

        // 7. Navigation in Dark / Missing Flashlight
        if (lower.Contains("flashlight") || lower.Contains("in the dark") || lower.Contains("total darkness"))
        {
            if (lower.Contains("stairs") || lower.Contains("planter"))
            {
                return new FormattedRewindReason
                {
                    categoryTag = "CAUSE OF DEATH",
                    primaryTitle = "FELL DOWN DARK STAIRS",
                    survivalRule = "Never navigate darkened stairwells without emergency lighting!",
                    tagColor = new Color(1f, 0.35f, 0.35f, 1f),
                    titleColor = new Color(1f, 0.45f, 0.45f, 1f)
                };
            }
            return new FormattedRewindReason
            {
                categoryTag = "CAUSE OF DEATH",
                primaryTitle = "SEARCHING IN TOTAL DARKNESS",
                survivalRule = "A packed emergency flashlight is essential for post-quake navigation & shut-offs!",
                tagColor = new Color(1f, 0.35f, 0.35f, 1f),
                titleColor = new Color(1f, 0.45f, 0.45f, 1f)
            };
        }

        // 8. Falling Facade / Street debris
        if (lower.Contains("facade") || lower.Contains("storefront") || lower.Contains("overhead hazards"))
        {
            return new FormattedRewindReason
            {
                categoryTag = "CAUSE OF DEATH",
                primaryTitle = "STRUCK BY FALLING FACADE",
                survivalRule = "Watch overhead hazards & falling glass when stepping onto the street!",
                tagColor = new Color(1f, 0.35f, 0.35f, 1f),
                titleColor = new Color(1f, 0.45f, 0.45f, 1f)
            };
        }

        // 9. Vehicle Gridlock
        if (lower.Contains("gridlock") || lower.Contains("car") || lower.Contains("drivers"))
        {
            return new FormattedRewindReason
            {
                categoryTag = "CAUSE OF DEATH",
                primaryTitle = "TRAPPED IN VEHICLE GRIDLOCK",
                survivalRule = "Never evacuate by car — keep roads completely clear for emergency vehicles!",
                tagColor = new Color(1f, 0.35f, 0.35f, 1f),
                titleColor = new Color(1f, 0.45f, 0.45f, 1f)
            };
        }

        // 10. Tsunami / Coastal Secondary Disaster
        if (lower.Contains("tsunami") || lower.Contains("coast") || lower.Contains("wave"))
        {
            return new FormattedRewindReason
            {
                categoryTag = "SECONDARY DISASTER",
                primaryTitle = "TSUNAMI INUNDATION HAZARD",
                survivalRule = "Earthquakes in coastal areas trigger giant tsunamis within minutes — move to high ground immediately!",
                tagColor = new Color(0.3f, 0.7f, 1f, 1f),
                titleColor = new Color(0.4f, 0.85f, 1f, 1f)
            };
        }

        // 11. Building Collapse (After-effect / stayed in apartment)
        if (lower.Contains("building collapse") || lower.Contains("structural") || lower.Contains("damaged apartment") || lower.Contains("collapsed") || lower.Contains("lingered inside"))
        {
            return new FormattedRewindReason
            {
                categoryTag = "AFTERSHOCK HAZARD",
                primaryTitle = "BUILDING COLLAPSED",
                survivalRule = "Damaged buildings can collapse at any moment — evacuate immediately after shutting off utilities!",
                tagColor = new Color(1f, 0.45f, 0.2f, 1f),
                titleColor = new Color(1f, 0.55f, 0.25f, 1f)
            };
        }

        // Fallback: Smart Split
        string[] parts = rawReason.Split(new char[] { ':', '!' }, StringSplitOptions.RemoveEmptyEntries);
        string cat = "CAUSE OF DEATH";
        string title = parts.Length > 0 ? parts[0].Trim().ToUpper() : "FATAL MISTAKE";
        string rule = parts.Length > 1 ? parts[parts.Length - 1].Trim() : rawReason;

        return new FormattedRewindReason
        {
            categoryTag = cat,
            primaryTitle = title,
            survivalRule = rule,
            tagColor = new Color(1f, 0.35f, 0.35f, 1f),
            titleColor = Color.white
        };
    }

    #endregion

    #region --- PROCEDURAL TEXTURES & SPRITES ---

    private Sprite GenerateTopEyelidSprite()
    {
        int width = 512;
        int height = 256;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (x / (float)(width - 1)) * 2f - 1f;
                float curve = (1f - (u * u)) * 30f;
                float edgeY = 35f - curve;
                float dist = y - edgeY;
                float alpha = Mathf.Clamp01((dist + 3f) / 6f);
                pixels[y * width + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f));
    }

    private Sprite GenerateBottomEyelidSprite()
    {
        int width = 512;
        int height = 256;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (x / (float)(width - 1)) * 2f - 1f;
                float curve = (1f - (u * u)) * 30f;
                float edgeY = (height - 35f) + curve;
                float dist = edgeY - y;
                float alpha = Mathf.Clamp01((dist + 3f) / 6f);
                pixels[y * width + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 1f));
    }

    private Sprite GenerateVignetteSprite()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float alpha = Mathf.Clamp01(dist * dist * dist);
                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha * 0.75f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite GenerateClockFaceSprite()
    {
        int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = 240f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                Color c = Color.clear;

                if (d <= radius)
                {
                    float dialNorm = d / radius;
                    Color dialBg = Color.Lerp(new Color(0.08f, 0.12f, 0.18f, 1f), new Color(0.03f, 0.04f, 0.07f, 1f), dialNorm);

                    if (d >= radius - 18f)
                    {
                        dialBg = new Color(0.85f, 0.70f, 0.28f, 1f);
                    }
                    c = dialBg;
                }

                if (d > radius - 2f && d <= radius + 2f)
                {
                    c.a = Mathf.Clamp01(1f - (d - (radius - 2f)) / 4f);
                }

                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite GenerateHandSprite(int width, int height, Color color)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float normY = y / (float)(height - 1);
            float maxW = (width * 0.5f) * Mathf.Lerp(1.2f, 0.3f, normY);

            for (int x = 0; x < width; x++)
            {
                float distFromCenter = Mathf.Abs(x - (width * 0.5f));
                float alpha = distFromCenter <= maxW ? 1f : 0f;
                pixels[y * width + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    private Sprite GenerateCircleSprite(int size, float radius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = Mathf.Clamp01(1f - (d / radius));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    #endregion

    #region --- PROCEDURAL AUDIO SYNTHESIS ---

    private void GenerateProceduralAudio()
    {
        synthReverseTick = CreateReverseTickClip();
        synthRewindWhoosh = CreateTimeWarpWhooshClip();
        synthTimeLockChime = CreateTimeLockChimeClip();
        synthEyelidThump = CreateEyelidThumpClip();
    }

    private AudioClip CreateReverseTickClip()
    {
        int sampleRate = 44100;
        float duration = 0.075f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Pow((float)i / sampleCount, 3.5f);
            float ping = (Mathf.Sin(2f * Mathf.PI * 1600f * t) + Mathf.Sin(2f * Mathf.PI * 800f * t)) * 0.6f;
            float noise = ((float)UnityEngine.Random.value * 2f - 1f) * 0.35f;
            samples[i] = (ping + noise) * env * 0.7f;
        }

        AudioClip clip = AudioClip.Create("SynthReverseTick", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateTimeWarpWhooshClip()
    {
        int sampleRate = 44100;
        float duration = 2.4f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float freq = Mathf.Lerp(140f, 520f, progress);
            float mod = Mathf.Sin(2f * Mathf.PI * 9f * t) * 0.35f;
            float tone = Mathf.Sin(2f * Mathf.PI * (freq + mod * 40f) * t);
            float noise = ((float)UnityEngine.Random.value * 2f - 1f) * 0.15f;
            float env = Mathf.Sin(progress * Mathf.PI);
            samples[i] = (tone * 0.75f + noise) * env * 0.45f;
        }

        AudioClip clip = AudioClip.Create("SynthTimeWarp", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateTimeLockChimeClip()
    {
        int sampleRate = 44100;
        float duration = 1.2f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 4.2f);
            float bell = Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.5f +
                         Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.3f +
                         Mathf.Sin(2f * Mathf.PI * 1046.50f * t) * 0.2f;
            samples[i] = bell * env * 0.85f;
        }

        AudioClip clip = AudioClip.Create("SynthLockChime", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateEyelidThumpClip()
    {
        int sampleRate = 44100;
        float duration = 0.35f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 11f);
            float thump = Mathf.Sin(2f * Mathf.PI * 65f * t);
            samples[i] = thump * env * 0.7f;
        }

        AudioClip clip = AudioClip.Create("SynthEyelidThump", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void PlaySound(AudioClip custom, AudioClip synth, float volume = 1f)
    {
        AudioClip clipToPlay = custom != null ? custom : synth;
        if (clipToPlay != null && audioSource != null)
        {
            audioSource.PlayOneShot(clipToPlay, volume);
        }
    }

    #endregion

    #region --- REWIND SEQUENCER & ANIMATION ---

    public void PlayTimeRewind(string educationalReason, float addedSeconds, Action onRewindStateRestore, Action onComplete)
    {
        PlayTimeRewind(educationalReason, addedSeconds, 1, 3, onRewindStateRestore, onComplete);
    }

    public void PlayTimeRewind(string educationalReason, float addedSeconds, int remainingLoops, int maxLoops, Action onRewindStateRestore, Action onComplete)
    {
        if (isRewinding) return;
        StartCoroutine(RewindSequenceRoutine(educationalReason, addedSeconds, remainingLoops, maxLoops, onRewindStateRestore, onComplete));
    }

    private IEnumerator RewindSequenceRoutine(string reason, float addedSeconds, int remainingLoops, int maxLoops, Action onRewindStateRestore, Action onComplete)
    {
        isRewinding = true;

        if (eyelidsRoot != null) eyelidsRoot.SetActive(true);
        if (clockRoot != null) clockRoot.SetActive(false);

        // -------------------------------------------------------------
        // PHASE 1: Eye Closes on the current screen (0.65s)
        // -------------------------------------------------------------
        PlaySound(customEyelidThump, synthEyelidThump, 0.8f);
        float elapsed = 0f;
        while (elapsed < eyeCloseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / eyeCloseDuration);
            float openAmount = 1f - Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(0f); // Screen is pitch black

        yield return new WaitForSecondsRealtime(0.25f);

        // -------------------------------------------------------------
        // PHASE 2: Setup Clock UI Behind Closed Eyes
        // -------------------------------------------------------------
        FormattedRewindReason formatted = FormatReason(reason);

        if (clockRoot != null)
        {
            clockRoot.SetActive(true);

            if (categoryTagText != null)
            {
                categoryTagText.text = formatted.categoryTag;
                categoryTagText.characterSpacing = 4f;
                categoryTagText.color = formatted.tagColor;
            }

            if (primaryTitleText != null)
            {
                primaryTitleText.text = formatted.primaryTitle;
                primaryTitleText.color = formatted.titleColor;
            }

            if (survivalRuleText != null)
            {
                survivalRuleText.text = $"\"{formatted.survivalRule}\"";
            }

            if (bonusBadgeText != null)
            {
                bonusBadgeText.gameObject.SetActive(false);
                bonusBadgeText.text = $"<color=#00FFAA>+{addedSeconds:0}s ADDED TO TIMELINE</color>   <size=75%><color=#70D6FF>(LOOPS: {remainingLoops}/{maxLoops})</color></size>";
            }

            if (promptText != null)
            {
                promptText.text = "[ Press SPACE / E or Click to Continue ]";
            }

            if (digitalClockText != null) digitalClockText.text = "00:00";

            // Initial hand rotations
            if (minuteHand != null) minuteHand.localEulerAngles = Vector3.zero;
            if (hourHand != null) hourHand.localEulerAngles = Vector3.zero;
        }

        // Open eyelids to reveal clock
        elapsed = 0f;
        while (elapsed < eyeOpenDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / eyeOpenDuration);
            float openAmount = Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(1f); // Fully revealed

        // -------------------------------------------------------------
        // PHASE 3: Clock Spinning Backwards with Audio & Vibration (2.4s)
        // -------------------------------------------------------------
        PlaySound(customRewindWhoosh, synthRewindWhoosh, 0.75f);

        float spinElapsed = 0f;
        float tickTimer = 0f;
        float currentDisplaySeconds = 0f;

        while (spinElapsed < rewindSpinDuration)
        {
            float dt = Time.unscaledDeltaTime;
            spinElapsed += dt;
            tickTimer += dt;

            float progress = spinElapsed / rewindSpinDuration;
            float speedMult = progress > 0.75f ? Mathf.Lerp(1f, 0.05f, (progress - 0.75f) / 0.25f) : 1f;

            // Spin hands COUNTER-CLOCKWISE (Z rotation increases in Unity 2D)
            float minSpin = 720f * speedMult * dt;
            float hourSpin = 60f * speedMult * dt;

            if (minuteHand != null) minuteHand.Rotate(0f, 0f, minSpin);
            if (hourHand != null) hourHand.Rotate(0f, 0f, hourSpin);

            // Clock pulse / time vortex breath
            if (clockPanel != null)
            {
                float pulse = 1f + Mathf.Sin(spinElapsed * 14f) * 0.025f;
                clockPanel.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (clockAura != null)
            {
                float auraAlpha = 0.25f + Mathf.Sin(spinElapsed * 10f) * 0.15f;
                clockAura.color = new Color(0.2f, 0.75f, 1f, auraAlpha);
            }

            // Rapid reverse ticks
            float tickInterval = Mathf.Lerp(0.09f, 0.22f, progress > 0.75f ? (progress - 0.75f) / 0.25f : 0f);
            if (tickTimer >= tickInterval)
            {
                tickTimer = 0f;
                PlaySound(customReverseTick, synthReverseTick, 0.5f);
            }

            // Digital clock display rolling backward
            currentDisplaySeconds += dt * (addedSeconds / rewindSpinDuration);
            int totalSec = Mathf.FloorToInt(currentDisplaySeconds);
            if (digitalClockText != null)
            {
                digitalClockText.text = $"-{totalSec / 60:00}:{totalSec % 60:00}";
            }

            yield return null;
        }

        // Time lock chime & Bonus Badge display
        PlaySound(customTimeLockChime, synthTimeLockChime, 0.9f);

        if (bonusBadgeText != null)
        {
            bonusBadgeText.gameObject.SetActive(true);
            bonusBadgeText.transform.localScale = Vector3.one * 1.3f;
        }

        if (digitalClockText != null)
        {
            digitalClockText.text = $"<color=#00FFAA>+{addedSeconds:0}s RESTORED</color>";
        }

        // Hold clock & cause of death for comfortable reading before returning to live game
        float holdTimer = 0f;
        while (holdTimer < educationalReasonHoldDuration)
        {
            holdTimer += Time.unscaledDeltaTime;
            if (bonusBadgeText != null)
            {
                bonusBadgeText.transform.localScale = Vector3.Lerp(bonusBadgeText.transform.localScale, Vector3.one, Time.unscaledDeltaTime * 6f);
            }

            // Advance if player presses Space/E/Click (after at least 0.75s)
            if (holdTimer > 0.75f)
            {
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
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

        // -------------------------------------------------------------
        // PHASE 4: Eye Closes Again over the Clock (0.55s)
        // -------------------------------------------------------------
        PlaySound(customEyelidThump, synthEyelidThump, 0.8f);
        elapsed = 0f;
        while (elapsed < eyeCloseDuration * 0.85f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / (eyeCloseDuration * 0.85f));
            float openAmount = 1f - Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(0f); // Screen is black

        yield return new WaitForSecondsRealtime(0.25f);

        // -------------------------------------------------------------
        // PHASE 5: Restore Game State Behind Closed Eyes (+15s / +5s & Undo Action)
        // -------------------------------------------------------------
        if (clockRoot != null)
        {
            clockRoot.SetActive(false);
        }

        try
        {
            onRewindStateRestore?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TimeRewind] Error during state restore: {ex}");
        }

        yield return null; // Wait 1 frame for physics and scene to settle

        // -------------------------------------------------------------
        // PHASE 6: Eye Opens Back to Live Gameplay (0.7s)
        // -------------------------------------------------------------
        elapsed = 0f;
        while (elapsed < eyeOpenDuration * 1.3f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / (eyeOpenDuration * 1.3f));
            float openAmount = Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(1f); // Fully open

        if (eyelidsRoot != null)
        {
            eyelidsRoot.SetActive(false);
        }

        isRewinding = false;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Plays the intro rewind animation when starting from the start screen before Stage 1 begins:
    /// Focuses sharply on "60 SECONDS BEFORE EARTHQUAKE — BE READY!"
    /// </summary>
    public void PlayIntroRewind(string introMessage, Action onComplete)
    {
        if (isRewinding) return;
        StartCoroutine(IntroRewindRoutine(introMessage, onComplete));
    }

    private IEnumerator IntroRewindRoutine(string message, Action onComplete)
    {
        isRewinding = true;

        if (eyelidsRoot != null) eyelidsRoot.SetActive(true);
        if (clockRoot != null) clockRoot.SetActive(false);
        SetEyeOpenImmediate(0f); // Screen is pitch black

        yield return new WaitForSecondsRealtime(0.2f);

        // -------------------------------------------------------------
        // PHASE 1: Setup Clock Behind Closed Eyes
        // -------------------------------------------------------------
        if (clockRoot != null)
        {
            clockRoot.SetActive(true);

            if (categoryTagText != null)
            {
                categoryTagText.text = "TIMELINE RESET";
                categoryTagText.characterSpacing = 4f;
                categoryTagText.color = new Color(0.35f, 0.85f, 1f, 1f);
            }

            if (primaryTitleText != null)
            {
                primaryTitleText.text = "<color=#FFDD00>60 SECONDS BEFORE EARTHQUAKE</color> — BE READY!";
                primaryTitleText.color = Color.white;
            }

            if (survivalRuleText != null)
            {
                survivalRuleText.text = "\"Secure loose hazards & pack emergency supplies before the tremor!\"";
            }

            if (bonusBadgeText != null)
            {
                bonusBadgeText.gameObject.SetActive(false);
                bonusBadgeText.text = "<color=#00FFAA>TIMELINE SYNCHRONIZED — 60 SECONDS</color>";
            }

            if (promptText != null)
            {
                promptText.text = "[ Press SPACE / E or Click to Begin ]";
            }

            if (digitalClockText != null) digitalClockText.text = "01:00";

            if (minuteHand != null) minuteHand.localEulerAngles = Vector3.zero;
            if (hourHand != null) hourHand.localEulerAngles = Vector3.zero;
        }

        // Open eyelids to reveal clock
        float elapsed = 0f;
        while (elapsed < eyeOpenDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / eyeOpenDuration);
            float openAmount = Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(1f);

        // -------------------------------------------------------------
        // PHASE 2: Clock Spinning Backwards with Audio (2.4s)
        // -------------------------------------------------------------
        PlaySound(customRewindWhoosh, synthRewindWhoosh, 0.75f);

        float spinElapsed = 0f;
        float tickTimer = 0f;
        float currentDisplaySeconds = 0f;

        while (spinElapsed < rewindSpinDuration)
        {
            float dt = Time.unscaledDeltaTime;
            spinElapsed += dt;
            tickTimer += dt;

            float progress = spinElapsed / rewindSpinDuration;
            float speedMult = progress > 0.75f ? Mathf.Lerp(1f, 0.05f, (progress - 0.75f) / 0.25f) : 1f;

            float minSpin = 720f * speedMult * dt;
            float hourSpin = 60f * speedMult * dt;

            if (minuteHand != null) minuteHand.Rotate(0f, 0f, minSpin);
            if (hourHand != null) hourHand.Rotate(0f, 0f, hourSpin);

            if (clockPanel != null)
            {
                float pulse = 1f + Mathf.Sin(spinElapsed * 14f) * 0.025f;
                clockPanel.localScale = new Vector3(pulse, pulse, 1f);
            }

            if (clockAura != null)
            {
                float auraAlpha = 0.25f + Mathf.Sin(spinElapsed * 10f) * 0.15f;
                clockAura.color = new Color(0.2f, 0.75f, 1f, auraAlpha);
            }

            float tickInterval = Mathf.Lerp(0.09f, 0.22f, progress > 0.75f ? (progress - 0.75f) / 0.25f : 0f);
            if (tickTimer >= tickInterval)
            {
                tickTimer = 0f;
                PlaySound(customReverseTick, synthReverseTick, 0.5f);
            }

            currentDisplaySeconds += dt * (60f / rewindSpinDuration);
            int totalSec = Mathf.FloorToInt(currentDisplaySeconds);
            if (digitalClockText != null)
            {
                digitalClockText.text = $"-{totalSec / 60:00}:{totalSec % 60:00}";
            }

            yield return null;
        }

        // Time lock chime & Target Time badge
        PlaySound(customTimeLockChime, synthTimeLockChime, 0.9f);

        if (bonusBadgeText != null)
        {
            bonusBadgeText.gameObject.SetActive(true);
            bonusBadgeText.transform.localScale = Vector3.one * 1.3f;
        }

        if (digitalClockText != null)
        {
            digitalClockText.text = "<color=#00FFAA>01:00 PREPARATION</color>";
        }

        // Hold clock for comfortable reading
        float holdTimer = 0f;
        while (holdTimer < educationalReasonHoldDuration)
        {
            holdTimer += Time.unscaledDeltaTime;
            if (bonusBadgeText != null)
            {
                bonusBadgeText.transform.localScale = Vector3.Lerp(bonusBadgeText.transform.localScale, Vector3.one, Time.unscaledDeltaTime * 6f);
            }

            if (holdTimer > 0.75f)
            {
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
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

        // -------------------------------------------------------------
        // PHASE 3: Eye Closes over the Clock (0.55s)
        // -------------------------------------------------------------
        PlaySound(customEyelidThump, synthEyelidThump, 0.8f);
        elapsed = 0f;
        while (elapsed < eyeCloseDuration * 0.85f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / (eyeCloseDuration * 0.85f));
            float openAmount = 1f - Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(0f); // Screen is black

        yield return new WaitForSecondsRealtime(0.25f);

        if (clockRoot != null)
        {
            clockRoot.SetActive(false);
        }

        yield return null;

        // -------------------------------------------------------------
        // PHASE 4: Eye Opens Back to Live Game (0.7s)
        // -------------------------------------------------------------
        elapsed = 0f;
        while (elapsed < eyeOpenDuration * 1.3f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / (eyeOpenDuration * 1.3f));
            float openAmount = Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(1f);

        if (eyelidsRoot != null)
        {
            eyelidsRoot.SetActive(false);
        }

        isRewinding = false;

        // -------------------------------------------------------------
        // PHASE 5: Start Game!
        // -------------------------------------------------------------
        onComplete?.Invoke();
    }

    /// <summary>
    /// Executes the Game Over sequence when all time loop rewinds have been exhausted.
    /// </summary>
    public void PlayGameOverSequence(string educationalReason, Action onRestart)
    {
        if (isRewinding) return;
        StartCoroutine(GameOverSequenceRoutine(educationalReason, onRestart));
    }

    private IEnumerator GameOverSequenceRoutine(string reason, Action onRestart)
    {
        isRewinding = true;

        if (eyelidsRoot != null) eyelidsRoot.SetActive(true);
        if (clockRoot != null) clockRoot.SetActive(false);

        // PHASE 1: Eye Closes into darkness with heavy thump
        PlaySound(customEyelidThump, synthEyelidThump, 1f);
        float elapsed = 0f;
        while (elapsed < eyeCloseDuration * 1.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / (eyeCloseDuration * 1.2f));
            float openAmount = 1f - Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(0f);

        yield return new WaitForSecondsRealtime(0.3f);

        FormattedRewindReason formatted = FormatReason(reason);

        // PHASE 2: Eye opens onto frozen clock with red warning
        if (clockRoot != null)
        {
            clockRoot.SetActive(true);

            if (categoryTagText != null)
            {
                categoryTagText.text = "TIMELINE COLLAPSED";
                categoryTagText.characterSpacing = 4f;
                categoryTagText.color = new Color(1f, 0.2f, 0.2f, 1f);
            }

            if (primaryTitleText != null)
            {
                primaryTitleText.text = "<color=#FF4444>ALL TIME LOOPS EXHAUSTED (0/3)</color>";
                primaryTitleText.color = Color.white;
            }

            if (survivalRuleText != null)
            {
                survivalRuleText.text = $"CAUSE: {formatted.primaryTitle} — {formatted.survivalRule}";
            }

            if (bonusBadgeText != null)
            {
                bonusBadgeText.gameObject.SetActive(true);
                bonusBadgeText.text = "<color=#FFD700>PRESS [R] OR CLICK TO RESTART RUN</color>";
            }

            if (promptText != null)
            {
                promptText.text = "[ Game Over — Run Terminated ]";
            }

            if (digitalClockText != null)
            {
                digitalClockText.text = "<color=#FF4444>00:00</color>";
            }

            if (clockAura != null)
            {
                clockAura.color = new Color(0.9f, 0.15f, 0.15f, 0.45f);
            }
        }

        elapsed = 0f;
        while (elapsed < eyeOpenDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / eyeOpenDuration);
            float openAmount = Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(1f);

        // Wait for player restart input
        while (true)
        {
            if ((Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
            {
                break;
            }
            yield return null;
        }

        // PHASE 3: Eye closes again before resetting run
        PlaySound(customEyelidThump, synthEyelidThump, 0.8f);
        elapsed = 0f;
        while (elapsed < eyeCloseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / eyeCloseDuration);
            float openAmount = 1f - Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(0f);

        if (clockRoot != null) clockRoot.SetActive(false);

        yield return new WaitForSecondsRealtime(0.2f);

        try
        {
            onRestart?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TimeRewind] Error during Game Over restart: {ex}");
        }

        yield return null;

        // PHASE 4: Eye opens to fresh game run
        elapsed = 0f;
        while (elapsed < eyeOpenDuration * 1.3f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / (eyeOpenDuration * 1.3f));
            float openAmount = Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(1f);

        if (eyelidsRoot != null) eyelidsRoot.SetActive(false);
        isRewinding = false;
    }

    /// <summary>
    /// Sets the opening percentage of the eyelids (0 = completely shut/black, 1 = completely open/clear).
    /// </summary>
    private void SetEyeOpenAmount(float amount)
    {
        amount = Mathf.Clamp01(amount);

        if (topEyelid != null)
        {
            float topY = Mathf.Lerp(-150f, 650f, amount);
            topEyelid.anchoredPosition = new Vector2(0f, topY);
        }

        if (bottomEyelid != null)
        {
            float bottomY = Mathf.Lerp(150f, -650f, amount);
            bottomEyelid.anchoredPosition = new Vector2(0f, bottomY);
        }

        if (fullBlackOverlay != null)
        {
            float blackAlpha = amount <= 0.05f ? 1f : Mathf.Clamp01((1f - amount) * 1.5f);
            fullBlackOverlay.color = new Color(0f, 0f, 0f, blackAlpha);
        }

        if (centerVignette != null)
        {
            float alpha = Mathf.Clamp01(1f - amount);
            centerVignette.color = new Color(0f, 0f, 0f, alpha);
        }
    }

    private void SetEyeOpenImmediate(float amount)
    {
        SetEyeOpenAmount(amount);
        if (amount >= 0.99f && eyelidsRoot != null)
        {
            eyelidsRoot.SetActive(false);
        }
    }

    #endregion

    #region --- DEBUG CONTEXT MENU ---

    [ContextMenu("Test Rewind In Editor")]
    private void TestRewind()
    {
        PlayTimeRewind(
            educationalReason: "FATAL EXPLOSION: The electrical light switch sparked and ignited the leaking gas! NEVER flip light switches or create electrical sparks when you smell gas!",
            addedSeconds: 15f,
            remainingLoops: 2,
            maxLoops: 3,
            onRewindStateRestore: () => Debug.Log("<color=cyan>[TimeRewind] Restored state with +15s!</color>"),
            onComplete: () => Debug.Log("<color=green>[TimeRewind] Sequence finished!</color>")
        );
    }

    [ContextMenu("Test Intro Rewind In Editor")]
    private void TestIntroRewind()
    {
        PlayIntroRewind(
            introMessage: "60 SECONDS BEFORE EARTHQUAKE — BE READY!",
            onComplete: () => Debug.Log("<color=green>[TimeRewind] Intro finished!</color>")
        );
    }

    #endregion
}
