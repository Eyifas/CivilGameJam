using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Controls the interactive DoorScene for the apartment exit door.
/// Features:
/// - Front view of the door with hallway wallpaper (door_front.png).
/// - Drag & drop Key ("Anahtar") from inventory onto the keyhole.
/// - Once inserted, displays anahtar2.png (horizontal view).
/// - Player rotates the key 360° clockwise using mouse dragging or holding [D] / [Right Arrow] / [Space].
/// - 2-stage Tumbler Catch QTE (at 90° and 225°):
///     The cylinder hits a stuck pin! A circular timing ring shrinks towards a target sweet spot.
///     Player must press [Space], [E], or Click inside the sweet spot to force the pin.
///     Failing slips the lock backward 30°, requiring another attempt.
/// - Completing the 360° turn unlocks the door, plays door unlatch audio, shows door cracked open visual,
///   and triggers GameTimelineManager.Instance.CompleteGameAndEvacuate().
/// </summary>
public class DoorSceneController : MonoBehaviour
{
    public static DoorSceneController Instance { get; private set; }

    [Header("Sprites")]
    [SerializeField] private Sprite doorFrontSprite;
    [SerializeField] private Sprite anahtar2Sprite; // 0 deg (horizontal)
    [SerializeField] private Sprite anahtar3Sprite; // 45 deg (diagonal)
    [SerializeField] private Sprite anahtar4Sprite; // 90 deg (vertical)

    [Header("Keyhole & Drop Zone")]
    [SerializeField] private Vector3 keyholeWorldPos = new Vector3(1.26f, -1.75f, 0f);
    [SerializeField] private Vector2 dropZoneSize = new Vector2(2.5f, 3.0f);
    [SerializeField] private float keyScale = 2.2f;

    [Header("Rotation & QTE")]
    [SerializeField] private float keyboardTurnSpeed = 80.0f;
    [SerializeField] private float autoEvacuateDelay = 1.6f;

    // State
    private bool isKeyInserted = false;
    private bool isUnlocked = false;
    private bool isEvacuating = false;
    private float currentAngle = 0f; // 0 to 360 deg
    private float lastCreakAngle = 0f;
    private bool isDragging = false;
    private float lastDragMouseAngle = 0f;

    // Tumbler Pin QTE State
    private bool isTumblerCatchActive = false;
    private int currentTumblerIndex = 0;
    private readonly float[] tumblerAngles = new float[] { 90f, 225f };
    private readonly bool[] tumblerPassed = new bool[] { false, false };
    private float tumblerTimer = 0f;
    private const float tumblerDuration = 1.35f;
    private const float tumblerSweetSpotMin = 0.58f;
    private const float tumblerSweetSpotMax = 0.82f;

    public bool IsBusy => (isKeyInserted && !isUnlocked) || isEvacuating;
    public bool IsKeyInserted => isKeyInserted;
    public bool IsUnlocked => isUnlocked;

    // Transforms & Visuals
    private Camera sceneCamera;
    private Transform doorBackgroundTransform;
    private SpriteRenderer doorBgRenderer;
    private Transform keyTransform;
    private SpriteRenderer keyRenderer;
    private GameObject doorCrackObj;
    private SpriteRenderer doorCrackRenderer;

    // UI Elements
    private Canvas doorCanvas;
    private TMP_Text instructionText;
    private Slider unlockProgressSlider;
    private Image progressFillImage;
    private GameObject qtePanelObj;
    private RectTransform qteTargetRing;
    private RectTransform qteShrinkingRing;
    private Image qteShrinkingImage;
    private TMP_Text qtePromptText;
    private GameObject successBannerObj;
    private TMP_Text successBannerText;

    // Visual Guidance Cues
    private InteractionVisualCue dragKeyCue;
    private InteractionVisualCue turnKeyCue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnSceneLoadedRuntime()
    {
        EnsureDoorControllerInScene();
        SceneManager.sceneLoaded += (scene, mode) => EnsureDoorControllerInScene();
    }

