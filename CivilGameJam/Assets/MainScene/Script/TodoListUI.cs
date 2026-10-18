using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Persistent on-screen To-Do List HUD using the custom wooden sign asset (to do lis.png).
/// - Position: Top & Center of the screen
/// - Size: 0.9x scale (477x184), preserving the wooden board aspect ratio
/// - Text: Centered neatly inside the lower tan parchment area (below the "TO DO" title header)
/// - Stage 1 (Preparation):
///     1. Anchor the bookshelf
///     2. Look around (requires exploring rooms & checking the drawers)
///     3. Address the hazards
/// - Stage 2 (Earthquake):
///     Disappears during the tremor.
/// - Stage 3 (Utilities till End of Game):
///     Appears again with:
///     1. Turn off the gas
///     2. Find your way out
/// </summary>
public class TodoListUI : MonoBehaviour
{
    public static TodoListUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Image panelBackground;
    [SerializeField] private TMP_Text tasksText;

    [Header("Sprite Asset")]
    [SerializeField] private Sprite todoBackgroundSprite;

    [Header("Look Around Exploration Tracking")]
    private static bool s_hasExploredRooms = false;
    private static bool s_hasCheckedDrawers = false;
    private static readonly HashSet<int> s_visitedRooms = new HashSet<int>();
    private static float s_totalDistanceMoved = 0f;
    private static Vector3 s_lastPlayerPos = Vector3.zero;
    private static bool s_hasLastPos = false;

    // Previous completion states for audio feedback
    private bool prevBookshelf = false;
    private bool prevLookAround = false;
    private bool prevHazards = false;
    private bool prevGas = false;
    private bool prevWayOut = false;

    // Layout configuration (0.9 scale: 477x184, Top & Center)
    private readonly Vector2 topCenterPos = new Vector2(0f, -15f);
    private readonly Vector2 cardSize = new Vector2(477f, 184f);
    private readonly Color backgroundColor = new Color(1f, 1f, 1f, 0.95f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnSceneLoadedRuntime()
    {
        EnsureTodoListUI();
    }

    public static void EnsureTodoListUI()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "StartScene")
        {
            if (Instance != null && Instance.panelRoot != null)
            {
                Instance.panelRoot.SetActive(false);
            }
            return;
        }

