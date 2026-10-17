using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central manager for Quick Time Events. Provides easy static/singleton access
/// to trigger either a Timing Bar QTE, a Mash Prompt QTE, or a Target Click QTE.
/// Persists across scenes with DontDestroyOnLoad.
/// </summary>
public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    [SerializeField] private QTETimingBar timingBar;
    [SerializeField] private QTEMashPrompt mashPrompt;
    [SerializeField] private QTETargetClick targetClick;

    public bool IsAnyQTEActive => (targetClick != null && targetClick.IsActive) ||
                                  (timingBar != null && timingBar.IsActive) ||
                                  (mashPrompt != null && mashPrompt.IsActive);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        FindQTEReferences();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindQTEReferences();
    }

    public void FindQTEReferences()
    {
        if (targetClick == null)
        {
            targetClick = FindFirstObjectByType<QTETargetClick>(FindObjectsInactive.Include);
        }

        if (mashPrompt == null)
        {
            mashPrompt = FindFirstObjectByType<QTEMashPrompt>(FindObjectsInactive.Include);
        }

        if (timingBar == null)
        {
            timingBar = FindFirstObjectByType<QTETimingBar>(FindObjectsInactive.Include);
        }
    }

    /// <summary>
    /// Starts a target click / reaction QTE (spawns targets in random positions to click/hit).
    /// </summary>
    public void StartTargetClickQTE(int targetCount = 3, float timeLimitPerTarget = 1.8f, Action onComplete = null, Action onFail = null)
    {
        if (targetClick == null)
        {
            FindQTEReferences();
        }

        if (targetClick == null)
        {
            Debug.LogError("[QTEManager] QTETargetClick reference is missing in the scene!");
            return;
        }

        targetClick.StartQTE(targetCount, timeLimitPerTarget, onComplete, onFail);
    }

    /// <summary>
    /// Starts a timing bar QTE (e.g. Bookshelf L-bracket).
    /// </summary>
    public void StartTimingQTE(int hitsNeeded, Action onComplete, Action onFail = null)
    {
        if (timingBar == null)
        {
            FindQTEReferences();
        }

        if (timingBar == null)
        {
            Debug.LogError("[QTEManager] QTETimingBar reference is missing!");
            return;
        }

        timingBar.StartQTE(hitsNeeded, onComplete, onFail);
    }

    /// <summary>
    /// Starts a mash meter QTE (e.g. loose cabinet or falling sign).
    /// </summary>
    public void StartMashQTE(string promptText, float duration, float clickPower, float decayRate, Action onComplete, Action onFail)
    {
        if (mashPrompt == null)
        {
            FindQTEReferences();
        }

        if (mashPrompt == null)
        {
            Debug.LogError("[QTEManager] QTEMashPrompt reference is missing!");
            return;
        }

        mashPrompt.StartQTE(promptText, duration, clickPower, decayRate, onComplete, onFail);
    }

    public void CancelAll()
    {
        if (targetClick != null) targetClick.CancelQTE();
        if (timingBar != null) timingBar.CancelQTE();
        if (mashPrompt != null) mashPrompt.CancelQTE();
    }
}
