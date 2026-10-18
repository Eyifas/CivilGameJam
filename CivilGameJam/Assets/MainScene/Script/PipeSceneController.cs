using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Controls the PipeScene interactive QTE event.
/// An active gas leak is hissing from the pipe fixture. The player must grab the red valve handle
/// and smoothly turn it a full semicircle (from 0° pointing right, up through 90°, to 180° pointing left)
/// to close the emergency gas valve and stop the leak.
/// 
/// Features:
/// - Real-time mouse dragging along the semicircle arc + keyboard fallback ([SPACE] / [E] / [A] / Arrows).
/// - Dynamic procedural visual guide arc and pulsing grab indicator.
/// - Procedural gas particle spray from the bolt that diminishes with valve progress.
/// - Procedural looping gas hiss audio, ratchet metal squeaks, and heavy locking latch clank.
/// - Automatic integration with GameTimelineManager and ReturnToRoom.
/// </summary>
public class PipeSceneController : MonoBehaviour
{
    public static PipeSceneController Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private Transform pipeBodyTransform;
    [SerializeField] private Transform valveHandleTransform;
    [SerializeField] private Camera sceneCamera;

    [Header("QTE Settings")]
    [SerializeField] private float turnSpeedKeyboard = 75f;
    [SerializeField] private float maxTurnSpeedMouse = 360f;
    [SerializeField] private float autoReturnDelay = 1.8f;

    [Header("Audio Settings (Optional Inspector Overrides)")]
    [SerializeField] private AudioClip customGasHiss;
    [SerializeField] private AudioClip customMetalCreak;
    [SerializeField] private AudioClip customValveLatch;

    // State
    private bool isBusy = false;
    public bool IsBusy => isBusy;
    public bool IsGasShutOff => isValveShut;

    private bool isValveShut = false;
    private bool isDragging = false;
    private bool isKeyboardTurning = false;
    private bool isCompleting = false;

    private float currentAngle = 0f; // 0 deg = open (pointing right), 180 deg = closed (pointing left)
    private float lastCreakAngle = 0f;

    // Geometry & Transforms
    // Texture 681x372, Center (340.5, 186.0), Bolt (202.4, 145.5). Local offset = (-1.381, +0.405)
    private readonly Vector2 localBoltOffset = new Vector2(-1.381f, 0.405f);
    private Vector3 boltWorldPos;
    private Transform handlePivot;
    private float leverGripRadius = 3.85f;

    // Visual Arc & Reticle
    private LineRenderer arcLineRenderer;
    private GameObject grabIndicatorObj;
    private Transform grabIndicatorTransform;
    private Image grabIndicatorImage;
    private SpriteRenderer grabIndicatorSr;

    // Particles & Gas Visuals
    private ParticleSystem gasParticleSystem;
    private ParticleSystem.EmissionModule gasEmission;

    // Audio
    private AudioSource hissAudioSource;
    private AudioSource sfxAudioSource;
    private AudioClip synthGasHiss;
    private AudioClip synthMetalCreak;
    private AudioClip synthValveLatch;
    private const float maxHissVolume = 0.75f;

    // Procedural UI
    private Canvas overlayCanvas;
    private TMP_Text headerText;
    private TMP_Text instructionText;
    private TMP_Text angleReadoutText;
    private TMP_Text flowReadoutText;
    private Slider progressSlider;
    private Image sliderFillImage;
    private GameObject successBannerObj;
    private TMP_Text successBannerText;
    private InteractionVisualCue turnValveCue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnRuntimeSceneLoaded()
    {
        EnsurePipeControllerInScene();
        SceneManager.sceneLoaded += (scene, mode) => EnsurePipeControllerInScene();
    }

