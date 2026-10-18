using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EndSceneController : MonoBehaviour
{
    [Header("Story")]
    [SerializeField] private Image storyImage;
    [SerializeField] private Sprite[] storySprites;

    [Header("Scene")]
    [SerializeField] private string startSceneName = "StartScene";

    private int currentStoryIndex = 0;
    private bool storyFinished = false;

    private void Start()
    {
        // EndScene'e gelince oyun timerı artık çalışmasın
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.enabled = false;
        }

        // Story görselleri varsa ilkini göster
        if (storySprites != null && storySprites.Length > 0)
        {
            currentStoryIndex = 0;

            if (storyImage != null)
            {
                storyImage.gameObject.SetActive(true);
                storyImage.sprite = storySprites[currentStoryIndex];

                FitStoryImageToScreen();
            }
        }
        else
        {
            ReplayGame();
        }
    }

    private void Update()
    {
        if (storyFinished)
            return;

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

    private void ShowNextStoryImage()
    {
        currentStoryIndex++;

        // Bütün final görselleri bittiyse
        if (currentStoryIndex >= storySprites.Length)
        {
            storyFinished = true;

            // StoryImage burada kapatılmıyor.
            // StartScene yüklenene kadar son görsel ekranda kalıyor.
            ReplayGame();
            return;
        }

        if (storyImage != null)
        {
            storyImage.sprite = storySprites[currentStoryIndex];
            FitStoryImageToScreen();
        }
    }

    private void FitStoryImageToScreen()
    {
        if (storyImage == null)
            return;

        RectTransform rect = storyImage.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        rect.localScale = Vector3.one;
    }

    public void ReplayGame()
    {
        // Envanteri temizle
        InventoryManager.ResetInventory();

        // Oyuncunun kayıtlı pozisyonunu temizle
        PlayerMovement.ClearSavedPosition();

        // Eski timeline'ı kaldır
        if (GameTimelineManager.Instance != null)
        {
            Destroy(GameTimelineManager.Instance.gameObject);
        }

        // Persistent HUD varsa kaldır
        PersistentCanvas[] persistentCanvases =
            FindObjectsByType<PersistentCanvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (PersistentCanvas canvas in persistentCanvases)
        {
            Destroy(canvas.gameObject);
        }

        // Başlangıç ekranına dön
        SceneManager.LoadScene(startSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}