using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages in-world interaction prompts (e.g. "InteractionPrompt", "E Button").
/// Guarantees that all prompts are hidden at startup, shown only when the player
/// is directly inside the object's trigger zone, and cleanly disabled when time expires.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [SerializeField] private SpriteRenderer interactionPrompt; // Optional global prompt

    private List<GameObject> allScenePrompts = new List<GameObject>();
    private HashSet<InteractionZone> activeZones = new HashSet<InteractionZone>();
    private bool isSystemActive = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-find all "InteractionPrompt" and "E Button" objects in the scene and hide them immediately
        CollectAndHideAllPrompts();
    }

    public void CollectAndHideAllPrompts()
    {
        allScenePrompts.Clear();

        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            if (t != null && (t.name.Equals("InteractionPrompt", System.StringComparison.OrdinalIgnoreCase) ||
                              t.name.Equals("E Button", System.StringComparison.OrdinalIgnoreCase)))
            {
                allScenePrompts.Add(t.gameObject);
                t.gameObject.SetActive(false);
            }
        }

        if (interactionPrompt != null)
        {
            interactionPrompt.enabled = false;
        }
    }

    public void EnterZone(InteractionZone zone)
    {
        if (!isSystemActive) return;

        activeZones.Add(zone);
        UpdateGlobalPrompt();
    }

    public void ExitZone(InteractionZone zone)
    {
        activeZones.Remove(zone);
        UpdateGlobalPrompt();
    }

    private void UpdateGlobalPrompt()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.enabled = isSystemActive && activeZones.Count > 0;
        }
    }

    /// <summary>
    /// Called when Stage 1 time expires (Earthquake strikes).
    /// Hides and locks all interaction prompts across the scene.
    /// </summary>
    public void DisableAllPrompts()
    {
        isSystemActive = false;
        activeZones.Clear();

        for (int i = 0; i < allScenePrompts.Count; i++)
        {
            if (allScenePrompts[i] != null)
            {
                allScenePrompts[i].SetActive(false);
            }
        }

        if (interactionPrompt != null)
        {
            interactionPrompt.enabled = false;
        }
    }

    /// <summary>
    /// Called when the timeline rewinds back to Stage 1.
    /// </summary>
    public void ResetPromptsSystem()
    {
        isSystemActive = true;
        activeZones.Clear();
        CollectAndHideAllPrompts();
    }
}