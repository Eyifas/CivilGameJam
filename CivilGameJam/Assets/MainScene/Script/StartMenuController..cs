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

    private void Start()
    {
        if (storyImage != null)
        {
            storyImage.gameObject.SetActive(false);
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
            SceneManager.LoadScene(gameSceneName);
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
        RectTransform rect = storyImage.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        rect.localScale = Vector3.one;
    }

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}