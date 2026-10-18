using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FlashlightController : MonoBehaviour
{
    [SerializeField] private Light2D flashlightLight;

    private void Awake()
    {
        if (flashlightLight == null)
        {
            flashlightLight = GetComponent<Light2D>();
        }
    }

    private void Update()
    {
        if (flashlightLight == null)
        {
            return;
        }

        bool hasFlashlight = InventoryManager.HasItem("Flashlight") ||
            (GameTimelineManager.Instance != null && GameTimelineManager.Instance.HasFlashlight);

        bool isStage3 = GameTimelineManager.Instance != null &&
            (GameTimelineManager.Instance.CurrentStage == GameStage.Stage3_Utilities ||
             GameTimelineManager.Instance.CurrentStage == GameStage.Stage4_Stairwell);

        bool isGameActive = GameTimelineManager.Instance == null ||
            (GameTimelineManager.Instance.CurrentStage != GameStage.Victory &&
             GameTimelineManager.Instance.CurrentStage != GameStage.Rewinding &&
             GameTimelineManager.Instance.CurrentStage != GameStage.Stage1_Preparation &&
             GameTimelineManager.Instance.CurrentStage != GameStage.Stage2_Tremor &&
             !SwitchInteraction.IsAnyHazardActive);

        flashlightLight.enabled = hasFlashlight && isStage3 && isGameActive;
    }
}