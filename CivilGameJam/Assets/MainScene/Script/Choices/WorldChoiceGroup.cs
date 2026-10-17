using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class ChoiceOptionData
{
    public string actionId;                // Unique ID e.g. "DropCover", "RunDoor"
    public UnityEngine.InputSystem.Key key;// e.g. Key.S, Key.E
    public string label;                   // e.g. "Drop, Cover & Hold"
    public WorldChoicePrompt promptObject; // The prompt placed at the physical spot in world/UI
}

/// <summary>
/// Manages timed multi-choice scenarios by activating world-space buttons at their object locations.
/// Includes an urgency countdown timer.
/// </summary>
public class WorldChoiceGroup : MonoBehaviour
{
    [Header("UI Visuals")]
    [SerializeField] private GameObject timerDisplayRoot;
    [SerializeField] private Slider timerBar;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text headerTitleText; // e.g. "EARTHQUAKE STRIKES! MAKE YOUR DECISION!"

    [Header("Choice Prompts in this Group")]
    [SerializeField] private List<ChoiceOptionData> choiceOptions = new List<ChoiceOptionData>();

    // Callbacks
    public event Action<string> OnChoiceMade;
    public event Action OnChoiceTimedOut;

    private bool isActive = false;
    private float timeRemaining = 0f;
    private float totalDuration = 5f;

    private void Awake()
    {
        HideTimerAndPrompts();
    }

    private void Start()
    {
        if (!isActive)
        {
            HideTimerAndPrompts();
        }
    }

    /// <summary>
    /// Displays the choice prompts at their locations with a countdown timer.
    /// </summary>
    public void PresentChoices(string headerTitle, float duration, Action<string> onChoice, Action onTimeout = null)
    {
        // Make sure this choice group GameObject is active and on top of canvas
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (timerDisplayRoot != null && timerDisplayRoot != gameObject)
        {
            timerDisplayRoot.SetActive(true);
        }

        if (timerBar != null)
        {
            timerBar.gameObject.SetActive(true);
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
        }

        if (headerTitleText != null)
        {
            headerTitleText.gameObject.SetActive(true);
            headerTitleText.text = headerTitle;
        }
        totalDuration = duration;
        timeRemaining = duration;
        OnChoiceMade = onChoice;
        OnChoiceTimedOut = onTimeout;

        if (choiceOptions == null || choiceOptions.Count == 0)
        {
            Debug.LogWarning("[WorldChoiceGroup] No choice options configured in the list!");
        }

        Debug.Log($"[WorldChoiceGroup] PresentChoices called with {choiceOptions.Count} configured options.");

        for (int i = 0; i < choiceOptions.Count; i++)
        {
            var option = choiceOptions[i];
            if (option.promptObject != null)
            {
                Debug.Log($"[WorldChoiceGroup] Option [{i}]: ID='{option.actionId}', Key='{option.key}', PromptObject='{option.promptObject.name}'");
                option.promptObject.OnChoiceSelected -= HandleChoiceSelected;
                option.promptObject.OnChoiceSelected += HandleChoiceSelected;
                option.promptObject.ShowPrompt(option.actionId, option.key, option.label);
            }
            else
            {
                Debug.LogWarning($"[WorldChoiceGroup] Choice option at index {i} ({option.actionId}) has no Prompt Object assigned in the Inspector!");
            }
        }

        isActive = true;
        UpdateTimerVisuals();
    }

    private void Update()
    {
        if (!isActive) return;

        timeRemaining -= Time.unscaledDeltaTime;
        UpdateTimerVisuals();

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            HandleTimeout();
        }
    }

    private void UpdateTimerVisuals()
    {
        if (timerBar != null)
        {
            timerBar.value = Mathf.Clamp01(timeRemaining / totalDuration);
        }

        if (timerText != null)
        {
            timerText.text = $"{timeRemaining:F1}s";
        }
    }

    private void HandleChoiceSelected(string actionId)
    {
        if (!isActive) return;

        EndChoiceInteraction();
        OnChoiceMade?.Invoke(actionId);
    }

    private void HandleTimeout()
    {
        EndChoiceInteraction();
        OnChoiceTimedOut?.Invoke();
    }

    private void EndChoiceInteraction()
    {
        isActive = false;
        HideTimerAndPrompts();
    }

    public void HideTimerAndPrompts()
    {
        if (timerDisplayRoot != null && timerDisplayRoot != gameObject)
        {
            timerDisplayRoot.SetActive(false);
        }

        if (timerBar != null)
        {
            timerBar.gameObject.SetActive(false);
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
        }

        if (headerTitleText != null)
        {
            headerTitleText.gameObject.SetActive(false);
        }

        HideAllPrompts();
    }

    public void HideAllPrompts()
    {
        foreach (var option in choiceOptions)
        {
            if (option.promptObject != null)
            {
                option.promptObject.HidePromptImmediate();
            }
        }
    }
}

