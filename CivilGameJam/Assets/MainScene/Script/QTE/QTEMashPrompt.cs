using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Rapid Click / Button Mash QTE with circular hand-drawn visual style matching the game jam art direction.
/// (e.g. Stage 3 Kitchen Cabinet, Stage 5 Falling Street Sign).
/// The player must mash [E] / [SPACE] / click repeatedly to fill up the radial outer ring against decay.
/// Includes overall countdown timer and juicy tactile button punch animations.
/// </summary>
public class QTEMashPrompt : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text timerText;

    [Header("Circular Visual Hierarchy (Auto-Constructed)")]
    [SerializeField] private RectTransform circleContainer;
    [SerializeField] private Image trackRingImage;
    [SerializeField] private Image radialProgressRing;
    [SerializeField] private Image centerButtonImage;

    [Header("Sprites (Auto-Resolved)")]
    [SerializeField] private Sprite outerRingSprite;
    [SerializeField] private Sprite trackRingSprite;
    [SerializeField] private Sprite circleButtonSprite;
    [SerializeField] private Sprite eButtonSprite;

    [Header("Style Colors")]
    [SerializeField] private Color trackRingColor = new Color(181f / 255f, 191f / 255f, 127f / 255f, 0.25f);
    [SerializeField] private Color normalRingColor = new Color(181f / 255f, 191f / 255f, 127f / 255f, 1f); // #B5BF7F
    [SerializeField] private Color warningRingColor = new Color(243f / 255f, 156f / 255f, 18f / 255f, 1f); // #F39C12
    [SerializeField] private Color successRingColor = new Color(39f / 255f, 174f / 255f, 96f / 255f, 1f);  // #27AE60
    [SerializeField] private Color failRingColor = new Color(231f / 255f, 76f / 255f, 60f / 255f, 1f);     // #E74C3C

    [Header("Mash Settings")]
    [Tooltip("How much progress is added per valid click/press (0 to 1).")]
    [SerializeField] private float progressPerClick = 0.08f;

    [Tooltip("How much progress decays per second (0 to 1).")]
    [SerializeField] private float decayRatePerSecond = 0.25f;

    [Tooltip("Time limit to complete the mash before failure.")]
    [SerializeField] private float timeLimit = 5f;

    // Events
    public event Action OnQTECompleted;
    public event Action OnQTEFailed;

    // Internal state
    private bool isActive = false;
    public bool IsActive => isActive;
    private bool isEnding = false;
    private float currentProgress = 0f;
    private float timeRemaining = 0f;
    private Coroutine punchCoroutine;

    private void Awake()
    {
        EnsureVisualHierarchy();

        if (rootPanel == null)
        {
            rootPanel = gameObject;
        }

        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Loads or resolves a sprite with multiple fallback strategies.
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
    /// Dynamically constructs the circular radial mash prompt UI matching the hand-drawn drawing style.
    /// </summary>
    public void EnsureVisualHierarchy()
    {
        if (outerRingSprite == null) outerRingSprite = ResolveSprite("QTE_Outer_Ring.png");
        if (trackRingSprite == null) trackRingSprite = ResolveSprite("QTE_Ring_Track.png");
        if (circleButtonSprite == null) circleButtonSprite = ResolveSprite("QTE_Circle_Button.png");
        if (eButtonSprite == null) eButtonSprite = ResolveSprite("QTE_Button_E.png");

        if (rootPanel == null) rootPanel = gameObject;

        // Hide legacy linear slider if present so circular meter takes center stage
        if (progressSlider != null)
        {
            progressSlider.gameObject.SetActive(false);
        }

        // 1. Circle Container
        if (circleContainer == null && rootPanel != null)
        {
            Transform found = rootPanel.transform.Find("CircleContainer");
            if (found != null)
            {
                circleContainer = found.GetComponent<RectTransform>();
            }
            else
            {
                GameObject containerObj = new GameObject("CircleContainer");
                containerObj.transform.SetParent(rootPanel.transform, false);
                circleContainer = containerObj.AddComponent<RectTransform>();
                circleContainer.anchorMin = new Vector2(0.5f, 0.5f);
                circleContainer.anchorMax = new Vector2(0.5f, 0.5f);
                circleContainer.pivot = new Vector2(0.5f, 0.5f);
                circleContainer.anchoredPosition = new Vector2(0f, 0f);
                circleContainer.sizeDelta = new Vector2(180f, 180f);
            }
        }

        if (circleContainer != null)
        {
            circleContainer.sizeDelta = new Vector2(180f, 180f);
            circleContainer.pivot = new Vector2(0.5f, 0.5f);

            // Transparent Raycast Receiver Button on container
            var btn = circleContainer.GetComponent<Button>();
            if (btn == null) btn = circleContainer.gameObject.AddComponent<Button>();
            var btnImg = circleContainer.GetComponent<Image>();
            if (btnImg == null) btnImg = circleContainer.gameObject.AddComponent<Image>();
            btnImg.color = Color.clear;
            btnImg.raycastTarget = true;
            btn.targetGraphic = btnImg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnMashInputTriggered);

            // 2. Track Ring (Background ring)
            if (trackRingImage == null)
            {
                Transform found = circleContainer.Find("TrackRing");
                if (found != null)
                {
                    trackRingImage = found.GetComponent<Image>();
                }
                else
                {
                    GameObject trackObj = new GameObject("TrackRing");
                    trackObj.transform.SetParent(circleContainer, false);
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

            // 3. Radial Progress Ring (Fills clockwise from top as player mashes)
            if (radialProgressRing == null)
            {
                Transform found = circleContainer.Find("RadialRing");
                if (found != null)
                {
                    radialProgressRing = found.GetComponent<Image>();
                }
                else
                {
                    GameObject ringObj = new GameObject("RadialRing");
                    ringObj.transform.SetParent(circleContainer, false);
                    RectTransform rt = ringObj.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    radialProgressRing = ringObj.AddComponent<Image>();
                    radialProgressRing.raycastTarget = false;
                }
            }

            if (radialProgressRing != null)
            {
                radialProgressRing.sprite = outerRingSprite;
                radialProgressRing.type = Image.Type.Filled;
                radialProgressRing.fillMethod = Image.FillMethod.Radial360;
                radialProgressRing.fillOrigin = (int)Image.Origin360.Top;
                radialProgressRing.fillClockwise = true;
                radialProgressRing.fillAmount = 0.1f;
                radialProgressRing.color = normalRingColor;
                radialProgressRing.raycastTarget = false;
                radialProgressRing.transform.SetSiblingIndex(1);
            }

            // 4. Center Button Graphic (Iconic [E] prompt)
            if (centerButtonImage == null)
            {
                Transform found = circleContainer.Find("CenterButton");
                if (found != null)
                {
                    centerButtonImage = found.GetComponent<Image>();
                }
                else
                {
                    GameObject circleObj = new GameObject("CenterButton");
                    circleObj.transform.SetParent(circleContainer, false);
                    RectTransform rt = circleObj.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    centerButtonImage = circleObj.AddComponent<Image>();
                    centerButtonImage.raycastTarget = false;
                }
            }

            if (centerButtonImage != null)
            {
                centerButtonImage.sprite = eButtonSprite != null ? eButtonSprite : circleButtonSprite;
                centerButtonImage.type = Image.Type.Simple;
                centerButtonImage.preserveAspect = true;
                centerButtonImage.color = Color.white;
                centerButtonImage.raycastTarget = false;
                centerButtonImage.transform.SetSiblingIndex(2);
            }
        }

        // 5. Instruction text styling & positioning
        if (instructionText != null)
        {
            instructionText.fontStyle = FontStyles.Bold;
            instructionText.fontSize = 26f;
            instructionText.alignment = TextAlignmentOptions.Center;
            instructionText.color = Color.white;
            RectTransform itRect = instructionText.GetComponent<RectTransform>();
            if (itRect != null)
            {
                itRect.anchorMin = new Vector2(0.5f, 0.5f);
                itRect.anchorMax = new Vector2(0.5f, 0.5f);
                itRect.anchoredPosition = new Vector2(0f, 130f);
                itRect.sizeDelta = new Vector2(800f, 40f);
            }
        }

        // 6. Timer text styling & positioning
        if (timerText != null)
        {
            timerText.fontStyle = FontStyles.Bold;
            timerText.fontSize = 22f;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.color = new Color(1f, 0.88f, 0.4f, 1f);
            RectTransform ttRect = timerText.GetComponent<RectTransform>();
            if (ttRect != null)
            {
                ttRect.anchorMin = new Vector2(0.5f, 0.5f);
                ttRect.anchorMax = new Vector2(0.5f, 0.5f);
                ttRect.anchoredPosition = new Vector2(0f, -125f);
                ttRect.sizeDelta = new Vector2(300f, 32f);
            }
        }
    }

    /// <summary>
    /// Starts the rapid mash QTE with the circular radial visual presentation.
    /// </summary>
    public void StartQTE(string promptMessage = "MASH [E] OR CLICK RAPIDLY!", float duration = 5f, float clickPower = 0.08f, float decay = 0.25f, Action onComplete = null, Action onFail = null)
    {
        EnsureVisualHierarchy();

        timeLimit = duration;
        timeRemaining = duration;
        progressPerClick = clickPower;
        decayRatePerSecond = decay;
        currentProgress = 0.12f; // Initial small head start
        isEnding = false;

        OnQTECompleted = onComplete;
        OnQTEFailed = onFail;

        if (instructionText != null)
        {
            instructionText.text = promptMessage;
        }

        if (radialProgressRing != null)
        {
            radialProgressRing.color = normalRingColor;
            radialProgressRing.fillAmount = currentProgress;
        }

        if (circleContainer != null)
        {
            circleContainer.localScale = Vector3.one;
        }

        isActive = true;
        if (rootPanel != null) rootPanel.SetActive(true);

        UpdateVisuals();
    }

    private void Update()
    {
        if (!isActive || isEnding) return;

        // Handle countdown timer
        timeRemaining -= Time.unscaledDeltaTime;
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            UpdateVisuals();
            StartCoroutine(FailRoutine());
            return;
        }

        // Apply progress decay over time
        currentProgress -= decayRatePerSecond * Time.unscaledDeltaTime;
        currentProgress = Mathf.Clamp01(currentProgress);

        // Check for Keyboard [E], [SPACE], [W], or Mouse click
        bool inputTriggered = (Keyboard.current != null && (
                                Keyboard.current.eKey.wasPressedThisFrame ||
                                Keyboard.current.spaceKey.wasPressedThisFrame ||
                                Keyboard.current.wKey.wasPressedThisFrame))
                           || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (inputTriggered)
        {
            OnMashInputTriggered();
        }
        else
        {
            UpdateVisuals();
        }
    }

    /// <summary>
    /// Called when any valid mash input (key or mouse click) occurs.
    /// </summary>
    public void OnMashInputTriggered()
    {
        if (!isActive || isEnding) return;

        currentProgress += progressPerClick;

        // Trigger juicy button punch feedback
        if (punchCoroutine != null) StopCoroutine(punchCoroutine);
        punchCoroutine = StartCoroutine(PunchRoutine());

        if (currentProgress >= 1f)
        {
            currentProgress = 1f;
            UpdateVisuals();
            StartCoroutine(SuccessRoutine());
            return;
        }

        UpdateVisuals();
    }

    private IEnumerator PunchRoutine()
    {
        if (circleContainer == null) yield break;

        // Quick squash down then bounce overshoot
        circleContainer.localScale = Vector3.one * 0.92f;
        yield return new WaitForSecondsRealtime(0.035f);
        circleContainer.localScale = Vector3.one * 1.06f;
        yield return new WaitForSecondsRealtime(0.045f);
        circleContainer.localScale = Vector3.one;
    }

    private void UpdateVisuals()
    {
        if (radialProgressRing != null)
        {
            radialProgressRing.fillAmount = currentProgress;

            // Low progress warning pulse
            if (currentProgress < 0.25f)
            {
                float pulse = Mathf.PingPong(Time.unscaledTime * 10f, 1f);
                radialProgressRing.color = Color.Lerp(normalRingColor, warningRingColor, pulse);
            }
            else
            {
                radialProgressRing.color = normalRingColor;
            }
        }

        if (progressSlider != null)
        {
            progressSlider.value = currentProgress;
        }

        if (timerText != null)
        {
            timerText.text = $"{timeRemaining:F1}s";
        }
    }

    private IEnumerator SuccessRoutine()
    {
        isEnding = true;

        if (radialProgressRing != null)
        {
            radialProgressRing.color = successRingColor;
        }

        if (circleContainer != null)
        {
            circleContainer.localScale = Vector3.one * 1.15f;
        }

        yield return new WaitForSecondsRealtime(0.12f);

        if (circleContainer != null)
        {
            circleContainer.localScale = Vector3.one;
        }

        CompleteQTE(true);
    }

    private IEnumerator FailRoutine()
    {
        isEnding = true;

        if (radialProgressRing != null)
        {
            radialProgressRing.color = failRingColor;
        }

        // Quick shake
        if (circleContainer != null)
        {
            Vector2 orig = circleContainer.anchoredPosition;
            circleContainer.anchoredPosition = orig + new Vector2(8f, 0f);
            yield return new WaitForSecondsRealtime(0.04f);
            circleContainer.anchoredPosition = orig + new Vector2(-8f, 0f);
            yield return new WaitForSecondsRealtime(0.04f);
            circleContainer.anchoredPosition = orig;
        }

        yield return new WaitForSecondsRealtime(0.08f);

        CompleteQTE(false);
    }

    private void CompleteQTE(bool success)
    {
        isActive = false;
        isEnding = false;
        if (punchCoroutine != null) StopCoroutine(punchCoroutine);
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
        isEnding = false;
        if (punchCoroutine != null) StopCoroutine(punchCoroutine);
        if (rootPanel != null) rootPanel.SetActive(false);
    }
}
