using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Attached to SceneManager in mini scenes (DrawerScene, BookshelfScene, TableScene).
/// Displays an on-screen prompt matching the room interaction buttons:
/// the button icon with the letter [E] and the text label next to it.
/// Players can click it with the mouse or press [E] / [Esc] to return to the room.
/// </summary>
public class ReturnToRoom : MonoBehaviour
{
    [Header("Scene Navigation")]
    [Tooltip("Target scene to return to when exiting")]
    [SerializeField] private string returnSceneName = "MainScene";

    [Header("Button Visual Setup")]
    [Tooltip("Text displayed next to the button icon (set in Inspector)")]
    [SerializeField] private string labelText = "Go Back";

    [Tooltip("Sprite for the button with the letter (defaults to E Button)")]
    [SerializeField] private Sprite buttonSprite;

    [Tooltip("Screen position (top-left by default)")]
    [SerializeField] private Vector2 position = new Vector2(25f, 20f);

    [Tooltip("Size of the button icon in pixels")]
    [SerializeField] private float iconSize = 42f;

    [Tooltip("Font size for the label text")]
    [SerializeField] private int fontSize = 18;

    [Tooltip("Default text color")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Hover text color")]
    [SerializeField] private Color hoverColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Draw a subtle background pill behind the button")]
    [SerializeField] private bool showBackground = true;

    [Tooltip("Whether to display the on-screen button")]
    [SerializeField] private bool showOnScreenButton = true;

    [Header("Optional Canvas Button")]
    [Tooltip("Optional Canvas Button reference. If assigned, clicks on this button will also trigger return.")]
    [SerializeField] private Button customCanvasButton;

    private bool canExit = false;
    private bool isReturning = false;

    // Runtime textures & styles
    private Texture2D iconTexture;
    private Texture2D pillBgNormal;
    private Texture2D pillBgHover;
    private GUIStyle labelStyle;
    private GUIStyle shadowStyle;

    private void Awake()
    {
        if (string.IsNullOrEmpty(returnSceneName))
        {
            returnSceneName = "MainScene";
        }

        LoadButtonTexture();

        if (customCanvasButton != null)
        {
            customCanvasButton.onClick.RemoveListener(ReturnToMain);
            customCanvasButton.onClick.AddListener(ReturnToMain);
        }
    }

    private void Start()
    {
        if (iconTexture == null)
        {
            LoadButtonTexture();
        }
    }

    private void LoadButtonTexture()
    {
        if (buttonSprite != null)
        {
            iconTexture = buttonSprite.texture;
            return;
        }

        // 1. Try loading from Resources
        var loadedSprite = Resources.Load<Sprite>("EButton");
        if (loadedSprite != null)
        {
            buttonSprite = loadedSprite;
            iconTexture = loadedSprite.texture;
            return;
        }

        var loadedTex = Resources.Load<Texture2D>("EButton");
        if (loadedTex != null)
        {
            iconTexture = loadedTex;
            return;
        }

        // 2. Try finding any loaded Sprite named "E Button"
        var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s.name.IndexOf("E Button", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                buttonSprite = s;
                iconTexture = s.texture;
                return;
            }
        }
    }

