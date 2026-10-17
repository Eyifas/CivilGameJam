using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Target Click / Prompt Key QTE.
/// Spawns a target button in random positions with a randomized key prompt (e.g. Q, E, F, SPACE, R, etc.).
/// The target smoothly fades out over its time limit until it disappears (counting as a miss).
/// The player can either click the button with the mouse OR press the displayed key prompt before it fades out.
/// </summary>
public class QTETargetClick : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private RectTransform spawnArea;
    [SerializeField] private Button targetButton;
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private CanvasGroup targetCanvasGroup; // Controls the fade-out of the button and its contents
    [SerializeField] private TMP_Text promptKeyText;        // Shows current key (e.g. "E", "F", "SPACE")
    [SerializeField] private TMP_Text progressText;

    [Header("Target Settings")]
    [Tooltip("Time in seconds allowed to hit each target before it completely fades out.")]
    [SerializeField] private float timePerTarget = 1.8f;

    [Tooltip("Total targets player needs to hit to win.")]
    [SerializeField] private int requiredHits = 3;

    [Tooltip("If true, failing a target fails the entire QTE. If false, it spawns a new target.")]
    [SerializeField] private bool failOnMiss = false;
    [SerializeField] private int maxMisses = 2;

    [Header("Key Pool Configuration")]
    [SerializeField] private Key[] allowedKeys = new Key[]
    {
        Key.E,
        Key.F,
        Key.Q,
        Key.R,
        Key.Space
    };

    // Events
    public event Action OnQTECompleted;
    public event Action OnQTEFailed;

    // Internal state
    private bool isActive = false;
    private int currentHits = 0;
    private int currentMisses = 0;
    private float targetTimer = 0f;
    private Key currentPromptKey;

    private void Awake()
    {
        // Prevent background elements from blocking clicks
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

        // Auto-add CanvasGroup to targetButton if not already assigned
        if (targetCanvasGroup == null && targetButton != null)
        {
            targetCanvasGroup = targetButton.GetComponent<CanvasGroup>();
            if (targetCanvasGroup == null)
            {
                targetCanvasGroup = targetButton.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (targetButton != null)
        {
            targetButton.onClick.AddListener(OnTargetClicked);
        }
    }

    /// <summary>
    /// Starts the Target Click QTE.
    /// </summary>
    public void StartQTE(int targetCount = 3, float timeLimitPerTarget = 1.8f, Action onComplete = null, Action onFail = null)
    {
        requiredHits = targetCount;
        timePerTarget = timeLimitPerTarget;
        currentHits = 0;
        currentMisses = 0;

        OnQTECompleted = onComplete;
        OnQTEFailed = onFail;

        isActive = true;
        if (rootPanel != null) rootPanel.SetActive(true);

        SpawnNextTarget();
    }

    private void SpawnNextTarget()
    {
        targetTimer = timePerTarget;
        SetTargetAlpha(1f);
        PickRandomKey();
        PositionTargetRandomly();
        UpdateProgressUI();
    }

    private void PickRandomKey()
    {
        if (allowedKeys == null || allowedKeys.Length == 0)
        {
            currentPromptKey = Key.Space;
        }
        else
        {
            int index = UnityEngine.Random.Range(0, allowedKeys.Length);
            currentPromptKey = allowedKeys[index];
        }

        if (promptKeyText != null)
        {
            string label = currentPromptKey == Key.Space ? "SPACE" : currentPromptKey.ToString();
            promptKeyText.text = label;
        }
    }

    private void PositionTargetRandomly()
    {
        if (spawnArea == null || targetRect == null) return;

        targetRect.SetAsLastSibling();

        float areaWidth = spawnArea.rect.width;
        float areaHeight = spawnArea.rect.height;
        float targetWidth = targetRect.rect.width;
        float targetHeight = targetRect.rect.height;

        float minX = -areaWidth * 0.5f + targetWidth * 0.5f;
        float maxX = areaWidth * 0.5f - targetWidth * 0.5f;
        float minY = -areaHeight * 0.5f + targetHeight * 0.5f;
        float maxY = areaHeight * 0.5f - targetHeight * 0.5f;

        float randomX = UnityEngine.Random.Range(minX, maxX);
        float randomY = UnityEngine.Random.Range(minY, maxY);

        targetRect.anchoredPosition = new Vector2(randomX, randomY);
    }

    private void Update()
    {
        if (!isActive) return;

        // Smoothly fade target out over its lifetime
        targetTimer -= Time.unscaledDeltaTime;
        float normalizedLifetime = Mathf.Clamp01(targetTimer / timePerTarget);
        SetTargetAlpha(normalizedLifetime);

        // Time ran out and completely faded out
        if (targetTimer <= 0f)
        {
            HandleMiss();
            return;
        }

        // Check if the current random key was pressed
        if (Keyboard.current != null && Keyboard.current[currentPromptKey].wasPressedThisFrame)
        {
            OnTargetClicked();
        }
    }

    private void SetTargetAlpha(float alpha)
    {
        if (targetCanvasGroup != null)
        {
            targetCanvasGroup.alpha = alpha;
        }
    }

    public void OnTargetClicked()
    {
        if (!isActive) return;

        currentHits++;
        UpdateProgressUI();

        if (currentHits >= requiredHits)
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
        currentMisses++;
        if (failOnMiss || currentMisses >= maxMisses)
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
            progressText.text = $"{currentHits} / {requiredHits}";
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
