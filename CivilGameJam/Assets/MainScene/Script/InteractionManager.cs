using UnityEngine;
using System.Collections.Generic;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance;

    [SerializeField] private SpriteRenderer interactionPrompt;

    private HashSet<InteractionZone> activeZones = new HashSet<InteractionZone>();

    private void Awake()
    {
        Instance = this;

        if (interactionPrompt != null)
        {
            interactionPrompt.enabled = false;
        }
    }

    public void EnterZone(InteractionZone zone)
    {
        activeZones.Add(zone);
        UpdatePrompt();
    }

    public void ExitZone(InteractionZone zone)
    {
        activeZones.Remove(zone);
        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.enabled = activeZones.Count > 0;
        }
    }
}