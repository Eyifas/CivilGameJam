using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InteractionZone : MonoBehaviour
{
    [SerializeField] private string sceneName;
    [SerializeField] private GameObject interactionPrompt;

    private bool playerInside = false;
    private bool isInteractable = true;

    private void Awake()
    {
        FindPrompt();

        if (interactionPrompt != null)
        {
            if (interactionPrompt.TryGetComponent<Button>(out var btn))
            {
                btn.onClick.RemoveListener(OnInteractTriggered);
                btn.onClick.AddListener(OnInteractTriggered);
            }
            interactionPrompt.SetActive(false);
        }
    }

    private void Start()
    {
        CheckCompletionStatus();
    }

    private void OnEnable()
    {
        CheckCompletionStatus();
    }

    public void CheckCompletionStatus()
    {
        if (GameTimelineManager.Instance != null)
        {
            if (IsBookshelfZone() && GameTimelineManager.Instance.IsBookshelfAnchored)
            {
                DisableZonePermanently();
            }
        }
    }

    public bool IsBookshelfZone()
    {
        if (!string.IsNullOrEmpty(sceneName) && sceneName.IndexOf("Bookshelf", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (transform.parent != null && transform.parent.name.IndexOf("Bookshelf", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (name.IndexOf("Bookshelf", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    public void DisableZonePermanently()
    {
        isInteractable = false;
        playerInside = false;

        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        if (interactionPrompt == null) FindPrompt();
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }

        if (transform.parent != null)
        {
            var siblingPrompt = transform.parent.Find("InteractionPrompt");
            if (siblingPrompt == null) siblingPrompt = transform.parent.Find("E Button");
            if (siblingPrompt != null)
            {
                siblingPrompt.gameObject.SetActive(false);
            }
        }

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.ExitZone(this);
        }
    }

    private void FindPrompt()
    {
        if (interactionPrompt != null) return;

        // 1. Check direct children
        var child = transform.Find("InteractionPrompt");
        if (child == null) child = transform.Find("E Button");
        if (child != null)
        {
            interactionPrompt = child.gameObject;
            return;
        }

        // 2. Check siblings under the same parent (e.g. Drawer -> InteractionZone + InteractionPrompt)
        if (transform.parent != null)
        {
            var sibling = transform.parent.Find("InteractionPrompt");
            if (sibling == null) sibling = transform.parent.Find("E Button");
            if (sibling != null)
            {
                interactionPrompt = sibling.gameObject;
                return;
            }
        }
    }

    private void Update()
    {
        if (!isInteractable) return;

        // Stage guard: Only interactable during Stage 1
        if (GameTimelineManager.Instance != null)
        {
            if (GameTimelineManager.Instance.CurrentStage != GameStage.Stage1_Preparation)
            {
                SetInteractable(false);
                return;
            }

            if (IsBookshelfZone() && GameTimelineManager.Instance.IsBookshelfAnchored)
            {
                DisableZonePermanently();
                return;
            }
        }

        if (playerInside && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            OnInteractTriggered();
        }
    }

    public void OnInteractTriggered()
    {
        if (!isInteractable || !playerInside) return;

        if (IsBookshelfZone() && GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsBookshelfAnchored)
        {
            DisableZonePermanently();
            return;
        }

        if (!string.IsNullOrEmpty(sceneName))
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                PlayerMovement.SavePosition(player.transform.position);
            }

            SceneManager.LoadScene(sceneName);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isInteractable) return;

        if (GameTimelineManager.Instance != null)
        {
            if (GameTimelineManager.Instance.CurrentStage != GameStage.Stage1_Preparation)
                return;

            if (IsBookshelfZone() && GameTimelineManager.Instance.IsBookshelfAnchored)
            {
                DisableZonePermanently();
                return;
            }
        }

        if (other.CompareTag("Player"))
        {
            playerInside = true;

            if (interactionPrompt == null) FindPrompt();

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }

            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.EnterZone(this);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;

            if (interactionPrompt == null) FindPrompt();

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }

            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.ExitZone(this);
            }
        }
    }

    public void SetInteractable(bool active)
    {
        isInteractable = active;
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = active;
        }

        if (!active)
        {
            playerInside = false;
            if (interactionPrompt == null) FindPrompt();

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }

            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.ExitZone(this);
            }
        }
    }
}