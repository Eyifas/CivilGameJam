using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Rapid Click / Button Mash QTE (e.g., Stage 3 Kitchen Cabinet, Stage 5 Falling Store Sign).
/// The player must mash click/key repeatedly to fill up a progress meter against decay.
/// Includes an overall countdown timer.
/// </summary>
public class QTEMashPrompt : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text timerText;

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
    private float currentProgress = 0f;
    private float timeRemaining = 0f;

    private void Awake()
    {
        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Starts the rapid mash QTE.
    /// </summary>
    /// <param name="promptMessage">Instruction message shown to the player.</param>
    /// <param name="duration">Duration in seconds allowed before failing.</param>
    /// <param name="clickPower">Progress gain per click (e.g. 0.08f = ~12-13 clicks to win without decay).</param>
    /// <param name="decay">Decay rate per second.</param>
    /// <param name="onComplete">Callback on success.</param>
    /// <param name="onFail">Callback on failure.</param>
    public void StartQTE(string promptMessage = "MASH TO HOLD!", float duration = 5f, float clickPower = 0.08f, float decay = 0.25f, Action onComplete = null, Action onFail = null)
    {
        timeLimit = duration;
        timeRemaining = duration;
        progressPerClick = clickPower;
        decayRatePerSecond = decay;
        currentProgress = 0.1f; // Give a slight initial head start

        OnQTECompleted = onComplete;
        OnQTEFailed = onFail;

        if (instructionText != null)
        {
            instructionText.text = promptMessage;
        }

        isActive = true;
        if (rootPanel != null) rootPanel.SetActive(true);

        UpdateVisuals();
    }

    private void Update()
    {
        if (!isActive) return;

        // Handle timer countdown
        timeRemaining -= Time.unscaledDeltaTime;
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            UpdateVisuals();
            CompleteQTE(false);
            return;
        }

        // Apply decay
        currentProgress -= decayRatePerSecond * Time.unscaledDeltaTime;
        currentProgress = Mathf.Clamp01(currentProgress);

        // Check for click/mash input
        bool inputTriggered = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame))
                           || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (inputTriggered)
        {
            currentProgress += progressPerClick;
            if (currentProgress >= 1f)
            {
                currentProgress = 1f;
                UpdateVisuals();
                CompleteQTE(true);
                return;
            }
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (progressSlider != null)
        {
            progressSlider.value = currentProgress;
        }

        if (timerText != null)
        {
            timerText.text = $"{timeRemaining:F1}s";
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

