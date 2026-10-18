using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public class PersistentCanvas : MonoBehaviour
{
    public static PersistentCanvas Instance { get; private set; }
    public static EventSystem PersistentEventSystem => persistentEventSystem;

    private static EventSystem persistentEventSystem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        persistentEventSystem = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CleanupDuplicateEventSystems();
        TodoListUI.EnsureTodoListUI();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // EndScene'de Main Canvas'ı gizle
        if (scene.name == "EndScene")
        {
            SetCanvasVisible(false);
            CleanupDuplicateEventSystems();
            return;
        }

        // Diğer sahnelerde tekrar göster
        SetCanvasVisible(true);

        CleanupDuplicateEventSystems();
        TodoListUI.EnsureTodoListUI();
    }

    private void SetCanvasVisible(bool visible)
    {
        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);

        foreach (Canvas canvas in canvases)
        {
            canvas.enabled = visible;
        }
    }

    private void CleanupDuplicateEventSystems()
    {
        EventSystem[] systems = FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (systems == null || systems.Length == 0)
        {
            var esObj = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );

            persistentEventSystem = esObj.GetComponent<EventSystem>();
            DontDestroyOnLoad(esObj);
            return;
        }

        // Keep or elect the persistent EventSystem
        if (persistentEventSystem == null)
        {
            persistentEventSystem = systems[0];
            DontDestroyOnLoad(persistentEventSystem.gameObject);
        }

        // Destroy any duplicate EventSystems loaded from subsequent scenes
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null &&
                systems[i] != persistentEventSystem)
            {
                Destroy(systems[i].gameObject);
            }
        }
    }
}