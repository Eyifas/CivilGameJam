using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Hand-drawn Circular QTE Controller (matches Screenshot 2026-10-18 040716 style).
/// Displays a circular prompt button (e.g. [E]) surrounded by an olive-green radial timer ring.
/// The outer ring smoothly drains clockwise as time counts down.
/// Supports both mouse clicks and keyboard key presses (with Key.E as primary).
/// </summary>
public class QTETargetClick : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private RectTransform spawnArea;
    [SerializeField] private Button targetButton;
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private CanvasGroup targetCanvasGroup;
    [SerializeField] private TMP_Text promptKeyText;
    [SerializeField] private TMP_Text progressText;

    [Header("Hand-Drawn Visual Sprites")]
    [SerializeField] private Sprite outerRingSprite;
    [SerializeField] private Sprite trackRingSprite;
    [SerializeField] private Sprite circleButtonSprite;
    [SerializeField] private Sprite eButtonSprite;

    [Header("Dynamic Ring References")]
    [SerializeField] private Image timerRingImage;
    [SerializeField] private Image trackRingImage;
    [SerializeField] private Image buttonImage;

    [Header("Target Settings")]
    [Tooltip("Time in seconds allowed to hit each target before the ring completely drains.")]
    [SerializeField] private float timePerTarget = 1.8f;

    [Tooltip("Total targets player needs to hit to win.")]
    [SerializeField] private int requiredHits = 3;

    [Tooltip("If true, failing a target fails the entire QTE. If false, it spawns a new target.")]
    [SerializeField] private bool failOnMiss = false;
    [SerializeField] private int maxMisses = 2;

    [Header("Colors (Sampled from Artwork)")]
    private readonly Color normalRingColor = new Color(181f / 255f, 191f / 255f, 127f / 255f, 1f); // #B5BF7F
    private readonly Color trackRingColor = new Color(181f / 255f, 191f / 255f, 127f / 255f, 0.25f);
    private readonly Color warningRingColor = new Color(245f / 255f, 160f / 255f, 60f / 255f, 1f);  // Urgency amber
    private readonly Color successRingColor = new Color(110f / 255f, 220f / 255f, 85f / 255f, 1f);  // Punch green
    private readonly Color failRingColor = new Color(230f / 255f, 70f / 255f, 70f / 255f, 1f);     // Miss red

    [Header("Key Pool Configuration")]
    [Tooltip("Keys that can be randomly chosen as targets. Key.E is the primary default.")]
    [SerializeField] private Key[] allowedKeys = new Key[]
    {
        Key.E,
        Key.Space,
        Key.W,
        Key.Q,
        Key.F
    };

    // Events
    public event Action OnQTECompleted;
    public event Action OnQTEFailed;

    // Internal state
    private bool isActive = false;
    public bool IsActive => isActive;
    private int currentHits = 0;
    private int currentMisses = 0;
    private float targetTimer = 0f;
    private Key currentPromptKey = Key.E;
    private bool isHandlingHitOrMiss = false;

    private void Awake()
    {
        EnsureVisualHierarchy();
        SanitizeAllowedKeys();

        if (rootPanel == null)
        {
            rootPanel = gameObject;
        }

        if (rootPanel != null)
        {
            if (rootPanel.TryGetComponent<Image>(out var rootImg))
            {
                rootImg.raycastTarget = false;
            }
            rootPanel.SetActive(false);
        }

        if (spawnArea != null && spawnArea.TryGetComponent<Image>(out var areaImg))
        {
            areaImg.raycastTarget = false;
        }

        if (promptKeyText != null)
        {
            promptKeyText.raycastTarget = false;
        }

        if (targetButton != null)
        {
            targetButton.onClick.RemoveAllListeners();
            targetButton.onClick.AddListener(OnTargetClicked);
        }
    }

    /// <summary>
    /// Loads or resolves a sprite by filename with high-reliability fallbacks.
    /// </summary>
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

    /// <summary>
    /// Builds and styles the circular QTE hierarchy (Timer Ring, Track Ring, Center Circle Button, Prompt Text).
    /// </summary>
    public void EnsureVisualHierarchy()
    {
        // 1. Resolve sprites
        if (outerRingSprite == null) outerRingSprite = ResolveSprite("QTE_Outer_Ring.png");
        if (trackRingSprite == null) trackRingSprite = ResolveSprite("QTE_Ring_Track.png");
        if (circleButtonSprite == null) circleButtonSprite = ResolveSprite("QTE_Circle_Button.png");
        if (eButtonSprite == null) eButtonSprite = ResolveSprite("QTE_Button_E.png");

        // 2. Set targetRect container size to 160x160
        if (targetRect == null && targetButton != null)
        {
            targetRect = targetButton.GetComponent<RectTransform>();
        }

        if (targetRect != null)
        {
            targetRect.sizeDelta = new Vector2(160f, 160f);
            targetRect.pivot = new Vector2(0.5f, 0.5f);
        }

        // 3. Configure the root Image on targetRect to act as a transparent raycast receiver
        if (targetRect != null && targetRect.TryGetComponent<Image>(out var rootImg))
        {
            rootImg.color = Color.clear;
            rootImg.sprite = null;
            rootImg.raycastTarget = true;
        }

        if (targetButton != null)
        {
            if (targetRect != null && targetRect.TryGetComponent<Image>(out var rootGraphic))
            {
                targetButton.targetGraphic = rootGraphic;
            }
            targetButton.transition = Selectable.Transition.None;
        }

        // 4. Track Ring (Background ring) - Sibling Index 0
        if (trackRingImage == null && targetRect != null)
        {
            Transform found = targetRect.Find("TrackRing");
            if (found != null)
            {
                trackRingImage = found.GetComponent<Image>();
            }
            else
            {
                GameObject trackObj = new GameObject("TrackRing");
                trackObj.transform.SetParent(targetRect, false);
                RectTransform rt = trackObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                trackRingImage = trackObj.AddComponent<Image>();
                trackRingImage.raycastTarget = false;
            }
        }

        if (trackRingImage != null)
        {
            trackRingImage.sprite = trackRingSprite != null ? trackRingSprite : outerRingSprite;
            trackRingImage.color = trackRingColor;
            trackRingImage.raycastTarget = false;
            trackRingImage.transform.SetSiblingIndex(0);
        }

        // 5. Timer Ring (Radial 360 Fill) - Sibling Index 1
        if (timerRingImage == null && targetRect != null)
        {
            Transform found = targetRect.Find("TimerRing");
            if (found != null)
            {
                timerRingImage = found.GetComponent<Image>();
            }
            else
            {
                GameObject ringObj = new GameObject("TimerRing");
                ringObj.transform.SetParent(targetRect, false);
                RectTransform rt = ringObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                timerRingImage = ringObj.AddComponent<Image>();
                timerRingImage.raycastTarget = false;
            }
        }

        if (timerRingImage != null)
        {
            timerRingImage.sprite = outerRingSprite;
            timerRingImage.type = Image.Type.Filled;
            timerRingImage.fillMethod = Image.FillMethod.Radial360;
            timerRingImage.fillOrigin = (int)Image.Origin360.Top;
            timerRingImage.fillClockwise = true;
            timerRingImage.fillAmount = 1f;
            timerRingImage.color = normalRingColor;
            timerRingImage.raycastTarget = false;
            timerRingImage.transform.SetSiblingIndex(1);
        }

        // 6. Center Circle Graphic - Sibling Index 2
        if (buttonImage == null && targetRect != null)
        {
            Transform found = targetRect.Find("CenterCircle");
            if (found != null)
            {
                buttonImage = found.GetComponent<Image>();
            }
            else
            {
                GameObject circleObj = new GameObject("CenterCircle");
                circleObj.transform.SetParent(targetRect, false);
                RectTransform rt = circleObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                buttonImage = circleObj.AddComponent<Image>();
                buttonImage.raycastTarget = false;
            }
        }

        if (buttonImage != null)
        {
            buttonImage.sprite = circleButtonSprite;
            buttonImage.type = Image.Type.Simple;
            buttonImage.preserveAspect = true;
            buttonImage.color = Color.white;
            buttonImage.raycastTarget = false;
            buttonImage.transform.SetSiblingIndex(2);
        }

        // 7. Center Text - Sibling Index 3
        if (promptKeyText == null && targetRect != null)
        {
            promptKeyText = targetRect.GetComponentInChildren<TMP_Text>();
        }

        if (promptKeyText != null)
        {
            RectTransform textRect = promptKeyText.GetComponent<RectTransform>();
            if (textRect != null)
            {
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }
            promptKeyText.alignment = TextAlignmentOptions.Center;
            promptKeyText.fontStyle = FontStyles.Bold;
            promptKeyText.fontSize = 54f;
            promptKeyText.color = new Color(15f / 255f, 15f / 255f, 15f / 255f, 1f); // #0F0F0F
            promptKeyText.raycastTarget = false;
            promptKeyText.transform.SetSiblingIndex(3);
        }

        // 8. Progress text styling
        if (progressText != null)
        {
            progressText.fontStyle = FontStyles.Bold;
            progressText.fontSize = 24f;
            progressText.color = new Color(0.95f, 0.95f, 0.9f, 1f);
        }
    }

    /// <summary>
    /// Starts the Target Click QTE.
    /// </summary>
    public void StartQTE(int targetCount = 3, float timeLimitPerTarget = 1.8f, Action onComplete = null, Action onFail = null)
    {
        EnsureVisualHierarchy();
        SanitizeAllowedKeys();

        requiredHits = targetCount;
        timePerTarget = timeLimitPerTarget;
        currentHits = 0;
        currentMisses = 0;
        isHandlingHitOrMiss = false;

        OnQTECompleted = onComplete;
        OnQTEFailed = onFail;

        isActive = true;
        if (rootPanel != null) rootPanel.SetActive(true);

        SpawnNextTarget();
    }

    private void SanitizeAllowedKeys()
    {
        var cleanKeys = new List<Key>();
        if (allowedKeys != null)
        {
            foreach (var k in allowedKeys)
            {
                if (k != Key.Escape && k != Key.None)
                {
                    cleanKeys.Add(k);
                }
            }
        }

        if (cleanKeys.Count == 0)
        {
            cleanKeys.Add(Key.E);
            cleanKeys.Add(Key.Space);
            cleanKeys.Add(Key.W);
            cleanKeys.Add(Key.Q);
            cleanKeys.Add(Key.F);
        }

        allowedKeys = cleanKeys.ToArray();
    }

    private void SpawnNextTarget()
    {
        isHandlingHitOrMiss = false;
        targetTimer = timePerTarget;

        if (targetCanvasGroup != null) targetCanvasGroup.alpha = 1f;

        if (timerRingImage != null)
        {
            timerRingImage.fillAmount = 1f;
            timerRingImage.color = normalRingColor;
        }

        PickRandomKey();
        PositionTargetRandomly();
        UpdateProgressUI();
        AudioManager.PlayUIHover();

        // Punchy spawn bounce animation
        if (targetRect != null)
        {
            StopAllCoroutines();
            StartCoroutine(SpawnBounceRoutine());
        }
    }

    private IEnumerator SpawnBounceRoutine()
    {
        if (targetRect == null) yield break;

        targetRect.localScale = Vector3.one * 0.72f;
        float elapsed = 0f;
        float duration = 0.12f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Overshoot bounce easing
            float scale = Mathf.Lerp(0.72f, 1.05f, t);
            if (t > 0.75f) scale = Mathf.Lerp(1.05f, 1.0f, (t - 0.75f) / 0.25f);
            targetRect.localScale = Vector3.one * scale;
            yield return null;
        }

        targetRect.localScale = Vector3.one;
    }

    private void PickRandomKey()
    {
        SanitizeAllowedKeys();

        if (allowedKeys == null || allowedKeys.Length == 0)
        {
            currentPromptKey = Key.E;
        }
        else
        {
            bool hasE = Array.IndexOf(allowedKeys, Key.E) >= 0;
            if (hasE && (allowedKeys.Length == 1 || UnityEngine.Random.value < 0.85f))
            {
                currentPromptKey = Key.E;
            }
            else
            {
                int index = UnityEngine.Random.Range(0, allowedKeys.Length);
                currentPromptKey = allowedKeys[index];
            }
        }

        string label = currentPromptKey == Key.Space ? "SPACE" : currentPromptKey.ToString();

        // When the key is E and we have the dedicated pre-rendered artwork, use it directly!
        if (currentPromptKey == Key.E && eButtonSprite != null)
        {
            if (buttonImage != null) buttonImage.sprite = eButtonSprite;
            if (promptKeyText != null) promptKeyText.text = "";
        }
        else
        {
            if (buttonImage != null && circleButtonSprite != null)
            {
                buttonImage.sprite = circleButtonSprite;
            }

            if (promptKeyText != null)
            {
                promptKeyText.text = label;
                promptKeyText.fontSize = (label.Length > 2) ? 26f : 54f;
            }
        }
    }

    private void PositionTargetRandomly()
    {
        if (targetRect == null) return;

        targetRect.SetAsLastSibling();

        float areaWidth = 800f;
        float areaHeight = 500f;

        if (spawnArea != null && spawnArea.rect.width > 250f && spawnArea.rect.height > 250f)
        {
            areaWidth = spawnArea.rect.width;
            areaHeight = spawnArea.rect.height;
        }
        else
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.GetComponent<RectTransform>() != null)
            {
                Rect canvasRect = parentCanvas.GetComponent<RectTransform>().rect;
                areaWidth = canvasRect.width * 0.7f;
                areaHeight = canvasRect.height * 0.7f;
            }
        }

        float targetWidth = targetRect.rect.width > 0f ? targetRect.rect.width : 160f;
        float targetHeight = targetRect.rect.height > 0f ? targetRect.rect.height : 160f;

        float halfW = Mathf.Max(0f, (areaWidth - targetWidth) * 0.5f);
        float halfH = Mathf.Max(0f, (areaHeight - targetHeight) * 0.5f);

        float randomX = UnityEngine.Random.Range(-halfW, halfW);
        float randomY = UnityEngine.Random.Range(-halfH, halfH);

        targetRect.anchoredPosition = new Vector2(randomX, randomY);
    }

    private void Update()
    {
        if (!isActive || isHandlingHitOrMiss) return;

        // Radial countdown timer
        targetTimer -= Time.unscaledDeltaTime;
        float normalizedTime = Mathf.Clamp01(targetTimer / timePerTarget);

        if (timerRingImage != null)
        {
            timerRingImage.fillAmount = normalizedTime;

            // Visual urgency: pulse warning amber in last 25% of timer
            if (normalizedTime < 0.25f)
            {
                float pulse = Mathf.PingPong(Time.unscaledTime * 12f, 1f);
                timerRingImage.color = Color.Lerp(normalRingColor, warningRingColor, pulse);
            }
            else
            {
                timerRingImage.color = normalRingColor;
            }
        }

        // Time ran out
        if (targetTimer <= 0f)
        {
            HandleMiss();
            return;
        }

        // 1. Mouse Click on circular target
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Camera eventCam = null;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCam = canvas.worldCamera;
            }

            if (targetRect != null && RectTransformUtility.RectangleContainsScreenPoint(targetRect, mousePos, eventCam))
            {
                OnTargetClicked();
                return;
            }
        }

        // 2. Keyboard Key Press matching current prompt key
        if (Keyboard.current != null && Keyboard.current[currentPromptKey].wasPressedThisFrame)
        {
            OnTargetClicked();
        }
    }

    public void OnTargetClicked()
    {
        if (!isActive || isHandlingHitOrMiss) return;

        currentHits++;
        UpdateProgressUI();

        bool isHammerQTE = BookshelfSceneController.Instance != null && BookshelfSceneController.Instance.IsQTERunning;
        if (isHammerQTE)
        {
            AudioManager.PlayHammerHit();
        }
        else
        {
            AudioManager.PlayQTEHit();
        }

        StartCoroutine(HitFeedbackRoutine(currentHits >= requiredHits));
    }

    private IEnumerator HitFeedbackRoutine(bool isFinalHit)
    {
        isHandlingHitOrMiss = true;

        // Flash green and pop scale
        if (timerRingImage != null)
        {
            timerRingImage.color = successRingColor;
        }

        if (targetRect != null)
        {
            targetRect.localScale = Vector3.one * 1.15f;
        }

        yield return new WaitForSecondsRealtime(0.09f);

        if (targetRect != null)
        {
            targetRect.localScale = Vector3.one;
        }

        if (isFinalHit)
        {
            CompleteQTE(true);
        }
        else
        {
            SpawnNextTarget();
        }
    }

    private void HandleMiss()
    {
        if (isHandlingHitOrMiss) return;
        currentMisses++;
        AudioManager.PlayQTEMiss();

        StartCoroutine(MissFeedbackRoutine(failOnMiss || currentMisses >= maxMisses));
    }

    private IEnumerator MissFeedbackRoutine(bool isGameOver)
    {
        isHandlingHitOrMiss = true;

        if (timerRingImage != null)
        {
            timerRingImage.color = failRingColor;
            timerRingImage.fillAmount = 1f;
        }

        // Quick shake
        if (targetRect != null)
        {
            Vector2 origPos = targetRect.anchoredPosition;
            targetRect.anchoredPosition = origPos + new Vector2(8f, 0f);
            yield return new WaitForSecondsRealtime(0.04f);
            targetRect.anchoredPosition = origPos + new Vector2(-8f, 0f);
            yield return new WaitForSecondsRealtime(0.04f);
            targetRect.anchoredPosition = origPos;
        }

        yield return new WaitForSecondsRealtime(0.06f);

        if (isGameOver)
        {
            CompleteQTE(false);
        }
        else
        {
            SpawnNextTarget();
        }
    }

    private void UpdateProgressUI()
    {
        if (progressText != null)
        {
            progressText.text = $"ANCHOR: {currentHits} / {requiredHits}";
        }
    }

    private void CompleteQTE(bool success)
    {
        isActive = false;
        isHandlingHitOrMiss = false;
        if (rootPanel != null) rootPanel.SetActive(false);

        if (success)
        {
            OnQTECompleted?.Invoke();
        }
        else
        {
            OnQTEFailed?.Invoke();
        }
    }

    public void CancelQTE()
    {
        isActive = false;
        isHandlingHitOrMiss = false;
        StopAllCoroutines();
        if (rootPanel != null) rootPanel.SetActive(false);
    }
}
