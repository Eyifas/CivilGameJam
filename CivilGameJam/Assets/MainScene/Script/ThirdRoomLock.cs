using UnityEngine;

public class ThirdRoomLock : MonoBehaviour
{
    private Collider2D wallCollider;

    private void Awake()
    {
        wallCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (GameTimelineManager.Instance == null || wallCollider == null)
        {
            return;
        }

        GameStage stage = GameTimelineManager.Instance.CurrentStage;

        bool roomUnlocked =
            stage == GameStage.Stage3_Utilities ||
            stage == GameStage.Stage4_Stairwell ||
            stage == GameStage.Stage5_StreetFinale ||
            stage == GameStage.Victory;

        // Objeyi kapatmıyoruz!
        // Sadece duvarın collider'ını açıp kapatıyoruz.
        wallCollider.enabled = !roomUnlocked;
    }
}