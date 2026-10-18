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
    [Tooltip("Flashlight local offset relative to character (X will flip when facing left)")]
    [SerializeField] private Vector2 flashlightOffset = new Vector2(0.18f, 0.05f);
    [SerializeField] private float smoothFollowSpeed = 12f;

    [Header("Character Small Light")]
    [SerializeField] private Light2D characterLight;
    [SerializeField] private float characterLightIntensity = 0.45f;
    [SerializeField] private float characterLightRadius = 3f;
    [SerializeField] private Vector2 characterLightOffset = new Vector2(0f, 0.05f);
    [SerializeField] private Color characterLightColor = new Color(1f, 0.992f, 0.91f, 1f);

    private Vector2 movement;
    private float targetFacingAngle = -90f;

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

        if (characterLight != null)
        {
            characterLight.transform.localPosition = new Vector3(characterLightOffset.x, characterLightOffset.y, 0f);
        }
    }

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        EnsureFlashlight();
        EnsureCharacterLight();
        ResetFlashlight();

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
        EnsureFlashlight();
        EnsureCharacterLight();
        ResetFlashlight();
    }

    private void EnsureFlashlight()
    {
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

        // NOTE: flashlightBeam üzerindeki tüm açı, yarıçap, renk, gölge ayarları
        // Unity Inspector'da nasıl ayarlandıysa aynen kullanılır, koddan ezilmez.
    }

    private void EnsureCharacterLight()
    {
        if (characterLight == null)
        {
            Transform found = transform.Find("CharacterLight");
            if (found != null)
            {
                characterLight = found.GetComponent<Light2D>();
            }
        }

        // Eğer Inspector'da atanmamışsa ve sahnede yoksa karakter üzerinde küçük ışık oluştur
        if (characterLight == null)
        {
            GameObject charLightObj = new GameObject("CharacterLight");
            charLightObj.transform.SetParent(transform, false);
            charLightObj.transform.localPosition = new Vector3(characterLightOffset.x, characterLightOffset.y, 0f);
            characterLight = charLightObj.AddComponent<Light2D>();
            ApplyScreenshotSettingsToLight(characterLight);
        }
        else
        {
            // Inspector'dan atanan veya hiyerarşide bulunan ışığın konumunu koru
            characterLight.transform.localPosition = new Vector3(characterLightOffset.x, characterLightOffset.y, 0f);
            characterLight.enabled = true;
        }
    }

    private void ApplyScreenshotSettingsToLight(Light2D light)
    {
        if (light == null) return;
        // User Screenshot 2026-10-17 161420 Settings:
        // Light Type: Spot (360/360 point spot)
        // Radius: Inner 0, Outer 3
        // Inner/Outer Angle: 360 / 360
        // Falloff Strength: 0.5
        // Blend Style: Multiply (index 0)
        // Overlap Operation: Additive
        // Light Order: 0
        // Intensity: 0.45
        // Shadows: Off
        // Volumetric: Off
        light.lightType = Light2D.LightType.Point;
        light.pointLightInnerAngle = 360f;
        light.pointLightOuterAngle = 360f;
        light.pointLightInnerRadius = 0f;
        light.pointLightOuterRadius = characterLightRadius;
        light.falloffIntensity = 0.5f;
        light.blendStyleIndex = 0; // Multiply
        light.overlapOperation = Light2D.OverlapOperation.Additive;
        light.lightOrder = 0;
        light.intensity = characterLightIntensity;
        light.shadowsEnabled = false;
        light.volumetricEnabled = false;
        light.color = characterLightColor;
        light.enabled = true;
    }

    private void OnEnable()
    {
        UpdateCharacterLight();
        UpdateFlashlightState();
    }

    private void OnDisable()
    {
        TurnOffAllLights();
        if (animator != null)
        {
            animator.SetBool("isWalking", false);
        }
    }

    public void ResetFlashlight()
    {
        if (flashlightBeam != null)
        {
            flashlightBeam.enabled = false;
        }

        if (flashlightLight != null)
        {
            var l2d = flashlightLight.GetComponent<Light2D>();
            if (l2d != null) l2d.enabled = false;
            flashlightLight.gameObject.SetActive(false);
        }
    }

    public void TurnOffAllLights()
    {
        ResetFlashlight();

        if (characterLight != null)
        {
            characterLight.enabled = false;
        }

        Light2D[] allLights = GetComponentsInChildren<Light2D>(true);
        foreach (var l in allLights)
        {
            if (l != null)
            {
                l.enabled = false;
            }
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

        // Karakterin kendi üzerindeki küçük ışık (karanlıkta karakteri görünür tutar)
        UpdateCharacterLight();

        // Fenerin açık/kapalı durumunu kontrol et
        UpdateFlashlightState();

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

            // Inspector'dan yapılandırılabilen flashlightOffset (X yönü karakter yönüne göre döner)
            Vector3 targetLocalPos = new Vector3(facingLeft ? -flashlightOffset.x : flashlightOffset.x, flashlightOffset.y, 0f);
            flashlightLight.localPosition = Vector3.Lerp(flashlightLight.localPosition, targetLocalPos, Time.deltaTime * smoothFollowSpeed);

            float tilt = 0f;
            if (movement.y > 0.1f)
            {
                tilt = facingLeft ? -20f : 20f;
            }
            else if (movement.y < -0.1f)
            {
                tilt = facingLeft ? 20f : -20f;
            }

            float sway = 0f;
            if (movement.sqrMagnitude > 0.01f)
            {
                sway = Mathf.Sin(Time.time * 8.5f) * 1.5f;
            }

            Quaternion targetRot = Quaternion.Euler(0f, 0f, targetFacingAngle + tilt + sway);
            flashlightLight.localRotation = Quaternion.Slerp(flashlightLight.localRotation, targetRot, Time.deltaTime * smoothFollowSpeed);
        }
    }

    private void UpdateCharacterLight()
    {
        if (characterLight == null) return;

        if (SwitchInteraction.IsAnyHazardActive)
        {
            if (characterLight.enabled) characterLight.enabled = false;
            return;
        }

        characterLight.transform.localPosition = new Vector3(characterLightOffset.x, characterLightOffset.y, 0f);

        if (!characterLight.enabled)
        {
            characterLight.enabled = true;
        }
    }

    private void UpdateFlashlightState()
    {
        if (flashlightBeam == null && flashlightLight == null) return;

        bool hasFlashlight = InventoryManager.HasItem("Flashlight");
        if (!hasFlashlight && GameTimelineManager.Instance != null)
        {
            hasFlashlight = GameTimelineManager.Instance.HasFlashlight;
        }

        // Fener Stage 3 başladığında otomatik olarak açılır (Stage 4'te de açık kalır)
        bool isStage3OrLater = GameTimelineManager.Instance != null &&
            (GameTimelineManager.Instance.CurrentStage == GameStage.Stage3_Utilities ||
             GameTimelineManager.Instance.CurrentStage == GameStage.Stage4_Stairwell);

        // Stage 1, Stage 2, Victory ve Rewind/Reset/Hazard aşamalarında kapalı kalır
        bool shouldFlashlightBeOn = hasFlashlight && isStage3OrLater && !SwitchInteraction.IsAnyHazardActive;

        // Timeline olmayan sahnede test ediliyorsa fener varsa açık tut
        if (GameTimelineManager.Instance == null)
        {
            shouldFlashlightBeOn = hasFlashlight && !SwitchInteraction.IsAnyHazardActive;
        }

        if (flashlightBeam != null && flashlightBeam.enabled != shouldFlashlightBeOn)
        {
            flashlightBeam.enabled = shouldFlashlightBeOn;
        }

        if (flashlightLight != null && flashlightLight.gameObject.activeSelf != shouldFlashlightBeOn)
        {
            flashlightLight.gameObject.SetActive(shouldFlashlightBeOn);
        }
    }
}