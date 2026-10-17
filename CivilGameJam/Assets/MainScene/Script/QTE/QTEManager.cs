using System;
using UnityEngine;

/// <summary>
/// Central manager for Quick Time Events. Provides easy static/singleton access
/// to trigger either a Timing Bar QTE or a Mash Prompt QTE from any trigger or game manager.
/// </summary>
public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    [SerializeField] private QTETimingBar timingBar;
    [SerializeField] private QTEMashPrompt mashPrompt;
    [SerializeField] private QTETargetClick targetClick;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Starts a target click / reaction QTE (spawns targets in random positions to click/hit).
    /// </summary>
    public void StartTargetClickQTE(int targetCount = 3, float timeLimitPerTarget = 1.8f, Action onComplete = null, Action onFail = null)
    {
        if (targetClick == null)
        {
            Debug.LogError("QTEManager: QTETargetClick reference is missing!");
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
            Debug.LogError("QTEManager: QTETimingBar reference is missing!");
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
            Debug.LogError("QTEManager: QTEMashPrompt reference is missing!");
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

