using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    // Sahne değişimlerinde oyuncunun bulunduğu konumu hatırlar
    public static Vector3? LastSavedPosition = null;

    [Header("Spawn Settings")]
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(-9.6f, -2.2f, 0f);

    [Header("Movement")]
    [SerializeField] private float speed = 8f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    private Vector2 movement;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticData()
    {
        LastSavedPosition = null;
    }

    private void OnValidate()
    {
        if (Mathf.Approximately(defaultSpawnPosition.y, 0f))
        {
            defaultSpawnPosition.y = -2.2f;
        }
    }

    private void Awake()
    {
        // Inspector'dan bağlamadıysan otomatik bulmaya çalış
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // Önceden kaydedilmiş konum varsa oradan başla
        if (LastSavedPosition.HasValue)
        {
            transform.position = LastSavedPosition.Value;
            LastSavedPosition = null;
        }
        else
        {
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
        // Hareket
        Vector3 direction = new Vector3(movement.x, movement.y, 0f);
        transform.position += direction * speed * Time.deltaTime;

        // Sağa / sola dönme
        if (spriteRenderer != null)
        {
            if (movement.x > 0.01f)
            {
                spriteRenderer.flipX = false;
            }
            else if (movement.x < -0.01f)
            {
                spriteRenderer.flipX = true;
            }
        }

        // Yürüme animasyonu
        if (animator != null)
        {
            animator.SetBool("isWalking", movement.sqrMagnitude > 0.01f);
        }
    }
}