    public static void EnsurePipeControllerInScene()
    {
        if (SceneManager.GetActiveScene().name == "PipeScene")
        {
            if (Instance == null)
            {
                var sm = GameObject.Find("SceneManager");
                if (sm != null)
                {
                    if (sm.GetComponent<PipeSceneController>() == null)
                    {
                        sm.AddComponent<PipeSceneController>();
                    }
                }
                else
                {
                    GameObject obj = new GameObject("PipeSceneController");
                    obj.AddComponent<PipeSceneController>();
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

        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
            if (sceneCamera == null) sceneCamera = FindFirstObjectByType<Camera>();
        }

        SetupSceneObjects();
        SetupAudio();
        SetupGasParticles();
        SetupVisualArcGuide();
        BuildProceduralUI();
    }

    private void Start()
    {
        // Check if gas was already shut off previously
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsGasShutOff)
        {
            SetAlreadyShutOffState();
            return;
        }

        // Animated turn cue with counter-clockwise arrow
        if (!isValveShut && overlayCanvas != null)
        {
            Vector3 cuePos = boltWorldPos + new Vector3(0.5f, 2.5f, 0f);
            turnValveCue = InteractionVisualCue.CreateTurnCue(
                overlayCanvas.transform,
                cuePos,
                false, // counter-clockwise turn
                "TURN",
                "Counter-Clockwise to Close",
                new Vector2(0f, 60f),
                sceneCamera
            );
        }

        // Play initial gas hissing
        if (hissAudioSource != null && !hissAudioSource.isPlaying)
        {
            hissAudioSource.Play();
        }
    }

    private void SetupSceneObjects()
    {
        // 1. Locate Pipe Body (vanAa)
        if (pipeBodyTransform == null)
        {
            var vanAaObj = GameObject.Find("vanAa");
            if (vanAaObj != null) pipeBodyTransform = vanAaObj.transform;
        }

        // 2. Locate Valve Handle (vana)
        if (valveHandleTransform == null)
        {
            var vanaObj = GameObject.Find("vana");
            if (vanaObj != null) valveHandleTransform = vanaObj.transform;
        }

        // 3. Disable any duplicate/redundant vanAa under background
        var redundantVanAa = GameObject.Find("GameObject/vanAa");
        if (redundantVanAa != null)
        {
            redundantVanAa.SetActive(false);
        }

        // Standardize pipe position and scale for cinematic framing
        if (pipeBodyTransform != null)
        {
            pipeBodyTransform.position = new Vector3(1.0f, -1.1f, 0f);
            pipeBodyTransform.localScale = new Vector3(2.3f, 2.3f, 1f);
            pipeBodyTransform.rotation = Quaternion.identity;

            if (pipeBodyTransform.TryGetComponent<SpriteRenderer>(out var pipeSr))
            {
                pipeSr.sortingOrder = 7;
            }
        }

        // 4. Calculate Bolt World Center
        Vector3 pipePos = pipeBodyTransform != null ? pipeBodyTransform.position : new Vector3(1.0f, -1.1f, 0f);
        Vector3 pipeScale = pipeBodyTransform != null ? pipeBodyTransform.localScale : new Vector3(2.3f, 2.3f, 1f);
        boltWorldPos = pipePos + new Vector3(localBoltOffset.x * pipeScale.x, localBoltOffset.y * pipeScale.y, 0f);
        leverGripRadius = (398f - 202.4f) / 100f * pipeScale.x * 0.92f; // ~4.15 units

        // 5. Construct Handle Pivot hierarchy
        GameObject pivotObj = new GameObject("ValveHandlePivot");
        pivotObj.transform.position = boltWorldPos;
        pivotObj.transform.rotation = Quaternion.identity;
        handlePivot = pivotObj.transform;

        if (valveHandleTransform != null)
        {
            valveHandleTransform.SetParent(handlePivot, true);
            valveHandleTransform.localPosition = new Vector3(-localBoltOffset.x * pipeScale.x, -localBoltOffset.y * pipeScale.y, 0f);
            valveHandleTransform.localRotation = Quaternion.identity;
            valveHandleTransform.localScale = pipeScale;

            if (valveHandleTransform.TryGetComponent<SpriteRenderer>(out var handleSr))
            {
                handleSr.sortingOrder = 9; // In front of pipe
            }
        }

        currentAngle = 0f;
        lastCreakAngle = 0f;
        UpdateHandleRotation();
    }

    private void UpdateHandleRotation()
    {
        if (handlePivot != null)
        {
            handlePivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        }

        // Update grab indicator world position along the handle tip
        if (grabIndicatorTransform != null)
        {
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 tipPos = boltWorldPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * leverGripRadius;
            grabIndicatorTransform.position = tipPos;
        }
    }

    #region --- AUDIO SETUP & PROCEDURAL SYNTHESIS ---

    private void SetupAudio()
    {
        // Looping Gas Hiss AudioSource
        GameObject hissObj = new GameObject("GasHissSource");
        hissObj.transform.SetParent(transform, false);
        hissAudioSource = hissObj.AddComponent<AudioSource>();
        hissAudioSource.playOnAwake = false;
        hissAudioSource.loop = true;
        hissAudioSource.volume = maxHissVolume;

        synthGasHiss = customGasHiss != null ? customGasHiss : CreateGasHissClip();
        hissAudioSource.clip = synthGasHiss;

        // SFX AudioSource (creaks, latch lock)
        GameObject sfxObj = new GameObject("ValveSFXSource");
        sfxObj.transform.SetParent(transform, false);
        sfxAudioSource = sfxObj.AddComponent<AudioSource>();
        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.loop = false;
        sfxAudioSource.volume = 1.0f;

        synthMetalCreak = customMetalCreak != null ? customMetalCreak : CreateMetalCreakClip();
        synthValveLatch = customValveLatch != null ? customValveLatch : CreateValveLatchClip();
    }

    private AudioClip CreateGasHissClip()
    {
        int sampleRate = 44100;
        float duration = 1.4f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        float filter = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float white = (UnityEngine.Random.value * 2f - 1f);
            // High-pass / band-pass filter to sound like pressurized gas escaping a narrow gasket
            filter = Mathf.Lerp(filter, white, 0.42f);
            float hiss = white - filter * 0.75f;
            // Add subtle undulating turbulence
            float flutter = 1.0f + 0.12f * Mathf.Sin(2f * Mathf.PI * 6.5f * t) + 0.08f * Mathf.Sin(2f * Mathf.PI * 14.2f * t);
            samples[i] = hiss * flutter * 0.55f;
        }

        AudioClip clip = AudioClip.Create("SynthGasHiss", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateMetalCreakClip()
    {
        int sampleRate = 44100;
        float duration = 0.16f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Sin(t / duration * Mathf.PI);
            float mod = Mathf.Sin(2f * Mathf.PI * 140f * t) * 110f;
            float tone = Mathf.Sin(2f * Mathf.PI * (560f + mod) * t);
            float grit = (UnityEngine.Random.value * 2f - 1f) * 0.28f;
            samples[i] = (tone * 0.72f + grit) * env * 0.65f;
        }

        AudioClip clip = AudioClip.Create("SynthMetalCreak", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateValveLatchClip()
    {
        int sampleRate = 44100;
        float duration = 0.5f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float punchEnv = Mathf.Exp(-t * 28f);
            float ringEnv = Mathf.Exp(-t * 9f);
            float thud = Mathf.Sin(2f * Mathf.PI * 95f * t) * punchEnv;
            float metallic = (Mathf.Sin(2f * Mathf.PI * 1380f * t) + 0.65f * Mathf.Sin(2f * Mathf.PI * 2640f * t)) * ringEnv;
            float impactNoise = (UnityEngine.Random.value * 2f - 1f) * punchEnv * 0.35f;
            samples[i] = (thud * 0.75f + metallic * 0.6f + impactNoise) * 0.95f;
        }

        AudioClip clip = AudioClip.Create("SynthValveLatch", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void PlayMetalCreak()
    {
        if (sfxAudioSource != null && synthMetalCreak != null)
        {
            sfxAudioSource.pitch = UnityEngine.Random.Range(0.92f, 1.12f);
            sfxAudioSource.PlayOneShot(synthMetalCreak, 0.7f);
        }
    }

    private void PlayValveLatch()
    {
        if (sfxAudioSource != null && synthValveLatch != null)
        {
            sfxAudioSource.pitch = 1.0f;
            sfxAudioSource.PlayOneShot(synthValveLatch, 1.0f);
        }
    }

    #endregion

    #region --- GAS PARTICLES SYSTEM ---

    private void SetupGasParticles()
    {
        GameObject gasObj = new GameObject("GasLeakParticles");
        gasObj.transform.position = boltWorldPos;
        gasParticleSystem = gasObj.AddComponent<ParticleSystem>();

        var main = gasParticleSystem.main;
        main.startLifetime = 0.85f;
        main.startSpeed = 3.6f;
        main.startSize = 0.5f;
        main.startColor = new Color(0.9f, 0.95f, 1f, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.playOnAwake = true;

        gasEmission = gasParticleSystem.emission;
        gasEmission.rateOverTime = 55f;

        var shape = gasParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 24f;
        shape.radius = 0.18f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // billow upwards

        var sizeOverLifetime = gasParticleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.45f);
        sizeCurve.AddKey(1f, 2.4f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = gasParticleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.9f, 0.96f, 1f), 0f), new GradientColorKey(new Color(0.78f, 0.85f, 0.92f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.48f, 0f), new GradientAlphaKey(0.32f, 0.4f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        // Create soft radial texture for puff particles
        Texture2D puffTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = (x - 15.5f) / 15.5f;
                float dy = (y - 15.5f) / 15.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - dist);
                a = a * a;
                puffTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        puffTex.Apply();

        var psr = gasObj.GetComponent<ParticleSystemRenderer>();
        Shader spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit") ?? Shader.Find("Sprites/Default");
        Material mat = new Material(spriteShader);
        mat.mainTexture = puffTex;
        psr.material = mat;
        psr.sortingOrder = 8; // between pipe and handle
    }

    #endregion

    #region --- VISUAL ARC GUIDE & GRAB RETICLE ---

    private void SetupVisualArcGuide()
    {
        // Semicircle Line Renderer (from 0 to 180 degrees)
        GameObject arcObj = new GameObject("SemicircleGuideArc");
        arcObj.transform.position = Vector3.zero;
        arcLineRenderer = arcObj.AddComponent<LineRenderer>();

        Shader lineShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit") ?? Shader.Find("Sprites/Default");
        Material lineMat = new Material(lineShader);
        arcLineRenderer.material = lineMat;
        arcLineRenderer.useWorldSpace = true;
        arcLineRenderer.startWidth = 0.08f;
        arcLineRenderer.endWidth = 0.08f;
        arcLineRenderer.sortingOrder = 6;

        int segments = 45;
        arcLineRenderer.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float deg = (180f / segments) * i;
            float rad = deg * Mathf.Deg2Rad;
            Vector3 pt = boltWorldPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * leverGripRadius;
            arcLineRenderer.SetPosition(i, pt);
        }

        UpdateArcColors();

        // Grab Indicator Reticle at handle grip
        grabIndicatorObj = new GameObject("GrabIndicator");
        grabIndicatorTransform = grabIndicatorObj.transform;
        float initRad = currentAngle * Mathf.Deg2Rad;
        grabIndicatorTransform.position = boltWorldPos + new Vector3(Mathf.Cos(initRad), Mathf.Sin(initRad), 0f) * leverGripRadius;

        grabIndicatorSr = grabIndicatorObj.AddComponent<SpriteRenderer>();
        Sprite handDrawnBtn = ResolveSprite("QTE_Button_E.png");
        grabIndicatorSr.sprite = handDrawnBtn != null ? handDrawnBtn : CreateCircleSprite(48);
        grabIndicatorSr.color = Color.white;
        grabIndicatorSr.sortingOrder = 10;
        grabIndicatorTransform.localScale = Vector3.one * (handDrawnBtn != null ? 0.35f : 0.9f);
    }

    private Sprite ResolveSprite(string filename)
    {
        string baseName = Path.GetFileNameWithoutExtension(filename);
        Sprite res = Resources.Load<Sprite>("UI/" + baseName) ?? Resources.Load<Sprite>(baseName);
        if (res != null) return res;

#if UNITY_EDITOR
        string assetPath = "Assets/MainScene/Sprite/UI/" + filename;
        Sprite adSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (adSprite != null) return adSprite;
#endif

        string fullPath = Path.Combine(Application.dataPath, "MainScene/Sprite/UI", filename);
        if (File.Exists(fullPath))
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }
        return null;
    }

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float radius = (size - 2) * 0.5f;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                // Hollow ring shape
                float ringDist = Mathf.Abs(dist - radius * 0.75f);
                float alpha = Mathf.Clamp01(1f - (ringDist / 2.2f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void UpdateArcColors()
    {
        if (arcLineRenderer == null) return;

        float ratio = Mathf.Clamp01(currentAngle / 180f);
        Gradient grad = new Gradient();

        // 0 to ratio: Completed portion (bright cyan/green)
        // ratio to 1: Upcoming portion (warning amber)
        Color doneColor = new Color(0.1f, 1f, 0.5f, 0.95f);
        Color todoColor = new Color(1f, 0.72f, 0.15f, 0.45f);

        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(doneColor, 0f),
                new GradientColorKey(doneColor, Mathf.Clamp01(ratio - 0.02f)),
                new GradientColorKey(todoColor, Mathf.Clamp01(ratio + 0.02f)),
                new GradientColorKey(todoColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.9f, Mathf.Clamp01(ratio - 0.02f)),
                new GradientAlphaKey(0.45f, Mathf.Clamp01(ratio + 0.02f)),
                new GradientAlphaKey(0.45f, 1f)
            }
        );

        arcLineRenderer.colorGradient = grad;
    }

    #endregion

    #region --- PROCEDURAL UI CONSTRUCTION ---

    private void BuildProceduralUI()
    {
        GameObject canvasObj = new GameObject("PipeQTELayer");
        canvasObj.transform.SetParent(transform, false);

        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 500;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Header Title
        GameObject headerObj = new GameObject("HeaderTitle");
        headerObj.transform.SetParent(canvasObj.transform, false);
        RectTransform headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 0.94f);
        headerRect.anchorMax = new Vector2(0.5f, 0.94f);
        headerRect.sizeDelta = new Vector2(1200f, 48f);
        headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = "GAS LEAK DETECTED - SHUT OFF VALVE";
        headerText.fontSize = 34;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = new Color(1f, 0.35f, 0.2f, 1f);

        // Instruction Subtitle
        GameObject instObj = new GameObject("InstructionText");
        instObj.transform.SetParent(canvasObj.transform, false);
        RectTransform instRect = instObj.AddComponent<RectTransform>();
        instRect.anchorMin = new Vector2(0.5f, 0.89f);
        instRect.anchorMax = new Vector2(0.5f, 0.89f);
        instRect.sizeDelta = new Vector2(1200f, 36f);
        instructionText = instObj.AddComponent<TextMeshProUGUI>();
        instructionText.text = "Click & Drag the red handle in a semicircle (180°)  |  Or Hold [SPACE] / [E] / [A]";
        instructionText.fontSize = 20;
        instructionText.alignment = TextAlignmentOptions.Center;
        instructionText.color = new Color(0.92f, 0.92f, 0.95f, 1f);

        // HUD Panel (bottom center)
        GameObject hudPanel = new GameObject("HUDStatusPanel");
        hudPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = hudPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.12f);
        panelRect.anchorMax = new Vector2(0.5f, 0.12f);
        panelRect.sizeDelta = new Vector2(650f, 110f);

        Image panelBg = hudPanel.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.1f, 0.14f, 0.82f);

