using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Timing Bar QTE (e.g., Stage 1 3-hit L-bracket anchoring).
/// A pointer oscillates along a bar. The player must press input when the pointer
/// is inside the target success zone.
/// </summary>
public class QTETimingBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private RectTransform barContainer;
    [SerializeField] private RectTransform pointer;
    [SerializeField] private RectTransform targetZone;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text progressText;

    [Header("QTE Settings")]
    [Tooltip("Oscillation speed of the pointer across the bar.")]
    [SerializeField] private float pointerSpeed = 1.5f;

    [Tooltip("Normalized width of the target zone (0.1 = 10% of total bar width).")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float targetZoneWidthNormalized = 0.2f;

    [Tooltip("Target normalized center position (0.5 = middle). If randomizeEveryHit is true, this moves.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float targetCenterNormalized = 0.5f;
    [SerializeField] private bool randomizeTargetEveryHit = true;

    [Header("Requirements")]
    [SerializeField] private int requiredHits = 3;
    [SerializeField] private int maxMisses = 3;
    [SerializeField] private bool failOnMiss = false;

    // Events
    public event Action OnQTECompleted;
    public event Action OnQTEFailed;

    // Internal state
    private bool isActive = false;
    public bool IsActive => isActive;
    private int currentHits = 0;
    private int currentMisses = 0;
    private float pointerNormalized = 0f;
    private bool movingForward = true;

    private void Awake()
    {
        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Starts the Timing Bar QTE.
    /// </summary>
    public void StartQTE(int targetHits = 3, Action onComplete = null, Action onFail = null)
    {
        requiredHits = targetHits;
        currentHits = 0;
        currentMisses = 0;
        pointerNormalized = 0f;
        movingForward = true;

        OnQTECompleted = onComplete;
        OnQTEFailed = onFail;

        isActive = true;
        if (rootPanel != null) rootPanel.SetActive(true);

        PositionTargetZone();
        UpdateUI();
    }

    private void Update()
    {
        if (!isActive) return;

        // Move pointer back and forth (PingPong)
        float delta = pointerSpeed * Time.unscaledDeltaTime;
        if (movingForward)
        {
            pointerNormalized += delta;
            if (pointerNormalized >= 1f)
            {
                pointerNormalized = 1f;
                movingForward = false;
            }
        }
        else
        {
            pointerNormalized -= delta;
            if (pointerNormalized <= 0f)
            {
                pointerNormalized = 0f;
                movingForward = true;
            }
        }

        UpdatePointerVisual();

        // Check Input: Space bar, W key, or Left Mouse Click
        bool inputTriggered = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame))
                           || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (inputTriggered)
        {
            EvaluateHit();
        }
    }

    private void EvaluateHit()
    {
        float halfZone = targetZoneWidthNormalized * 0.5f;
        float min = targetCenterNormalized - halfZone;
        float max = targetCenterNormalized + halfZone;

        bool hit = (pointerNormalized >= min && pointerNormalized <= max);

        if (hit)
        {
            currentHits++;
            if (randomizeTargetEveryHit)
            {
                // Place target randomly within 0.15 to 0.85 to keep it comfortably inside bounds
                targetCenterNormalized = UnityEngine.Random.Range(0.15f + halfZone, 0.85f - halfZone);
                PositionTargetZone();
            }

            UpdateUI();

            if (currentHits >= requiredHits)
            {
                CompleteQTE(true);
            }
        }
        else
        {
            currentMisses++;
            UpdateUI();

            if (failOnMiss && currentMisses >= maxMisses)
            {
                CompleteQTE(false);
            }
        }
    }

    private void PositionTargetZone()
    {
        if (barContainer == null || targetZone == null) return;

        float barWidth = barContainer.rect.width;
        float zoneWidth = barWidth * targetZoneWidthNormalized;

        targetZone.sizeDelta = new Vector2(zoneWidth, targetZone.sizeDelta.y);

        // Center is between -barWidth/2 and +barWidth/2
        float xPos = (targetCenterNormalized - 0.5f) * barWidth;
        targetZone.anchoredPosition = new Vector2(xPos, targetZone.anchoredPosition.y);
    }

    private void UpdatePointerVisual()
    {
        if (barContainer == null || pointer == null) return;

        float barWidth = barContainer.rect.width;
        float xPos = (pointerNormalized - 0.5f) * barWidth;
        pointer.anchoredPosition = new Vector2(xPos, pointer.anchoredPosition.y);
    }

    private void UpdateUI()
    {
        if (progressText != null)
        {
            progressText.text = $"Progress: {currentHits}/{requiredHits}";
        }
        if (instructionText != null && string.IsNullOrEmpty(instructionText.text))
        {
            instructionText.text = "Press SPACE or Click when inside the zone!";
        }
    }

    private void CompleteQTE(bool success)
    {
        isActive = false;
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
        if (rootPanel != null) rootPanel.SetActive(false);
    }
}

