using UnityEngine;

public class CabinetStageInteraction : MonoBehaviour
{
    [SerializeField] private InteractionZone interactionZone;

    private void Awake()
    {
        if (interactionZone == null)
        {
            interactionZone = GetComponent<InteractionZone>();
        }

        if (interactionZone != null)
        {
            interactionZone.SetInteractable(false);
        }
    }

    private void Update()
    {
        if (GameTimelineManager.Instance == null || interactionZone == null)
            return;

        bool canInteract =
            GameTimelineManager.Instance.CurrentStage == GameStage.Stage3_Utilities;

        interactionZone.SetInteractable(canInteract);
    }
}