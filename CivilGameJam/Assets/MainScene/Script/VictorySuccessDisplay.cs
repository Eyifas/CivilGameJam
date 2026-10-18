using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Controls the final cutscene pipeline:
/// 1. Plays the comic outro (Comic Panel 4: Escaped to the street, tsunami cliffhanger) in pure fullscreen without lighting/shader artifacts.
/// 2. Smoothly transitions into the hand-drawn Mission Success screen featuring 'hole bacround' and the game's sketch UI buttons.
/// </summary>
public class VictorySuccessDisplay : MonoBehaviour
{
    public static VictorySuccessDisplay Instance { get; private set; }

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Image backgroundImage;
    private Image darkWashOverlay;
    private GameObject outroContainer;
    private Image comicPanelImage;
    private TextMeshProUGUI outroSubtitlesText;
    private GameObject successCardObj;

    private Action onRestartAction;
    private Action onMainMenuAction;
    private bool isTransitioning = false;
    private bool isSuccessScreenActive = false;

    private static Sprite s_whitePixel;

    public static VictorySuccessDisplay PlayOutroThenSuccess(Action onRestart, Action onMainMenu)
    {
        if (Instance != null)
        {
            Destroy(Instance.gameObject);
        }

        GameObject host = new GameObject("VictorySuccessHost");
        var display = host.AddComponent<VictorySuccessDisplay>();
        display.onRestartAction = onRestart;
        display.onMainMenuAction = onMainMenu;
        display.StartCoroutine(display.OutroAndSuccessPipelineRoutine());
        return display;
    }

    private void Awake()
    {
        Instance = this;
    }

