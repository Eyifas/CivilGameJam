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

    [Header("Flashlight Visual & Placement")]
    [SerializeField] private Transform flashlightVisual;
    [SerializeField] private SpriteRenderer flashlightSpriteRenderer;
    [SerializeField] private Sprite flashlightSprite;
    [Tooltip("Offset relative to character center for hand placement (X flips when facing left)")]
    [SerializeField] private Vector2 flashlightHandOffset = new Vector2(-0.05f, -0.48f);
    [Tooltip("Visual scale of the flashlight sprite")]
    [SerializeField] private Vector3 flashlightVisualScale = new Vector3(0.12f, 0.12f, 1f);
    [Tooltip("Forward offset from hand where the light beam originates")]
    [SerializeField] private float flashlightLensOffset = 0.22f;
    [Tooltip("Upward tilt angle in degrees for holding flashlight in hand")]
    [SerializeField] private float flashlightUpwardAngle = 18f;

    [Header("Flashlight Light")]
    [SerializeField] private Transform flashlightLight;
    [SerializeField] private Light2D flashlightBeam;
    [Tooltip("Legacy offset (superseded by flashlightArmpitOffset + flashlightLensOffset)")]
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
    private float footstepTimer = 0f;
    private const float FOOTSTEP_INTERVAL = 0.34f;
    private bool prevFlashlightState = false;

    public static bool IsFlashlightEquipped { get; set; } = false;
    public static PlayerMovement Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticData()
    {
        LastSavedPosition = null;
        IsFlashlightEquipped = false;
        Instance = null;
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
        Instance = this;

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
        UpdateFlashlightState();

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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        EnsureFlashlight();
        EnsureCharacterLight();
        UpdateFlashlightState();
    }

    private void EnsureFlashlight()
    {
        // 1. Flashlight Light Beam
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

        // 2. Flashlight Sprite Visual (held between armpits)
        if (flashlightVisual == null)
        {
            Transform foundVisual = transform.Find("FlashlightVisual");
            if (foundVisual != null)
            {
                flashlightVisual = foundVisual;
            }
            else
            {
                GameObject visualObj = new GameObject("FlashlightVisual");
                visualObj.transform.SetParent(transform, false);
                flashlightVisual = visualObj.transform;
            }
        }

        if (flashlightVisual != null)
        {
            if (flashlightSpriteRenderer == null)
            {
                flashlightSpriteRenderer = flashlightVisual.GetComponent<SpriteRenderer>();
                if (flashlightSpriteRenderer == null)
                {
                    flashlightSpriteRenderer = flashlightVisual.gameObject.AddComponent<SpriteRenderer>();
                }
            }

            if (flashlightSprite == null)
            {
                flashlightSprite = InventoryUI.GetFlashlightSprite();
            }

            if (flashlightSpriteRenderer != null)
            {
                if (flashlightSprite != null)
                {
                    flashlightSpriteRenderer.sprite = flashlightSprite;
                }

                if (spriteRenderer != null)
                {
                    flashlightSpriteRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                    // Render BEHIND the hand so the character's hand wraps in front of the flashlight
                    flashlightSpriteRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
                    flashlightSpriteRenderer.material = spriteRenderer.material;
                }
                else
                {
                    flashlightSpriteRenderer.sortingOrder = 9;
                }
            }

            flashlightVisual.localScale = flashlightVisualScale;
            flashlightVisual.gameObject.SetActive(IsFlashlightEquipped && !SwitchInteraction.IsAnyHazardActive);
        }
    }

    public void EnsureCharacterLight()
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
            charLightObj.SetActive(false); // Prevents OnEnable from temporarily registering as default Global light in URP
            charLightObj.transform.SetParent(transform, false);
            charLightObj.transform.localPosition = new Vector3(characterLightOffset.x, characterLightOffset.y, 0f);
            characterLight = charLightObj.AddComponent<Light2D>();
            ApplyScreenshotSettingsToLight(characterLight);
            charLightObj.SetActive(true);
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

        if (flashlightVisual != null)
        {
            flashlightVisual.gameObject.SetActive(false);
        }
    }

    public void EquipFlashlight()
    {
        IsFlashlightEquipped = true;
        EnsureFlashlight();
        UpdateFacingAndFlashlight();
        UpdateFlashlightState();
    }

    public static void SetFlashlightEquipped(bool equipped)
    {
        IsFlashlightEquipped = equipped;
        if (Instance != null)
        {
            Instance.EnsureFlashlight();
            Instance.UpdateFacingAndFlashlight();
            Instance.UpdateFlashlightState();
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
        if (isCharacterHidden) return;

        // Hareket
        Vector3 direction = new Vector3(movement.x, movement.y, 0f);
        transform.position += direction * speed * Time.deltaTime;

        // Sağa / sola dönme ve fener takibi (elde tutulma + ışık huzmesi)
        UpdateFacingAndFlashlight();

        // Karakterin kendi üzerindeki küçük ışık (karanlıkta karakteri görünür tutar)
        UpdateCharacterLight();

        // Fenerin açık/kapalı durumunu kontrol et
        UpdateFlashlightState();

        // Yürüme animasyonu ve ayak sesleri
        bool isMoving = movement.sqrMagnitude > 0.01f;
        if (animator != null)
        {
            animator.SetBool("isWalking", isMoving);
        }

        if (isMoving)
        {
            footstepTimer += Time.deltaTime;
            if (footstepTimer >= FOOTSTEP_INTERVAL)
            {
                footstepTimer = 0f;
                AudioManager.PlayFootstep();
            }
        }
        else
        {
            // Reset to near interval so the first step upon resuming movement sounds promptly
            footstepTimer = FOOTSTEP_INTERVAL * 0.85f;
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

        bool facingLeft = spriteRenderer != null && spriteRenderer.flipX;

        float tilt = 0f;
        if (movement.y > 0.1f)
        {
            tilt = 10f;
        }
        else if (movement.y < -0.1f)
        {
            tilt = -10f;
        }

        float sway = 0f;
        if (movement.sqrMagnitude > 0.01f)
        {
            sway = Mathf.Sin(Time.time * 8.5f) * 1.5f;
        }

        // Fenerin elde tutulma konumu (karakter yönüne göre X ekseninde döner)
        Vector3 targetHandPos = new Vector3(facingLeft ? -flashlightHandOffset.x : flashlightHandOffset.x, flashlightHandOffset.y, 0f);

        // Yukarıya doğru açı: Sağa bakarken +flashlightUpwardAngle, sola bakarken -flashlightUpwardAngle
        float visualRotZ;
        Vector3 dir;

        if (!facingLeft)
        {
            visualRotZ = flashlightUpwardAngle + tilt + sway;
            float rad = visualRotZ * Mathf.Deg2Rad;
            dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        }
        else
        {
            visualRotZ = -flashlightUpwardAngle - tilt + sway;
            float rad = (-visualRotZ) * Mathf.Deg2Rad;
            dir = new Vector3(-Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        }

        // 1. Eldeki Fener Görseli (Elin arkasında render edilir, ucu hafif yukarı yönlüdür)
        if (flashlightVisual != null)
        {
            flashlightVisual.localPosition = Vector3.Lerp(flashlightVisual.localPosition, targetHandPos, Time.deltaTime * smoothFollowSpeed);

            if (flashlightSpriteRenderer != null)
            {
                // fener.png görselinde fener başlığı soldadır.
                // Karakter sağa bakarken başlık sağa bakmalı -> flipX = true.
                // Karakter sola bakarken başlık sola bakmalı -> flipX = false.
                flashlightSpriteRenderer.flipX = !facingLeft;

                // Elin arkasında kalması için sıralama düzenini koru
                if (spriteRenderer != null)
                {
                    flashlightSpriteRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                    flashlightSpriteRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
                }
            }

            flashlightVisual.localRotation = Quaternion.Euler(0f, 0f, visualRotZ);
        }

        // 2. Fenerin Işık Huzmesi (Light2D)
        // Işık kaynağı fenerin ön ucundan/merceğinden yayılır ve fenerin açısıyla kusursuz hizalanır
        if (flashlightLight != null)
        {
            Vector3 targetLightPos = targetHandPos + dir * flashlightLensOffset;
            flashlightLight.localPosition = Vector3.Lerp(flashlightLight.localPosition, targetLightPos, Time.deltaTime * smoothFollowSpeed);

            float lightRotZ = facingLeft ? (90f + visualRotZ) : (-90f + visualRotZ);
            Quaternion targetRot = Quaternion.Euler(0f, 0f, lightRotZ);
            flashlightLight.localRotation = Quaternion.Slerp(flashlightLight.localRotation, targetRot, Time.deltaTime * smoothFollowSpeed);
        }
    }

    public void UpdateCharacterLight()
    {
        if (characterLight == null) return;

        if (isCharacterHidden || SwitchInteraction.IsAnyHazardActive)
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
        if (isCharacterHidden) return;

        bool hasFlashlight = InventoryManager.HasItem("Flashlight") || InventoryManager.HasItem("fener");
        if (!hasFlashlight && GameTimelineManager.Instance != null)
        {
            hasFlashlight = GameTimelineManager.Instance.HasFlashlight;
        }

        // Fener yalnızca envanterden sahneye sürüklendiğinde (kuşanıldığında) açılır!
        bool isEquipped = hasFlashlight && IsFlashlightEquipped;
        bool isElectricityOn = GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsElectricityRestored;

        // Işık huzmesi yalnızca fener kuşanıldığında ve elektrik kesikken/karanlıkken yanar
        bool shouldFlashlightBeOn = false;
        if (GameTimelineManager.Instance == null)
        {
            shouldFlashlightBeOn = isEquipped && !SwitchInteraction.IsAnyHazardActive;
        }
        else
        {
            shouldFlashlightBeOn = isEquipped && !SwitchInteraction.IsAnyHazardActive && !isElectricityOn;
        }

        // Eldeki fener görseli kuşanıldığı sürece karakterin elinde görünür kalır
        bool shouldVisualBeShown = isEquipped && !SwitchInteraction.IsAnyHazardActive;

        if (flashlightVisual != null && flashlightVisual.gameObject.activeSelf != shouldVisualBeShown)
        {
            flashlightVisual.gameObject.SetActive(shouldVisualBeShown);
        }

        if (flashlightBeam != null && flashlightBeam.enabled != shouldFlashlightBeOn)
        {
            flashlightBeam.enabled = shouldFlashlightBeOn;
        }

        if (flashlightLight != null && flashlightLight.gameObject.activeSelf != shouldFlashlightBeOn)
        {
            flashlightLight.gameObject.SetActive(shouldFlashlightBeOn);
        }

        if (prevFlashlightState != shouldFlashlightBeOn)
        {
            prevFlashlightState = shouldFlashlightBeOn;
            AudioManager.PlayFlashlightToggle(shouldFlashlightBeOn);
        }
    }

    private bool isCharacterHidden = false;
    public bool IsCharacterHidden => isCharacterHidden;

    public void SetCharacterHidden(bool hidden)
    {
        isCharacterHidden = hidden;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = !hidden;
        }

        if (flashlightVisual != null)
        {
            flashlightVisual.gameObject.SetActive(!hidden && IsFlashlightEquipped && !SwitchInteraction.IsAnyHazardActive);
        }

        if (hidden)
        {
            if (characterLight != null) characterLight.enabled = false;
        }
        else
        {
            UpdateCharacterLight();
        }
    }

    public void Teleport(Vector3 newPosition)
    {
        transform.position = newPosition;
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}