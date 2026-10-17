using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// A world-space choice prompt that hovers directly over an object in the room
/// (e.g. '[S] Drop & Cover' under desk, or '[E] Run for Door' at the exit).
/// Can be triggered either by pressing its assigned keyboard key OR clicking on it with the mouse.
/// </summary>
public class WorldChoicePrompt : MonoBehaviour
{
    [Header("UI Visuals")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button promptButton;
    [SerializeField] private TMP_Text keyText;         // e.g. "S" or "E"
    [SerializeField] private TMP_Text actionLabelText; // e.g. "Drop & Cover"

    [Header("Key Binding")]
    [SerializeField] private Key triggerKey = Key.E;
    [SerializeField] private string actionIdentifier = "Desk_Cover";

    // Callbacks
    public event Action<string> OnChoiceSelected;

    private bool isPromptActive = false;
    private bool isInitialized = false;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        // Only hide on Start if it wasn't already activated by a ShowPrompt call
        if (!isPromptActive)
        {
            HidePromptImmediate();
        }
    }

    private void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (promptButton == null)
        {
            promptButton = GetComponent<Button>();
        }

        if (promptButton != null)
        {
            promptButton.onClick.RemoveListener(TriggerChoice);
            promptButton.onClick.AddListener(TriggerChoice);
        }
    }

    /// <summary>
    /// Configures and shows the world prompt.
    /// </summary>
    public void ShowPrompt(string actionId, Key key, string label)
    {
        EnsureInitialized();

        actionIdentifier = actionId;
        triggerKey = key;

        if (keyText != null)
        {
            keyText.text = key == Key.Space ? "SPACE" : key.ToString();
        }

        if (actionLabelText != null)
        {
            actionLabelText.text = label;
        }

        isPromptActive = true;
        gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        Debug.Log($"[WorldChoicePrompt] Activated '{gameObject.name}' for action: '{actionId}' (Key: {key}) at position: {transform.position}");
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

        // Check if the assigned keyboard key is pressed
        if (Keyboard.current != null && Keyboard.current[triggerKey].wasPressedThisFrame)
        {
            TriggerChoice();
        }
    }

    public void TriggerChoice()
    {
        if (!isPromptActive) return;

        Debug.Log($"[WorldChoicePrompt] Choice triggered: '{actionIdentifier}' (Key: {triggerKey}) on '{gameObject.name}'");
        isPromptActive = false;
        OnChoiceSelected?.Invoke(actionIdentifier);
    }

    public string ActionIdentifier => actionIdentifier;
}