    private void Update()
    {
        if (isReturning) return;
        if (CupboardSceneController.Instance != null && CupboardSceneController.Instance.IsBusy) return;
        if (TableSceneController.Instance != null && TableSceneController.Instance.IsBusy) return;

        // Prevent immediate exit if player was holding [E] upon entering the scene
        if (!canExit)
        {
            if (Keyboard.current == null || !Keyboard.current.eKey.isPressed)
            {
                canExit = true;
            }
            return;
        }

        // Keyboard shortcuts: E or Escape
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReturnToMain();
            }
        }
    }

    /// <summary>
    /// Returns the player to the main room scene.
    /// Can be called by keyboard press, button click, or custom Canvas Button.
    /// </summary>
    public void ReturnToMain()
    {
        if (isReturning) return;

        // Cancel any active QTE so timers don't linger
        if (QTEManager.Instance != null)
        {
            QTEManager.Instance.CancelAll();
        }

        isReturning = true;
        string targetScene = string.IsNullOrEmpty(returnSceneName) ? "MainScene" : returnSceneName;
        Debug.Log($"[ReturnToRoom] Player exiting mini scene. Loading '{targetScene}'...");
        SceneManager.LoadScene(targetScene);
    }

    private void OnGUI()
    {
        if (!showOnScreenButton || customCanvasButton != null) return;
        if (CupboardSceneController.Instance != null && CupboardSceneController.Instance.IsBusy) return;
        if (TableSceneController.Instance != null && TableSceneController.Instance.IsBusy) return;

        EnsureStyles();

        // Calculate layout
        GUIContent textContent = new GUIContent(labelText);
        Vector2 textSize = labelStyle.CalcSize(textContent);
        float paddingX = 8f;
        float paddingY = 6f;
        float gap = 10f;
        float totalWidth = paddingX * 2 + iconSize + gap + textSize.x + 6f;
        float totalHeight = iconSize + paddingY * 2;

        Rect totalRect = new Rect(position.x, position.y, totalWidth, totalHeight);
        bool isHovered = totalRect.Contains(Event.current.mousePosition);

        // 1. Draw Background Pill (if enabled)
        if (showBackground)
        {
            Texture2D bg = isHovered ? pillBgHover : pillBgNormal;
            if (bg != null)
            {
                GUI.DrawTexture(totalRect, bg, ScaleMode.StretchToFill);
            }
        }

        // 2. Draw Button Icon (The button with the letter E)
        Rect iconRect = new Rect(position.x + paddingX, position.y + paddingY, iconSize, iconSize);
        if (iconTexture != null)
        {
            Color oldCol = GUI.color;
            if (isHovered)
            {
                GUI.color = new Color(1f, 1f, 1f, 1f);
                GUI.DrawTexture(new Rect(iconRect.x - 1, iconRect.y - 1, iconRect.width + 2, iconRect.height + 2), iconTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.color = new Color(0.95f, 0.95f, 0.95f, 0.95f);
                GUI.DrawTexture(iconRect, iconTexture, ScaleMode.ScaleToFit);
            }
            GUI.color = oldCol;
        }

        // 3. Draw Text next to the button
        Rect textRect = new Rect(position.x + paddingX + iconSize + gap, position.y + paddingY + (iconSize - textSize.y) * 0.5f, textSize.x + 10f, textSize.y);

        // Shadow
        Color prevColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(textRect.x + 1.5f, textRect.y + 1.5f, textRect.width, textRect.height), labelText, shadowStyle);

        // Text
        GUI.color = isHovered ? hoverColor : textColor;
        GUI.Label(textRect, labelText, labelStyle);
        GUI.color = prevColor;

        // 4. Click detection over the entire button area
        if (GUI.Button(totalRect, GUIContent.none, GUIStyle.none))
        {
            ReturnToMain();
        }
    }

    private void EnsureStyles()
    {
        if (labelStyle != null && pillBgNormal != null) return;

        pillBgNormal = MakeBorderedTexture(16, 16, new Color(0.08f, 0.10f, 0.14f, 0.65f), new Color(0.6f, 0.65f, 0.75f, 0.35f), 2);
        pillBgHover = MakeBorderedTexture(16, 16, new Color(0.15f, 0.18f, 0.25f, 0.92f), new Color(1.0f, 0.84f, 0.15f, 0.95f), 2);

        labelStyle = new GUIStyle();
        labelStyle.fontSize = fontSize;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.normal.textColor = textColor;
        labelStyle.alignment = TextAnchor.MiddleLeft;

        shadowStyle = new GUIStyle(labelStyle);
        shadowStyle.normal.textColor = Color.black;
    }

    private static Texture2D MakeBorderedTexture(int width, int height, Color fill, Color border, int borderWidth)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] cols = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < borderWidth || x >= width - borderWidth || y < borderWidth || y >= height - borderWidth;
                cols[y * width + x] = isBorder ? border : fill;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    private void OnDestroy()
    {
        if (pillBgNormal != null) Destroy(pillBgNormal);
        if (pillBgHover != null) Destroy(pillBgHover);
    }
}