        // Angle Readout
        GameObject angleObj = new GameObject("AngleText");
        angleObj.transform.SetParent(hudPanel.transform, false);
        RectTransform angleRect = angleObj.AddComponent<RectTransform>();
        angleRect.anchorMin = new Vector2(0.06f, 0.65f);
        angleRect.anchorMax = new Vector2(0.48f, 0.92f);
        angleRect.sizeDelta = Vector2.zero;
        angleReadoutText = angleObj.AddComponent<TextMeshProUGUI>();
        angleReadoutText.text = "VALVE: 0° / 180°";
        angleReadoutText.fontSize = 20;
        angleReadoutText.fontStyle = FontStyles.Bold;
        angleReadoutText.alignment = TextAlignmentOptions.Left;
        angleReadoutText.color = new Color(1f, 0.85f, 0.25f, 1f);

        // Flow Readout
        GameObject flowObj = new GameObject("FlowText");
        flowObj.transform.SetParent(hudPanel.transform, false);
        RectTransform flowRect = flowObj.AddComponent<RectTransform>();
        flowRect.anchorMin = new Vector2(0.52f, 0.65f);
        flowRect.anchorMax = new Vector2(0.94f, 0.92f);
        flowRect.sizeDelta = Vector2.zero;
        flowReadoutText = flowObj.AddComponent<TextMeshProUGUI>();
        flowReadoutText.text = "GAS FLOW: 100%";
        flowReadoutText.fontSize = 20;
        flowReadoutText.fontStyle = FontStyles.Bold;
        flowReadoutText.alignment = TextAlignmentOptions.Right;
        flowReadoutText.color = new Color(1f, 0.35f, 0.25f, 1f);

