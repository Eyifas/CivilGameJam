using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach this script to any GameObject (or the Canvas) in your scene to test
/// both QTE types directly using keyboard hotkeys in Play Mode.
/// </summary>
public class QTETester : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current == null) return;

        // Press '1' to test Timing Bar QTE (3-hit L-bracket anchoring)
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            Debug.Log("[QTETester] Starting Timing Bar QTE (3 hits)...");
            QTEManager.Instance.StartTimingQTE(
                hitsNeeded: 3,
                onComplete: () => Debug.Log("<color=green>[QTETester] Timing Bar SUCCESS! Anchored safely!</color>"),
                onFail: () => Debug.Log("<color=red>[QTETester] Timing Bar FAILED!</color>")
            );
        }

        // Press '2' to test Mash Meter QTE (Cabinet / Sign mash)
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            Debug.Log("[QTETester] Starting Mash QTE (5s timer)...");
            QTEManager.Instance.StartMashQTE(
                promptText: "MASH RAPIDLY TO SECURE THE CABINET!",
                duration: 5.0f,
                clickPower: 0.09f, // ~11 rapid clicks needed to win
                decayRate: 0.22f,  // 22% decay per second
                onComplete: () => Debug.Log("<color=green>[QTETester] Mash Prompt SUCCESS! Hazard avoided!</color>"),
                onFail: () => Debug.Log("<color=red>[QTETester] Mash Prompt FAILED! Hazard struck player!</color>")
            );
        }

        // Press '3' to test Target Click QTE (Random positions, click button or press Space/E)
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            Debug.Log("[QTETester] Starting Target Click QTE (3 targets, 2s each)...");
            QTEManager.Instance.StartTargetClickQTE(
                targetCount: 3,
                timeLimitPerTarget: 2.0f,
                onComplete: () => Debug.Log("<color=green>[QTETester] Target Click SUCCESS! All points hit!</color>"),
                onFail: () => Debug.Log("<color=red>[QTETester] Target Click FAILED!</color>")
            );
        }

        // Press 'Escape' to cancel any running QTE
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            QTEManager.Instance.CancelAll();
            Debug.Log("[QTETester] Cancelled all active QTEs.");
        }
    }
}