    private IEnumerator OutroAndSuccessPipelineRoutine()
    {
        BuildOverlayCanvas();

        // -------------------------------------------------------------
        // PHASE 1: OUTRO COMIC PANEL (FULLSCREEN, 100% RELIABLE)
        // -------------------------------------------------------------
        Debug.Log("<color=green>[OUTRO] Loading and displaying Comic Panel 4 Outro...</color>");

        // Black backdrop for comic letterboxing
        backgroundImage.color = Color.black;
        darkWashOverlay.color = Color.clear;

        // Load Comic Panel 4 Sprite
        Sprite comicSprite = LoadProjectSprite(
            resourcePath: "Hazards/comic_panel_4_tsunami_ending",
            relativeFilePaths: new string[]
            {
                "Resources/Hazards/comic_panel_4_tsunami_ending.jpg",
                "MainScene/Sprite/Hazards/comic_panel_4_tsunami_ending.jpg",
                "Resources/Hazards/comic-panel_4.jpeg",
                "MainScene/Sprite/Hazards/comic-panel_4.jpeg"
            }
        );

        if (comicSprite != null)
        {
            comicPanelImage.sprite = comicSprite;
            comicPanelImage.color = Color.white;
            Debug.Log($"<color=cyan>[OUTRO] Successfully loaded comic panel sprite ({comicSprite.rect.width}x{comicSprite.rect.height}).</color>");
        }
        else
        {
            Debug.LogWarning("[OUTRO] Could not load comic panel sprite; using fallback tint.");
            comicPanelImage.color = new Color(0.15f, 0.2f, 0.35f, 1f);
        }

        outroSubtitlesText.text =
            "<color=#B0BEC5><size=20><i>You escaped the damaged building and evacuated into the street...</i></size></color>\n" +
            "<color=#FFFFFF><size=26><b>\"Ah... I can finally relax. I made it.\"</b></size></color>   " +
            "<color=#00FFAA><size=18><b>[ TO BE CONTINUED ]</b></size></color>";

        AudioManager.PlayBGM(BgmTrack.VictoryOutro, 0.4f);

        // Fade in outro
        float fadeElapsed = 0f;
        while (fadeElapsed < 0.4f)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(fadeElapsed / 0.4f);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Hold outro for ~4.5 seconds (skippable after 0.8s on Space/E/Click)
        float outroTimer = 0f;
        float maxOutroHold = 4.5f;
        while (outroTimer < maxOutroHold)
        {
            outroTimer += Time.unscaledDeltaTime;
            if (outroTimer > 0.8f)
            {
                bool skip = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)) ||
                            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
                if (skip) break;
            }
            yield return null;
        }

        // -------------------------------------------------------------
        // PHASE 2: TRANSITION TO HAND-DRAWN SUCCESS SCREEN
        // -------------------------------------------------------------
        Debug.Log("<color=green>[VICTORY] Transitioning from Outro to Hand-Drawn Success Screen (hole bacround)...</color>");

        // Fade out outro comic container
        float dimElapsed = 0f;
        while (dimElapsed < 0.45f)
        {
            dimElapsed += Time.unscaledDeltaTime;
            float t = dimElapsed / 0.45f;
            comicPanelImage.color = Color.Lerp(Color.white, new Color(1f, 1f, 1f, 0f), t);
            outroSubtitlesText.color = Color.Lerp(Color.white, new Color(1f, 1f, 1f, 0f), t);
            yield return null;
        }
        outroContainer.SetActive(false);

        // Load hole bacround sprite for the hand-drawn backdrop
        Sprite holeBgSprite = LoadProjectSprite(
            resourcePath: "hole bacround",
            relativeFilePaths: new string[]
            {
                "MainScene/Sprite/hole bacround.png",
                "Resources/hole bacround.png"
            }
        );

        if (holeBgSprite != null)
        {
            backgroundImage.sprite = holeBgSprite;
            backgroundImage.color = Color.white;
            Debug.Log("<color=cyan>[VICTORY] Successfully set hole bacround for Success Screen.</color>");
        }
        else
        {
            backgroundImage.color = new Color(0.12f, 0.14f, 0.18f, 1f);
        }

        // Soft dark ink wash over hole bacround so text is crisp and readable
        darkWashOverlay.color = new Color(0.04f, 0.06f, 0.10f, 0.72f);

        // Build and display the Hand-Drawn Success Card
        BuildHandDrawnSuccessCard();
        successCardObj.SetActive(true);
        isSuccessScreenActive = true;

        PlayVictorySound();
        AudioManager.PlayVictoryFanfare();
        AudioManager.PlayBGM(BgmTrack.VictoryLoop, 0.8f);

        // Fade in success card
        var cardCg = successCardObj.GetComponent<CanvasGroup>();
        if (cardCg != null)
        {
            float cardFade = 0f;
            while (cardFade < 0.4f)
            {
                cardFade += Time.unscaledDeltaTime;
                cardCg.alpha = Mathf.Clamp01(cardFade / 0.4f);
                yield return null;
            }
            cardCg.alpha = 1f;
        }
    }

    private void BuildOverlayCanvas()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // Top of everything, 100% unlit & unaffected by lighting

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // 1. Fullscreen Background Image (Black during Outro, hole bacround during Success)
        GameObject bgObj = new GameObject("FullscreenBackground");
        bgObj.transform.SetParent(transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        backgroundImage = bgObj.AddComponent<Image>();
        backgroundImage.sprite = GetWhitePixelSprite();
        backgroundImage.color = Color.black;

        // 2. Dark Wash Overlay (Soft vignette over hole bacround for maximum text legibility)
        GameObject washObj = new GameObject("DarkWashOverlay");
        washObj.transform.SetParent(transform, false);
        var washRect = washObj.AddComponent<RectTransform>();
        washRect.anchorMin = Vector2.zero;
        washRect.anchorMax = Vector2.one;
        washRect.sizeDelta = Vector2.zero;
        darkWashOverlay = washObj.AddComponent<Image>();
        darkWashOverlay.sprite = GetWhitePixelSprite();
        darkWashOverlay.color = Color.clear;

        // 3. Outro Cutscene Container
        outroContainer = new GameObject("OutroContainer");
        outroContainer.transform.SetParent(transform, false);
        var ocRect = outroContainer.AddComponent<RectTransform>();
        ocRect.anchorMin = Vector2.zero;
        ocRect.anchorMax = Vector2.one;
        ocRect.sizeDelta = Vector2.zero;

        // Comic Panel Image
        GameObject cpObj = new GameObject("ComicPanelImage");
        cpObj.transform.SetParent(outroContainer.transform, false);
        var cpRect = cpObj.AddComponent<RectTransform>();
        cpRect.anchorMin = new Vector2(0.5f, 0.5f);
        cpRect.anchorMax = new Vector2(0.5f, 0.5f);
        cpRect.sizeDelta = new Vector2(1600f, 880f);
        comicPanelImage = cpObj.AddComponent<Image>();
        comicPanelImage.preserveAspect = true;

        // Subtitles
        GameObject subObj = new GameObject("OutroSubtitles");
        subObj.transform.SetParent(outroContainer.transform, false);
        var subRect = subObj.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0f);
        subRect.anchorMax = new Vector2(0.5f, 0f);
        subRect.pivot = new Vector2(0.5f, 0f);
        subRect.anchoredPosition = new Vector2(0f, 32f);
        subRect.sizeDelta = new Vector2(1600f, 100f);

        outroSubtitlesText = subObj.AddComponent<TextMeshProUGUI>();
        outroSubtitlesText.alignment = TextAlignmentOptions.Center;
    }

    private void BuildHandDrawnSuccessCard()
    {
        successCardObj = new GameObject("HandDrawnSuccessCard");
        successCardObj.transform.SetParent(transform, false);
        var cardRect = successCardObj.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(1040f, 780f);
        cardRect.anchoredPosition = Vector2.zero;

        var cardCg = successCardObj.AddComponent<CanvasGroup>();
        cardCg.alpha = 0f;

        // Card Background Panel (Dark Sketch Paper Tone)
        var cardImg = successCardObj.AddComponent<Image>();
        cardImg.sprite = GetWhitePixelSprite();
        cardImg.color = new Color(0.07f, 0.09f, 0.14f, 0.94f);

        // Card Sketch Border Outline
        var outline = successCardObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.75f, 0.44f, 0.85f);
        outline.effectDistance = new Vector2(3f, -3f);

        // Header: Stars & Title
        GameObject headObj = new GameObject("HeaderSection");
        headObj.transform.SetParent(successCardObj.transform, false);
        var headRect = headObj.AddComponent<RectTransform>();
        headRect.anchorMin = new Vector2(0.5f, 1f);
        headRect.anchorMax = new Vector2(0.5f, 1f);
        headRect.pivot = new Vector2(0.5f, 1f);
        headRect.anchoredPosition = new Vector2(0f, -22f);
        headRect.sizeDelta = new Vector2(980f, 120f);

        var titleText = headObj.AddComponent<TextMeshProUGUI>();
        titleText.text =
            "<color=#FFDD00><size=26>★ ★ ★ ★ ★</size></color>\n" +
            "<color=#FFDD00><size=44><b>MISSION SUCCESSFUL!</b></size></color>\n" +
            "<color=#00FFAA><size=22><b>YOU SURVIVED THE EARTHQUAKE & EVACUATED!</b></size></color>";
        titleText.alignment = TextAlignmentOptions.Center;

        // Checklist Panel (Paper Sketch Container)
        GameObject checkPanel = new GameObject("ChecklistPanel");
        checkPanel.transform.SetParent(successCardObj.transform, false);
        var cpRect = checkPanel.AddComponent<RectTransform>();
        cpRect.anchorMin = new Vector2(0.5f, 1f);
        cpRect.anchorMax = new Vector2(0.5f, 1f);
        cpRect.pivot = new Vector2(0.5f, 1f);
        cpRect.anchoredPosition = new Vector2(0f, -145f);
        cpRect.sizeDelta = new Vector2(940f, 320f);

        var checkBg = checkPanel.AddComponent<Image>();
        checkBg.sprite = GetWhitePixelSprite();
        checkBg.color = new Color(0.04f, 0.06f, 0.09f, 0.92f);

        var checkOutline = checkPanel.AddComponent<Outline>();
        checkOutline.effectColor = new Color(0.15f, 0.35f, 0.25f, 0.7f);
        checkOutline.effectDistance = new Vector2(2f, -2f);

        GameObject checkTextObj = new GameObject("ChecklistContent");
        checkTextObj.transform.SetParent(checkPanel.transform, false);
        var ctRect = checkTextObj.AddComponent<RectTransform>();
        ctRect.anchorMin = Vector2.zero;
        ctRect.anchorMax = Vector2.one;
        ctRect.offsetMin = new Vector2(28f, 14f);
        ctRect.offsetMax = new Vector2(-28f, -14f);

        var checkText = checkTextObj.AddComponent<TextMeshProUGUI>();
        checkText.fontSize = 18f;
        checkText.color = Color.white;
        checkText.lineSpacing = 7f;
        checkText.text =
            "<color=#00FFAA><size=19><b>DISASTER SURVIVAL PROTOCOLS COMPLETED:</b></size></color>\n\n" +
            "<color=#2ECC71><b>[✔] Furniture Anchored:</b></color> Heavy bookshelf and picture frames secured to walls\n" +
            "<color=#2ECC71><b>[✔] Fire Hazards Eliminated:</b></color> Open flames & candles extinguished before tremor\n" +
            "<color=#2ECC71><b>[✔] Emergency Supplies Retrieved:</b></color> Packed flashlight and first aid kit collected\n" +
            "<color=#2ECC71><b>[✔] Drop, Cover & Hold On:</b></color> Protected vital organs under sturdy table during shaking\n" +
            "<color=#2ECC71><b>[✔] Gas Pipeline Secured:</b></color> Kitchen valve shut off safely, preventing explosion\n" +
            "<color=#2ECC71><b>[✔] Exit Corridor Cleared:</b></color> Heavy fallen dolap lifted and safely evacuated into street";

        // Rating & Advice
        GameObject ratingObj = new GameObject("RatingSection");
        ratingObj.transform.SetParent(successCardObj.transform, false);
        var rateRect = ratingObj.AddComponent<RectTransform>();
        rateRect.anchorMin = new Vector2(0.5f, 1f);
        rateRect.anchorMax = new Vector2(0.5f, 1f);
        rateRect.pivot = new Vector2(0.5f, 1f);
        rateRect.anchoredPosition = new Vector2(0f, -485f);
        rateRect.sizeDelta = new Vector2(940f, 110f);

        var rateText = ratingObj.AddComponent<TextMeshProUGUI>();
        rateText.alignment = TextAlignmentOptions.Center;
        rateText.text =
            "<color=#F1C40F><size=24><b>SURVIVAL RATING: S-RANK (100% PREPARED)</b></size></color>\n" +
            "<color=#B0BEC5><size=16><i>\"Earthquakes trigger dangerous aftershocks, collapses, and coastal tsunamis.\n" +
            "Once utilities are secured, always evacuate damaged structures immediately on foot to open high ground!\"</i></size></color>";

        // Button Container
        GameObject btnContainer = new GameObject("ButtonContainer");
        btnContainer.transform.SetParent(successCardObj.transform, false);
        var bcRect = btnContainer.AddComponent<RectTransform>();
        bcRect.anchorMin = new Vector2(0.5f, 0f);
        bcRect.anchorMax = new Vector2(0.5f, 0f);
        bcRect.pivot = new Vector2(0.5f, 0f);
        bcRect.anchoredPosition = new Vector2(0f, 32f);
        bcRect.sizeDelta = new Vector2(940f, 85f);

        var hlg = btnContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 50f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // Try to load actual hand-drawn UI button sprites (new game_0 & Quit_0)
        Sprite newGameSprite = LoadProjectSprite("UI/new game", new string[] { "MainScene/Sprite/UI/new game.png" });
        Sprite quitSprite = LoadProjectSprite("UI/Quit", new string[] { "MainScene/Sprite/UI/Quit.png" });

        // Button 1: Play Again
        CreateHandDrawnButton(
            parent: btnContainer.transform,
            label: "PLAY AGAIN [SPACE]",
            buttonSprite: newGameSprite,
            fallbackColor: new Color(0.14f, 0.62f, 0.36f, 1f),
            onClick: TriggerPlayAgain
        );

        // Button 2: Main Menu
        CreateHandDrawnButton(
            parent: btnContainer.transform,
            label: "MAIN MENU [ESC]",
            buttonSprite: quitSprite,
            fallbackColor: new Color(0.24f, 0.32f, 0.44f, 1f),
            onClick: TriggerMainMenu
        );
    }

    private GameObject CreateHandDrawnButton(Transform parent, string label, Sprite buttonSprite, Color fallbackColor, Action onClick)
    {
        GameObject btnObj = new GameObject("Btn_" + label);
        btnObj.transform.SetParent(parent, false);
        var rt = btnObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(340f, 72f);

        var img = btnObj.AddComponent<Image>();
        if (buttonSprite != null)
        {
            img.sprite = buttonSprite;
            img.color = Color.white;
            img.preserveAspect = true;
        }
        else
        {
            img.sprite = GetWhitePixelSprite();
            img.color = fallbackColor;
        }

        var btn = btnObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 0.6f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = colors;

        btn.onClick.AddListener(() => 
        {
            AudioManager.PlayUIClick();
            onClick?.Invoke();
        });

        // Overlay text
        GameObject textObj = new GameObject("BtnText");
        textObj.transform.SetParent(btnObj.transform, false);
        var trt = textObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        var txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.text = $"<b>{label}</b>";
        txt.fontSize = 20f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;

        // Add soft outline on text for high contrast
        var txtOutline = textObj.AddComponent<Outline>();
        txtOutline.effectColor = Color.black;
        txtOutline.effectDistance = new Vector2(1.5f, -1.5f);

        return btnObj;
    }

    private void Update()
    {
        if (isTransitioning || !isSuccessScreenActive) return;

        // Keyboard navigation
        bool spaceOrE = Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame);
        if (spaceOrE)
        {
            TriggerPlayAgain();
            return;
        }

        bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (esc)
        {
            TriggerMainMenu();
            return;
        }
    }

    public void TriggerPlayAgain()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        Destroy(gameObject);

        if (onRestartAction != null)
        {
            onRestartAction.Invoke();
        }
        else if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.ResetToCheckpoint();
        }
        else
        {
            SceneManager.LoadScene("MainScene");
        }
    }

    public void TriggerMainMenu()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        StartMenuController.ClearDontDestroyOnLoadObjects(excludeObject: gameObject);
        Destroy(gameObject);

        if (onMainMenuAction != null)
        {
            onMainMenuAction.Invoke();
        }
        else
        {
            SceneManager.LoadScene("StartScene");
        }
    }

    private void PlayVictorySound()
    {
        var audioSrc = GetComponent<AudioSource>();
        if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;
        audioSrc.ignoreListenerPause = true;

        AudioClip fanfare = GenerateProceduralFanfare();
        if (fanfare != null)
        {
            audioSrc.PlayOneShot(fanfare, 0.85f);
        }
    }

    private AudioClip GenerateProceduralFanfare()
    {
        int sampleRate = 44100;
        float duration = 1.6f;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        float[] notes = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
        float noteDuration = 0.22f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            int noteIndex = Mathf.Min((int)(t / noteDuration), notes.Length - 1);
            float freq = notes[noteIndex];

            float noteT = t - (noteIndex * noteDuration);
            float decay = Mathf.Exp(-noteT * 3.5f);

            float s1 = Mathf.Sin(2f * Mathf.PI * freq * t);
            float s2 = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.35f;
            float s3 = Mathf.Sin(2f * Mathf.PI * freq * 0.5f * t) * 0.15f;

            samples[i] = (s1 + s2 + s3) * decay * 0.35f;
        }

        AudioClip clip = AudioClip.Create("VictoryFanfare", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Highly robust multi-tier sprite loader:
    /// 1. AssetDatabase in Unity Editor
    /// 2. Resources.Load
    /// 3. Resources.LoadAll
    /// 4. Direct byte loading from disk using Texture2D.LoadImage
    /// </summary>
    public static Sprite LoadProjectSprite(string resourcePath, string[] relativeFilePaths)
    {
#if UNITY_EDITOR
        // 1. Try AssetDatabase in Editor
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

        // 2. Try Resources.Load
        if (!string.IsNullOrEmpty(resourcePath))
        {
            var res = Resources.Load<Sprite>(resourcePath);
            if (res != null) return res;

            var allRes = Resources.LoadAll<Sprite>(resourcePath);
            if (allRes != null && allRes.Length > 0) return allRes[0];
        }

        // 3. Fallback: Direct disk file loading
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
                        Debug.LogWarning($"[VictorySuccessDisplay] Failed reading {fullPath}: {ex.Message}");
                    }
                }
            }
        }

        return null;
    }

    public static Sprite GetWhitePixelSprite()
    {
        if (s_whitePixel != null) return s_whitePixel;
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Color[] px = new Color[] { Color.white, Color.white, Color.white, Color.white };
        tex.SetPixels(px);
        tex.Apply();
        s_whitePixel = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
        return s_whitePixel;
    }
}
