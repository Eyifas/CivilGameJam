using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Controls the zoomed-in CupboardScene interactive event.
/// An earthquake aftershock strikes, causing the cabinet door to break its hinges
/// and fall directly towards the camera/player. The player must rapidly mash
/// [SPACE], [E], or click to physically push the door back and secure the latch.
/// Includes procedural audio synthesis, camera shake, visual struggle animation,
/// and full integration with GameTimelineManager.
/// </summary>
public class CupboardSceneController : MonoBehaviour
{
    public static CupboardSceneController Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private Transform doorTransform;
    [SerializeField] private Transform cupboardBodyTransform;
    [SerializeField] private Camera mainCamera;

    [Header("QTE Settings")]
    [SerializeField] private float qteDuration = 5.5f;
    [SerializeField] private float mashGainPerPress = 0.075f;
    [SerializeField] private float decayRatePerSecond = 0.20f;
    [SerializeField] private float initialProgress = 0.70f;

    [Header("Audio Settings (Optional Inspector Overrides)")]
    [SerializeField] private AudioClip customRumble;
    [SerializeField] private AudioClip customCreak;
    [SerializeField] private AudioClip customMashThump;
    [SerializeField] private AudioClip customSlam;
    [SerializeField] private AudioClip customLatch;
    [SerializeField] private AudioClip customCrash;

    // State
    private bool isBusy = false;
    public bool IsBusy => isBusy;
    private bool isQTEActive = false;
    private float currentProgress = 0f;
    private float timeRemaining = 0f;
    private float pushImpulse = 0f;

    // Visual offset from sprite transform position to visible center of door pixels (texture 256x256, door [21..128, 63..211])
    private readonly Vector2 visualCenterOffset = new Vector2(-0.535f, -0.10f);
    private Vector2 initialVisualCenter;
    private readonly Vector2 targetVisualCenter = new Vector2(0f, 0f); // Screen center (0, 0)
    private Vector3 doorMaxThreatScale;

    // Initial Transform baselines
    private Vector3 doorInitialPos;
    private Quaternion doorInitialRot;
    private Vector3 doorInitialScale;

    // Camera Shake
    private Vector3 cameraInitialPos;
    private float shakeIntensity = 0f;

    // Audio
    private AudioSource audioSource;
    private AudioClip synthRumble;
    private AudioClip synthCreak;
    private AudioClip synthMashThump;
    private AudioClip synthSlam;
    private AudioClip synthLatch;
    private AudioClip synthCrash;

    // Procedural UI
    private Canvas overlayCanvas;
    private GameObject qtePanel;
    private TMP_Text headerText;
    private TMP_Text promptText;
    private TMP_Text timerText;
    private Slider mashSlider;
    private Image sliderFillImage;
    private RectTransform keyBadgeRect;
    private TMP_Text resultBannerText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnRuntimeSceneLoaded()
    {
        EnsureCupboardControllerInScene();
        SceneManager.sceneLoaded += (scene, mode) => EnsureCupboardControllerInScene();
    }

