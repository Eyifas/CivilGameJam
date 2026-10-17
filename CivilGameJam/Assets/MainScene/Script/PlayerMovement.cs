using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    // Remembers the player's exact room coordinates when transitioning to/from scenes like DrawerScene
    public static Vector3? LastSavedPosition = null;

    [Header("Spawn Settings")]
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(-9.6f, -2.2f, 0f);

    public float speed = 8f;
    private Vector2 movement;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticData()
    {
        LastSavedPosition = null;
    }

    private void OnValidate()
    {
        // Auto-fix if Unity Inspector previously serialized Y as 0
        if (Mathf.Approximately(defaultSpawnPosition.y, 0f))
        {
            defaultSpawnPosition.y = -2.2f;
        }
    }

    private void Awake()
    {
        if (LastSavedPosition.HasValue)
        {
            transform.position = LastSavedPosition.Value;
            LastSavedPosition = null; // One-time consumption: clear it so future resets spawn at default!
        }
        else
        {
            // Safety guard: Ensure Y is -2.2f if Inspector held the old 0f value
            if (Mathf.Approximately(defaultSpawnPosition.y, 0f))
            {
                defaultSpawnPosition.y = -2.2f;
            }
            transform.position = defaultSpawnPosition;
        }
    }

    public static void SavePosition(Vector3 pos)
    {
        LastSavedPosition = pos;
    }

    public static void ClearSavedPosition()
    {
        LastSavedPosition = null;
    }

    public void OnMove(InputValue value)
    {
        movement = value.Get<Vector2>();
    }

    private void Update()
    {
        Vector3 direction = new Vector3(movement.x, movement.y, 0f);
        transform.position += direction * speed * Time.deltaTime;
    }
}