        if (Instance == null)
        {
            var existing = FindAnyObjectByType<TodoListUI>();
            if (existing != null)
            {
                Instance = existing;
                Instance.ApplyLayoutSettings();
                return;
            }

            // Prefer attaching to PersistentCanvas
            if (PersistentCanvas.Instance != null)
            {
                var comp = PersistentCanvas.Instance.GetComponent<TodoListUI>();
                if (comp == null) comp = PersistentCanvas.Instance.gameObject.AddComponent<TodoListUI>();
                Instance = comp;
                Instance.ApplyLayoutSettings();
                return;
            }

            // Fallback: Create standalone GameObject on active Canvas
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                var obj = new GameObject("TodoListUI");
                obj.transform.SetParent(canvas.transform, false);
                Instance = obj.AddComponent<TodoListUI>();
                Instance.ApplyLayoutSettings();
            }
        }
        else
        {
            Instance.ApplyLayoutSettings();
        }
    }

    public static void ResetExplorationState()
    {
        s_hasExploredRooms = false;
        s_hasCheckedDrawers = false;
        s_visitedRooms.Clear();
        s_totalDistanceMoved = 0f;
        s_hasLastPos = false;
    }

    public static void RegisterDrawerChecked()
    {
        s_hasCheckedDrawers = true;
    }

    public static bool CheckDrawersCondition()
    {
        if (s_hasCheckedDrawers) return true;

        if (InventoryManager.HasItem("Hammer") ||
            InventoryManager.HasItem("Vida") ||
            InventoryManager.HasItem("Screw") ||
            InventoryManager.HasItem("Nail") ||
            InventoryManager.HasItem("Nails") ||
            InventoryManager.HasItem("Flashlight") ||
            InventoryManager.HasItem("Anahtar"))
        {
            s_hasCheckedDrawers = true;
            return true;
        }

        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadBackgroundSprite();
        BuildUIHierarchy();
        ApplyLayoutSettings();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HookTimelineEvents();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnhookTimelineEvents();
    }

    private void Start()
    {
        HookTimelineEvents();
        ApplyLayoutSettings();
        RefreshTodoList();
    }

    private void HookTimelineEvents()
    {
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.OnStageChanged -= HandleStageChanged;
            GameTimelineManager.Instance.OnStageChanged += HandleStageChanged;

            GameTimelineManager.Instance.OnStateUpdated -= RefreshTodoList;
            GameTimelineManager.Instance.OnStateUpdated += RefreshTodoList;
        }
    }

    private void UnhookTimelineEvents()
    {
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.OnStageChanged -= HandleStageChanged;
            GameTimelineManager.Instance.OnStateUpdated -= RefreshTodoList;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookTimelineEvents();
        ApplyLayoutSettings();

        // In Stage 1: visiting mini-scenes triggers exploration progress
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.CurrentStage == GameStage.Stage1_Preparation)
        {
            if (scene.name == "DrawerScene")
            {
                s_hasCheckedDrawers = true;
            }
            if (scene.name == "BookshelfScene" || scene.name == "TableScene")
            {
                s_hasExploredRooms = true;
            }
        }

        RefreshTodoList();
    }

    private void HandleStageChanged(GameStage newStage)
    {
        RefreshTodoList();
    }

    public void ApplyLayoutSettings()
    {
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.sizeDelta = cardSize;
            panelRect.anchoredPosition = topCenterPos;
        }

        if (panelBackground != null)
        {
            panelBackground.color = backgroundColor;
        }

        if (tasksText != null)
        {
            tasksText.fontSize = 20f;
            tasksText.fontStyle = FontStyles.Bold;
            tasksText.color = new Color(0.15f, 0.08f, 0.03f, 1f);
            tasksText.alignment = TextAlignmentOptions.MidlineLeft;
            tasksText.textWrappingMode = TextWrappingModes.Normal;
            tasksText.richText = true;
            tasksText.lineSpacing = 1.5f;

            var tRect = tasksText.rectTransform;
            if (tRect != null)
            {
                tRect.anchorMin = new Vector2(0f, 0f);
                tRect.anchorMax = new Vector2(1f, 1f);
                tRect.pivot = new Vector2(0f, 0.5f);
                // Lower tan parchment area: cleanly below top "TO DO" title header (-86px) and above bottom rim (+26px)
                tRect.offsetMin = new Vector2(66f, 26f);
                tRect.offsetMax = new Vector2(-64f, -112f);
            }
        }
    }

    private void Update()
    {
        TrackPlayerExploration();
        RefreshTodoList();
    }

    private void TrackPlayerExploration()
    {
        var timeline = GameTimelineManager.Instance;
        if (timeline == null || timeline.CurrentStage != GameStage.Stage1_Preparation) return;

        if (!s_hasExploredRooms)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                var pm = FindAnyObjectByType<PlayerMovement>();
                if (pm != null) player = pm.gameObject;
            }

            if (player != null)
            {
                Vector3 pos = player.transform.position;

                if (s_hasLastPos)
                {
                    float dist = Vector3.Distance(pos, s_lastPlayerPos);
                    if (dist > 0.001f && dist < 10f)
                    {
                        s_totalDistanceMoved += dist;
                    }
                }
                s_lastPlayerPos = pos;
                s_hasLastPos = true;

                // Room detection based on leftEdge = -38.4f, roomWidth = 19.2f
                const float leftEdge = -38.4f;
                const float roomWidth = 19.2f;
                int room = Mathf.FloorToInt((pos.x - leftEdge) / roomWidth);
                room = Mathf.Clamp(room, 0, 2);
                s_visitedRooms.Add(room);

                if (s_visitedRooms.Count >= 2 || s_totalDistanceMoved >= 12f)
                {
                    s_hasExploredRooms = true;
                }
            }
        }
    }

    public void RefreshTodoList()
    {
        var timeline = GameTimelineManager.Instance;
        string activeScene = SceneManager.GetActiveScene().name;

        // Disappear in StartScene, during active earthquake (Stage 2 Tremor), or during Rewind
        if (timeline == null || activeScene == "StartScene")
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            return;
        }

        GameStage stage = timeline.CurrentStage;

        // User requirement: "after that it will disappear during the earthquake"
        if (stage == GameStage.Stage2_Tremor || stage == GameStage.Rewinding)
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            return;
        }

        // Stage 1 (Preparation)
        if (stage == GameStage.Stage1_Preparation)
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            RenderStage1();
            return;
        }

        // Stage 3 & Beyond (Post-Earthquake Utilities till the end of the game)
        if (stage >= GameStage.Stage3_Utilities)
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            RenderStage3();
            return;
        }

        // Any other state
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void RenderStage1()
    {
        var timeline = GameTimelineManager.Instance;
        if (timeline == null) return;

        // Task 1: Anchor the bookshelf
        bool isBookshelfDone = timeline.IsBookshelfAnchored;
        string item1 = isBookshelfDone
            ? "<color=#145A32><b>[✓] <s>1. Anchor the bookshelf</s></b></color>"
            : "<color=#2C1A0E><b>[ ] 1. Anchor the bookshelf</b></color>";

        // Task 2: Look around (requires exploring rooms & checking the drawers)
        bool hasDrawers = CheckDrawersCondition();
        bool isLookAroundDone = s_hasExploredRooms && hasDrawers;
        string item2;
        if (isLookAroundDone)
        {
            item2 = "<color=#145A32><b>[✓] <s>2. Look around</s></b></color>";
        }
        else
        {
            item2 = "<color=#2C1A0E><b>[ ] 2. Look around</b></color>";
        }

        // Task 3: Address the hazards (candle extinguished + painting removed)
        int hazardsFixed = (timeline.IsCandleExtinguished ? 1 : 0) + (timeline.IsPaintingRemoved ? 1 : 0);
        bool isHazardsDone = hazardsFixed >= 2;
        string item3;
        if (isHazardsDone)
        {
            item3 = "<color=#145A32><b>[✓] <s>3. Address the hazards (2/2)</s></b></color>";
        }
        else if (hazardsFixed == 1)
        {
            item3 = "<color=#7E5109><b>[ ] 3. Address the hazards (1/2)</b></color>";
        }
        else
        {
            item3 = "<color=#2C1A0E><b>[ ] 3. Address the hazards (0/2)</b></color>";
        }

        if (tasksText != null)
        {
            tasksText.text = $"{item1}\n{item2}\n{item3}";
        }

        CheckAudioTriggers(isBookshelfDone, isLookAroundDone, isHazardsDone, false, false);
    }

    private void RenderStage3()
    {
        var timeline = GameTimelineManager.Instance;
        if (timeline == null) return;

        // Task 1: Turn off the gas
        bool isGasDone = timeline.IsGasShutOff;
        string item1 = isGasDone
            ? "<color=#145A32><b>[✓] <s>1. Turn off the gas</s></b></color>"
            : "<color=#2C1A0E><b>[ ] 1. Turn off the gas</b></color>";

        // Task 2: Find your way out
        bool isWayOutDone = (DoorSceneController.Instance != null && DoorSceneController.Instance.IsUnlocked) ||
                            (ApartmentExitDoor.Instance != null && ApartmentExitDoor.Instance.IsEvacuated) ||
                            timeline.CurrentStage >= GameStage.Stage4_Stairwell ||
                            timeline.CurrentStage == GameStage.Victory;

        string item2 = isWayOutDone
            ? "<color=#145A32><b>[✓] <s>2. Find your way out</s></b></color>"
            : "<color=#2C1A0E><b>[ ] 2. Find your way out</b></color>";

        if (tasksText != null)
        {
            tasksText.text = $"{item1}\n{item2}";
        }

        CheckAudioTriggers(false, false, false, isGasDone, isWayOutDone);
    }

    private void CheckAudioTriggers(bool b, bool l, bool h, bool g, bool w)
    {
        bool newlyChecked = false;
        if (!prevBookshelf && b) newlyChecked = true;
        if (!prevLookAround && l) newlyChecked = true;
        if (!prevHazards && h) newlyChecked = true;
        if (!prevGas && g) newlyChecked = true;
        if (!prevWayOut && w) newlyChecked = true;

        prevBookshelf = b;
        prevLookAround = l;
        prevHazards = h;
        prevGas = g;
        prevWayOut = w;

        if (newlyChecked)
        {
            AudioManager.PlayUIClick();
        }
    }

    private void LoadBackgroundSprite()
    {
        if (todoBackgroundSprite != null) return;

        // 1. Resources load
        todoBackgroundSprite = Resources.Load<Sprite>("to do lis") ??
                               Resources.Load<Sprite>("UI/to do lis");

        if (todoBackgroundSprite == null)
        {
            var sprites = Resources.LoadAll<Sprite>("to do lis");
            if (sprites != null && sprites.Length > 0) todoBackgroundSprite = sprites[0];
        }

#if UNITY_EDITOR
        // 2. Editor AssetDatabase load
        if (todoBackgroundSprite == null)
        {
            todoBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MainScene/Sprite/to do lis.png");
            if (todoBackgroundSprite == null)
            {
                var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/MainScene/Sprite/to do lis.png");
                if (all != null)
                {
                    foreach (var o in all)
                    {
                        if (o is Sprite s)
                        {
                            todoBackgroundSprite = s;
                            break;
                        }
                    }
                }
            }
        }
#endif

        // 3. Disk bytes fallback
        if (todoBackgroundSprite == null)
        {
            string[] paths = new string[]
            {
                Path.Combine(Application.dataPath, "MainScene/Sprite/to do lis.png"),
                Path.Combine(Application.dataPath, "Resources/to do lis.png")
            };

            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(p);
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        tex.filterMode = FilterMode.Point;
                        if (tex.LoadImage(bytes))
                        {
                            Rect r = (tex.width == 256 && tex.height == 256)
                                ? new Rect(1, 86, 252, 97)
                                : new Rect(0, 0, tex.width, tex.height);
                            todoBackgroundSprite = Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), 100f);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[TodoListUI] Failed to load disk sprite: {ex.Message}");
                    }
                }
            }
        }
    }

    private void BuildUIHierarchy()
    {
        if (panelRoot != null) return;

        // Ensure parent Canvas exists
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            if (PersistentCanvas.Instance != null)
            {
                transform.SetParent(PersistentCanvas.Instance.transform, false);
                canvas = PersistentCanvas.Instance.GetComponent<Canvas>();
            }
            else
            {
                canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    transform.SetParent(canvas.transform, false);
                }
            }
        }

        // 1. Root Wooden Sign Panel
        panelRoot = new GameObject("TodoListWoodenSign");
        panelRoot.transform.SetParent(transform, false);

        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = topCenterPos;
        panelRect.sizeDelta = cardSize;

        // Background Image using "to do lis.png"
        panelBackground = panelRoot.AddComponent<Image>();
        panelBackground.sprite = todoBackgroundSprite;
        panelBackground.color = backgroundColor;
        panelBackground.preserveAspect = false;

        // 2. Tasks Body Text (inside the tan parchment box)
        var tasksObj = new GameObject("TasksText");
        tasksObj.transform.SetParent(panelRoot.transform, false);
        var tRect = tasksObj.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0f, 0f);
        tRect.anchorMax = new Vector2(1f, 1f);
        tRect.pivot = new Vector2(0f, 0.5f);

        // Tan parchment box offsets in the 477x184 wooden sign
        tRect.offsetMin = new Vector2(66f, 26f);
        tRect.offsetMax = new Vector2(-64f, -86f);

        tasksText = tasksObj.AddComponent<TextMeshProUGUI>();
        tasksText.fontSize = 15.5f;
        tasksText.fontStyle = FontStyles.Bold;
        tasksText.color = new Color(0.15f, 0.08f, 0.03f, 1f); // Dark rich brown ink on parchment
        tasksText.alignment = TextAlignmentOptions.MidlineLeft;
        tasksText.textWrappingMode = TextWrappingModes.Normal;
        tasksText.richText = true;
        tasksText.lineSpacing = 1.5f;
    }
}
