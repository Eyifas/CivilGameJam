using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FlashlightController : MonoBehaviour
{
    [SerializeField] private Light2D flashlightLight;

    private void Update()
    {
        if (flashlightLight == null)
        {
            return;
        }

        bool hasFlashlight = InventoryManager.HasItem("Flashlight");

        bool isDark =
            GameTimelineManager.Instance != null &&
            GameTimelineManager.Instance.CurrentStage == GameStage.Stage3_Utilities;

        bool flashlightSelected =
            InventorySelection.SelectedItem == "Flashlight";

        flashlightLight.enabled =
            hasFlashlight &&
            isDark &&
            flashlightSelected;
    }
}