        // Progress Slider
        GameObject sliderObj = new GameObject("TurnSlider");
        sliderObj.transform.SetParent(hudPanel.transform, false);
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.06f, 0.22f);
        sliderRect.anchorMax = new Vector2(0.94f, 0.52f);
        sliderRect.sizeDelta = Vector2.zero;

        progressSlider = sliderObj.AddComponent<Slider>();
        progressSlider.minValue = 0f;
        progressSlider.maxValue = 180f;
        progressSlider.value = 0f;
        progressSlider.interactable = false;

        GameObject sliderBg = new GameObject("Background");
        sliderBg.transform.SetParent(sliderObj.transform, false);
        RectTransform sbgRect = sliderBg.AddComponent<RectTransform>();
        sbgRect.anchorMin = Vector2.zero;
        sbgRect.anchorMax = Vector2.one;
        sbgRect.sizeDelta = Vector2.zero;
        Image sbgImage = sliderBg.AddComponent<Image>();
        sbgImage.color = new Color(0.18f, 0.22f, 0.28f, 0.9f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRect = fillArea.AddComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero;
        faRect.anchorMax = Vector2.one;
        faRect.sizeDelta = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        sliderFillImage = fillObj.AddComponent<Image>();
        sliderFillImage.color = new Color(1f, 0.55f, 0.1f, 1f);

        progressSlider.fillRect = fillRect;

        // Success Banner (centered popup)
        successBannerObj = new GameObject("SuccessBanner");
        successBannerObj.transform.SetParent(canvasObj.transform, false);
        RectTransform sbRect = successBannerObj.AddComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(0.5f, 0.5f);
        sbRect.anchorMax = new Vector2(0.5f, 0.5f);
        sbRect.sizeDelta = new Vector2(900f, 180f);

        Image sbBg = successBannerObj.AddComponent<Image>();
        sbBg.color = new Color(0.05f, 0.22f, 0.12f, 0.92f);

        GameObject sbTextObj = new GameObject("SuccessText");
        sbTextObj.transform.SetParent(successBannerObj.transform, false);
        RectTransform sbtRect = sbTextObj.AddComponent<RectTransform>();
        sbtRect.anchorMin = Vector2.zero;
        sbtRect.anchorMax = Vector2.one;
        sbtRect.sizeDelta = Vector2.zero;
        successBannerText = sbTextObj.AddComponent<TextMeshProUGUI>();
        successBannerText.text = "<color=#00FF88><size=36>VALVE SHUT OFF - GAS SECURED!</size></color>\n<size=22>Leak stopped. The apartment is safe from gas explosion.</size>";
        successBannerText.alignment = TextAlignmentOptions.Center;
        successBannerText.color = Color.white;

        successBannerObj.SetActive(false);
    }

    #endregion

    #region --- INTERACTION & UPDATE LOOP ---

    private void Update()
    {
        if (isValveShut) return;

        HandleInput();
        UpdateAudioAndParticles();
        UpdateUI();

        // Check completion condition (180 deg)
        if (currentAngle >= 178f && !isCompleting)
        {
            CompleteValveShutOff();
        }
    }

    private void HandleInput()
    {
        isBusy = isDragging || isKeyboardTurning || isCompleting;

        // 1. Mouse Dragging Interaction
        if (Mouse.current != null && sceneCamera != null)
        {
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mouseWorld = sceneCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, -sceneCamera.transform.position.z));
            mouseWorld.z = 0f;

            Vector2 toMouse = mouseWorld - boltWorldPos;
            float mouseDist = toMouse.magnitude;
            float mouseAngle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;

            // Handle quadrant mapping (Atan2 returns -180..180)
            if (mouseAngle < 0)
            {
                if (mouseAngle > -50f) mouseAngle = 0f;
                else if (mouseAngle < -130f) mouseAngle = 180f;
            }
            mouseAngle = Mathf.Clamp(mouseAngle, 0f, 180f);

            // Left Mouse Down: Check if grabbing near handle grip
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                float handleRad = currentAngle * Mathf.Deg2Rad;
                Vector3 gripPos = boltWorldPos + new Vector3(Mathf.Cos(handleRad), Mathf.Sin(handleRad), 0f) * leverGripRadius;
                float distToGrip = Vector2.Distance(mouseWorld, gripPos);

                // Allow grab if clicked near grip (within 2.2 units) or reasonable radius
                if (distToGrip < 2.4f || (mouseDist >= 1.2f && mouseDist <= 6.2f && Mathf.Abs(Mathf.DeltaAngle(currentAngle, mouseAngle)) < 55f))
                {
                    isDragging = true;
                }
            }

            // Dragging: Smoothly advance handle towards mouse angle
            if (isDragging)
            {
                if (Mouse.current.leftButton.isPressed)
                {
                    float targetAngle = mouseAngle;
                    // Move handle smoothly towards target
                    currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, maxTurnSpeedMouse * Time.deltaTime);

                    CheckCreakAudio();
                    UpdateHandleRotation();
                    UpdateArcColors();
                }
                else
                {
                    isDragging = false;
                }
            }
        }

        // 2. Keyboard Fallback ([SPACE], [E], [A], [LeftArrow])
        if (Keyboard.current != null)
        {
            bool keyHeld = Keyboard.current.spaceKey.isPressed ||
                           Keyboard.current.eKey.isPressed ||
                           Keyboard.current.aKey.isPressed ||
                           Keyboard.current.leftArrowKey.isPressed;

            if (keyHeld)
            {
                isKeyboardTurning = true;
                currentAngle = Mathf.MoveTowards(currentAngle, 180f, turnSpeedKeyboard * Time.deltaTime);

                CheckCreakAudio();
                UpdateHandleRotation();
                UpdateArcColors();
            }
            else
            {
                isKeyboardTurning = false;
            }
        }

        // Animate grab indicator pulse
        if (grabIndicatorTransform != null)
        {
            float baseScale = (grabIndicatorSr != null && grabIndicatorSr.sprite != null && grabIndicatorSr.sprite.rect.width > 200f) ? 0.35f : 0.9f;
            if (isDragging || isKeyboardTurning)
            {
                grabIndicatorTransform.localScale = Vector3.one * (baseScale * 1.25f);
                if (grabIndicatorSr != null) grabIndicatorSr.color = new Color(0.2f, 1f, 0.5f, 1f);
            }
            else
            {
                float pulse = 1.0f + Mathf.Sin(Time.time * 5.5f) * 0.12f;
                grabIndicatorTransform.localScale = Vector3.one * (baseScale * pulse);
                if (grabIndicatorSr != null) grabIndicatorSr.color = Color.white;
            }
        }
    }

    private void CheckCreakAudio()
    {
        if (Mathf.Abs(currentAngle - lastCreakAngle) >= 11f)
        {
            PlayMetalCreak();
            lastCreakAngle = currentAngle;
        }
    }

    private void UpdateAudioAndParticles()
    {
        float gasPressure = Mathf.Clamp01(1f - (currentAngle / 180f));

        // Modulate gas hissing volume & pitch
        if (hissAudioSource != null)
        {
            hissAudioSource.volume = gasPressure * maxHissVolume;
            hissAudioSource.pitch = Mathf.Lerp(0.82f, 1.15f, gasPressure);
        }

        // Modulate gas particle spray
        if (gasParticleSystem != null)
        {
            gasEmission.rateOverTime = 55f * gasPressure;
        }
    }

    private void UpdateUI()
    {
        float ratio = Mathf.Clamp01(currentAngle / 180f);
        float gasPercent = (1f - ratio) * 100f;

        if (angleReadoutText != null)
        {
            angleReadoutText.text = $"VALVE: {currentAngle:F0}° / 180°";
        }

        if (flowReadoutText != null)
        {
            flowReadoutText.text = $"GAS FLOW: {gasPercent:F0}%";
            flowReadoutText.color = Color.Lerp(new Color(0.1f, 1f, 0.5f, 1f), new Color(1f, 0.35f, 0.25f, 1f), ratio > 0.01f ? (1f - ratio) : 1f);
        }

        if (progressSlider != null)
        {
            progressSlider.value = currentAngle;
        }

        if (sliderFillImage != null)
        {
            sliderFillImage.color = Color.Lerp(new Color(1f, 0.55f, 0.1f, 1f), new Color(0.1f, 1f, 0.5f, 1f), ratio);
        }
    }

    private void CompleteValveShutOff()
    {
        isCompleting = true;
        isValveShut = true;
        currentAngle = 180f;
        UpdateHandleRotation();
        UpdateArcColors();

        if (turnValveCue != null)
        {
            turnValveCue.Dismiss();
            turnValveCue = null;
        }

        // Stop hissing audio immediately
        if (hissAudioSource != null)
        {
            hissAudioSource.Stop();
        }

        // Stop gas particles
        if (gasParticleSystem != null)
        {
            gasEmission.rateOverTime = 0f;
            gasParticleSystem.Stop();
        }

        // Play heavy latch lock sound
        PlayValveLatch();
        AudioManager.SetGasHissActive(false);
        AudioManager.PlayAnchorSuccess();

        // Update Timeline State
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.HandleGasValveShutOff();
        }

        // Show Success HUD
        if (successBannerObj != null)
        {
            successBannerObj.SetActive(true);
        }

        if (headerText != null)
        {
            headerText.text = "<color=#00FF88>GAS VALVE SECURELY CLOSED</color>";
        }

        if (instructionText != null)
        {
            instructionText.text = "All gas flow has stopped safely.";
        }

        if (flowReadoutText != null)
        {
            flowReadoutText.text = "GAS FLOW: 0% (OFF)";
            flowReadoutText.color = new Color(0.1f, 1f, 0.5f, 1f);
        }

        StartCoroutine(ReturnToMainAfterDelay());
    }

    private IEnumerator ReturnToMainAfterDelay()
    {
        yield return new WaitForSeconds(autoReturnDelay);

        var rtr = FindFirstObjectByType<ReturnToRoom>();
        if (rtr != null)
        {
            rtr.ReturnToMain();
        }
        else
        {
            Debug.Log("[PipeSceneController] Gas valve shut complete. Returning to 'MainScene'...");
            SceneManager.LoadScene("MainScene");
        }
    }

    private void SetAlreadyShutOffState()
    {
        isValveShut = true;
        currentAngle = 180f;
        UpdateHandleRotation();
        UpdateArcColors();

        if (turnValveCue != null)
        {
            turnValveCue.Dismiss();
            turnValveCue = null;
        }

        if (hissAudioSource != null) hissAudioSource.Stop();
        if (gasParticleSystem != null)
        {
            gasEmission.rateOverTime = 0f;
            gasParticleSystem.Stop();
        }

        if (headerText != null) headerText.text = "<color=#00FF88>GAS VALVE IS ALREADY SHUT OFF</color>";
        if (instructionText != null) instructionText.text = "The apartment gas supply is secured. Press [E] or 'Go Back' to return.";
        if (flowReadoutText != null)
        {
            flowReadoutText.text = "GAS FLOW: 0% (OFF)";
            flowReadoutText.color = new Color(0.1f, 1f, 0.5f, 1f);
        }
        if (progressSlider != null) progressSlider.value = 180f;
    }

    #endregion
}
