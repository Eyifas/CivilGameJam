using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Test script to verify the World Choice System in action.
/// Press '4' to trigger the Stage 2 Earthquake choice scenario.
/// </summary>
public class WorldChoiceTester : MonoBehaviour
{
    [SerializeField] private WorldChoiceGroup stage2ChoiceGroup;

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Press '4' to trigger the Stage 2 Earthquake Decision
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            if (stage2ChoiceGroup == null)
            {
                Debug.LogError("[WorldChoiceTester] stage2ChoiceGroup is not assigned!");
                return;
            }

            Debug.Log("[WorldChoiceTester] Earthquake hits! Choose [S] Drop under desk or [E] Run for door!");

            stage2ChoiceGroup.PresentChoices(
                headerTitle: "EARTHQUAKE STRIKES! WHAT DO YOU DO?",
                duration: 5.0f,
                onChoice: (actionId) =>
                {
                    if (actionId == "DropCover")
                    {
                        Debug.Log("<color=green>[SUCCESS] Player dove under sturdy desk! Safe from falling objects!</color>");
                    }
                    else if (actionId == "RunDoor")
                    {
                        Debug.Log("<color=red>[FAIL - REWIND] Player ran during violent shaking and was thrown against walls!</color>");
                    }
                },
                onTimeout: () =>
                {
                    Debug.Log("<color=red>[FAIL - TIMEOUT] Frozen in panic! Struck by falling hazards!</color>");
                }
            );
        }
    }
}

