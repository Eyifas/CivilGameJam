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

    [Header("Earthquake Effect")]
    [SerializeField] private float shakeStrength = 10f;
    [SerializeField] private float rotationStrength = 0.6f;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "MainScene";

    private int currentStoryIndex = 0;
    private bool storyStarted = false;

    // Deprem
    private bool earthquakeActive = false;
    private Vector2 storyStartPosition;
    private Quaternion storyStartRotation;

    private void Awake()
    {
        ClearDontDestroyOnLoadObjects(excludeObject: gameObject);
    }

    public static void ClearDontDestroyOnLoadObjects(GameObject excludeObject = null)
    {
        // 1. Explicitly destroy known persistent singletons
        if (GameTimelineManager.Instance != null &&
            GameTimelineManager.Instance.gameObject != excludeObject)
        {
            Destroy(GameTimelineManager.Instance.gameObject);
        }

        if (TimeRewindController.Instance != null &&
            TimeRewindController.Instance.gameObject != excludeObject)
        {
            Destroy(TimeRewindController.Instance.gameObject);
        }

        if (PersistentCanvas.Instance != null &&
            PersistentCanvas.Instance.gameObject != excludeObject)
        {
            Destroy(PersistentCanvas.Instance.gameObject);
        }

        if (QTEManager.Instance != null &&
            QTEManager.Instance.gameObject != excludeObject)
        {
            Destroy(QTEManager.Instance.gameObject);
        }

        if (HazardVisualManager.Instance != null &&
            HazardVisualManager.Instance.gameObject != excludeObject)
        {
            Destroy(HazardVisualManager.Instance.gameObject);
        }

        if (ApartmentExitDoor.Instance != null &&
            ApartmentExitDoor.Instance.gameObject != excludeObject)
        {
            Destroy(ApartmentExitDoor.Instance.gameObject);
        }

        if (MainSceneDolap.Instance != null &&
            MainSceneDolap.Instance.gameObject != excludeObject)
        {
            Destroy(MainSceneDolap.Instance.gameObject);
        }

        if (VictorySuccessDisplay.Instance != null &&
            VictorySuccessDisplay.Instance.gameObject != excludeObject)
        {
            Destroy(VictorySuccessDisplay.Instance.gameObject);
        }

        // 2. Destroy everything currently in DontDestroyOnLoad scene
        try
        {
            GameObject probe = new GameObject("DDOL_Cleanup_Probe");
            DontDestroyOnLoad(probe);

            var ddolScene = probe.scene;
            var rootObjects = ddolScene.GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                if (root != null &&
                    root != excludeObject &&
                    root != probe &&
                    (AudioManager.Instance == null ||
                     root != AudioManager.Instance.gameObject))
                {
                    Destroy(root);
                }
            }

            Destroy(probe);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning(
                $"[StartMenuController] DDOL sweep exception: {ex.Message}"
            );
        }

        // 3. Reset static state
        InventoryManager.ResetInventory();
        InventorySelection.ResetSelection();
        PlayerMovement.ClearSavedPosition();
        SwitchInteraction.IsAnyHazardActive = false;

        Time.timeScale = 1f;

        Debug.Log(
            "<color=cyan>[StartMenuController] Destroyed all DontDestroyOnLoad objects.</color>"
        );
    }

    private void Start()
    {
        if (storyImage != null)
        {
            storyImage.gameObject.SetActive(false);

            storyStartPosition =
                storyImage.rectTransform.anchoredPosition;

            storyStartRotation =
                storyImage.rectTransform.localRotation;
        }

        SetupButtonSounds();
    }

    private void SetupButtonSounds()
    {
        if (startButton != null &&
            startButton.TryGetComponent<Button>(out var sBtn))
        {
            sBtn.onClick.AddListener(() =>
                AudioManager.PlayUIClick()
            );
        }

        if (exitButton != null &&
            exitButton.TryGetComponent<Button>(out var eBtn))
        {
            eBtn.onClick.AddListener(() =>
                AudioManager.PlayUIClick()
            );
        }
    }

    private void Update()
    {
        if (!storyStarted)
        {
            return;
        }

        bool pressed =
            (Mouse.current != null &&
             Mouse.current.leftButton.wasPressedThisFrame)
            ||
            (Keyboard.current != null &&
             Keyboard.current.anyKey.wasPressedThisFrame);

        if (pressed)
        {
            ShowNextStoryImage();
        }
    }

    private void LateUpdate()
{
    if (storyImage == null || !storyImage.gameObject.activeSelf)
    {
        return;
    }

    if (earthquakeActive)
    {
        // 3. görsel = index 2
        // 6. görsel = index 5
        // Şiddet 3'ten 6'ya giderek artar
        float progress = Mathf.InverseLerp(
            2f,
            5f,
            currentStoryIndex
        );

        // 3. görselde %25 güçle başlasın,
        // 6. görselde %100 güce ulaşsın
        float intensity = Mathf.Lerp(
            0.25f,
            1f,
            progress
        );

        float currentShake =
            shakeStrength * intensity;

        float currentRotation =
            rotationStrength * intensity;

        Vector2 shakeOffset =
            Random.insideUnitCircle * currentShake;

        storyImage.rectTransform.anchoredPosition =
            storyStartPosition + shakeOffset;

        float rotation =
            Random.Range(
                -currentRotation,
                currentRotation
            );

        storyImage.rectTransform.localRotation =
            Quaternion.Euler(0f, 0f, rotation);
    }
    else
    {
        storyImage.rectTransform.anchoredPosition =
            storyStartPosition;

        storyImage.rectTransform.localRotation =
            storyStartRotation;
    }
}

    public void StartGame()
    {
        if (storySprites == null ||
            storySprites.Length == 0)
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        storyStarted = true;
        currentStoryIndex = 0;
        earthquakeActive = false;

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
            storyImage.sprite =
                storySprites[currentStoryIndex];

            FitStoryImageToScreen();
        }
    }

    private void ShowNextStoryImage()
    {
        currentStoryIndex++;

        // Tüm görseller bitti
        if (currentStoryIndex >= storySprites.Length)
        {
            storyStarted = false;

            // Son görsel kapandığında deprem durur
            StopEarthquakeEffect();

            AudioManager.PlayUIClick();

            SceneManager.LoadScene(gameSceneName);
            return;
        }

        AudioManager.PlayPageTurn();

        if (storyImage != null)
        {
            storyImage.sprite =
                storySprites[currentStoryIndex];

            FitStoryImageToScreen();
        }

        // 0 = 1. görsel
        // 1 = 2. görsel
        // 2 = 3. görsel
        //
        // Yani 3. görsel açıldığı anda deprem başlar.
        if (currentStoryIndex >= 2)
        {
            earthquakeActive = true;
        }
    }

    private void StopEarthquakeEffect()
    {
        earthquakeActive = false;

        if (storyImage != null)
        {
            storyImage.rectTransform.anchoredPosition =
                storyStartPosition;

            storyImage.rectTransform.localRotation =
                storyStartRotation;
        }
    }

    private void FitStoryImageToScreen()
    {
        if (storyImage == null)
        {
            return;
        }

        RectTransform rect = storyImage.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        rect.localScale = Vector3.one;

        // Depremin referans alacağı doğru konumu kaydet
        storyStartPosition = rect.anchoredPosition;
        storyStartRotation = rect.localRotation;
    }

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}