using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// World-space choice prompt styled with the hand-drawn circular QTE visual aesthetic.
/// Features a radial timer ring (Image.FillMethod.Radial360) surrounding the button medallion
/// that drains clockwise as time counts down (identical to the bookshelf scene QTE).
/// Includes background track ring, center button medallion (QTE_Circle_Button / QTE_Button_E),
/// dark pill action badge, and responsive tactile punch animations.
/// Triggerable via either assigned keyboard key OR mouse click.
/// </summary>
public class WorldChoicePrompt : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI Visuals")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button promptButton;
    [SerializeField] private TMP_Text keyText;         // e.g. "S" or "E"
    [SerializeField] private TMP_Text actionLabelText; // e.g. "Drop & Cover"

    [Header("Key Binding")]
    [SerializeField] private Key triggerKey = Key.E;
    [SerializeField] private string actionIdentifier = "Desk_Cover";

    [Header("QTE Button Sprites")]
    [SerializeField] private Sprite circleButtonSprite;
    [SerializeField] private Sprite eButtonSprite;
    [SerializeField] private Sprite outerRingSprite;
    [SerializeField] private Sprite trackRingSprite;

    [Header("Colors")]
    [SerializeField] private Color normalRingColor = new Color(181f / 255f, 191f / 255f, 127f / 255f, 1f); // #B5BF7F Olive Green
    [SerializeField] private Color warningRingColor = new Color(243f / 255f, 156f / 255f, 18f / 255f, 1f); // #F39C12 Warning Amber
    [SerializeField] private Color dangerRingColor = new Color(231f / 255f, 76f / 255f, 60f / 255f, 1f);   // #E74C3C Urgent Red
    [SerializeField] private Color trackRingColor = new Color(0.18f, 0.20f, 0.24f, 0.40f);                 // Faint Ink Track

    // Visual Children
    private RectTransform rootRect;
    private Image timerRingImage;
    private Image trackRingImage;
    private Image buttonImage;
    private RectTransform labelBadgeRect;
    private Image labelBadgeBg;

    // State
    public event Action<string> OnChoiceSelected;
    private bool isPromptActive = false;
    private bool isInitialized = false;
    private Coroutine punchCoroutine;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        if (!isPromptActive)
        {
            HidePromptImmediate();
        }
    }

    private void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (promptButton == null) promptButton = GetComponent<Button>();

        ResolveSprites();
        BuildQTEVisualHierarchy();

        if (promptButton != null)
        {
            promptButton.onClick.RemoveListener(TriggerChoice);
            promptButton.onClick.AddListener(TriggerChoice);
        }
    }

    private void ResolveSprites()
    {
        if (outerRingSprite == null) outerRingSprite = HazardVisualManager.LoadHazardSprite("UI/QTE_Outer_Ring") ?? Resources.Load<Sprite>("UI/QTE_Outer_Ring");
        if (trackRingSprite == null) trackRingSprite = HazardVisualManager.LoadHazardSprite("UI/QTE_Ring_Track") ?? Resources.Load<Sprite>("UI/QTE_Ring_Track");
        if (circleButtonSprite == null) circleButtonSprite = HazardVisualManager.LoadHazardSprite("UI/QTE_Circle_Button") ?? Resources.Load<Sprite>("UI/QTE_Circle_Button");
        if (eButtonSprite == null) eButtonSprite = HazardVisualManager.LoadHazardSprite("UI/QTE_Button_E") ?? Resources.Load<Sprite>("UI/QTE_Button_E");
    }

    private void BuildQTEVisualHierarchy()
    {
        rootRect = GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.sizeDelta = new Vector2(160f, 160f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
        }

        // Clean up legacy ring object if present from previous implementation
        Transform oldRing = transform.Find("OuterRing_QTE");
        if (oldRing != null) Destroy(oldRing.gameObject);

        // Configure root graphic as invisible raycast receiver for button clicks
        var rootGraphic = GetComponent<Image>();
        if (rootGraphic == null) rootGraphic = gameObject.AddComponent<Image>();
        rootGraphic.color = Color.clear;
        rootGraphic.sprite = null;
        rootGraphic.raycastTarget = true;

        if (promptButton != null)
        {
            promptButton.targetGraphic = rootGraphic;
            promptButton.transition = Selectable.Transition.None;
        }

        // 1. Track Ring (Background ring) - Sibling Index 0
        Transform trackTransform = transform.Find("TrackRing_QTE");
        GameObject trackObj;
        if (trackTransform != null)
        {
            trackObj = trackTransform.gameObject;
        }
        else
        {
            trackObj = new GameObject("TrackRing_QTE");
            trackObj.transform.SetParent(transform, false);
            var rt = trackObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        trackRingImage = trackObj.GetComponent<Image>();
        if (trackRingImage == null) trackRingImage = trackObj.AddComponent<Image>();
        trackRingImage.sprite = trackRingSprite != null ? trackRingSprite : outerRingSprite;
        trackRingImage.color = trackRingColor;
        trackRingImage.preserveAspect = true;
        trackRingImage.raycastTarget = false;
        trackObj.transform.SetSiblingIndex(0);

        // 2. Timer Ring (Radial 360 Fill around the button) - Sibling Index 1
        Transform timerTransform = transform.Find("TimerRing_QTE");
        GameObject timerObj;
        if (timerTransform != null)
        {
            timerObj = timerTransform.gameObject;
        }
        else
        {
            timerObj = new GameObject("TimerRing_QTE");
            timerObj.transform.SetParent(transform, false);
            var rt = timerObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        timerRingImage = timerObj.GetComponent<Image>();
        if (timerRingImage == null) timerRingImage = timerObj.AddComponent<Image>();
        timerRingImage.sprite = outerRingSprite;
        timerRingImage.type = Image.Type.Filled;
        timerRingImage.fillMethod = Image.FillMethod.Radial360;
        timerRingImage.fillOrigin = (int)Image.Origin360.Top;
        timerRingImage.fillClockwise = true;
        timerRingImage.fillAmount = 1f;
        timerRingImage.color = normalRingColor;
        timerRingImage.preserveAspect = true;
        timerRingImage.raycastTarget = false;
        timerObj.transform.SetSiblingIndex(1);

        // 3. Center Circle Graphic - Sibling Index 2
        Transform circleTransform = transform.Find("CenterCircle_QTE");
        GameObject circleObj;
        if (circleTransform != null)
        {
            circleObj = circleTransform.gameObject;
        }
        else
        {
            circleObj = new GameObject("CenterCircle_QTE");
            circleObj.transform.SetParent(transform, false);
            var rt = circleObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        buttonImage = circleObj.GetComponent<Image>();
        if (buttonImage == null) buttonImage = circleObj.AddComponent<Image>();
        buttonImage.type = Image.Type.Simple;
        buttonImage.preserveAspect = true;
        buttonImage.color = Color.white;
        buttonImage.raycastTarget = false;
        circleObj.transform.SetSiblingIndex(2);

        // 4. Center Key Text (e.g. "S") - Sibling Index 3
        if (keyText != null)
        {
            keyText.transform.SetParent(transform, false);
            keyText.transform.SetSiblingIndex(3);
            var ktRect = keyText.GetComponent<RectTransform>();
            if (ktRect != null)
            {
                ktRect.anchorMin = Vector2.zero;
                ktRect.anchorMax = Vector2.one;
                ktRect.anchoredPosition = new Vector2(0f, -2f);
                ktRect.sizeDelta = Vector2.zero;
            }
        }

        // 5. Action Label Badge
        if (actionLabelText != null)
        {
            var badgeObj = actionLabelText.gameObject;
            labelBadgeRect = badgeObj.GetComponent<RectTransform>();
            if (labelBadgeRect != null)
            {
                labelBadgeRect.anchorMin = new Vector2(0.5f, 0.5f);
                labelBadgeRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelBadgeRect.pivot = new Vector2(0.5f, 0.5f);
                labelBadgeRect.anchoredPosition = new Vector2(0f, -100f);
                labelBadgeRect.sizeDelta = new Vector2(250f, 42f);
            }

            Transform bgTransform = transform.Find("BadgeBg_QTE");
            GameObject bgObj;
            if (bgTransform != null)
            {
                bgObj = bgTransform.gameObject;
            }
            else
            {
                bgObj = new GameObject("BadgeBg_QTE");
                bgObj.transform.SetParent(transform, false);
            }

            var bgRect = bgObj.GetComponent<RectTransform>();
            if (bgRect == null) bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = new Vector2(0f, -100f);
            bgRect.sizeDelta = new Vector2(260f, 44f);

            labelBadgeBg = bgObj.GetComponent<Image>();
            if (labelBadgeBg == null) labelBadgeBg = bgObj.AddComponent<Image>();
            if (labelBadgeBg != null)
            {
                labelBadgeBg.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
                labelBadgeBg.raycastTarget = false;
            }

            bgObj.transform.SetSiblingIndex(4);
            badgeObj.transform.SetSiblingIndex(5);

            actionLabelText.fontStyle = FontStyles.Bold;
            actionLabelText.fontSize = 17f;
            actionLabelText.alignment = TextAlignmentOptions.Center;
            actionLabelText.color = Color.white;
            actionLabelText.characterSpacing = 1.5f;
        }

        ApplyKeyVisuals();
    }

    private void ApplyKeyVisuals()
    {
        if (buttonImage == null) return;

        bool isKeyE = (triggerKey == Key.E);

        if (isKeyE && eButtonSprite != null)
        {
            buttonImage.sprite = eButtonSprite;
            buttonImage.color = Color.white;
            buttonImage.preserveAspect = true;
            if (keyText != null) keyText.gameObject.SetActive(false); // eButtonSprite already has the "E" drawn
        }
        else
        {
            buttonImage.sprite = circleButtonSprite != null ? circleButtonSprite : eButtonSprite;
            buttonImage.color = Color.white;
            buttonImage.preserveAspect = true;

            if (keyText != null)
            {
                keyText.gameObject.SetActive(true);
                keyText.text = triggerKey == Key.Space ? "SPC" : triggerKey.ToString();
                keyText.fontSize = 48f;
                keyText.fontStyle = FontStyles.Bold;
                keyText.color = new Color(0.18f, 0.18f, 0.18f, 1f); // Dark Charcoal ink
                keyText.alignment = TextAlignmentOptions.Center;
            }
        }
    }

    /// <summary>
    /// Updates the radial timer slider draining clockwise around the button medallion.
    /// Ratio ranges from 1.0 (full time) down to 0.0 (expired).
    /// </summary>
    public void UpdateTimerProgress(float ratio)
    {
        if (timerRingImage != null)
        {
            timerRingImage.fillAmount = ratio;

            if (ratio < 0.25f)
            {
                // Flashing urgency in the final 25% of time remaining!
                float pulse = Mathf.PingPong(Time.unscaledTime * 7f, 1f);
                timerRingImage.color = Color.Lerp(warningRingColor, dangerRingColor, pulse);
            }
            else if (ratio < 0.5f)
            {
                // Transitioning into warning amber
                timerRingImage.color = Color.Lerp(dangerRingColor, warningRingColor, (ratio - 0.25f) / 0.25f);
            }
            else
            {
                // Healthy olive green into warning amber
                timerRingImage.color = Color.Lerp(warningRingColor, normalRingColor, (ratio - 0.5f) * 2f);
            }
        }
    }

    public void ShowPrompt(string actionId, Key key, string label)
    {
        EnsureInitialized();

        actionIdentifier = actionId;
        triggerKey = key;

        ApplyKeyVisuals();
        UpdateTimerProgress(1f); // Reset slider ring to full

        if (actionLabelText != null)
        {
            actionLabelText.text = label.ToUpperInvariant();
        }

        isPromptActive = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        transform.localScale = baseScale;

        Debug.Log($"<color=cyan>[WorldChoicePrompt] Shown QTE radial button '{actionId}' (Key: {key}) at {transform.position}</color>");
    }

    public void HidePromptImmediate()
    {
        isPromptActive = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isPromptActive) return;

        if (Keyboard.current != null && Keyboard.current[triggerKey].wasPressedThisFrame)
        {
            TriggerChoice();
        }
    }

    public void TriggerChoice()
    {
        if (!isPromptActive) return;

        if (punchCoroutine != null) StopCoroutine(punchCoroutine);
        StartCoroutine(PunchFeedback(() =>
        {
            isPromptActive = false;
            OnChoiceSelected?.Invoke(actionIdentifier);
        }));
    }

    private IEnumerator PunchFeedback(Action onDone)
    {
        transform.localScale = baseScale * 0.88f;
        yield return new WaitForSecondsRealtime(0.08f);
        transform.localScale = baseScale * 1.05f;
        yield return new WaitForSecondsRealtime(0.06f);
        transform.localScale = baseScale;
        onDone?.Invoke();
    }

    // Pointer feedback
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isPromptActive) return;
        transform.localScale = baseScale * 1.06f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isPromptActive) return;
        transform.localScale = baseScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isPromptActive) return;
        transform.localScale = baseScale * 0.92f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPromptActive) return;
        transform.localScale = baseScale;
    }

    public string ActionIdentifier => actionIdentifier;
}
