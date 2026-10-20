using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Controller for DolapScene interactive QTE event.
/// Features a two-phase mechanical struggle to lift and brace the fallen cupboard:
/// 1. Phase 1: Mash [S] to lift leverage from the floor.
/// 2. Phase 2: HOLD [S] for leverage while mashing [W] to heave the cupboard upright!
/// The cupboard visually transitions from fallen (düşme dolap4) -> tilting (3 & 2) -> upright (1).
/// Completed with the authentic circular QTE visual aesthetic (Radial360 rings & button medallions).
/// </summary>
public class DolapSceneController : MonoBehaviour
{
    public static DolapSceneController Instance { get; private set; }

    [Header("Sprites")]
    [SerializeField] private Sprite frame1Upright;
    [SerializeField] private Sprite frame2Tilt;
    [SerializeField] private Sprite frame3Falling;
    [SerializeField] private Sprite frame4Fallen;

    // QTE Sprites
    private Sprite outerRingSprite;
    private Sprite trackRingSprite;
    private Sprite circleButtonSprite;
    private Sprite wButtonSprite;

    // Scene Visuals
    [Header("Scene Visuals")]
    [SerializeField] private SpriteRenderer dolapRenderer;
    [SerializeField] private SpriteRenderer backgroundRenderer;
    private Transform dolapTransform;
    private Vector3 dolapBasePos = new Vector3(0f, -0.65f, 0f);
    private Vector3 dolapBaseScale = new Vector3(1.6f, 1.6f, 1f);

    // QTE States
    private enum QTEPhase { Phase1_MashS, Phase2_HoldS_MashW, Completed }
    private QTEPhase currentPhase = QTEPhase.Phase1_MashS;
    private float phase1Progress = 0.10f;
    private float phase2Progress = 0.05f;
    private bool isMouseHoldingS = false;
    private bool isEnding = false;
    private InteractionVisualCue liftCue;

    // UI Elements
    private Canvas overlayCanvas;
    private TMP_Text headerTitleText;
    private TMP_Text subtitleText;
    private TMP_Text timelineTimerText;

    // Phase 1 UI Elements (Center S Button)
    private GameObject phase1Root;
    private RectTransform p1ButtonRect;
    private Image p1TimerRing;
    private Image p1TrackRing;

    // Phase 2 UI Elements (Dual Medallions: Hold S on left, Mash W on right)
    private GameObject phase2Root;
    private RectTransform p2HoldButtonRect;
    private Image p2HoldRing;
    private TMP_Text p2HoldStatusLabel;
    private RectTransform p2MashButtonRect;
    private Image p2MashTimerRing;
    private TMP_Text p2MashStatusLabel;

    // Success Banner
    private GameObject successBannerObj;

    // Audio
    private AudioSource audioSource;
    private AudioClip sfxThump;
    private AudioClip sfxCreak;
    private AudioClip sfxLatch;
    private AudioClip sfxVictory;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnSceneLoaded()
    {
        EnsureDolapControllerInScene();
        SceneManager.sceneLoaded += (scene, mode) => EnsureDolapControllerInScene();
    }

