using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attached to dolap mutfak1/InteractionZone in MainScene.
/// When the player touches its 2D collider, smoothly transitions to CupboardScene
/// where the falling cabinet door QTE sequence begins.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CupboardTriggerZone : MonoBehaviour
{
    [SerializeField] private string targetScene = "CupboardScene";
    private bool isTriggered = false;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Start()
    {
        CheckStatus();
    }

    private void OnEnable()
    {
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.OnStateUpdated += CheckStatus;
            GameTimelineManager.Instance.OnStageChanged += HandleStageChanged;
        }
        CheckStatus();
    }

    private void OnDisable()
    {
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.OnStateUpdated -= CheckStatus;
            GameTimelineManager.Instance.OnStageChanged -= HandleStageChanged;
        }
    }

    private void HandleStageChanged(GameStage stage)
    {
        CheckStatus();
    }

    public void CheckStatus()
    {
        // Kitchen cupboard is only interactable after the earthquake (Stage 3 Utilities)!
        bool allowedStage = GameTimelineManager.Instance != null && GameTimelineManager.Instance.CurrentStage == GameStage.Stage3_Utilities;
        bool secured = GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsCabinetSecured;

        if (!allowedStage || secured)
        {
            isTriggered = true;
            if (TryGetComponent<Collider2D>(out var col))
            {
                col.enabled = false;
            }
        }
        else
        {
            isTriggered = false;
            if (TryGetComponent<Collider2D>(out var col))
            {
                col.enabled = true;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered) return;
        if (!other.CompareTag("Player")) return;

        // The kitchen should only be interactable after the earthquake (Stage 3 Utilities)!
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.CurrentStage != GameStage.Stage3_Utilities)
        {
            return;
        }

        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsCabinetSecured)
        {
            return;
        }

        isTriggered = true;
        Debug.Log("[CupboardTriggerZone] Player touched dolap mutfak1/InteractionZone in Stage 3! Loading CupboardScene...");

        // Save position so player returns right outside the cabinet
        PlayerMovement.LastSavedPosition = other.transform.position;

        SceneManager.LoadScene(targetScene);
    }
}
