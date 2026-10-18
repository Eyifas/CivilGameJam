using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Controls high-impact, fullscreen educational death/hazard visuals for all failure reasons in the game.
/// Rendered on a persistent ScreenSpaceOverlay Canvas (sortingOrder = 9500), guaranteeing 100% visibility
/// regardless of camera coordinates, zoom level, room boundaries, or 2D lighting.
/// Plays dramatic hazard-specific audio, letterboxes the scene, animates the comic artwork card with a punchy bounce,
/// and presents clear educational cause & survival rules before seamlessly transitioning to TimeRewind.
/// </summary>
public class HazardVisualManager : MonoBehaviour
{
    private static HazardVisualManager s_instance;
    public static HazardVisualManager Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = FindFirstObjectByType<HazardVisualManager>();
                if (s_instance == null)
                {
                    GameObject go = new GameObject("HazardVisualManager");
                    s_instance = go.AddComponent<HazardVisualManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return s_instance;
        }
        private set => s_instance = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (s_instance == null)
        {
            var go = new GameObject("HazardVisualManager");
            s_instance = go.AddComponent<HazardVisualManager>();
            DontDestroyOnLoad(go);
        }
    }

    public enum HazardSFXType
    {
        Explosion,
        CabinetCrash,
        WardrobeCrash,
        PaintingFall,
        DoorRushFall,
        GasSuffocation,
        TremorRumble
    }

    [System.Serializable]
    public class HazardConfig
    {
        public string id;
        public string title;
        public string[] matchKeywords;
        public string resourcePath;
        public string[] filePaths;
        public HazardSFXType sfxType;
        public Color accentColor = new Color(1f, 0.28f, 0.25f, 1f);
    }

    private List<HazardConfig> hazardConfigs;

    // Overlay Canvas & UI Components
    private Canvas hazardCanvas;
    private CanvasGroup canvasGroup;
    private Image backdropImage;
    private Image redVignetteImage;
    private RectTransform cardTransform;
    private Image comicImage;
    private Image cardBorderImage;
    private TMP_Text badgeText;
    private TMP_Text titleText;
    private TMP_Text reasonText;
    private TMP_Text promptText;

    private bool isShowing = false;
    public bool IsShowing => isShowing;

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeDefaultConfigs();
        BuildCanvasUI();
    }

    private void InitializeDefaultConfigs()
    {
        hazardConfigs = new List<HazardConfig>
        {
            // 1. Stage 2 - Candle fire (Comic Panel 2)
            new HazardConfig
            {
                id = "Hazard_CandleFire",
                title = "APARTMENT FIRE HAZARD",
                matchKeywords = new[] { "candle", "APARTMENT FIRE", "flames", "igniting the room" },
                resourcePath = "Hazards/comic_panel_2_earthquake_fire",
                filePaths = new[]
                {
                    "Resources/Hazards/comic_panel_2_earthquake_fire.png",
                    "MainScene/Sprite/Hazards/comic_panel_2_earthquake_fire.png"
                },
                sfxType = HazardSFXType.Explosion,
                accentColor = new Color(1f, 0.45f, 0.15f, 1f)
            },

            // 2. Stage 2 - Heavy wall painting falls (Comic Panel 1)
            new HazardConfig
            {
                id = "Hazard_PaintingFall",
                title = "FALLING WALL DECORATION",
                matchKeywords = new[] { "painting", "wall hangings", "shattered glass", "heavy wall painting" },
                resourcePath = "Hazards/comic_panel_1_picture_frame",
                filePaths = new[]
                {
                    "Resources/Hazards/comic_panel_1_picture_frame.png",
                    "MainScene/Sprite/Hazards/comic_panel_1_picture_frame.png"
                },
                sfxType = HazardSFXType.PaintingFall,
                accentColor = new Color(1f, 0.35f, 0.25f, 1f)
            },

            // 3. Stage 3 - Light switch spark explosion (Explosion Art)
            new HazardConfig
            {
                id = "Hazard_ElectricSwitch",
                title = "ELECTRICAL SPARK GAS EXPLOSION",
                matchKeywords = new[] { "electrical light switch", "switch sparked", "FATAL EXPLOSION", "leaking gas", "spark caused gas explosion", "TEST REWIND" },
                resourcePath = "Hazards/explosion",
                filePaths = new[]
                {
                    "Resources/Hazards/explosion.png",
                    "MainScene/Sprite/Hazards/explosion.png",
                    "Scenes/1000000106.png"
                },
                sfxType = HazardSFXType.Explosion,
                accentColor = new Color(1f, 0.85f, 0.2f, 1f)
            },

            // 4. Stage 3 - Overhead Kitchen Cabinet Fall (Cupboard QTE Fail)
            new HazardConfig
            {
                id = "Hazard_CabinetFall",
                title = "OVERHEAD CABINET FALL",
                matchKeywords = new[] { "cabinet fell", "shifting cabinet", "overhead obstacles", "STRUCK BY CABINET DOOR" },
                resourcePath = "dolap mutfak1",
                filePaths = new[]
                {
                    "MainScene/Sprite/dolap mutfak1.png",
                    "MainScene/Sprite/dolap mutfak.png",
                    "Resources/Hazards/comic_panel_1_picture_frame.png"
                },
                sfxType = HazardSFXType.CabinetCrash,
                accentColor = new Color(0.95f, 0.55f, 0.2f, 1f)
            },

            // 5. Stage 3 - Heavy Hallway Wardrobe Fall (Dolap QTE Fail)
            new HazardConfig
            {
                id = "Hazard_WardrobeFall",
                title = "FALLEN WARDROBE CRUSH",
                matchKeywords = new[] { "wardrobe fell", "heavy wardrobe", "düşme dolap", "brace and hold your footing" },
                resourcePath = "düşme dolap4",
                filePaths = new[]
                {
                    "MainScene/Sprite/düşme dolap4.png",
                    "MainScene/Sprite/düşme dolap3.png",
                    "MainScene/Sprite/düşme dolap1.png"
                },
                sfxType = HazardSFXType.WardrobeCrash,
                accentColor = new Color(0.9f, 0.4f, 0.2f, 1f)
            },

            // 6. Stage 3 - Toxic gas suffocation / gas in dark / gas leak timeout (Comic Panel 3)
            new HazardConfig
            {
                id = "Hazard_GasSuffocation",
                title = "TOXIC GAS SUFFOCATION",
                matchKeywords = new[] { "Lingered too long", "leaking natural gas", "TOXIC GAS", "gas valve in total darkness", "packed flashlight is essential", "broken fixtures", "IN THE DARK", "gas leak" },
                resourcePath = "Hazards/comic_panel_3_gas_leak",
                filePaths = new[]
                {
                    "Resources/Hazards/comic_panel_3_gas_leak.jpg",
                    "MainScene/Sprite/Hazards/comic_panel_3_gas_leak.jpg"
                },
                sfxType = HazardSFXType.GasSuffocation,
                accentColor = new Color(0.4f, 0.9f, 0.4f, 1f)
            },

            // 7. Stage 2 - Ran to the door during tremor (Violent Fall)
            new HazardConfig
            {
                id = "Hazard_DoorRush",
                title = "VIOLENT LATERAL FORCES FALL",
                matchKeywords = new[] { "lateral forces", "RunDoor", "FATAL FALL", "violent lateral", "Never run while the building is shaking" },
                resourcePath = "Story/1000000104",
                filePaths = new[]
                {
                    "MainScene/Sprite/Story/1000000104.png",
                    "Scenes/1000000106.png"
                },
                sfxType = HazardSFXType.DoorRushFall,
                accentColor = new Color(1f, 0.3f, 0.35f, 1f)
            },

            // 8. Stage 2 - Tremor timeout (panic / freeze)
            new HazardConfig
            {
                id = "Hazard_TremorPanicTimeout",
                title = "PANIC FREEZE / UNPROTECTED",
                matchKeywords = new[] { "Frozen in panic", "drop under sturdy", "knocks you down", "TIME RAN OUT: Frozen" },
                resourcePath = "Story/1000000104",
                filePaths = new[]
                {
                    "MainScene/Sprite/Story/1000000104.png",
                    "MainScene/Sprite/Story/1000000110.png"
                },
                sfxType = HazardSFXType.TremorRumble,
                accentColor = new Color(1f, 0.65f, 0.2f, 1f)
            },

            // 9. Stage 3 Building Collapse (lingered in damaged apartment)
            new HazardConfig
            {
                id = "Hazard_BuildingCollapse",
                title = "STRUCTURAL BUILDING COLLAPSE",
                matchKeywords = new[] { "building can collapse", "BUILDING COLLAPSE", "damaged apartment", "structural weakening", "lingered inside" },
                resourcePath = "Story/1000000119",
                filePaths = new[]
                {
                    "MainScene/Sprite/Story/1000000119.png",
                    "Resources/Hazards/comic_panel_2_earthquake_fire.png"
                },
                sfxType = HazardSFXType.Explosion,
                accentColor = new Color(1f, 0.45f, 0.2f, 1f)
            },

            // 10. Stage 4 - Tripped over stairs planter
            new HazardConfig
            {
                id = "Hazard_StairsPlanter",
                title = "STAIRWELL FALL IN DARKNESS",
                matchKeywords = new[] { "heavy planter", "fell down the stairs" },
                resourcePath = "Story/1000000104",
                filePaths = new[]
                {
                    "MainScene/Sprite/Story/1000000104.png"
                },
                sfxType = HazardSFXType.DoorRushFall,
                accentColor = new Color(0.9f, 0.35f, 0.35f, 1f)
            },

            // 11. Stage 4 - Blind search for neighbor
            new HazardConfig
            {
                id = "Hazard_CorridorSearch",
                title = "BLIND CORRIDOR SEARCH",
                matchKeywords = new[] { "Mr. Ahmet", "BLIND SEARCH", "dark corridor" },
                resourcePath = "Hazards/comic_panel_3_gas_leak",
                filePaths = new[]
                {
                    "Resources/Hazards/comic_panel_3_gas_leak.jpg"
                },
                sfxType = HazardSFXType.GasSuffocation,
                accentColor = new Color(0.4f, 0.5f, 0.9f, 1f)
            },

            // 12. Stage 5 - Falling storefront facade
            new HazardConfig
            {
                id = "Hazard_StreetFacade",
                title = "FALLING STOREFRONT FACADE",
                matchKeywords = new[] { "storefront facade", "falling facade", "overhead hazards", "FATAL TRAUMA" },
                resourcePath = "Hazards/comic_panel_1_picture_frame",
                filePaths = new[]
                {
                    "Resources/Hazards/comic_panel_1_picture_frame.png",
                    "MainScene/Sprite/Story/1000000112.png"
                },
                sfxType = HazardSFXType.PaintingFall,
                accentColor = new Color(0.95f, 0.35f, 0.25f, 1f)
            },

            // 13. Stage 5 - Mountain road gridlock in car
            new HazardConfig
            {
                id = "Hazard_CarGridlock",
                title = "MOUNTAIN ROAD GRIDLOCK",
                matchKeywords = new[] { "gridlocked", "GRIDLOCK TRAP", "Stay out of cars", "mountain road" },
                resourcePath = "Story/1000000113",
                filePaths = new[]
                {
                    "MainScene/Sprite/Story/1000000113.png"
                },
                sfxType = HazardSFXType.TremorRumble,
                accentColor = new Color(0.7f, 0.6f, 0.55f, 1f)
            },

            // 14. Stage 5 Finale / Tsunami (Comic Panel 4)
            new HazardConfig
            {
                id = "Hazard_TsunamiEnding",
                title = "TSUNAMI SECONDARY DISASTER",
                matchKeywords = new[] { "tsunami", "DISASTER ESCALATION", "coastal areas" },
                resourcePath = "Hazards/comic_panel_4_tsunami_ending",
                filePaths = new[]
                {
                    "Resources/Hazards/comic_panel_4_tsunami_ending.jpg",
                    "MainScene/Sprite/Hazards/comic_panel_4_tsunami_ending.jpg"
                },
                sfxType = HazardSFXType.Explosion,
                accentColor = new Color(0.2f, 0.65f, 1f, 1f)
            }
        };
    }

    private void BuildCanvasUI()
    {
        var existingCanvas = transform.Find("HazardVisualOverlayCanvas");
        if (existingCanvas != null)
        {
            Destroy(existingCanvas.gameObject);
        }

        GameObject canvasObj = new GameObject("HazardVisualOverlayCanvas");
        canvasObj.transform.SetParent(transform, false);

        hazardCanvas = canvasObj.AddComponent<Canvas>();
        hazardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hazardCanvas.sortingOrder = 9500; // Directly beneath eyelids at 9999

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // 1. Dark Fullscreen Backdrop
        GameObject backdropObj = new GameObject("Backdrop");
        backdropObj.transform.SetParent(canvasObj.transform, false);
        var bdRect = backdropObj.AddComponent<RectTransform>();
        bdRect.anchorMin = Vector2.zero;
        bdRect.anchorMax = Vector2.one;
        bdRect.sizeDelta = Vector2.zero;

        backdropImage = backdropObj.AddComponent<Image>();
        backdropImage.sprite = GetSolidSprite();
        backdropImage.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);
        backdropImage.raycastTarget = true;

        // 2. Red Disaster Vignette Glow
        GameObject vigObj = new GameObject("RedVignette");
        vigObj.transform.SetParent(canvasObj.transform, false);
        var vigRect = vigObj.AddComponent<RectTransform>();
        vigRect.anchorMin = Vector2.zero;
        vigRect.anchorMax = Vector2.one;
        vigRect.sizeDelta = Vector2.zero;

        redVignetteImage = vigObj.AddComponent<Image>();
        redVignetteImage.sprite = GetVignetteSprite();
        redVignetteImage.color = new Color(0.85f, 0.12f, 0.12f, 0.40f);
        redVignetteImage.raycastTarget = false;

        // 3. Top Banner (Warning Badge + Hazard Title)
        GameObject topBannerObj = new GameObject("TopBanner");
        topBannerObj.transform.SetParent(canvasObj.transform, false);
        var tbRect = topBannerObj.AddComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0.5f, 1f);
        tbRect.anchorMax = new Vector2(0.5f, 1f);
        tbRect.pivot = new Vector2(0.5f, 1f);
        tbRect.anchoredPosition = new Vector2(0f, -28f);
        tbRect.sizeDelta = new Vector2(1200f, 110f);

        var tbBg = topBannerObj.AddComponent<Image>();
        tbBg.sprite = GetSolidSprite();
        tbBg.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);

        GameObject badgeObj = new GameObject("BadgeText");
        badgeObj.transform.SetParent(topBannerObj.transform, false);
        var bRect = badgeObj.AddComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0f, 0.55f);
        bRect.anchorMax = new Vector2(1f, 1f);
        bRect.sizeDelta = Vector2.zero;

        badgeText = badgeObj.AddComponent<TextMeshProUGUI>();
        badgeText.fontSize = 20f;
        badgeText.fontStyle = FontStyles.Bold;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.text = "<color=#FF4444>[ CRITICAL DISASTER HAZARD ]</color>";

        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(topBannerObj.transform, false);
        var tRect = titleObj.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0f, 0f);
        tRect.anchorMax = new Vector2(1f, 0.60f);
        tRect.sizeDelta = Vector2.zero;

        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 32f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.text = "HAZARD FAILURE";

        // 4. Central Artwork Card
        GameObject cardBorderObj = new GameObject("ArtworkCardBorder");
        cardBorderObj.transform.SetParent(canvasObj.transform, false);
        cardTransform = cardBorderObj.AddComponent<RectTransform>();
        cardTransform.anchorMin = new Vector2(0.5f, 0.5f);
        cardTransform.anchorMax = new Vector2(0.5f, 0.5f);
        cardTransform.pivot = new Vector2(0.5f, 0.5f);
        cardTransform.anchoredPosition = new Vector2(0f, 32f);
        cardTransform.sizeDelta = new Vector2(1048f, 558f);

        cardBorderImage = cardBorderObj.AddComponent<Image>();
        cardBorderImage.sprite = GetSolidSprite();
        cardBorderImage.color = new Color(0.9f, 0.25f, 0.22f, 0.9f);

        GameObject comicObj = new GameObject("ComicImage");
        comicObj.transform.SetParent(cardBorderObj.transform, false);
        var cRect = comicObj.AddComponent<RectTransform>();
        cRect.anchorMin = Vector2.zero;
        cRect.anchorMax = Vector2.one;
        cRect.sizeDelta = new Vector2(-8f, -8f); // 4px padding inside border
        cRect.anchoredPosition = Vector2.zero;

        comicImage = comicObj.AddComponent<Image>();
        comicImage.preserveAspect = true;
        comicImage.color = Color.white;

        // 5. Bottom Educational Panel (Cause & Survival Rule)
        GameObject botPanelObj = new GameObject("BottomEducationalPanel");
        botPanelObj.transform.SetParent(canvasObj.transform, false);
        var bpRect = botPanelObj.AddComponent<RectTransform>();
        bpRect.anchorMin = new Vector2(0.5f, 0f);
        bpRect.anchorMax = new Vector2(0.5f, 0f);
        bpRect.pivot = new Vector2(0.5f, 0f);
        bpRect.anchoredPosition = new Vector2(0f, 54f);
        bpRect.sizeDelta = new Vector2(1380f, 130f);

        var bpBg = botPanelObj.AddComponent<Image>();
        bpBg.sprite = GetSolidSprite();
        bpBg.color = new Color(0.07f, 0.09f, 0.13f, 0.94f);

        GameObject reasonObj = new GameObject("ReasonText");
        reasonObj.transform.SetParent(botPanelObj.transform, false);
        var rRect = reasonObj.AddComponent<RectTransform>();
        rRect.anchorMin = Vector2.zero;
        rRect.anchorMax = Vector2.one;
        rRect.sizeDelta = new Vector2(-40f, -16f);
        rRect.anchoredPosition = Vector2.zero;

        reasonText = reasonObj.AddComponent<TextMeshProUGUI>();
        reasonText.fontSize = 21f;
        reasonText.alignment = TextAlignmentOptions.Center;
        reasonText.color = Color.white;
        reasonText.richText = true;

        // 6. Bottom Skip / Continue Prompt
        GameObject promptObj = new GameObject("PromptText");
        promptObj.transform.SetParent(canvasObj.transform, false);
        var prRect = promptObj.AddComponent<RectTransform>();
        prRect.anchorMin = new Vector2(0.5f, 0f);
        prRect.anchorMax = new Vector2(0.5f, 0f);
        prRect.pivot = new Vector2(0.5f, 0f);
        prRect.anchoredPosition = new Vector2(0f, 16f);
        prRect.sizeDelta = new Vector2(1000f, 30f);

        promptText = promptObj.AddComponent<TextMeshProUGUI>();
        promptText.fontSize = 17f;
        promptText.fontStyle = FontStyles.Italic;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = new Color(0.8f, 0.86f, 0.92f, 0.85f);
        promptText.text = "Press [SPACE] or [CLICK] to Rewind Time";

        canvasObj.SetActive(false);
    }

    /// <summary>
    /// Displays the high-impact hazard failure card, plays dramatic SFX, freezes gameplay,
    /// and smoothly triggers onFinished (initiating TimeRewind) after display duration.
    /// </summary>
    public void ShowHazardVisual(string educationalReason, Action onVisualFinished)
    {
        if (hazardCanvas == null) BuildCanvasUI();

        StopAllCoroutines();
        StartCoroutine(ShowHazardVisualRoutine(educationalReason, onVisualFinished));
    }

    private IEnumerator ShowHazardVisualRoutine(string educationalReason, Action onVisualFinished)
    {
        isShowing = true;
        Time.timeScale = 0f;

        // Stop earthquake shake and sound effects to allow clear visual presentation
        var cam = FindFirstObjectByType<RoomCamera>();
        if (cam != null) cam.StopEarthquake();
        AudioManager.SetEarthquakeRumbleActive(false);
        AudioManager.SetGasHissActive(false);

        // Turn off player lights
        var allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            p.TurnOffAllLights();
            p.enabled = false;
        }

        // Match Hazard Config
        HazardConfig config = FindBestMatchingConfig(educationalReason);

        // Play matching dramatic audio
        PlayHazardSound(config.sfxType);

        // Load and assign comic artwork sprite
        Sprite artworkSprite = LoadProjectSprite(config.resourcePath, config.filePaths);
        if (artworkSprite == null)
        {
            artworkSprite = LoadFallbackSprite(config.title);
        }

        comicImage.sprite = artworkSprite;
        cardBorderImage.color = config.accentColor;
        titleText.text = $"<b>{config.title}</b>";
        titleText.color = config.accentColor;

        // Format educational cause & safety rule nicely
        FormatEducationalReason(educationalReason, out string formattedCause, out string formattedRule);
        if (!string.IsNullOrEmpty(formattedRule))
        {
            reasonText.text = $"<color=#FFE082><b>CAUSE:</b> {formattedCause}</color>\n<color=#80D8FF><b>SAFETY RULE:</b> {formattedRule}</color>";
        }
        else
        {
            reasonText.text = $"<color=#FFE082><b>INCIDENT:</b> {formattedCause}</color>";
        }

        hazardCanvas.gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        cardTransform.localScale = Vector3.one * 0.90f;

        // Punchy scale-in bounce animation (unscaled time)
        float animElapsed = 0f;
        float animDuration = 0.22f;
        while (animElapsed < animDuration)
        {
            animElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(animElapsed / animDuration);
            // Ease-out overshoot
            float scale = Mathf.Lerp(0.90f, 1.0f, Mathf.Sin(t * Mathf.PI * 0.5f));
            cardTransform.localScale = new Vector3(scale, scale, 1f);
            canvasGroup.alpha = t;
            yield return null;
        }

        cardTransform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;

        // Hold display for 2.4s (skippable after 0.45s)
        float holdElapsed = 0f;
        float holdDuration = 2.4f;
        while (holdElapsed < holdDuration)
        {
            holdElapsed += Time.unscaledDeltaTime;

            if (holdElapsed >= 0.45f)
            {
                bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
                bool enterPressed = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
                bool ePressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
                bool clickPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

                if (spacePressed || enterPressed || ePressed || clickPressed)
                {
                    break;
                }
            }

            yield return null;
        }

        // Note: Do NOT hide canvas immediately here; let the eyelids close OVER it smoothly,
        // then Hide() will be called when eyelids are fully closed (onRewindStateRestore).
        onVisualFinished?.Invoke();
    }

    /// <summary>
    /// Instantly hides the hazard overlay canvas (called when eyelids are fully shut or rewind finishes).
    /// </summary>
    public void Hide()
    {
        if (hazardCanvas != null)
        {
            hazardCanvas.gameObject.SetActive(false);
        }
        isShowing = false;
    }

    private void PlayHazardSound(HazardSFXType sfxType)
    {
        switch (sfxType)
        {
            case HazardSFXType.Explosion:
                AudioManager.PlayExplosion();
                break;
            case HazardSFXType.CabinetCrash:
                AudioManager.PlayCabinetCrash();
                break;
            case HazardSFXType.WardrobeCrash:
                AudioManager.PlayWardrobeCrash();
                break;
            case HazardSFXType.PaintingFall:
                AudioManager.PlayHammerHit();
                break;
            case HazardSFXType.DoorRushFall:
                AudioManager.PlayHammerHit();
                break;
            case HazardSFXType.GasSuffocation:
                AudioManager.SetGasHissActive(true, 0.75f);
                break;
            case HazardSFXType.TremorRumble:
                AudioManager.SetEarthquakeRumbleActive(true, 0.85f);
                break;
        }
    }

    private HazardConfig FindBestMatchingConfig(string reason)
    {
        if (hazardConfigs == null || hazardConfigs.Count == 0)
        {
            InitializeDefaultConfigs();
        }

        if (string.IsNullOrEmpty(reason))
        {
            return hazardConfigs[0];
        }

        // 1. Keyword matching
        foreach (var cfg in hazardConfigs)
        {
            if (reason.IndexOf(cfg.title, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return cfg;
            }

            if (cfg.matchKeywords != null)
            {
                foreach (var kw in cfg.matchKeywords)
                {
                    if (reason.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return cfg;
                    }
                }
            }
        }

        // 2. Fallback default
        return hazardConfigs[0];
    }

    private void FormatEducationalReason(string reason, out string cause, out string rule)
    {
        cause = reason ?? "An unexpected hazard occurred!";
        rule = "";

        if (string.IsNullOrEmpty(reason)) return;

        // If in "[CATEGORY]: [Cause]! [Rule]" structure:
        int colonIdx = reason.IndexOf(':');
        string body = colonIdx >= 0 && colonIdx < reason.Length - 1 ? reason.Substring(colonIdx + 1).Trim() : reason.Trim();

        int splitIdx = -1;
        string[] ruleMarkers = new[] { " Always ", " Never ", " In an earthquake", " A packed flashlight", " Once ", " Watch ", " Stay out of", " Drop, Cover" };

        foreach (var marker in ruleMarkers)
        {
            int idx = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (splitIdx == -1 || idx < splitIdx))
            {
                splitIdx = idx;
            }
        }

        if (splitIdx > 0)
        {
            cause = body.Substring(0, splitIdx).Trim();
            rule = body.Substring(splitIdx).Trim();
        }
        else
        {
            cause = body;
            rule = "";
        }
    }

    /// <summary>
    /// Loads a sprite by resource path or relative asset path from disk/Resources/AssetDatabase.
    /// Preserved for backwards compatibility with WorldChoicePrompt, ApartmentExitDoor, and DolapSceneController.
    /// </summary>
    public static Sprite LoadHazardSprite(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;

        // 1. Try Resources.Load<Sprite>
        Sprite spr = Resources.Load<Sprite>(resourcePath);
        if (spr != null) return spr;

        // 2. Try Resources.Load<Texture2D> and create Sprite
        Texture2D tex = Resources.Load<Texture2D>(resourcePath);
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        string[] extensions = new[] { ".png", ".jpg", ".jpeg" };
        List<string> candidatePaths = new List<string>();

        foreach (var ext in extensions)
        {
            candidatePaths.Add("Resources/" + resourcePath + ext);
            candidatePaths.Add("MainScene/Sprite/" + resourcePath + ext);
            candidatePaths.Add(resourcePath + ext);
        }

        return LoadProjectSprite(resourcePath, candidatePaths.ToArray());
    }

    /// <summary>
    /// Highly robust multi-tier sprite loader:
    /// 1. AssetDatabase in Unity Editor
    /// 2. Resources.Load
    /// 3. Direct byte loading from disk via File.ReadAllBytes & Texture2D.LoadImage
    /// </summary>
    public static Sprite LoadProjectSprite(string resourcePath, string[] relativeFilePaths)
    {
#if UNITY_EDITOR
        if (relativeFilePaths != null)
        {
            foreach (var relPath in relativeFilePaths)
            {
                string assetPath = "Assets/" + relPath.Replace("\\", "/");
                var adSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (adSprite != null) return adSprite;

                var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
                if (allAssets != null)
                {
                    foreach (var a in allAssets)
                    {
                        if (a is Sprite s) return s;
                    }
                }
            }
        }
#endif

        if (!string.IsNullOrEmpty(resourcePath))
        {
            var res = Resources.Load<Sprite>(resourcePath);
            if (res != null) return res;

            var allRes = Resources.LoadAll<Sprite>(resourcePath);
            if (allRes != null && allRes.Length > 0) return allRes[0];
        }

        if (relativeFilePaths != null)
        {
            foreach (var relPath in relativeFilePaths)
            {
                string fullPath = Path.Combine(Application.dataPath, relPath);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(fullPath);
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (tex.LoadImage(bytes))
                        {
                            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[HazardVisualManager] Failed loading disk texture '{fullPath}': {ex.Message}");
                    }
                }
            }
        }

        // Direct search in MainScene/Sprite and Resources
        string cleanBase = Path.GetFileNameWithoutExtension(resourcePath);
        string[] searchFolders = new[]
        {
            Path.Combine(Application.dataPath, "MainScene", "Sprite"),
            Path.Combine(Application.dataPath, "MainScene", "Sprite", "Hazards"),
            Path.Combine(Application.dataPath, "MainScene", "Sprite", "Story"),
            Path.Combine(Application.dataPath, "Resources", "Hazards")
        };

        foreach (var dir in searchFolders)
        {
            if (!Directory.Exists(dir)) continue;

            var files = Directory.GetFiles(dir);
            foreach (var f in files)
            {
                if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string fName = Path.GetFileNameWithoutExtension(f);
                if (NormalizeName(fName) == NormalizeName(cleanBase) || fName.IndexOf(cleanBase, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(f);
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (tex.LoadImage(bytes))
                        {
                            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[HazardVisualManager] Failed loading disk texture '{f}': {ex.Message}");
                    }
                }
            }
        }

        return null;
    }

    private static string NormalizeName(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("ü", "u").Replace("ş", "s").Replace("ç", "c").Replace("ı", "i").ToLowerInvariant().Trim();
    }

    private Sprite LoadFallbackSprite(string title)
    {
        return LoadProjectSprite("Story/1000000104", new[] { "MainScene/Sprite/Story/1000000104.png", "Scenes/1000000106.png" }) ?? GetSolidSprite();
    }

    private static Sprite s_solidSprite;
    private static Sprite GetSolidSprite()
    {
        if (s_solidSprite != null) return s_solidSprite;
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Color[] p = { Color.white, Color.white, Color.white, Color.white };
        tex.SetPixels(p);
        tex.Apply();
        s_solidSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
        return s_solidSprite;
    }

    private static Sprite s_vignetteSprite;
    private static Sprite GetVignetteSprite()
    {
        if (s_vignetteSprite != null) return s_vignetteSprite;
        int w = 256;
        int h = 144;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w;
                float v = (float)y / h;
                float edgeDist = Mathf.Min(u, 1f - u, v, 1f - v) * 2.2f;
                float vig = Mathf.Clamp01(1f - edgeDist);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, vig * vig));
            }
        }
        tex.Apply();
        s_vignetteSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return s_vignetteSprite;
    }

    // -----------------------------------------------------------------------------------------
    // BACKWARDS COMPATIBILITY INTERFACES
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Legacy compatibility method for existing scripts referencing SwitchInteraction wrappers.
    /// </summary>
    public SwitchInteraction GetHazard(string educationalReason)
    {
        // Simply ensure HazardVisualManager is active; this method is kept so old call sites compile cleanly
        return null;
    }

    /// <summary>
    /// Legacy compatibility method to trigger hazard visual.
    /// </summary>
    public bool TriggerHazard(string educationalReason)
    {
        ShowHazardVisual(educationalReason, () =>
        {
            if (GameTimelineManager.Instance != null)
            {
                GameTimelineManager.Instance.TriggerRewind(educationalReason, isFromVisual: true);
            }
        });
        return true;
    }
}
