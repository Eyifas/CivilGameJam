using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class StartMenuController : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private GameObject startButton;
    [SerializeField] private GameObject exitButton;

    [Header("Story")]
    [SerializeField] private Image storyImage;
    [SerializeField] private Sprite[] storySprites;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "MainScene";

    private int currentStoryIndex = 0;
    private bool storyStarted = false;

    private void Awake()
    {
        ClearDontDestroyOnLoadObjects(excludeObject: gameObject);
    }

    public static void ClearDontDestroyOnLoadObjects(GameObject excludeObject = null)
    {
        // 1. Explicitly destroy known persistent singletons
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.gameObject != excludeObject)
        {
            Destroy(GameTimelineManager.Instance.gameObject);
        }
        if (TimeRewindController.Instance != null && TimeRewindController.Instance.gameObject != excludeObject)
        {
            Destroy(TimeRewindController.Instance.gameObject);
        }
        if (PersistentCanvas.Instance != null && PersistentCanvas.Instance.gameObject != excludeObject)
        {
            Destroy(PersistentCanvas.Instance.gameObject);
        }
        if (QTEManager.Instance != null && QTEManager.Instance.gameObject != excludeObject)
        {
            Destroy(QTEManager.Instance.gameObject);
        }
        if (HazardVisualManager.Instance != null && HazardVisualManager.Instance.gameObject != excludeObject)
        {
            Destroy(HazardVisualManager.Instance.gameObject);
        }
        if (ApartmentExitDoor.Instance != null && ApartmentExitDoor.Instance.gameObject != excludeObject)
        {
            Destroy(ApartmentExitDoor.Instance.gameObject);
        }
        if (MainSceneDolap.Instance != null && MainSceneDolap.Instance.gameObject != excludeObject)
        {
            Destroy(MainSceneDolap.Instance.gameObject);
        }
        if (VictorySuccessDisplay.Instance != null && VictorySuccessDisplay.Instance.gameObject != excludeObject)
        {
            Destroy(VictorySuccessDisplay.Instance.gameObject);
        }

        // 2. Comprehensive sweep: Destroy EVERYTHING currently in the DontDestroyOnLoad scene
        try
        {
            GameObject probe = new GameObject("DDOL_Cleanup_Probe");
            DontDestroyOnLoad(probe);
            var ddolScene = probe.scene;
            var rootObjects = ddolScene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                if (root != null && root != excludeObject && root != probe && (AudioManager.Instance == null || root != AudioManager.Instance.gameObject))
                {
                    Destroy(root);
                }
            }
            Destroy(probe);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[StartMenuController] DDOL sweep exception: {ex.Message}");
        }

        // 3. Reset static state
        InventoryManager.ResetInventory();
        InventorySelection.ResetSelection();
        PlayerMovement.ClearSavedPosition();
        SwitchInteraction.IsAnyHazardActive = false;
        Time.timeScale = 1f;

        Debug.Log("<color=cyan>[StartMenuController] Destroyed all DontDestroyOnLoad objects.</color>");
    }

    private void Start()
    {
        if (storyImage != null)
        {
            storyImage.gameObject.SetActive(false);
        }

        SetupButtonSounds();
    }

    private void SetupButtonSounds()
    {
        if (startButton != null && startButton.TryGetComponent<Button>(out var sBtn))
        {
            sBtn.onClick.AddListener(() => AudioManager.PlayUIClick());
        }

        if (exitButton != null && exitButton.TryGetComponent<Button>(out var eBtn))
        {
            eBtn.onClick.AddListener(() => AudioManager.PlayUIClick());
        }
    }

    private void Update()
    {
        if (!storyStarted)
        {
            return;
        }

        bool pressed =
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame);

        if (pressed)
        {
            ShowNextStoryImage();
        }
    }

    public void StartGame()
    {
        AudioManager.PlayUIClick();

        if (storySprites == null || storySprites.Length == 0)
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        storyStarted = true;
        currentStoryIndex = 0;

        if (startButton != null)
        {
            startButton.SetActive(false);
        }

        if (exitButton != null)
        {
            exitButton.SetActive(false);
        }

        if (storyImage != null)
        {
            storyImage.gameObject.SetActive(true);
            storyImage.sprite = storySprites[currentStoryIndex];

            FitStoryImageToScreen();
        }
    }

    private void ShowNextStoryImage()
    {
        currentStoryIndex++;

        if (currentStoryIndex >= storySprites.Length)
        {
            AudioManager.PlayUIClick();
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        AudioManager.PlayPageTurn();

        if (storyImage != null)
        {
            storyImage.sprite = storySprites[currentStoryIndex];

            FitStoryImageToScreen();
        }
    }

    private void FitStoryImageToScreen()
    {
        RectTransform rect = storyImage.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        rect.localScale = Vector3.one;
    }

    public void ExitGame()
    {
        AudioManager.PlayUIClick();
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}