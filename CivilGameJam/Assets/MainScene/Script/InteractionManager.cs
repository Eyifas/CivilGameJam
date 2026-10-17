using UnityEngine;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance;

    [SerializeField] private GameObject interactionPrompt;

    private int activeZones = 0;

    private void Awake()
    {
        Instance = this;

        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }

    public void EnterZone()
    {
        activeZones++;

        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(true);
        }
    }

    public void ExitZone()
    {
        activeZones--;

        if (activeZones < 0)
        {
            activeZones = 0;
        }

        if (activeZones == 0 && interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }
}