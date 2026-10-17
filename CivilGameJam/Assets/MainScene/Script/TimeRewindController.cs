using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the cinematic "Going Back in Time" death/failure rewind mechanism.
/// Plays an on-screen eye closing effect, reveals a clock spinning backward in time
/// with reverse ticking and time-warp audio, closes the eye again, adds 5 seconds,
/// and reopens the eye as if the fatal action never happened.
/// </summary>
public class TimeRewindController : MonoBehaviour
{
    public static TimeRewindController Instance { get; private set; }

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
    private RectTransform hourHand;
    private RectTransform minuteHand;
    private RectTransform secondHand;
    private Image clockAura;
    private TMP_Text headerText;
    private TMP_Text remainingLoopsText;
    private TMP_Text digitalClockText;
    private TMP_Text reasonText;
    private TMP_Text bonusBadgeText;

    // Audio
    private AudioSource audioSource;
    private AudioClip synthRewindWhoosh;
    private AudioClip synthReverseTick;
    private AudioClip synthTimeLockChime;
    private AudioClip synthEyelidThump;

    private bool isRewinding = false;

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

    #region --- PROCEDURAL UI CONSTRUCTION ---

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

    private void BuildEyelidsUI(GameObject parent)
    {
        eyelidsRoot = new GameObject("EyelidsRoot");
        eyelidsRoot.transform.SetParent(parent.transform, false);
        RectTransform rootRect = eyelidsRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        // Procedural Eyelid Sprites
        Sprite topEyelidSprite = GenerateTopEyelidSprite();
        Sprite bottomEyelidSprite = GenerateBottomEyelidSprite();

        // Top Eyelid
        GameObject topObj = new GameObject("TopEyelid");
        topObj.transform.SetParent(eyelidsRoot.transform, false);
        topEyelid = topObj.AddComponent<RectTransform>();
        topEyelid.anchorMin = new Vector2(0f, 0.5f);
        topEyelid.anchorMax = new Vector2(1f, 1f);
        topEyelid.pivot = new Vector2(0.5f, 0f);
        topEyelid.sizeDelta = new Vector2(400f, 650f); // Extra bleed over all edges
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

        // Center Slit Vignette (darkens edges and center seam)
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

    private void BuildClockUI(GameObject parent)
    {
        clockRoot = new GameObject("ClockRoot");
        clockRoot.transform.SetParent(parent.transform, false);
        RectTransform rootRect = clockRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.sizeDelta = Vector2.zero;

        // Dark background backdrop
        GameObject bgObj = new GameObject("DarkBackdrop");
        bgObj.transform.SetParent(clockRoot.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.02f, 0.03f, 0.05f, 0.94f);
        bgImg.raycastTarget = false;

        // Clock Panel Container
        GameObject panelObj = new GameObject("ClockPanel");
        panelObj.transform.SetParent(clockRoot.transform, false);
        clockPanel = panelObj.AddComponent<RectTransform>();
        clockPanel.anchorMin = new Vector2(0.5f, 0.55f);
        clockPanel.anchorMax = new Vector2(0.5f, 0.55f);
        clockPanel.pivot = new Vector2(0.5f, 0.5f);
        clockPanel.sizeDelta = new Vector2(480f, 480f);

        // Pulsing Aura Ring
        GameObject auraObj = new GameObject("ClockAura");
        auraObj.transform.SetParent(clockPanel, false);
        RectTransform auraRect = auraObj.AddComponent<RectTransform>();
        auraRect.anchorMin = Vector2.zero;
        auraRect.anchorMax = Vector2.one;
        auraRect.sizeDelta = new Vector2(120f, 120f);
        clockAura = auraObj.AddComponent<Image>();
        clockAura.sprite = GenerateCircleSprite(256, 128);
        clockAura.color = new Color(0.2f, 0.7f, 1f, 0.35f);
        clockAura.raycastTarget = false;

        // Clock Face Dial
        GameObject dialObj = new GameObject("ClockDial");
        dialObj.transform.SetParent(clockPanel, false);
        RectTransform dialRect = dialObj.AddComponent<RectTransform>();
        dialRect.anchorMin = Vector2.zero;
        dialRect.anchorMax = Vector2.one;
        dialRect.sizeDelta = Vector2.zero;
        Image dialImg = dialObj.AddComponent<Image>();
        dialImg.sprite = GenerateClockFaceSprite();
        dialImg.color = Color.white;
        dialImg.raycastTarget = false;

        // Hour Hand
        GameObject hourObj = new GameObject("HourHand");
        hourObj.transform.SetParent(clockPanel, false);
        hourHand = hourObj.AddComponent<RectTransform>();
        hourHand.anchorMin = new Vector2(0.5f, 0.5f);
        hourHand.anchorMax = new Vector2(0.5f, 0.5f);
        hourHand.pivot = new Vector2(0.5f, 0.15f);
        hourHand.sizeDelta = new Vector2(26f, 130f);
        Image hourImg = hourObj.AddComponent<Image>();
        hourImg.sprite = GenerateHandSprite(26, 130, new Color(0.88f, 0.75f, 0.35f, 1f));
        hourImg.raycastTarget = false;

        // Minute Hand
        GameObject minObj = new GameObject("MinuteHand");
        minObj.transform.SetParent(clockPanel, false);
        minuteHand = minObj.AddComponent<RectTransform>();
        minuteHand.anchorMin = new Vector2(0.5f, 0.5f);
        minuteHand.anchorMax = new Vector2(0.5f, 0.5f);
        minuteHand.pivot = new Vector2(0.5f, 0.12f);
        minuteHand.sizeDelta = new Vector2(20f, 185f);
        Image minImg = minObj.AddComponent<Image>();
        minImg.sprite = GenerateHandSprite(20, 185, new Color(0.95f, 0.88f, 0.65f, 1f));
        minImg.raycastTarget = false;

        // Second Hand
        GameObject secObj = new GameObject("SecondHand");
        secObj.transform.SetParent(clockPanel, false);
        secondHand = secObj.AddComponent<RectTransform>();
        secondHand.anchorMin = new Vector2(0.5f, 0.5f);
        secondHand.anchorMax = new Vector2(0.5f, 0.5f);
        secondHand.pivot = new Vector2(0.5f, 0.2f);
        secondHand.sizeDelta = new Vector2(8f, 215f);
        Image secImg = secObj.AddComponent<Image>();
        secImg.sprite = GenerateHandSprite(8, 215, new Color(1f, 0.35f, 0.25f, 1f));
        secImg.raycastTarget = false;

        // Center Brass Pin
        GameObject pinObj = new GameObject("CenterPin");
        pinObj.transform.SetParent(clockPanel, false);
        RectTransform pinRect = pinObj.AddComponent<RectTransform>();
        pinRect.anchorMin = new Vector2(0.5f, 0.5f);
        pinRect.anchorMax = new Vector2(0.5f, 0.5f);
        pinRect.sizeDelta = new Vector2(36f, 36f);
        Image pinImg = pinObj.AddComponent<Image>();
        pinImg.sprite = GenerateCircleSprite(64, 30);
        pinImg.color = new Color(0.95f, 0.82f, 0.45f, 1f);
        pinImg.raycastTarget = false;

        // Text: Header ("REWINDING TIMELINE...")
        GameObject headerObj = new GameObject("HeaderText");
        headerObj.transform.SetParent(clockRoot.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 0.86f);
        headerRect.anchorMax = new Vector2(0.5f, 0.86f);
        headerRect.sizeDelta = new Vector2(800f, 60f);
        headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = "REWINDING TIME";
        headerText.fontSize = 42;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = new Color(0.35f, 0.85f, 1f, 1f);

        // Text: Loops Remaining Counter
        GameObject loopsObj = new GameObject("RemainingLoopsText");
        loopsObj.transform.SetParent(clockRoot.transform, false);
        RectTransform loopsRect = loopsObj.AddComponent<RectTransform>();
        loopsRect.anchorMin = new Vector2(0.5f, 0.80f);
        loopsRect.anchorMax = new Vector2(0.5f, 0.80f);
        loopsRect.sizeDelta = new Vector2(800f, 40f);
        remainingLoopsText = loopsObj.AddComponent<TextMeshProUGUI>();
        remainingLoopsText.text = "TIME LOOPS REMAINING: 3 / 3";
        remainingLoopsText.fontSize = 26;
        remainingLoopsText.fontStyle = FontStyles.Bold;
        remainingLoopsText.alignment = TextAlignmentOptions.Center;
        remainingLoopsText.color = new Color(0.4f, 0.9f, 1f, 1f);

        // Text: Digital Rolling Counter
        GameObject digObj = new GameObject("DigitalClockText");
        digObj.transform.SetParent(clockRoot.transform, false);
        RectTransform digRect = digObj.AddComponent<RectTransform>();
        digRect.anchorMin = new Vector2(0.5f, 0.26f);
        digRect.anchorMax = new Vector2(0.5f, 0.26f);
        digRect.sizeDelta = new Vector2(500f, 60f);
        digitalClockText = digObj.AddComponent<TextMeshProUGUI>();
        digitalClockText.text = "00:00";
        digitalClockText.fontSize = 48;
        digitalClockText.fontStyle = FontStyles.Bold;
        digitalClockText.alignment = TextAlignmentOptions.Center;
        digitalClockText.color = new Color(1f, 0.9f, 0.5f, 1f);

        // Text: +5s Bonus Badge
        GameObject bonusObj = new GameObject("BonusBadgeText");
        bonusObj.transform.SetParent(clockRoot.transform, false);
        RectTransform bonusRect = bonusObj.AddComponent<RectTransform>();
        bonusRect.anchorMin = new Vector2(0.5f, 0.18f);
        bonusRect.anchorMax = new Vector2(0.5f, 0.18f);
        bonusRect.sizeDelta = new Vector2(600f, 50f);
        bonusBadgeText = bonusObj.AddComponent<TextMeshProUGUI>();
        bonusBadgeText.text = "+5 SECONDS RESTORED";
        bonusBadgeText.fontSize = 32;
        bonusBadgeText.fontStyle = FontStyles.Bold;
        bonusBadgeText.alignment = TextAlignmentOptions.Center;
        bonusBadgeText.color = new Color(0.4f, 1f, 0.5f, 1f);

        // Text: Reason / Educational Tip
        GameObject reasonObj = new GameObject("ReasonText");
        reasonObj.transform.SetParent(clockRoot.transform, false);
        RectTransform reasonRect = reasonObj.AddComponent<RectTransform>();
        reasonRect.anchorMin = new Vector2(0.5f, 0.10f);
        reasonRect.anchorMax = new Vector2(0.5f, 0.10f);
        reasonRect.sizeDelta = new Vector2(1000f, 70f);
        reasonText = reasonObj.AddComponent<TextMeshProUGUI>();
        reasonText.text = "";
        reasonText.fontSize = 24;
        reasonText.alignment = TextAlignmentOptions.Center;
        reasonText.color = new Color(0.9f, 0.92f, 0.95f, 0.85f);

        clockRoot.SetActive(false);
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
                float u = (x / (float)(width - 1)) * 2f - 1f; // -1 .. 0 .. 1
                // Gentle natural eyelid curve (dips 30px in the middle)
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
                // Mirrored: curves upward in the middle
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
                    // Dark midnight dial background
                    float dialNorm = d / radius;
                    Color dialBg = Color.Lerp(new Color(0.08f, 0.12f, 0.18f, 1f), new Color(0.03f, 0.04f, 0.07f, 1f), dialNorm);

                    // Outer brass/golden bezel
                    if (d >= radius - 18f)
                    {
                        dialBg = new Color(0.85f, 0.70f, 0.28f, 1f); // Golden rim
                    }
                    else if (d >= radius - 24f && d <= radius - 20f)
                    {
                        dialBg = new Color(0.35f, 0.28f, 0.10f, 1f); // Inner groove
                    }
                    else if (Mathf.Abs(d - 145f) < 2f)
                    {
                        dialBg = new Color(0.3f, 0.6f, 0.85f, 0.6f); // Inner decorative track
                    }

                    c = dialBg;
                }

                // Anti-aliased outer circular border
                if (d > radius - 2f && d <= radius + 2f)
                {
                    c.a = Mathf.Clamp01(1f - (d - (radius - 2f)) / 4f);
                }

                pixels[y * size + x] = c;
            }
        }

        // Draw 12 Hour Ticks
        for (int h = 0; h < 12; h++)
        {
            float angle = h * (360f / 12f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            bool isMajor = (h % 3 == 0); // 12, 3, 6, 9
            float startR = isMajor ? 185f : 198f;
            float endR = 216f;
            int thickness = isMajor ? 5 : 3;

            for (float r = startR; r <= endR; r += 0.5f)
            {
                Vector2 pos = center + dir * r;
                for (int ox = -thickness; ox <= thickness; ox++)
                {
                    for (int oy = -thickness; oy <= thickness; oy++)
                    {
                        int px = Mathf.RoundToInt(pos.x + ox);
                        int py = Mathf.RoundToInt(pos.y + oy);
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            pixels[py * size + px] = isMajor ? new Color(1f, 0.88f, 0.4f, 1f) : new Color(0.8f, 0.75f, 0.6f, 0.9f);
                        }
                    }
                }
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
            // Tapered hand shape towards tip
            float maxW = (width * 0.5f) * Mathf.Lerp(1.2f, 0.3f, normY);

            for (int x = 0; x < width; x++)
            {
                float distFromCenter = Mathf.Abs(x - (width * 0.5f));
                float alpha = distFromCenter <= maxW ? 1f : 0f;
                // Add center bevel line highlight
                Color pixelColor = color;
                if (distFromCenter < 1.5f && alpha > 0.5f)
                {
                    pixelColor = Color.Lerp(color, Color.white, 0.35f);
                }
                pixels[y * width + x] = new Color(pixelColor.r, pixelColor.g, pixelColor.b, alpha);
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
            // Reverse envelope: soft rise, sharp snap at the end
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
            // Frequency sweeps upward indicating reverse time flow
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
            // Harmonic bell chime (C5 + G5 + C6)
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

    /// <summary>
    /// Executes the full time-rewind sequence requested by the player:
    /// 1. Eye closing effect on screen
    /// 2. Eye opens to reveal clock spinning backwards in time with audio
    /// 3. Eye closes again
    /// 4. Game state is restored + 5 seconds added
    /// 5. Eye opens back to live game as if the fatal action never happened.
    /// </summary>
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

        // Ensure UI is enabled
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
            // Ease in: eyelid accelerates as it shuts
            float openAmount = 1f - Mathf.SmoothStep(0f, 1f, t);
            SetEyeOpenAmount(openAmount);
            yield return null;
        }
        SetEyeOpenAmount(0f); // Fully closed/black

        yield return new WaitForSecondsRealtime(0.35f);

        // -------------------------------------------------------------
        // PHASE 2: Setup Clock & Eye Opens onto the Rewind Clock (0.5s)
        // -------------------------------------------------------------
        if (clockRoot != null)
        {
            clockRoot.SetActive(true);
            if (headerText != null) headerText.text = "REWINDING TIME";
            if (remainingLoopsText != null) remainingLoopsText.text = $"<color=#5CE1E6>TIME LOOPS REMAINING: {remainingLoops} / {maxLoops}</color>";
            if (reasonText != null) reasonText.text = reason;
            if (bonusBadgeText != null) bonusBadgeText.gameObject.SetActive(false);
            if (digitalClockText != null) digitalClockText.text = "00:00";
            if (hourHand != null) hourHand.localEulerAngles = new Vector3(0, 0, -30f);
            if (minuteHand != null) minuteHand.localEulerAngles = new Vector3(0, 0, 0f);
            if (secondHand != null) secondHand.localEulerAngles = new Vector3(0, 0, 0f);
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
        // PHASE 3: Clock Spinning Backwards with Audio & Animation (2.2s)
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

            // Decelerate near the end (ease-out curve)
            float speedMult = progress > 0.75f ? Mathf.Lerp(1f, 0.05f, (progress - 0.75f) / 0.25f) : 1f;

            // Spin hands COUNTER-CLOCKWISE (Z rotation increases in Unity 2D)
            float secSpin = 720f * speedMult * dt;
            float minSpin = 360f * speedMult * dt;
            float hourSpin = 60f * speedMult * dt;

            if (secondHand != null) secondHand.Rotate(0f, 0f, secSpin);
            if (minuteHand != null) minuteHand.Rotate(0f, 0f, minSpin);
            if (hourHand != null) hourHand.Rotate(0f, 0f, hourSpin);

            // Clock pulse / time vortex breath
            if (clockPanel != null)
            {
                float pulse = 1f + Mathf.Sin(spinElapsed * 14f) * 0.035f;
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
            currentDisplaySeconds += dt * 15f;
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
            bonusBadgeText.text = $"<color=#FFD700>+{addedSeconds:0} SECONDS RESTORED</color>\n<size=55%><color=#D0D0D0>[Space / Click to Continue]</color></size>";
            bonusBadgeText.transform.localScale = Vector3.one * 1.35f;
        }

        // Hold clock & educational reason for comfortable reading before returning to live game
        float holdTimer = 0f;
        while (holdTimer < educationalReasonHoldDuration)
        {
            holdTimer += Time.unscaledDeltaTime;
            if (bonusBadgeText != null)
            {
                bonusBadgeText.transform.localScale = Vector3.Lerp(bonusBadgeText.transform.localScale, Vector3.one, Time.unscaledDeltaTime * 6f);
            }

            // Allow advancing if the player finished reading early (after at least 0.75s to avoid accidental clicks)
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
        // PHASE 5: Restore Game State Behind Closed Eyes (+5s & Undo Action)
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

        yield return null; // Wait 1 frame for physics, scene, and positions to settle

        // -------------------------------------------------------------
        // PHASE 6: Eye Opens Back to the Live Game (0.7s)
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
    /// Starts with eyes closed in darkness, opens onto the clock spinning backward in time,
    /// displays the mission objective, closes eyes, and opens onto the live room.
    /// </summary>
    public void PlayIntroRewind(string introMessage, Action onComplete)
    {
        if (isRewinding) return;
        StartCoroutine(IntroRewindRoutine(introMessage, onComplete));
    }

    private IEnumerator IntroRewindRoutine(string message, Action onComplete)
    {
        isRewinding = true;

        // Ensure UI is initialized and screen starts pitch black behind closed eyelids
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
            if (headerText != null) headerText.text = "REWINDING TIME";
            if (remainingLoopsText != null) remainingLoopsText.text = "<color=#5CE1E6>TIME LOOPS REMAINING: 3 / 3</color>";
            if (reasonText != null) reasonText.text = message;
            if (bonusBadgeText != null) bonusBadgeText.gameObject.SetActive(false);
            if (digitalClockText != null) digitalClockText.text = "00:00";
            if (hourHand != null) hourHand.localEulerAngles = new Vector3(0, 0, -30f);
            if (minuteHand != null) minuteHand.localEulerAngles = new Vector3(0, 0, 0f);
            if (secondHand != null) secondHand.localEulerAngles = new Vector3(0, 0, 0f);
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
        SetEyeOpenAmount(1f); // Fully revealed

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

            float secSpin = 720f * speedMult * dt;
            float minSpin = 360f * speedMult * dt;
            float hourSpin = 60f * speedMult * dt;

            if (secondHand != null) secondHand.Rotate(0f, 0f, secSpin);
            if (minuteHand != null) minuteHand.Rotate(0f, 0f, minSpin);
            if (hourHand != null) hourHand.Rotate(0f, 0f, hourSpin);

            if (clockPanel != null)
            {
                float pulse = 1f + Mathf.Sin(spinElapsed * 14f) * 0.035f;
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

            currentDisplaySeconds += dt * 15f;
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
            bonusBadgeText.text = "<color=#FFD700>60 SECONDS UNTIL THE EARTHQUAKE</color>\n<size=55%><color=#D0D0D0>[Space / Click to Start]</color></size>";
            bonusBadgeText.transform.localScale = Vector3.one * 1.35f;
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
        // PHASE 4: Eye Opens Back to the Live Game (0.7s)
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

        // -------------------------------------------------------------
        // PHASE 5: Start Game!
        // -------------------------------------------------------------
        onComplete?.Invoke();
    }

    /// <summary>
    /// Executes the Game Over sequence when all 3 time loop rewinds have been exhausted.
    /// Displays timeline collapsed with cracked/frozen clock, educational reason,
    /// and waits for player to restart run.
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

        yield return new WaitForSecondsRealtime(0.4f);

        // PHASE 2: Eye opens onto frozen / collapsed clock with red warning
        if (clockRoot != null)
        {
            clockRoot.SetActive(true);
            if (headerText != null) headerText.text = "<color=#FF3333>TIMELINE COLLAPSED</color>";
            if (remainingLoopsText != null) remainingLoopsText.text = "<color=#FF6666>OUT OF TIME LOOPS (0/3)</color>";
            if (reasonText != null) reasonText.text = reason;
            if (digitalClockText != null) digitalClockText.text = "<color=#FF4444>00:00</color>";
            if (bonusBadgeText != null)
            {
                bonusBadgeText.gameObject.SetActive(true);
                bonusBadgeText.text = "<color=#FFD700>PRESS [R] OR CLICK TO RESTART RUN</color>";
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

        // Reset game to checkpoint
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
            // At amount = 1 (open): topY = 650 (pulled completely above screen)
            // At amount = 0 (closed): topY = -150 (descends well past the center line!)
            float topY = Mathf.Lerp(-150f, 650f, amount);
            topEyelid.anchoredPosition = new Vector2(0f, topY);
        }

        if (bottomEyelid != null)
        {
            // At amount = 1 (open): bottomY = -650 (pulled completely below screen)
            // At amount = 0 (closed): bottomY = +150 (ascends well past the center line!)
            float bottomY = Mathf.Lerp(150f, -650f, amount);
            bottomEyelid.anchoredPosition = new Vector2(0f, bottomY);
        }

        if (fullBlackOverlay != null)
        {
            // Fades into 100% solid pitch black; at <= 0.05 it is guaranteed pure solid black (#000000)
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
            educationalReason: "TEST REWIND: Electric spark caused gas ignition! Time rewound by 5 seconds.",
            addedSeconds: 5f,
            onRewindStateRestore: () => Debug.Log("<color=cyan>[TimeRewind] Restored state with +5s!</color>"),
            onComplete: () => Debug.Log("<color=green>[TimeRewind] Sequence finished!</color>")
        );
    }

    #endregion
}