    public static void EnsureDoorControllerInScene()
    {
        if (SceneManager.GetActiveScene().name == "DoorScene")
        {
            if (Instance == null)
            {
                var sm = GameObject.Find("SceneManager");
                if (sm != null)
                {
                    if (sm.GetComponent<DoorSceneController>() == null)
                    {
                        sm.AddComponent<DoorSceneController>();
                    }
                }
                else
                {
                    GameObject obj = new GameObject("DoorSceneController");
                    obj.AddComponent<DoorSceneController>();
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

        LoadSprites();
        BuildSceneObjects();
        BuildUI();

        // Ensure ReturnToRoom exists so player can press [E] / Go Back
        if (GetComponent<ReturnToRoom>() == null && FindFirstObjectByType<ReturnToRoom>() == null)
        {
            gameObject.AddComponent<ReturnToRoom>();
        }
    }

    private void Start()
    {
        UpdateInstruction();
        UpdateProgressUI();

        // Visual cue for dragging key from inventory to keyhole
        if (!isKeyInserted && doorCanvas != null)
        {
            dragKeyCue = InteractionVisualCue.CreateDragAndDropCue(
                doorCanvas.transform,
                keyholeWorldPos,
                new Vector2(170f, 170f),
                "DRAG KEY HERE",
                "From inventory to keyhole",
                new Vector2(0f, 95f),
                sceneCamera
            );
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LoadSprites()
    {
        // 1. door_front
        if (doorFrontSprite == null)
        {
            doorFrontSprite = Resources.Load<Sprite>("Door/door_front") ??
                              Resources.Load<Sprite>("door_front");
            if (doorFrontSprite == null)
            {
                doorFrontSprite = LoadSpriteFromDisk("Resources/Door/door_front.png", 100f) ??
                                  LoadSpriteFromDisk("MainScene/Sprite/door_front.png", 100f);
            }
        }

        // 2. anahtar2 (horizontal inserted view)
        if (anahtar2Sprite == null)
        {
            anahtar2Sprite = Resources.Load<Sprite>("Door/anahtar2") ??
                             Resources.Load<Sprite>("anahtar2");
            if (anahtar2Sprite == null)
            {
                var sprites = Resources.LoadAll<Sprite>("Door/anahtar2");
                if (sprites != null && sprites.Length > 0) anahtar2Sprite = sprites[0];
            }
            if (anahtar2Sprite == null)
            {
                anahtar2Sprite = LoadSpriteFromDisk("MainScene/Sprite/anahtar2.png", 100f) ??
                                 LoadSpriteFromDisk("Resources/Door/anahtar2.png", 100f);
            }
        }

        // 3. anahtar3 (diagonal 45 deg)
        if (anahtar3Sprite == null)
        {
            anahtar3Sprite = Resources.Load<Sprite>("Door/anahtar3") ??
                             Resources.Load<Sprite>("anahtar3");
            if (anahtar3Sprite == null)
            {
                var sprites = Resources.LoadAll<Sprite>("Door/anahtar3");
                if (sprites != null && sprites.Length > 0) anahtar3Sprite = sprites[0];
            }
            if (anahtar3Sprite == null)
            {
                anahtar3Sprite = LoadSpriteFromDisk("MainScene/Sprite/anahtar3.png", 100f) ??
                                 LoadSpriteFromDisk("Resources/Door/anahtar3.png", 100f);
            }
        }

        // 4. anahtar4 (vertical 90 deg)
        if (anahtar4Sprite == null)
        {
            anahtar4Sprite = Resources.Load<Sprite>("Door/anahtar4") ??
                             Resources.Load<Sprite>("anahtar4");
            if (anahtar4Sprite == null)
            {
                var sprites = Resources.LoadAll<Sprite>("Door/anahtar4");
                if (sprites != null && sprites.Length > 0) anahtar4Sprite = sprites[0];
            }
            if (anahtar4Sprite == null)
            {
                anahtar4Sprite = LoadSpriteFromDisk("MainScene/Sprite/anahtar4.png", 100f) ??
                                 LoadSpriteFromDisk("Resources/Door/anahtar4.png", 100f);
            }
        }
    }

    private Sprite LoadSpriteFromDisk(string relativePath, float ppu)
    {
        string fullPath = Path.Combine(Application.dataPath, relativePath);
        if (!File.Exists(fullPath)) return null;
        try
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            if (tex.LoadImage(bytes))
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DoorSceneController] Failed to load {relativePath} from disk: {ex.Message}");
        }
        return null;
    }

    private void BuildSceneObjects()
    {
        // 1. Camera check
        if (sceneCamera == null)
        {
            var camObj = new GameObject("Main Camera");
            sceneCamera = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(0.1f, 0.08f, 0.06f, 1f);
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 5.4f; // 1080p fit
            camObj.transform.position = new Vector3(0f, 0f, -10f);
        }
        else
        {
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 5.4f;
            sceneCamera.transform.position = new Vector3(0f, 0f, -10f);
        }

        // 2. Door Background
        var bgObj = GameObject.Find("DoorBackground");
        if (bgObj == null)
        {
            bgObj = new GameObject("DoorBackground");
            doorBgRenderer = bgObj.AddComponent<SpriteRenderer>();
            doorBgRenderer.sprite = doorFrontSprite;
            doorBgRenderer.sortingOrder = 0;
            bgObj.transform.position = Vector3.zero;

            // Fit exactly to 1920x1080 (19.2 x 10.8 units at 100 PPU)
            if (doorFrontSprite != null)
            {
                float targetWidth = 19.2f;
                float targetHeight = 10.8f;
                float curW = doorFrontSprite.rect.width / doorFrontSprite.pixelsPerUnit;
                float curH = doorFrontSprite.rect.height / doorFrontSprite.pixelsPerUnit;
                bgObj.transform.localScale = new Vector3(targetWidth / curH * (curH / curW), targetHeight / curH, 1f);
            }
        }
        doorBackgroundTransform = bgObj.transform;

        // 3. Key Transform & SpriteRenderer
        var kObj = GameObject.Find("InsertedKey");
        if (kObj == null)
        {
            kObj = new GameObject("InsertedKey");
            kObj.transform.position = keyholeWorldPos;
            kObj.transform.localScale = new Vector3(keyScale, keyScale, 1f);

            keyRenderer = kObj.AddComponent<SpriteRenderer>();
            keyRenderer.sprite = anahtar2Sprite;
            keyRenderer.sortingOrder = 10;
        }
        else
        {
            keyRenderer = kObj.GetComponent<SpriteRenderer>();
        }
        keyTransform = kObj.transform;
        keyRenderer.enabled = false; // Hidden until key is dropped

        // 4. Door crack opening visual (unlatched door effect)
        doorCrackObj = new GameObject("DoorCrackLight");
        doorCrackObj.transform.position = new Vector3(3.28f, 0f, -0.05f);
        doorCrackObj.transform.localScale = new Vector3(0.01f, 10.8f, 1f);
        doorCrackRenderer = doorCrackObj.AddComponent<SpriteRenderer>();
        Texture2D whiteTex = Texture2D.whiteTexture;
        doorCrackRenderer.sprite = Sprite.Create(whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        doorCrackRenderer.color = new Color(1f, 0.95f, 0.8f, 0f); // Warm daylight
        doorCrackRenderer.sortingOrder = 5;
    }

    private void BuildUI()
    {
        // Screen Overlay Canvas
        var canvasObj = new GameObject("DoorUI_Canvas");
        doorCanvas = canvasObj.AddComponent<Canvas>();
        doorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Instruction Text (Top center)
        var instrObj = new GameObject("InstructionText");
        instrObj.transform.SetParent(canvasObj.transform, false);
        var instrRect = instrObj.AddComponent<RectTransform>();
        instrRect.anchorMin = new Vector2(0.1f, 0.88f);
        instrRect.anchorMax = new Vector2(0.9f, 0.96f);
        instrRect.offsetMin = Vector2.zero;
        instrRect.offsetMax = Vector2.zero;

        instructionText = instrObj.AddComponent<TextMeshProUGUI>();
        instructionText.fontSize = 28;
        instructionText.fontStyle = FontStyles.Bold;
        instructionText.alignment = TextAlignmentOptions.Center;
        instructionText.color = Color.white;
        instructionText.textWrappingMode = TextWrappingModes.Normal;

        // Unlock Progress Bar (Bottom center, above inventory)
        var sliderObj = new GameObject("UnlockProgressSlider");
        sliderObj.transform.SetParent(canvasObj.transform, false);
        var sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.35f, 0.14f);
        sliderRect.anchorMax = new Vector2(0.65f, 0.17f);
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        unlockProgressSlider = sliderObj.AddComponent<Slider>();
        unlockProgressSlider.minValue = 0f;
        unlockProgressSlider.maxValue = 360f;
        unlockProgressSlider.value = 0f;
        unlockProgressSlider.interactable = false;

        // Slider Background
        var bgBar = new GameObject("Background");
        bgBar.transform.SetParent(sliderObj.transform, false);
        var bgBarRect = bgBar.AddComponent<RectTransform>();
        bgBarRect.anchorMin = Vector2.zero;
        bgBarRect.anchorMax = Vector2.one;
        bgBarRect.offsetMin = Vector2.zero;
        bgBarRect.offsetMax = Vector2.zero;
        var bgImg = bgBar.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        // Slider Fill Area & Fill
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        var fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(4, 4);
        fillAreaRect.offsetMax = new Vector2(-4, -4);

        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        progressFillImage = fillObj.AddComponent<Image>();
        progressFillImage.color = new Color(0.95f, 0.75f, 0.2f, 1f); // Golden amber
        unlockProgressSlider.fillRect = fillRect;

        // Tumbler Pin QTE Ring Container (Over the keyhole)
        BuildTumblerQTEUI(canvasObj.transform);

        // Success Banner
        var successObj = new GameObject("SuccessBanner");
        successObj.transform.SetParent(canvasObj.transform, false);
        var sRect = successObj.AddComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.2f, 0.45f);
        sRect.anchorMax = new Vector2(0.8f, 0.60f);
        sRect.offsetMin = Vector2.zero;
        sRect.offsetMax = Vector2.zero;
        var sImg = successObj.AddComponent<Image>();
        sImg.color = new Color(0.05f, 0.25f, 0.1f, 0.9f);

        var sTextObj = new GameObject("SuccessText");
        sTextObj.transform.SetParent(successObj.transform, false);
        var stRect = sTextObj.AddComponent<RectTransform>();
        stRect.anchorMin = Vector2.zero;
        stRect.anchorMax = Vector2.one;
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = Vector2.zero;
        successBannerText = sTextObj.AddComponent<TextMeshProUGUI>();
        successBannerText.fontSize = 38;
        successBannerText.fontStyle = FontStyles.Bold;
        successBannerText.alignment = TextAlignmentOptions.Center;
        successBannerText.color = new Color(0.4f, 1.0f, 0.4f, 1f);
        successBannerText.text = "DOOR UNLOCKED!\n<size=24><color=white>Safely evacuating apartment...</color></size>";
        successBannerObj = successObj;
        successBannerObj.SetActive(false);
    }

    private void BuildTumblerQTEUI(Transform canvasParent)
    {
        qtePanelObj = new GameObject("TumblerQTEPanel");
        qtePanelObj.transform.SetParent(canvasParent, false);
        var panelRect = qtePanelObj.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(260, 260);

        // Fixed Target Ring
        var targetObj = new GameObject("TargetRing");
        targetObj.transform.SetParent(qtePanelObj.transform, false);
        qteTargetRing = targetObj.AddComponent<RectTransform>();
        qteTargetRing.sizeDelta = new Vector2(140, 140);
        var targetImg = targetObj.AddComponent<Image>();
        targetImg.sprite = CreateCircleSprite(128, 6, new Color(0.7f, 0.8f, 0.4f, 0.9f));
        targetImg.color = Color.white;

        // Shrinking Ring
        var shrinkObj = new GameObject("ShrinkingRing");
        shrinkObj.transform.SetParent(qtePanelObj.transform, false);
        qteShrinkingRing = shrinkObj.AddComponent<RectTransform>();
        qteShrinkingRing.sizeDelta = new Vector2(260, 260);
        qteShrinkingImage = shrinkObj.AddComponent<Image>();
        qteShrinkingImage.sprite = CreateCircleSprite(128, 5, new Color(0.95f, 0.7f, 0.2f, 1f));
        qteShrinkingImage.color = Color.white;

        // Prompt text in center
        var promptObj = new GameObject("QTEPromptText");
        promptObj.transform.SetParent(qtePanelObj.transform, false);
        var pRect = promptObj.AddComponent<RectTransform>();
        pRect.sizeDelta = new Vector2(220, 60);
        qtePromptText = promptObj.AddComponent<TextMeshProUGUI>();
        qtePromptText.fontSize = 20;
        qtePromptText.fontStyle = FontStyles.Bold;
        qtePromptText.alignment = TextAlignmentOptions.Center;
        qtePromptText.color = Color.white;
        qtePromptText.text = "[SPACE] or CLICK!\n<size=15><color=#F39C12>ALIGN TUMBLER</color></size>";

        qtePanelObj.SetActive(false);
    }

    private Sprite CreateCircleSprite(int size, int strokeWidth, Color color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = size * 0.5f;
        float innerR = r - strokeWidth;
        Color clear = Color.clear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= r && dist >= innerR)
                {
                    tex.SetPixel(x, y, color);
                }
                else
                {
                    tex.SetPixel(x, y, clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void Update()
    {
        if (isUnlocked || isEvacuating) return;

        UpdateQTEPanelScreenPosition();

        if (!isKeyInserted)
        {
            HandleKeyInsertionInput();
            return;
        }

        if (isTumblerCatchActive)
        {
            HandleTumblerQTE();
            return;
        }

        HandleKeyRotation();
    }

    private void UpdateQTEPanelScreenPosition()
    {
        if (qtePanelObj == null || !qtePanelObj.activeSelf) return;
        if (sceneCamera != null)
        {
            Vector3 screenPos = sceneCamera.WorldToScreenPoint(keyholeWorldPos);
            qtePanelObj.transform.position = screenPos;
        }
    }

    /// <summary>
    /// Checks if a world position is within the keyhole drop zone.
    /// </summary>
    public bool IsInsideDropZone(Vector3 worldPos)
    {
        return Mathf.Abs(worldPos.x - keyholeWorldPos.x) <= dropZoneSize.x * 0.5f &&
               Mathf.Abs(worldPos.y - keyholeWorldPos.y) <= dropZoneSize.y * 0.5f;
    }

    /// <summary>
    /// Called when the player drags an item from their inventory onto the door/keyhole.
    /// </summary>
    public bool TryDropKeyFromInventory(string itemName, Vector3 worldPos)
    {
        if (isKeyInserted || isUnlocked) return false;

        if (!IsKeyItem(itemName))
        {
            UpdateInstruction("<color=#E74C3C>That doesn't fit in the door lock!</color>");
            return false;
        }

        InsertKey();
        return true;
    }

    private bool IsKeyItem(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Equals("Anahtar", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Key", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("anahtar1", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("anahtar2", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("DrawerKey", StringComparison.OrdinalIgnoreCase);
    }

    private void HandleKeyInsertionInput()
    {
        // Support direct clicking on keyhole if Key is in inventory
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && sceneCamera != null)
        {
            Vector3 mouseWorld = sceneCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mouseWorld.z = 0f;
            if (IsInsideDropZone(mouseWorld))
            {
                if (InventoryManager.HasItem("Anahtar"))
                {
                    InventoryManager.RemoveItem("Anahtar");
                    var ui = FindAnyObjectByType<InventoryUI>();
                    if (ui != null) ui.UpdateInventoryUI();
                    InsertKey();
                }
                else
                {
                    AudioManager.PlayDoorLocked();
                    UpdateInstruction("<color=#E74C3C>LOCKED: The door is locked. Find the key in the drawer first!</color>");
                }
            }
        }
    }

    private void InsertKey()
    {
        isKeyInserted = true;
        keyRenderer.enabled = true;
        keyRenderer.sprite = anahtar2Sprite;
        keyTransform.localRotation = Quaternion.identity;
        currentAngle = 0f;
        lastCreakAngle = 0f;

        AudioManager.PlayValveLatch();
        AudioManager.PlayItemPickup();

        // Dismiss drag cue and show animated clockwise turn cue
        if (dragKeyCue != null)
        {
            dragKeyCue.Dismiss();
            dragKeyCue = null;
        }

        if (turnKeyCue == null && doorCanvas != null)
        {
            turnKeyCue = InteractionVisualCue.CreateTurnCue(
                doorCanvas.transform,
                keyholeWorldPos,
                true, // clockwise
                "TURN",
                "Clockwise 360°",
                new Vector2(170f, 40f),
                sceneCamera
            );
        }

        Debug.Log("<color=green>[DoorScene] Key inserted into the keyhole!</color>");
        UpdateInstruction("<b>Key inserted!</b> Rotate the key clockwise: drag mouse around keyhole or hold [D] / [Right Arrow].");
        UpdateProgressUI();
    }

    private void HandleKeyRotation()
    {
        float angleDelta = 0f;

        // 1. Mouse Drag Rotation
        if (Mouse.current != null && sceneCamera != null)
        {
            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld = sceneCamera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -sceneCamera.transform.position.z));
            mouseWorld.z = 0f;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                float dist = Vector2.Distance(mouseWorld, keyholeWorldPos);
                if (dist <= 3.2f)
                {
                    isDragging = true;
                    Vector2 dir = (mouseWorld - keyholeWorldPos).normalized;
                    lastDragMouseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                }
            }
            else if (Mouse.current.leftButton.isPressed && isDragging)
            {
                Vector2 dir = (mouseWorld - keyholeWorldPos).normalized;
                float currentMouseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                float diff = Mathf.DeltaAngle(lastDragMouseAngle, currentMouseAngle);

                // Clockwise motion in 2D coordinate system is negative delta
                if (diff < -0.1f)
                {
                    angleDelta += (-diff);
                    lastDragMouseAngle = currentMouseAngle;
                }
                else if (diff > 0.1f)
                {
                    lastDragMouseAngle = currentMouseAngle; // Ignore counter-clockwise
                }
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }
        }

        // 2. Keyboard fallback [D] / [Right Arrow] / [S] / [Down Arrow] / [Space]
        if (Keyboard.current != null)
        {
            bool turningKey = Keyboard.current.dKey.isPressed ||
                              Keyboard.current.rightArrowKey.isPressed ||
                              Keyboard.current.sKey.isPressed ||
                              Keyboard.current.downArrowKey.isPressed;

            if (turningKey)
            {
                angleDelta += keyboardTurnSpeed * Time.deltaTime;
            }
        }

        if (angleDelta > 0f)
        {
            ApplyRotation(angleDelta);
        }
    }

    private void ApplyRotation(float delta)
    {
        float targetAngle = currentAngle + delta;

        // Check Tumbler Catch #1 at 90 deg
        if (currentAngle < tumblerAngles[0] && targetAngle >= tumblerAngles[0] && !tumblerPassed[0])
        {
            currentAngle = tumblerAngles[0];
            TriggerTumblerCatch(0);
            UpdateKeyVisuals();
            UpdateProgressUI();
            return;
        }

        // Check Tumbler Catch #2 at 225 deg
        if (currentAngle < tumblerAngles[1] && targetAngle >= tumblerAngles[1] && !tumblerPassed[1])
        {
            currentAngle = tumblerAngles[1];
            TriggerTumblerCatch(1);
            UpdateKeyVisuals();
            UpdateProgressUI();
            return;
        }

        currentAngle = targetAngle;

        // Play sound every ~20 degrees
        if (Mathf.Abs(currentAngle - lastCreakAngle) >= 20f)
        {
            AudioManager.PlayValveTurn();
            lastCreakAngle = currentAngle;
        }

        if (currentAngle >= 360f)
        {
            currentAngle = 360f;
            CompleteUnlock();
        }

        UpdateKeyVisuals();
        UpdateProgressUI();
    }

    private void TriggerTumblerCatch(int index)
    {
        isTumblerCatchActive = true;
        currentTumblerIndex = index;
        tumblerTimer = 0f;
        isDragging = false;

        AudioManager.PlayDolapStrain();
        AudioManager.PlayDoorLocked();

        if (turnKeyCue != null) turnKeyCue.SetVisible(false);

        if (qtePanelObj != null)
        {
            qtePanelObj.SetActive(true);
            UpdateQTEPanelScreenPosition();
        }

        UpdateInstruction("<color=#F39C12>TUMBLER CAUGHT! Press [SPACE] or Click when the ring aligns to force the pin!</color>");
    }

    private void HandleTumblerQTE()
    {
        tumblerTimer += Time.deltaTime;
        float normalizedT = Mathf.Clamp01(tumblerTimer / tumblerDuration);

        // Shrink the outer ring from 260px down to 50px
        float currentSize = Mathf.Lerp(260f, 60f, normalizedT);
        if (qteShrinkingRing != null)
        {
            qteShrinkingRing.sizeDelta = new Vector2(currentSize, currentSize);
        }

        // Highlight ring color when inside sweet spot
        bool inSweetSpot = normalizedT >= tumblerSweetSpotMin && normalizedT <= tumblerSweetSpotMax;
        if (qteShrinkingImage != null)
        {
            qteShrinkingImage.color = inSweetSpot
                ? new Color(0.4f, 1f, 0.4f, 1f)  // Green sweet spot
                : new Color(0.95f, 0.7f, 0.2f, 1f);
        }

        // Player input check: Space, E, or Mouse Left Click
        bool hitPressed = false;
        if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
        {
            hitPressed = true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            hitPressed = true;
        }

        if (hitPressed)
        {
            if (inSweetSpot)
            {
                OnTumblerSuccess();
            }
            else
            {
                OnTumblerFail();
            }
            return;
        }

        // Timer expired without hit
        if (tumblerTimer >= tumblerDuration)
        {
            OnTumblerFail();
        }
    }

    private void OnTumblerSuccess()
    {
        tumblerPassed[currentTumblerIndex] = true;
        isTumblerCatchActive = false;
        if (qtePanelObj != null) qtePanelObj.SetActive(false);
        if (turnKeyCue != null) turnKeyCue.SetVisible(true);

        AudioManager.PlayQTEHit();
        AudioManager.PlayValveLatch();

        Debug.Log($"<color=green>[DoorScene] Tumbler pin #{currentTumblerIndex + 1} cleared!</color>");
        UpdateInstruction("<color=#2ECC71>TUMBLER PIN POPPED!</color> Keep turning the deadbolt to unlock!");
    }

    private void OnTumblerFail()
    {
        isTumblerCatchActive = false;
        if (qtePanelObj != null) qtePanelObj.SetActive(false);
        if (turnKeyCue != null) turnKeyCue.SetVisible(true);

        AudioManager.PlaySFX(SFXType.QTEMiss);

        // Slip lock cylinder backwards by 30 degrees
        currentAngle = Mathf.Max(0f, currentAngle - 35f);
        lastCreakAngle = currentAngle;
        UpdateKeyVisuals();
        UpdateProgressUI();

        Debug.Log($"<color=red>[DoorScene] Tumbler slipped back to {currentAngle:F0} deg!</color>");
        UpdateInstruction("<color=#E74C3C>Pin slipped! Turn the key back up and align the timing ring!</color>");
    }

    private void UpdateKeyVisuals()
    {
        if (keyTransform == null || keyRenderer == null) return;

        // Smooth continuous rotation
        keyTransform.localRotation = Quaternion.Euler(0f, 0f, -currentAngle);

        // Choose appropriate pixel art perspective sprite based on angle modulo 180
        float modAngle = Mathf.Repeat(currentAngle, 180f);
        if (modAngle < 22.5f || modAngle >= 157.5f)
        {
            keyRenderer.sprite = anahtar2Sprite; // Horizontal
        }
        else if (modAngle >= 22.5f && modAngle < 67.5f)
        {
            keyRenderer.sprite = anahtar3Sprite; // 45 deg diagonal
        }
        else if (modAngle >= 67.5f && modAngle < 112.5f)
        {
            keyRenderer.sprite = anahtar4Sprite; // 90 deg vertical
        }
        else
        {
            keyRenderer.sprite = anahtar3Sprite; // 135 deg diagonal
        }
    }

    private void UpdateProgressUI()
    {
        if (unlockProgressSlider != null)
        {
            unlockProgressSlider.value = currentAngle;
        }
    }

    private void UpdateInstruction(string customMsg = null)
    {
        if (instructionText == null) return;

        if (!string.IsNullOrEmpty(customMsg))
        {
            instructionText.text = customMsg;
            return;
        }

        if (!isKeyInserted)
        {
            if (InventoryManager.HasItem("Anahtar"))
            {
                instructionText.text = "Drag the <b>Key</b> from your inventory onto the keyhole to insert it!";
            }
            else
            {
                instructionText.text = "<color=#E74C3C>LOCKED: The apartment door is locked! Find the key in the drawer first. Press [E] to return.</color>";
            }
        }
        else
        {
            instructionText.text = "TURN DEADBOLT: Drag mouse clockwise around the keyhole or hold [D] / [Right Arrow].";
        }
    }

    private void CompleteUnlock()
    {
        isUnlocked = true;
        isEvacuating = true;
        if (qtePanelObj != null) qtePanelObj.SetActive(false);

        if (turnKeyCue != null)
        {
            turnKeyCue.Dismiss();
            turnKeyCue = null;
        }

        if (dragKeyCue != null)
        {
            dragKeyCue.Dismiss();
            dragKeyCue = null;
        }

        AudioManager.PlayDoorOpen();
        AudioManager.PlayValveLatch();

        if (successBannerObj != null)
        {
            successBannerObj.SetActive(true);
        }

        UpdateInstruction("<color=#2ECC71>DOOR UNLOCKED! EVACUATING...</color>");
        StartCoroutine(EvacuateRoutine());
    }

    private IEnumerator EvacuateRoutine()
    {
        // Animate door cracking open
        float elapsed = 0f;
        float duration = autoEvacuateDelay;

        Vector3 startScale = doorCrackObj != null ? doorCrackObj.transform.localScale : Vector3.zero;
        Vector3 endScale = new Vector3(0.45f, 10.8f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (doorCrackObj != null && doorCrackRenderer != null)
            {
                doorCrackObj.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                doorCrackRenderer.color = new Color(1f, 0.95f, 0.8f, Mathf.Lerp(0f, 0.95f, t));
            }

            yield return null;
        }

        Debug.Log("<color=green>[DoorScene] Evacuation complete! Triggering GameTimelineManager.CompleteGameAndEvacuate()...</color>");
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.CompleteGameAndEvacuate();
        }
    }
}
