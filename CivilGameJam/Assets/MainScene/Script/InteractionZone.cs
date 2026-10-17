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
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.CurrentStage != GameStage.Stage1_Preparation)
        {
            SetInteractable(false);
            return;
        }

        if (playerInside && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            OnInteractTriggered();
        }
    }

    public void OnInteractTriggered()
    {
        if (!isInteractable || !playerInside) return;

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

        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.CurrentStage != GameStage.Stage1_Preparation)
            return;

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