    public static void EnsureCupboardControllerInScene()
    {
        if (SceneManager.GetActiveScene().name == "CupboardScene")
        {
            if (Instance == null)
            {
                var sm = GameObject.Find("SceneManager");
                if (sm != null)
                {
                    if (sm.GetComponent<CupboardSceneController>() == null)
                    {
                        sm.AddComponent<CupboardSceneController>();
                    }
                }
                else
                {
                    GameObject obj = new GameObject("CupboardSceneController");
                    obj.AddComponent<CupboardSceneController>();
                }
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

        FindSceneObjects();
        EnsureAudioSource();
        GenerateProceduralAudio();
        BuildProceduralUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void FindSceneObjects()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();
        }

        if (mainCamera != null)
        {
            cameraInitialPos = mainCamera.transform.localPosition;
        }

        // Auto-detect cabinet door ("dolapçık mutfak")
        if (doorTransform == null)
        {
            var doorObj = GameObject.Find("dolapçık mutfak");
            if (doorObj == null) doorObj = GameObject.Find("dolapcik mutfak");
            if (doorObj == null) doorObj = GameObject.Find("Door");
            if (doorObj != null) doorTransform = doorObj.transform;
        }

        // Auto-detect cupboard body ("dolap mutfak")
        if (cupboardBodyTransform == null)
        {
            var bodyObj = GameObject.Find("dolap mutfak");
            if (bodyObj != null) cupboardBodyTransform = bodyObj.transform;
        }

        if (doorTransform != null)
        {
            doorInitialPos = doorTransform.localPosition;
            doorInitialRot = doorTransform.localRotation;
            doorInitialScale = doorTransform.localScale;
            doorMaxThreatScale = doorInitialScale * 2.1f;

            // Visual center of the door when mounted on the cupboard
            initialVisualCenter = new Vector2(
                doorInitialPos.x + visualCenterOffset.x * doorInitialScale.x,
                doorInitialPos.y + visualCenterOffset.y * doorInitialScale.y
            );
        }
    }

    /// <summary>
    /// Calculates the Transform.localPosition required so the visual center of the door
    /// is at `desiredVisualCenter` when scaled at `scale`.
    /// </summary>
    private Vector3 GetTransformPosForVisualCenter(Vector2 desiredVisualCenter, Vector3 scale)
    {
        return new Vector3(
            desiredVisualCenter.x - visualCenterOffset.x * scale.x,
            desiredVisualCenter.y - visualCenterOffset.y * scale.y,
            0f
        );
    }

    /// <summary>
    /// Applies door position and scale based on fall factor (0 = secured on cabinet, 1 = centered in screen at threat scale)
    /// and tactile player push impulse.
    /// </summary>
    private void ApplyDoorState(float fallT, float impulse)
    {
        if (doorTransform == null) return;

        fallT = Mathf.Clamp01(fallT);

        // Ease-in perspective curve: starts gently and slowly, accelerates only as it nears screen
        float scaleT = Mathf.Pow(fallT, 1.7f);
        Vector3 currentScale = Vector3.Lerp(doorInitialScale, doorMaxThreatScale, scaleT);

        // Interpolate visual center from cupboard position directly towards screen center (0, 0)
        Vector2 visualCenter = Vector2.Lerp(initialVisualCenter, targetVisualCenter, fallT);

        // Tactile mash push impulse shoves the door back up-left towards cabinet and shrinks slightly
        if (impulse > 0.001f)
        {
            visualCenter = Vector2.Lerp(visualCenter, initialVisualCenter, impulse * 0.4f);
            currentScale -= Vector3.one * (impulse * 0.8f);
        }

        Vector3 targetPos = GetTransformPosForVisualCenter(visualCenter, currentScale);

        // Add upward push jolt
        targetPos += Vector3.up * (impulse * 0.8f);

        // Subtle struggle wobble when door is loose
        if (fallT > 0.05f)
        {
            float wobble = Mathf.Sin(Time.time * 18f) * (fallT * 0.035f);
            targetPos += new Vector3(wobble, -wobble * 0.5f, 0f);
        }

        doorTransform.localPosition = targetPos;
        doorTransform.localRotation = doorInitialRot;
        doorTransform.localScale = currentScale;
    }

    private void EnsureAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        // If cabinet was already secured, return immediately
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsCabinetSecured)
        {
            Debug.Log("[CupboardScene] Cabinet is already secured. Returning to MainScene...");
            SceneManager.LoadScene("MainScene");
            return;
        }