    public static void EnsureDolapControllerInScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "DolapScene")
        {
            if (FindFirstObjectByType<DolapSceneController>() == null)
            {
                var go = new GameObject("DolapSceneController");
                go.AddComponent<DolapSceneController>();
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadAllSprites();
        BuildSceneAudio();
        BuildDolapVisuals();
        BuildQTEOverlayUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        if (liftCue != null)
        {
            if (liftCue.gameObject != null) Destroy(liftCue.gameObject);
            liftCue = null;
        }
    }

    private void Start()
    {
        UpdateDolapVisuals();
        UpdateUIFeedback();

        if (overlayCanvas != null)
        {
            liftCue = InteractionVisualCue.CreateClickCue(
                overlayCanvas.transform,
                dolapBasePos + new Vector3(0f, 1.8f, 0f),
                "MASH [S] TO LIFT",
                "Leverage up from floor",
                false, // points up
                new Vector2(0f, 80f),
                Camera.main
            );
        }
    }

    private void LoadAllSprites()
    {
        if (frame1Upright == null) frame1Upright = LoadSpriteByName("düşme dolap1");
        if (frame2Tilt == null) frame2Tilt = LoadSpriteByName("düşme dolap2");
        if (frame3Falling == null) frame3Falling = LoadSpriteByName("düşme dolap3");
        if (frame4Fallen == null) frame4Fallen = LoadSpriteByName("düşme dolap4");

        outerRingSprite = HazardVisualManager.LoadHazardSprite("UI/QTE_Outer_Ring") ?? Resources.Load<Sprite>("UI/QTE_Outer_Ring");
        trackRingSprite = HazardVisualManager.LoadHazardSprite("UI/QTE_Ring_Track") ?? Resources.Load<Sprite>("UI/QTE_Ring_Track");
        circleButtonSprite = HazardVisualManager.LoadHazardSprite("UI/QTE_Circle_Button") ?? Resources.Load<Sprite>("UI/QTE_Circle_Button");
        wButtonSprite = HazardVisualManager.LoadHazardSprite("UI/QTE_Button_W") ?? Resources.Load<Sprite>("UI/QTE_Button_W");
    }

    private static Sprite LoadSpriteByName(string baseName)
    {
        Sprite s = Resources.Load<Sprite>(baseName);
        if (s != null) return s;

        s = Resources.Load<Sprite>("Dolap/" + baseName);
        if (s != null) return s;

        // Also check dusme_dolap (ASCII name)
        string asciiName = NormalizeName(baseName).Replace(" ", "_");
        s = Resources.Load<Sprite>("Dolap/" + asciiName);
        if (s != null) return s;
        s = Resources.Load<Sprite>(asciiName);
        if (s != null) return s;

#if UNITY_EDITOR
        string[] searchPaths = new string[]
        {
            "Assets/MainScene/Sprite/" + baseName + ".png",
            "Assets/MainScene/Sprite/" + baseName + ".jpg",
            "Assets/MainScene/Sprite/" + baseName + ".jpeg"
        };
        foreach (var p in searchPaths)
        {
            Sprite adSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (adSprite != null) return adSprite;
        }

        string cleanName = NormalizeName(baseName);
        string[] allGuids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new string[] { "Assets/MainScene/Sprite" });
        foreach (var guid in allGuids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            string fileNoExt = Path.GetFileNameWithoutExtension(assetPath);
            if (NormalizeName(fileNoExt) == cleanName || fileNoExt.Equals(baseName, StringComparison.OrdinalIgnoreCase))
            {
                Sprite adSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (adSprite != null) return adSprite;
            }
        }
#endif

        string folder = Path.Combine(Application.dataPath, "MainScene", "Sprite");
        if (Directory.Exists(folder))
        {
            var files = Directory.GetFiles(folder, "*.png");
            foreach (var f in files)
            {
                string fn = Path.GetFileNameWithoutExtension(f);
                if (fn.IndexOf(baseName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    baseName.IndexOf(fn, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    NormalizeName(fn) == NormalizeName(baseName))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(f);
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        tex.filterMode = FilterMode.Point;
                        if (tex.LoadImage(bytes))
                        {
                            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[DolapSceneController] Failed loading {f}: {ex.Message}");
                    }
                }
            }
        }
        return null;
    }

    private static string NormalizeName(string str)
    {
        return str.Replace("ü", "u").Replace("ş", "s").Replace("ç", "c").Replace("ı", "i").ToLowerInvariant().Trim();
    }

    private void BuildDolapVisuals()
    {
        // 1. Ensure Camera
        Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.14f, 1f);
        }

        var unlitMat = SwitchInteraction.GetUnlitMaterial();

        // 2. Room Wall Backdrop: Look for existing hole bacround in scene first
        if (backgroundRenderer == null)
        {
            var bgObj = GameObject.Find("hole bacround") ?? GameObject.Find("HallwayBackdrop");
            if (bgObj != null) backgroundRenderer = bgObj.GetComponent<SpriteRenderer>();
        }

        // If not found in scene, instantiate hole bacround sprite
        if (backgroundRenderer == null)
        {
            GameObject wallBackdrop = new GameObject("hole bacround");
            wallBackdrop.transform.position = Vector3.zero;
            wallBackdrop.transform.localScale = Vector3.one;
            backgroundRenderer = wallBackdrop.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = LoadSpriteByName("hole bacround");
            backgroundRenderer.sortingOrder = 0;
            if (unlitMat != null) backgroundRenderer.material = unlitMat;
        }

        // 3. Dolap Object: Look for existing dolap in scene first
        if (dolapRenderer == null)
        {
            var existingDolap = GameObject.Find("düşme dolap") ?? GameObject.Find("DolapVisual") ?? GameObject.Find("düşme dolap1");
            if (existingDolap != null)
            {
                dolapRenderer = existingDolap.GetComponent<SpriteRenderer>();
            }
        }

        // If not found, instantiate dolap object
        if (dolapRenderer == null)
        {
            GameObject dolapObj = new GameObject("düşme dolap");
            dolapTransform = dolapObj.transform;
            dolapTransform.position = dolapBasePos;
            dolapTransform.localScale = dolapBaseScale;
            dolapRenderer = dolapObj.AddComponent<SpriteRenderer>();
            if (unlitMat != null) dolapRenderer.material = unlitMat;
        }
        else
        {
            dolapTransform = dolapRenderer.transform;
            dolapBasePos = dolapTransform.position;
            dolapBaseScale = dolapTransform.localScale;
            if (unlitMat != null && dolapRenderer.material == null) dolapRenderer.material = unlitMat;
        }

        dolapRenderer.sprite = frame4Fallen != null ? frame4Fallen : (frame1Upright != null ? frame1Upright : LoadSpriteByName("düşme dolap4"));
        dolapRenderer.sortingOrder = 5;
    }

    private static Sprite s_solidSprite;
    private static Sprite GetSolidSprite()
    {
        if (s_solidSprite != null) return s_solidSprite;
        Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Color[] p = { Color.white, Color.white, Color.white, Color.white };
        t.SetPixels(p);
        t.Apply();
        s_solidSprite = Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
        return s_solidSprite;
    }

    private void BuildQTEOverlayUI()
    {
        var existingCanvas = GameObject.Find("DolapQTECanvas");
        if (existingCanvas != null) Destroy(existingCanvas);

        GameObject canvasObj = new GameObject("DolapQTECanvas");
        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 500;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 1. Header Banner
        GameObject headerPanel = new GameObject("HeaderPanel");
        headerPanel.transform.SetParent(canvasObj.transform, false);
        var hpRect = headerPanel.AddComponent<RectTransform>();
        hpRect.anchorMin = new Vector2(0.5f, 1f);
        hpRect.anchorMax = new Vector2(0.5f, 1f);
        hpRect.pivot = new Vector2(0.5f, 1f);
        hpRect.anchoredPosition = new Vector2(0f, -40f);
        hpRect.sizeDelta = new Vector2(900f, 90f);

        var hpBg = headerPanel.AddComponent<Image>();
        hpBg.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);
        hpBg.raycastTarget = false;

        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(headerPanel.transform, false);
        var tRect = titleObj.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.sizeDelta = Vector2.zero;
        tRect.anchoredPosition = new Vector2(0f, 12f);

        headerTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        headerTitleText.fontSize = 28f;
        headerTitleText.fontStyle = FontStyles.Bold;
        headerTitleText.alignment = TextAlignmentOptions.Center;
        headerTitleText.color = new Color(1f, 0.88f, 0.35f, 1f);
        headerTitleText.text = "PHASE 1: MASH [S] TO LIFT THE FALLEN WARDROBE!";

        GameObject subObj = new GameObject("SubtitleText");
        subObj.transform.SetParent(headerPanel.transform, false);
        var sRect = subObj.AddComponent<RectTransform>();
        sRect.anchorMin = Vector2.zero;
        sRect.anchorMax = Vector2.one;
        sRect.sizeDelta = Vector2.zero;
        sRect.anchoredPosition = new Vector2(0f, -22f);

        subtitleText = subObj.AddComponent<TextMeshProUGUI>();
        subtitleText.fontSize = 17f;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.color = new Color(0.85f, 0.88f, 0.92f, 1f);
        subtitleText.text = "Heave the heavy wardrobe off the floor to gain grip leverage!";

        // 2. Timeline Timer Indicator (Top-Right)
        GameObject timerObj = new GameObject("TimelineTimerText");
        timerObj.transform.SetParent(canvasObj.transform, false);
        var ttRect = timerObj.AddComponent<RectTransform>();
        ttRect.anchorMin = new Vector2(1f, 1f);
        ttRect.anchorMax = new Vector2(1f, 1f);
        ttRect.pivot = new Vector2(1f, 1f);
        ttRect.anchoredPosition = new Vector2(-40f, -40f);
        ttRect.sizeDelta = new Vector2(250f, 40f);

        timelineTimerText = timerObj.AddComponent<TextMeshProUGUI>();
        timelineTimerText.fontSize = 22f;
        timelineTimerText.fontStyle = FontStyles.Bold;
        timelineTimerText.alignment = TextAlignmentOptions.Right;
        timelineTimerText.color = new Color(1f, 0.85f, 0.4f, 1f);

        // 3. Build Phase 1 UI (Single Centered S Button)
        BuildPhase1UI(canvasObj.transform);

        // 4. Build Phase 2 UI (Dual Medallions: Hold S on left, Mash W on right)
        BuildPhase2UI(canvasObj.transform);

        // 5. Success Banner
        BuildSuccessBanner(canvasObj.transform);
    }

    private void BuildPhase1UI(Transform canvas)
    {
        phase1Root = new GameObject("Phase1_Root");
        phase1Root.transform.SetParent(canvas, false);
        var rootRect = phase1Root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        // Medallion container at (0, -180)
        GameObject btnObj = new GameObject("S_Medallion");
        btnObj.transform.SetParent(phase1Root.transform, false);
        p1ButtonRect = btnObj.AddComponent<RectTransform>();
        p1ButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
        p1ButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
        p1ButtonRect.pivot = new Vector2(0.5f, 0.5f);
        p1ButtonRect.anchoredPosition = new Vector2(0f, -170f);
        p1ButtonRect.sizeDelta = new Vector2(160f, 160f);

        // Invisible raycast receiver for click input
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = Color.clear;
        var btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => OnPhase1MashPressed());

        // Track Ring
        GameObject trackObj = new GameObject("TrackRing");
        trackObj.transform.SetParent(btnObj.transform, false);
        var trRect = trackObj.AddComponent<RectTransform>();
        trRect.anchorMin = Vector2.zero;
        trRect.anchorMax = Vector2.one;
        trRect.sizeDelta = Vector2.zero;
        p1TrackRing = trackObj.AddComponent<Image>();
        p1TrackRing.sprite = trackRingSprite != null ? trackRingSprite : outerRingSprite;
        p1TrackRing.color = new Color(0.18f, 0.20f, 0.24f, 0.45f);
        p1TrackRing.preserveAspect = true;
        p1TrackRing.raycastTarget = false;

        // Timer Ring (Radial360 Fill)
        GameObject ringObj = new GameObject("TimerRing");
        ringObj.transform.SetParent(btnObj.transform, false);
        var rRect = ringObj.AddComponent<RectTransform>();
        rRect.anchorMin = Vector2.zero;
        rRect.anchorMax = Vector2.one;
        rRect.sizeDelta = Vector2.zero;
        p1TimerRing = ringObj.AddComponent<Image>();
        p1TimerRing.sprite = outerRingSprite;
        p1TimerRing.type = Image.Type.Filled;
        p1TimerRing.fillMethod = Image.FillMethod.Radial360;
        p1TimerRing.fillOrigin = (int)Image.Origin360.Top;
        p1TimerRing.fillClockwise = true;
        p1TimerRing.fillAmount = phase1Progress;
        p1TimerRing.color = new Color(181f / 255f, 191f / 255f, 127f / 255f, 1f); // #B5BF7F
        p1TimerRing.preserveAspect = true;
        p1TimerRing.raycastTarget = false;

        // Center Medallion
        GameObject centerObj = new GameObject("CenterCircle");
        centerObj.transform.SetParent(btnObj.transform, false);
        var cRect = centerObj.AddComponent<RectTransform>();
        cRect.anchorMin = Vector2.zero;
        cRect.anchorMax = Vector2.one;
        cRect.sizeDelta = Vector2.zero;
        var cImg = centerObj.AddComponent<Image>();
        cImg.sprite = circleButtonSprite;
        cImg.preserveAspect = true;
        cImg.raycastTarget = false;

        // Key Text "S"
        GameObject keyTextObj = new GameObject("KeyText");
        keyTextObj.transform.SetParent(btnObj.transform, false);
        var ktRect = keyTextObj.AddComponent<RectTransform>();
        ktRect.anchorMin = Vector2.zero;
        ktRect.anchorMax = Vector2.one;
        ktRect.anchoredPosition = new Vector2(0f, -2f);
        ktRect.sizeDelta = Vector2.zero;
        var kt = keyTextObj.AddComponent<TextMeshProUGUI>();
        kt.text = "S";
        kt.fontSize = 48f;
        kt.fontStyle = FontStyles.Bold;
        kt.alignment = TextAlignmentOptions.Center;
        kt.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        kt.raycastTarget = false;

        // Action Label Badge
        GameObject badgeObj = new GameObject("Badge");
        badgeObj.transform.SetParent(btnObj.transform, false);
        var bRect = badgeObj.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.5f, 0.5f);
        bRect.anchorMax = new Vector2(0.5f, 0.5f);
        bRect.anchoredPosition = new Vector2(0f, -100f);
        bRect.sizeDelta = new Vector2(230f, 42f);

        var bBg = badgeObj.AddComponent<Image>();
        bBg.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
        bBg.raycastTarget = false;

        GameObject badgeTextObj = new GameObject("BadgeText");
        badgeTextObj.transform.SetParent(badgeObj.transform, false);
        var btRect = badgeTextObj.AddComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.sizeDelta = Vector2.zero;
        var bt = badgeTextObj.AddComponent<TextMeshProUGUI>();
        bt.text = "MASH [S]!";
        bt.fontSize = 17f;
        bt.fontStyle = FontStyles.Bold;
        bt.alignment = TextAlignmentOptions.Center;
        bt.color = Color.white;
        bt.characterSpacing = 1.5f;
        bt.raycastTarget = false;
    }

    private void BuildPhase2UI(Transform canvas)
    {
        phase2Root = new GameObject("Phase2_Root");
        phase2Root.transform.SetParent(canvas, false);
        var rootRect = phase2Root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        // --- Left Medallion: [HOLD S] at x = -150f ---
        GameObject holdObj = new GameObject("HoldS_Medallion");
        holdObj.transform.SetParent(phase2Root.transform, false);
        p2HoldButtonRect = holdObj.AddComponent<RectTransform>();
        p2HoldButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
        p2HoldButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
        p2HoldButtonRect.pivot = new Vector2(0.5f, 0.5f);
        p2HoldButtonRect.anchoredPosition = new Vector2(-150f, -170f);
        p2HoldButtonRect.sizeDelta = new Vector2(150f, 150f);

        var holdImg = holdObj.AddComponent<Image>();
        holdImg.color = Color.clear;
        var holdTrigger = holdObj.AddComponent<EventTrigger>();
        var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener((_) => isMouseHoldingS = true);
        holdTrigger.triggers.Add(downEntry);
        var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener((_) => isMouseHoldingS = false);
        holdTrigger.triggers.Add(upEntry);

        // Hold Track Ring
        GameObject hTrack = new GameObject("TrackRing");
        hTrack.transform.SetParent(holdObj.transform, false);
        var htRect = hTrack.AddComponent<RectTransform>();
        htRect.anchorMin = Vector2.zero;
        htRect.anchorMax = Vector2.one;
        htRect.sizeDelta = Vector2.zero;
        var htImg = hTrack.AddComponent<Image>();
        htImg.sprite = trackRingSprite != null ? trackRingSprite : outerRingSprite;
        htImg.color = new Color(0.18f, 0.20f, 0.24f, 0.45f);
        htImg.preserveAspect = true;
        htImg.raycastTarget = false;

        // Hold Ring (Glowing emerald or alert red)
        GameObject hRing = new GameObject("HoldRing");
        hRing.transform.SetParent(holdObj.transform, false);
        var hrRect = hRing.AddComponent<RectTransform>();
        hrRect.anchorMin = Vector2.zero;
        hrRect.anchorMax = Vector2.one;
        hrRect.sizeDelta = Vector2.zero;
        p2HoldRing = hRing.AddComponent<Image>();
        p2HoldRing.sprite = outerRingSprite;
        p2HoldRing.type = Image.Type.Filled;
        p2HoldRing.fillMethod = Image.FillMethod.Radial360;
        p2HoldRing.fillAmount = 1f;
        p2HoldRing.color = new Color(46f / 255f, 204f / 255f, 113f / 255f, 1f); // #2ECC71 Emerald
        p2HoldRing.preserveAspect = true;
        p2HoldRing.raycastTarget = false;

        // Center Medallion
        GameObject hCenter = new GameObject("CenterCircle");
        hCenter.transform.SetParent(holdObj.transform, false);
        var hcRect = hCenter.AddComponent<RectTransform>();
        hcRect.anchorMin = Vector2.zero;
        hcRect.anchorMax = Vector2.one;
        hcRect.sizeDelta = Vector2.zero;
        var hcImg = hCenter.AddComponent<Image>();
        hcImg.sprite = circleButtonSprite;
        hcImg.preserveAspect = true;
        hcImg.raycastTarget = false;

        // Text "S"
        GameObject hTextObj = new GameObject("KeyText");
        hTextObj.transform.SetParent(holdObj.transform, false);
        var hktRect = hTextObj.AddComponent<RectTransform>();
        hktRect.anchorMin = Vector2.zero;
        hktRect.anchorMax = Vector2.one;
        hktRect.anchoredPosition = new Vector2(0f, -2f);
        hktRect.sizeDelta = Vector2.zero;
        var hkt = hTextObj.AddComponent<TextMeshProUGUI>();
        hkt.text = "S";
        hkt.fontSize = 44f;
        hkt.fontStyle = FontStyles.Bold;
        hkt.alignment = TextAlignmentOptions.Center;
        hkt.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        hkt.raycastTarget = false;

        // Hold Badge
        GameObject hBadge = new GameObject("HoldBadge");
        hBadge.transform.SetParent(holdObj.transform, false);
        var hbRect = hBadge.AddComponent<RectTransform>();
        hbRect.anchorMin = new Vector2(0.5f, 0.5f);
        hbRect.anchorMax = new Vector2(0.5f, 0.5f);
        hbRect.anchoredPosition = new Vector2(0f, -96f);
        hbRect.sizeDelta = new Vector2(190f, 40f);
        var hbBg = hBadge.AddComponent<Image>();
        hbBg.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
        hbBg.raycastTarget = false;

        GameObject hbtObj = new GameObject("BadgeText");
        hbtObj.transform.SetParent(hBadge.transform, false);
        var hbtRect = hbtObj.AddComponent<RectTransform>();
        hbtRect.anchorMin = Vector2.zero;
        hbtRect.anchorMax = Vector2.one;
        hbtRect.sizeDelta = Vector2.zero;
        p2HoldStatusLabel = hbtObj.AddComponent<TextMeshProUGUI>();
        p2HoldStatusLabel.text = "HOLD [S]";
        p2HoldStatusLabel.fontSize = 15f;
        p2HoldStatusLabel.fontStyle = FontStyles.Bold;
        p2HoldStatusLabel.alignment = TextAlignmentOptions.Center;
        p2HoldStatusLabel.color = Color.white;
        p2HoldStatusLabel.raycastTarget = false;


        // --- Right Medallion: [MASH W] at x = +150f ---
        GameObject mashObj = new GameObject("MashW_Medallion");
        mashObj.transform.SetParent(phase2Root.transform, false);
        p2MashButtonRect = mashObj.AddComponent<RectTransform>();
        p2MashButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
        p2MashButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
        p2MashButtonRect.pivot = new Vector2(0.5f, 0.5f);
        p2MashButtonRect.anchoredPosition = new Vector2(150f, -170f);
        p2MashButtonRect.sizeDelta = new Vector2(150f, 150f);

        var mashImg = mashObj.AddComponent<Image>();
        mashImg.color = Color.clear;
        var mashBtn = mashObj.AddComponent<Button>();
        mashBtn.targetGraphic = mashImg;
        mashBtn.transition = Selectable.Transition.None;
        mashBtn.onClick.AddListener(() => OnPhase2MashPressed());

        // Mash Track Ring
        GameObject mTrack = new GameObject("TrackRing");
        mTrack.transform.SetParent(mashObj.transform, false);
        var mtRect = mTrack.AddComponent<RectTransform>();
        mtRect.anchorMin = Vector2.zero;
        mtRect.anchorMax = Vector2.one;
        mtRect.sizeDelta = Vector2.zero;
        var mtImg = mTrack.AddComponent<Image>();
        mtImg.sprite = trackRingSprite != null ? trackRingSprite : outerRingSprite;
        mtImg.color = new Color(0.18f, 0.20f, 0.24f, 0.45f);
        mtImg.preserveAspect = true;
        mtImg.raycastTarget = false;

        // Mash Timer Ring (Radial360 Fill)
        GameObject mRing = new GameObject("TimerRing");
        mRing.transform.SetParent(mashObj.transform, false);
        var mrRect = mRing.AddComponent<RectTransform>();
        mrRect.anchorMin = Vector2.zero;
        mrRect.anchorMax = Vector2.one;
        mrRect.sizeDelta = Vector2.zero;
        p2MashTimerRing = mRing.AddComponent<Image>();
        p2MashTimerRing.sprite = outerRingSprite;
        p2MashTimerRing.type = Image.Type.Filled;
        p2MashTimerRing.fillMethod = Image.FillMethod.Radial360;
        p2MashTimerRing.fillOrigin = (int)Image.Origin360.Top;
        p2MashTimerRing.fillClockwise = true;
        p2MashTimerRing.fillAmount = phase2Progress;
        p2MashTimerRing.color = new Color(181f / 255f, 191f / 255f, 127f / 255f, 1f);
        p2MashTimerRing.preserveAspect = true;
        p2MashTimerRing.raycastTarget = false;

        // Center Medallion
        GameObject mCenter = new GameObject("CenterCircle");
        mCenter.transform.SetParent(mashObj.transform, false);
        var mcRect = mCenter.AddComponent<RectTransform>();
        mcRect.anchorMin = Vector2.zero;
        mcRect.anchorMax = Vector2.one;
        mcRect.sizeDelta = Vector2.zero;
        var mcImg = mCenter.AddComponent<Image>();
        mcImg.sprite = wButtonSprite != null ? wButtonSprite : circleButtonSprite;
        mcImg.preserveAspect = true;
        mcImg.raycastTarget = false;

        // Text "W" (if wButtonSprite is not drawn)
        if (wButtonSprite == null)
        {
            GameObject mTextObj = new GameObject("KeyText");
            mTextObj.transform.SetParent(mashObj.transform, false);
            var mktRect = mTextObj.AddComponent<RectTransform>();
            mktRect.anchorMin = Vector2.zero;
            mktRect.anchorMax = Vector2.one;
            mktRect.anchoredPosition = new Vector2(0f, -2f);
            mktRect.sizeDelta = Vector2.zero;
            var mkt = mTextObj.AddComponent<TextMeshProUGUI>();
            mkt.text = "W";
            mkt.fontSize = 44f;
            mkt.fontStyle = FontStyles.Bold;
            mkt.alignment = TextAlignmentOptions.Center;
            mkt.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            mkt.raycastTarget = false;
        }

        // Mash Badge
        GameObject mBadge = new GameObject("MashBadge");
        mBadge.transform.SetParent(mashObj.transform, false);
        var mbRect = mBadge.AddComponent<RectTransform>();
        mbRect.anchorMin = new Vector2(0.5f, 0.5f);
        mbRect.anchorMax = new Vector2(0.5f, 0.5f);
        mbRect.anchoredPosition = new Vector2(0f, -96f);
        mbRect.sizeDelta = new Vector2(190f, 40f);
        var mbBg = mBadge.AddComponent<Image>();
        mbBg.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
        mbBg.raycastTarget = false;

        GameObject mbtObj = new GameObject("BadgeText");
        mbtObj.transform.SetParent(mBadge.transform, false);
        var mbtRect = mbtObj.AddComponent<RectTransform>();
        mbtRect.anchorMin = Vector2.zero;
        mbtRect.anchorMax = Vector2.one;
        mbtRect.sizeDelta = Vector2.zero;
        p2MashStatusLabel = mbtObj.AddComponent<TextMeshProUGUI>();
        p2MashStatusLabel.text = "MASH [W]!";
        p2MashStatusLabel.fontSize = 15f;
        p2MashStatusLabel.fontStyle = FontStyles.Bold;
        p2MashStatusLabel.alignment = TextAlignmentOptions.Center;
        p2MashStatusLabel.color = Color.white;
        p2MashStatusLabel.raycastTarget = false;

        phase2Root.SetActive(false);
    }

    private void BuildSuccessBanner(Transform canvas)
    {
        successBannerObj = new GameObject("SuccessBanner");
        successBannerObj.transform.SetParent(canvas, false);
        var sbRect = successBannerObj.AddComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(0.5f, 0.5f);
        sbRect.anchorMax = new Vector2(0.5f, 0.5f);
        sbRect.anchoredPosition = new Vector2(0f, 0f);
        sbRect.sizeDelta = new Vector2(750f, 160f);

        var sbImg = successBannerObj.AddComponent<Image>();
        sbImg.color = new Color(0.04f, 0.16f, 0.08f, 0.94f); // Rich dark emerald

        GameObject bannerText = new GameObject("BannerText");
        bannerText.transform.SetParent(successBannerObj.transform, false);
        var btRect = bannerText.AddComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.sizeDelta = Vector2.zero;

        var txt = bannerText.AddComponent<TextMeshProUGUI>();
        txt.text = "<color=#2ECC71><b>WARDROBE SECURED!</b></color>\n<size=20><color=#E0F2E9>Hallway cleared! Wardrobe lifted safely against the wall!</color></size>";
        txt.fontSize = 28f;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;

        successBannerObj.SetActive(false);
    }

    private void BuildSceneAudio()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        sfxThump = SynthesizeThumpClip();
        sfxCreak = SynthesizeCreakClip();
        sfxLatch = SynthesizeLatchClip();
        sfxVictory = SynthesizeVictoryClip();
    }

    private void Update()
    {
        if (isEnding) return;

        UpdateTimelineTimer();

        if (currentPhase == QTEPhase.Phase1_MashS)
        {
            HandlePhase1Input();
        }
        else if (currentPhase == QTEPhase.Phase2_HoldS_MashW)
        {
            HandlePhase2Input();
        }

        UpdateDolapVisuals();
        UpdateUIFeedback();
    }

    private void UpdateTimelineTimer()
    {
        if (GameTimelineManager.Instance != null && timelineTimerText != null)
        {
            float t = GameTimelineManager.Instance.CurrentStageTimer;
            timelineTimerText.text = $"TIME: {t:F1}s";
            timelineTimerText.color = t > 10f ? new Color(1f, 0.85f, 0.4f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);
        }
    }

    private void HandlePhase1Input()
    {
        // Keyboard S key press
        if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
        {
            OnPhase1MashPressed();
        }

        // Idle decay
        phase1Progress -= 0.12f * Time.unscaledDeltaTime;
        phase1Progress = Mathf.Clamp(phase1Progress, 0f, 1f);
    }

    private void OnPhase1MashPressed()
    {
        if (currentPhase != QTEPhase.Phase1_MashS || isEnding) return;

        phase1Progress += 0.080f;
        PlayAudio(sfxThump, 0.85f);
        PunchMedallion(p1ButtonRect);

        if (phase1Progress >= 1.0f)
        {
            phase1Progress = 1.0f;
            StartCoroutine(TransitionToPhase2());
        }
    }

    private IEnumerator TransitionToPhase2()
    {
        PlayAudio(sfxLatch, 1.0f);
        currentPhase = QTEPhase.Phase2_HoldS_MashW;

        if (liftCue != null)
        {
            liftCue.UpdateText("HEAVE UPRIGHT", "Hold [S] & Mash [W]");
        }

        if (headerTitleText != null)
        {
            headerTitleText.text = "<color=#2ECC71>GRIP SECURED!</color> NOW HOLD [S] & MASH [W]!";
        }
        if (subtitleText != null)
        {
            subtitleText.text = "Hold [S] down for leverage while mashing [W] to heave upright!";
        }

        yield return new WaitForSecondsRealtime(0.4f);

        if (phase1Root != null) phase1Root.SetActive(false);
        if (phase2Root != null) phase2Root.SetActive(true);

        headerTitleText.text = "PHASE 2: HOLD [S] & MASH [W]!";
    }

    private void HandlePhase2Input()
    {
        bool isHoldingS = (Keyboard.current != null && Keyboard.current.sKey.isPressed) || isMouseHoldingS;

        if (isHoldingS)
        {
            // S is securely held!
            if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame)
            {
                OnPhase2MashPressed();
            }
        }
        else
        {
            // Player let go of S! Progress rapidly slips back!
            phase2Progress -= 0.22f * Time.unscaledDeltaTime;
            phase2Progress = Mathf.Clamp(phase2Progress, 0f, 1f);

            if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame)
            {
                // Tried to push without leverage!
                PlayAudio(sfxCreak, 0.9f);
                if (p2HoldButtonRect != null) PunchMedallion(p2HoldButtonRect, 0.88f);
            }
        }
    }

    private void OnPhase2MashPressed()
    {
        if (currentPhase != QTEPhase.Phase2_HoldS_MashW || isEnding) return;

        bool isHoldingS = (Keyboard.current != null && Keyboard.current.sKey.isPressed) || isMouseHoldingS;
        if (!isHoldingS)
        {
            PlayAudio(sfxCreak, 0.9f);
            return;
        }

        phase2Progress += 0.075f;
        PlayAudio(sfxThump, 0.95f);
        PunchMedallion(p2MashButtonRect);

        if (phase2Progress >= 1.0f)
        {
            phase2Progress = 1.0f;
            StartCoroutine(CompleteQTE());
        }
    }

    private void UpdateDolapVisuals()
    {
        if (dolapRenderer == null || dolapTransform == null) return;

        if (currentPhase == QTEPhase.Phase1_MashS)
        {
            float p = Mathf.Clamp01(phase1Progress);

            // Sprite stages: frame 4 -> frame 3
            if (p < 0.45f)
            {
                if (frame4Fallen != null) dolapRenderer.sprite = frame4Fallen;
            }
            else
            {
                if (frame3Falling != null) dolapRenderer.sprite = frame3Falling;
            }

            // Slight physical lift bob
            dolapTransform.position = dolapBasePos + new Vector3(0f, p * 0.20f, 0f);
        }
        else if (currentPhase == QTEPhase.Phase2_HoldS_MashW)
        {
            float p = Mathf.Clamp01(phase2Progress);

            // Sprite stages: frame 3 -> frame 2 -> frame 1 (upright)
            if (p < 0.45f)
            {
                if (frame3Falling != null) dolapRenderer.sprite = frame3Falling;
            }
            else if (p < 0.85f)
            {
                if (frame2Tilt != null) dolapRenderer.sprite = frame2Tilt;
            }
            else
            {
                if (frame1Upright != null) dolapRenderer.sprite = frame1Upright;
            }

            // Move smoothly toward full upright position
            dolapTransform.position = dolapBasePos + new Vector3(0f, 0.20f + p * 0.40f, 0f);
        }
        else if (currentPhase == QTEPhase.Completed)
        {
            if (frame1Upright != null) dolapRenderer.sprite = frame1Upright;
            dolapTransform.position = dolapBasePos + new Vector3(0f, 0.60f, 0f);
        }
    }

    private void UpdateUIFeedback()
    {
        if (currentPhase == QTEPhase.Phase1_MashS)
        {
            if (p1TimerRing != null)
            {
                p1TimerRing.fillAmount = Mathf.Clamp01(phase1Progress);
                Color normalColor = new Color(181f / 255f, 191f / 255f, 127f / 255f, 1f); // #B5BF7F
                Color brightGold = new Color(241f / 255f, 196f / 255f, 15f / 255f, 1f);    // #F1C40F
                p1TimerRing.color = Color.Lerp(normalColor, brightGold, phase1Progress);
            }
        }
        else if (currentPhase == QTEPhase.Phase2_HoldS_MashW)
        {
            bool isHoldingS = (Keyboard.current != null && Keyboard.current.sKey.isPressed) || isMouseHoldingS;

            // Update Left Hold S button
            if (p2HoldRing != null)
            {
                if (isHoldingS)
                {
                    p2HoldRing.color = new Color(46f / 255f, 204f / 255f, 113f / 255f, 1f); // Vibrant emerald
                }
                else
                {
                    // Flashing urgent red
                    float flash = Mathf.PingPong(Time.unscaledTime * 8f, 1f);
                    p2HoldRing.color = Color.Lerp(new Color(231f / 255f, 76f / 255f, 60f / 255f, 1f), new Color(1f, 0.3f, 0.3f, 1f), flash);
                }
            }

            if (p2HoldStatusLabel != null)
            {
                p2HoldStatusLabel.text = isHoldingS ? "<color=#2ECC71><b>HOLDING!</b></color>" : "<color=#E74C3C><b>HOLD [S]!</b></color>";
            }

            // Update Right Mash W button
            if (p2MashTimerRing != null)
            {
                p2MashTimerRing.fillAmount = Mathf.Clamp01(phase2Progress);
                Color amber = new Color(243f / 255f, 156f / 255f, 18f / 255f, 1f);
                Color emerald = new Color(46f / 255f, 204f / 255f, 113f / 255f, 1f);
                p2MashTimerRing.color = Color.Lerp(amber, emerald, phase2Progress);
            }
        }
    }

    private void PunchMedallion(RectTransform rect, float punchScale = 0.92f)
    {
        if (rect != null)
        {
            StartCoroutine(PunchRoutine(rect, punchScale));
        }
    }

    private IEnumerator PunchRoutine(RectTransform rect, float punchScale)
    {
        rect.localScale = new Vector3(punchScale, punchScale, 1f);
        yield return new WaitForSecondsRealtime(0.06f);
        rect.localScale = new Vector3(1.05f, 1.05f, 1f);
        yield return new WaitForSecondsRealtime(0.05f);
        rect.localScale = Vector3.one;
    }

    private IEnumerator CompleteQTE()
    {
        isEnding = true;
        currentPhase = QTEPhase.Completed;

        if (liftCue != null)
        {
            liftCue.Dismiss();
            liftCue = null;
        }

        UpdateDolapVisuals();
        PlayAudio(sfxVictory, 1.0f);
        AudioManager.PlayAnchorSuccess();

        if (phase2Root != null) phase2Root.SetActive(false);
        if (successBannerObj != null) successBannerObj.SetActive(true);

        Debug.Log("<color=green>[DolapSceneController] Wardrobe successfully lifted & secured! Returning to MainScene...</color>");

        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.HandleDolapQTEResult(true);
        }

        yield return new WaitForSecondsRealtime(1.8f);

        SceneManager.LoadScene("MainScene");
    }

    private void PlayAudio(AudioClip clip, float volume = 1f)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    #region --- Audio Synthesis ---

    private AudioClip SynthesizeThumpClip()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.16f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 22f);
            float f = Mathf.Sin(2f * Mathf.PI * 75f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.3f;
            data[i] = f * env;
        }
        var clip = AudioClip.Create("QTE_Thump", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip SynthesizeCreakClip()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.25f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Sin(t / 0.25f * Mathf.PI);
            float freq = 220f + Mathf.Sin(t * 80f) * 60f;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
        }
        var clip = AudioClip.Create("QTE_Creak", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip SynthesizeLatchClip()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.22f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 30f);
            data[i] = Mathf.Sin(2f * Mathf.PI * 440f * t) * env * 0.6f + Mathf.Sin(2f * Mathf.PI * 880f * t) * env * 0.3f;
        }
        var clip = AudioClip.Create("QTE_Latch", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip SynthesizeVictoryClip()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.6f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 4f);
            float note = t < 0.18f ? 523.25f : (t < 0.36f ? 659.25f : 783.99f); // C - E - G
            data[i] = Mathf.Sin(2f * Mathf.PI * note * t) * env * 0.5f;
        }
        var clip = AudioClip.Create("QTE_Victory", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    #endregion
}
