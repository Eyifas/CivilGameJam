using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

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

    [Header("Flashlight")]
    [SerializeField] private Transform flashlightLight;
    [SerializeField] private Light2D flashlightBeam;
    [SerializeField] private Vector2 flashlightOffset = new Vector2(0.18f, 0.05f);
    [SerializeField] private float smoothFollowSpeed = 12f;

    [Header("Character Lighting (Half Visible)")]
    [SerializeField] private Light2D characterLight;
    [Range(0f, 1f)]
    [SerializeField] private float characterHalfIntensity = 0.45f;
    [SerializeField] private float characterLightRadius = 3.2f;

    private Vector2 movement;
    private float targetFacingAngle = -90f;
    private Light2D cachedGlobalLight;

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

        EnsureLighting();

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

    private void Start()
    {
        EnsureLighting();
    }

    private void EnsureLighting()
    {
        // Auto-find flashlight transform and beam
        if (flashlightLight == null)
        {
            Transform found = transform.Find("FlashlightLight");
            if (found != null)
            {
                flashlightLight = found;
            }
        }

        if (flashlightLight != null && flashlightBeam == null)
        {
            flashlightBeam = flashlightLight.GetComponent<Light2D>();
        }

        // Configure flashlight beam parameters for high visual quality
        if (flashlightBeam != null)
        {
            flashlightBeam.shadowsEnabled = false;
            flashlightBeam.pointLightInnerAngle = 45f;
            flashlightBeam.pointLightOuterAngle = 75f;
            flashlightBeam.pointLightInnerRadius = 0.6f;
            if (flashlightBeam.pointLightOuterRadius < 5f)
            {
                flashlightBeam.pointLightOuterRadius = 9.5f;
            }
        }

        // Auto-find or create character half-visible light
        if (characterLight == null)
        {
            Transform foundCharLight = transform.Find("CharacterLight");
            if (foundCharLight != null)
            {
                characterLight = foundCharLight.GetComponent<Light2D>();
            }
        }

        if (characterLight == null)
        {
            GameObject charLightObj = new GameObject("CharacterLight");
            charLightObj.transform.SetParent(transform, false);
            charLightObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            characterLight = charLightObj.AddComponent<Light2D>();
            characterLight.lightType = Light2D.LightType.Point;
            characterLight.pointLightInnerAngle = 360f;
            characterLight.pointLightOuterAngle = 360f;
            characterLight.pointLightInnerRadius = 0.6f;
            characterLight.pointLightOuterRadius = characterLightRadius;
            characterLight.intensity = characterHalfIntensity;
            characterLight.shadowsEnabled = false;
            characterLight.color = new Color(1f, 0.96f, 0.88f, 1f);
        }
        else
        {
            characterLight.lightType = Light2D.LightType.Point;
            characterLight.pointLightInnerAngle = 360f;
            characterLight.pointLightOuterAngle = 360f;
            characterLight.pointLightOuterRadius = characterLightRadius;
            characterLight.shadowsEnabled = false;
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

        // Sağa / sola dönme ve fener takibi
        UpdateFacingAndFlashlight();

        // Karakterin karanlıkta yarım görünür kalmasını sağla
        UpdateCharacterVisibility();

        // Yürüme animasyonu
        if (animator != null)
        {
            animator.SetBool(
                "isWalking",
                movement.sqrMagnitude > 0.01f
            );
        }
    }

    private void UpdateFacingAndFlashlight()
    {
        if (spriteRenderer != null)
        {
            if (movement.x > 0.01f)
            {
                spriteRenderer.flipX = false;
                targetFacingAngle = -90f;
            }
            else if (movement.x < -0.01f)
            {
                spriteRenderer.flipX = true;
                targetFacingAngle = 90f;
            }
        }

        if (flashlightLight != null)
        {
            bool facingLeft = spriteRenderer != null && spriteRenderer.flipX;

            // Yumuşak el pozisyonu geçişi (sağ/sol el)
            Vector3 targetLocalPos = new Vector3(facingLeft ? -flashlightOffset.x : flashlightOffset.x, flashlightOffset.y, 0f);
            flashlightLight.localPosition = Vector3.Lerp(flashlightLight.localPosition, targetLocalPos, Time.deltaTime * smoothFollowSpeed);

            // Çapraz hareket eğimi (yukarı/aşağı bakma)
            float tilt = 0f;
            if (movement.y > 0.1f)
            {
                tilt = facingLeft ? -22f : 22f;
            }
            else if (movement.y < -0.1f)
            {
                tilt = facingLeft ? 22f : -22f;
            }

            // Yürüme esnasında fenerin doğal sallanması
            float sway = 0f;
            if (movement.sqrMagnitude > 0.01f)
            {
                sway = Mathf.Sin(Time.time * 8.5f) * 1.5f;
            }

            Quaternion targetRot = Quaternion.Euler(0f, 0f, targetFacingAngle + tilt + sway);
            flashlightLight.localRotation = Quaternion.Slerp(flashlightLight.localRotation, targetRot, Time.deltaTime * smoothFollowSpeed);
        }
    }

    private void UpdateCharacterVisibility()
    {
        if (characterLight == null) return;

        if (cachedGlobalLight == null)
        {
            GameObject globalLightObj = GameObject.Find("Global Light 2D");
            if (globalLightObj != null)
            {
                cachedGlobalLight = globalLightObj.GetComponent<Light2D>();
            }
        }

        bool isStage3 = GameTimelineManager.Instance != null &&
                        GameTimelineManager.Instance.CurrentStage == GameStage.Stage3_Utilities;

        // Sahne karanlık mı? (Deprem sonrası elektrik kesintisi veya global ışığın sönmesi)
        bool isDark = isStage3 || (cachedGlobalLight != null && cachedGlobalLight.intensity < 0.6f);

        // Kullanıcı isteği: Karakter karanlıkta yarım görünür olmalı (half visible ~0.45)
        float targetIntensity = isDark ? characterHalfIntensity : 0.05f;
        characterLight.intensity = Mathf.MoveTowards(characterLight.intensity, targetIntensity, Time.deltaTime * 3f);
        characterLight.enabled = characterLight.intensity > 0.01f;
    }
}