        StartCoroutine(SequenceRoutine());
    }

    private void Update()
    {
        // Apply Camera Shake
        if (mainCamera != null)
        {
            if (shakeIntensity > 0.001f)
            {
                mainCamera.transform.localPosition = cameraInitialPos + (Vector3)(UnityEngine.Random.insideUnitCircle * shakeIntensity);
                shakeIntensity = Mathf.Lerp(shakeIntensity, 0f, Time.deltaTime * 3.5f);
            }
            else
            {
                mainCamera.transform.localPosition = cameraInitialPos;
            }
        }

        if (!isQTEActive) return;

        // Countdown timer
        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            UpdateUI();
            OnQTEFailed();
            return;
        }

        // Decay against player struggle
        currentProgress -= decayRatePerSecond * Time.deltaTime;
        currentProgress = Mathf.Clamp(currentProgress, 0f, 1.05f);

        if (currentProgress <= 0.01f)
        {
            OnQTEFailed();
            return;
        }

        // Check player mash input
        bool mashTriggered = false;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.wKey.wasPressedThisFrame)
            {
                mashTriggered = true;
            }
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            mashTriggered = true;
        }

        if (mashTriggered)
        {
            currentProgress += mashGainPerPress;
            pushImpulse = Mathf.Min(pushImpulse + 0.18f, 0.45f);
            PlaySound(customMashThump, synthMashThump, 0.7f);
            TriggerCameraJolt(0.08f);

            // Punch key badge animation
            if (keyBadgeRect != null)
            {
                keyBadgeRect.localScale = Vector3.one * 1.3f;
            }

            if (currentProgress >= 1f)
            {
                OnQTESucceeded();
                return;
            }
        }

        // Return key badge scale smoothly
        if (keyBadgeRect != null)
        {
            keyBadgeRect.localScale = Vector3.Lerp(keyBadgeRect.localScale, Vector3.one, Time.deltaTime * 8f);
        }

        pushImpulse = Mathf.Lerp(pushImpulse, 0f, Time.deltaTime * 10f);

        // Update Door Transform dynamically based on struggle progress
        if (doorTransform != null)
        {
            float fallT = Mathf.Clamp01(1f - currentProgress);
            ApplyDoorState(fallT, pushImpulse);
        }

        // Extra camera rumble when falling close
        if (currentProgress < 0.35f)
        {
            shakeIntensity = Mathf.Max(shakeIntensity, 0.06f);
        }

        UpdateUI();
    }

    #region --- CINEMATIC SEQUENCE ---

    private IEnumerator SequenceRoutine()
    {
        isBusy = true;
        isQTEActive = false;
        if (qtePanel != null) qtePanel.SetActive(false);
        if (resultBannerText != null) resultBannerText.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.4f);

        // -----------------------------------------------------------------
        // PHASE 1: Tremor hits the kitchen & hinges creak
        // -----------------------------------------------------------------
        PlaySound(customRumble, synthRumble, 0.85f);
        PlaySound(customCreak, synthCreak, 0.8f);
        shakeIntensity = 0.22f;

        if (headerText != null)
        {
            headerText.text = "<color=#FFAA00>EARTHQUAKE AFTERSHOCK!</color>";
            if (qtePanel != null) qtePanel.SetActive(true);
            if (mashSlider != null) mashSlider.gameObject.SetActive(false);
            if (promptText != null) promptText.text = "Violent vibrations shake the upper cupboards!";
            if (timerText != null) timerText.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(0.85f);

        // -----------------------------------------------------------------
        // PHASE 2: Top hinge snaps & door tilts forward towards camera
        // -----------------------------------------------------------------
        PlaySound(customCreak, synthCreak, 1.0f);
        float tiltElapsed = 0f;
        float tiltDuration = 0.65f;

        while (tiltElapsed < tiltDuration)
        {
            tiltElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(tiltElapsed / tiltDuration);
            float curve = Mathf.SmoothStep(0f, 1f, t);

            // Loosen to initial threat position
            float fallInitial = Mathf.Lerp(0f, 1f - initialProgress, curve);
            ApplyDoorState(fallInitial, 0f);

            shakeIntensity = Mathf.Max(shakeIntensity, 0.12f);
            yield return null;
        }

        // -----------------------------------------------------------------
        // PHASE 3: QTE MASH INITIATED
        // -----------------------------------------------------------------
        currentProgress = initialProgress;
        timeRemaining = qteDuration;
        isQTEActive = true;

        if (headerText != null) headerText.text = "<color=#FF4444>THE CABINET DOOR IS FALLING!</color>";
        if (promptText != null) promptText.text = "RAPIDLY MASH [SPACE] OR [E] TO PUSH IT AWAY!";
        if (mashSlider != null) mashSlider.gameObject.SetActive(true);
        if (timerText != null) timerText.gameObject.SetActive(true);

        UpdateUI();
    }

    private void OnQTESucceeded()
    {
        isQTEActive = false;
        StartCoroutine(SuccessRoutine());
    }

    private IEnumerator SuccessRoutine()
    {
        // -----------------------------------------------------------------
        // SLAM SHUT ANIMATION
        // -----------------------------------------------------------------
        PlaySound(customSlam, synthSlam, 1.0f);
        PlaySound(customLatch, synthLatch, 0.9f);
        TriggerCameraJolt(0.28f);

        // Snap door back into secured wall frame
        if (doorTransform != null)
        {
            doorTransform.localPosition = doorInitialPos;
            doorTransform.localRotation = doorInitialRot;
            doorTransform.localScale = doorInitialScale;
        }

        // Visual feedback
        if (headerText != null) headerText.gameObject.SetActive(false);
        if (qtePanel != null) qtePanel.SetActive(false);
        if (resultBannerText != null)
        {
            resultBannerText.gameObject.SetActive(true);
            resultBannerText.text = "<color=#55FF88>CABINET SECURED SAFELY!</color>\n<size=24><color=#FFFFFF>Held off the loose overhead obstacle!</color></size>";
        }

        Debug.Log("<color=green>[CupboardScene] Successfully pushed back and secured cabinet door!</color>");

        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.HandleCabinetQTEResult(true);
        }

        yield return new WaitForSeconds(1.6f);

        // Return to MainScene
        isBusy = false;
        SceneManager.LoadScene("MainScene");
    }

    private void OnQTEFailed()
    {
        isQTEActive = false;
        StartCoroutine(FailureRoutine());
    }

    private IEnumerator FailureRoutine()
    {
        // -----------------------------------------------------------------
        // DOOR COLLAPSES ONTO CAMERA ANIMATION
        // -----------------------------------------------------------------
        PlaySound(customCrash, synthCrash, 1.0f);
        TriggerCameraJolt(0.45f);

        // Slam right into screen center
        if (doorTransform != null)
        {
            Vector3 crashScale = doorInitialScale * 3.2f;
            doorTransform.localPosition = GetTransformPosForVisualCenter(targetVisualCenter, crashScale);
            doorTransform.localRotation = doorInitialRot;
            doorTransform.localScale = crashScale;
        }

        if (headerText != null) headerText.gameObject.SetActive(false);
        if (qtePanel != null) qtePanel.SetActive(false);
        if (resultBannerText != null)
        {
            resultBannerText.gameObject.SetActive(true);
            resultBannerText.text = "<color=#FF3333>CRASH! STRUCK BY CABINET DOOR!</color>";
        }

        yield return new WaitForSeconds(0.45f);

        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.HandleCabinetQTEResult(false);
        }
        else
        {
            // Standalone editor test fallback
            Debug.LogWarning("[CupboardScene] Failed QTE in standalone test. Reloading scene in 2s...");
            yield return new WaitForSeconds(2.0f);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private void TriggerCameraJolt(float intensity)
    {
        shakeIntensity = Mathf.Max(shakeIntensity, intensity);
    }

    #endregion

    #region --- PROCEDURAL UI CONSTRUCTION ---

    private void BuildProceduralUI()
    {
        GameObject canvasObj = new GameObject("CupboardQTECanvas");
        canvasObj.transform.SetParent(transform, false);

        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 500;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Header Text (top of screen)
        GameObject headerObj = new GameObject("HeaderText");
        headerObj.transform.SetParent(canvasObj.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 0.93f);
        headerRect.anchorMax = new Vector2(0.5f, 0.93f);
        headerRect.sizeDelta = new Vector2(1100f, 50f);
        headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = "THE CABINET IS FALLING!";
        headerText.fontSize = 38;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = new Color(1f, 0.35f, 0.2f, 1f);

        // Root QTE Panel (bottom HUD)
        qtePanel = new GameObject("QTEPanel");
        qtePanel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = qtePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.16f);
        panelRect.anchorMax = new Vector2(0.5f, 0.16f);
        panelRect.sizeDelta = new Vector2(900f, 220f);

        // Prompt Text
        GameObject promptObj = new GameObject("PromptText");
        promptObj.transform.SetParent(qtePanel.transform, false);
        RectTransform promptRect = promptObj.AddComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0.82f);
        promptRect.anchorMax = new Vector2(0.5f, 0.82f);
        promptRect.sizeDelta = new Vector2(900f, 40f);
        promptText = promptObj.AddComponent<TextMeshProUGUI>();
        promptText.text = "MASH [SPACE] OR [E] TO PUSH IT AWAY!";
        promptText.fontSize = 24;
        promptText.fontStyle = FontStyles.Bold;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = Color.white;

        // Key Badge Icon ("[SPACE]")
        GameObject badgeObj = new GameObject("KeyBadge");
        badgeObj.transform.SetParent(qtePanel.transform, false);
        keyBadgeRect = badgeObj.AddComponent<RectTransform>();
        keyBadgeRect.anchorMin = new Vector2(0.5f, 0.44f);
        keyBadgeRect.anchorMax = new Vector2(0.5f, 0.44f);
        keyBadgeRect.sizeDelta = new Vector2(160f, 44f);
        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = new Color(0.12f, 0.15f, 0.22f, 0.85f);

        GameObject badgeTextObj = new GameObject("BadgeText");
        badgeTextObj.transform.SetParent(badgeObj.transform, false);
        RectTransform btRect = badgeTextObj.AddComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.sizeDelta = Vector2.zero;
        var badgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
        badgeText.text = "[SPACE]";
        badgeText.fontSize = 22;
        badgeText.fontStyle = FontStyles.Bold;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.color = new Color(0.4f, 0.9f, 1f, 1f);

        // Mash Progress Slider
        GameObject sliderObj = new GameObject("MashSlider");
        sliderObj.transform.SetParent(qtePanel.transform, false);
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.06f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.06f);
        sliderRect.sizeDelta = new Vector2(550f, 32f);

        // Slider Background
        Image sliderBg = sliderObj.AddComponent<Image>();
        sliderBg.color = new Color(0.08f, 0.10f, 0.14f, 0.9f);

        // Slider Fill Area
        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-8f, -8f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        sliderFillImage = fillObj.AddComponent<Image>();
        sliderFillImage.color = new Color(0.2f, 0.85f, 1f, 1f);

        mashSlider = sliderObj.AddComponent<Slider>();
        mashSlider.fillRect = fillRect;
        mashSlider.minValue = 0f;
        mashSlider.maxValue = 1f;
        mashSlider.value = initialProgress;

        // Timer Text
        GameObject timerObj = new GameObject("TimerText");
        timerObj.transform.SetParent(qtePanel.transform, false);
        RectTransform timerRect = timerObj.AddComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0.5f, -0.18f);
        timerRect.anchorMax = new Vector2(0.5f, -0.18f);
        timerRect.sizeDelta = new Vector2(400f, 30f);
        timerText = timerObj.AddComponent<TextMeshProUGUI>();
        timerText.text = "TIME: 5.5s";
        timerText.fontSize = 20;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.color = new Color(0.85f, 0.88f, 0.95f, 0.8f);

        // Result Banner (Success / Failure big announcement)
        GameObject bannerObj = new GameObject("ResultBanner");
        bannerObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bannerRect = bannerObj.AddComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 0.5f);
        bannerRect.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRect.sizeDelta = new Vector2(1000f, 200f);
        resultBannerText = bannerObj.AddComponent<TextMeshProUGUI>();
        resultBannerText.text = "";
        resultBannerText.fontSize = 44;
        resultBannerText.fontStyle = FontStyles.Bold;
        resultBannerText.alignment = TextAlignmentOptions.Center;
        resultBannerText.color = Color.white;
        bannerObj.SetActive(false);

        qtePanel.SetActive(false);
    }

    private void UpdateUI()
    {
        if (mashSlider != null)
        {
            mashSlider.value = currentProgress;
        }

        if (sliderFillImage != null)
        {
            // Dynamic color: Red when critically low, Yellow in middle, Bright Cyan/Green when high
            if (currentProgress < 0.35f)
            {
                sliderFillImage.color = Color.Lerp(Color.red, Color.yellow, currentProgress / 0.35f);
            }
            else
            {
                sliderFillImage.color = Color.Lerp(Color.yellow, new Color(0.2f, 1f, 0.5f, 1f), (currentProgress - 0.35f) / 0.65f);
            }
        }

        if (timerText != null)
        {
            timerText.text = $"TIME REMAINING: {timeRemaining:0.0}s";
        }
    }

    #endregion

    #region --- PROCEDURAL AUDIO SYNTHESIS ---

    private void GenerateProceduralAudio()
    {
        synthRumble = CreateRumbleClip();
        synthCreak = CreateCreakClip();
        synthMashThump = CreateMashThumpClip();
        synthSlam = CreateSlamClip();
        synthLatch = CreateLatchClip();
        synthCrash = CreateCrashClip();
    }

    private AudioClip CreateRumbleClip()
    {
        int sampleRate = 44100;
        float duration = 1.2f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Sin((t / duration) * Mathf.PI);
            float f1 = Mathf.Sin(2f * Mathf.PI * 52f * t);
            float f2 = Mathf.Sin(2f * Mathf.PI * 78f * t);
            float noise = ((float)UnityEngine.Random.value * 2f - 1f) * 0.25f;
            samples[i] = (f1 * 0.6f + f2 * 0.4f + noise) * env * 0.75f;
        }

        AudioClip clip = AudioClip.Create("SynthRumble", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateCreakClip()
    {
        int sampleRate = 44100;
        float duration = 0.7f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;
            float freq = Mathf.Lerp(220f, 480f, progress) + Mathf.Sin(2f * Mathf.PI * 35f * t) * 60f;
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
            float noise = ((float)UnityEngine.Random.value * 2f - 1f) * 0.25f;
            float env = Mathf.Sin(progress * Mathf.PI);
            samples[i] = (wave * 0.75f + noise) * env * 0.65f;
        }

        AudioClip clip = AudioClip.Create("SynthCreak", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateMashThumpClip()
    {
        int sampleRate = 44100;
        float duration = 0.18f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 22f);
            float freq = Mathf.Lerp(120f, 45f, t / duration);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
            float noise = ((float)UnityEngine.Random.value * 2f - 1f) * 0.2f;
            samples[i] = (tone * 0.8f + noise) * env * 0.85f;
        }

        AudioClip clip = AudioClip.Create("SynthMashThump", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateSlamClip()
    {
        int sampleRate = 44100;
        float duration = 0.55f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 12f);
            float slap = Mathf.Sin(2f * Mathf.PI * 85f * t);
            float sub = Mathf.Sin(2f * Mathf.PI * 45f * t);
            float noise = ((float)UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 30f) * 0.5f;
            samples[i] = (slap * 0.6f + sub * 0.5f + noise) * env * 0.95f;
        }

        AudioClip clip = AudioClip.Create("SynthSlam", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateLatchClip()
    {
        int sampleRate = 44100;
        float duration = 0.15f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 45f);
            float tone = Mathf.Sin(2f * Mathf.PI * 2200f * t) + Mathf.Sin(2f * Mathf.PI * 3400f * t) * 0.6f;
            samples[i] = tone * env * 0.7f;
        }

        AudioClip clip = AudioClip.Create("SynthLatch", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateCrashClip()
    {
        int sampleRate = 44100;
        float duration = 1.1f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 4.5f);
            float heavy = Mathf.Sin(2f * Mathf.PI * 55f * t);
            float noise = ((float)UnityEngine.Random.value * 2f - 1f) * 0.75f;
            samples[i] = (heavy * 0.4f + noise * 0.6f) * env * 0.95f;
        }

        AudioClip clip = AudioClip.Create("SynthCrash", sampleCount, 1, sampleRate, false);
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
}
