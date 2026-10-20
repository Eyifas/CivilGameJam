using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the hallway cupboard in MainScene (GameObject 'düşme dolap1' at x = 9.03f).
/// - Stage 1 (Preparation): Inactive/uninteractable, standing upright (düşme dolap1).
/// - Stage 2 (Earthquake): When tremor strikes, if player is in the room, plays the
///   falling animation sequence (düşme dolap1 -> 2 -> 3 -> 4) with a crash!
/// - Stage 3 (Utilities): Becomes interactable! Approaching shows [E] prompt.
///   Pressing [E] takes the player into DolapScene.
///   Once secured in DolapScene, stands upright (düşme dolap1) and becomes uninteractable.
/// </summary>
public class MainSceneDolap : MonoBehaviour
{
    public static MainSceneDolap Instance { get; private set; }

    [Header("Dolap Sprites (Falling sequence 1 -> 4)")]
    [SerializeField] private Sprite frame1Upright;
    [SerializeField] private Sprite frame2Tilt;
    [SerializeField] private Sprite frame3Falling;
    [SerializeField] private Sprite frame4Fallen;

    private SpriteRenderer spriteRenderer;
    private InteractionZone interactionZone;
    private BoxCollider2D triggerCollider;
    private BoxCollider2D blockingCollider;
    public const float ExpectedDolapX = 9.03f;
    public const float ExpectedDolapY = -0.42f;

    private bool hasFallen = false;
    private Coroutine fallCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Ensure dolap is positioned at exact expected hallway position (x = 9.03f)
        transform.position = new Vector3(ExpectedDolapX, ExpectedDolapY, transform.position.z);

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        interactionZone = GetComponentInChildren<InteractionZone>(true);
        triggerCollider = GetComponentInChildren<BoxCollider2D>(true);

        // Ensure trigger collider is generously sized so player can easily trigger [E] prompt
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            if (triggerCollider.size.x < 3.0f || triggerCollider.size.y < 3.0f)
            {
                triggerCollider.size = new Vector2(3.5f, 3.5f);
            }
        }

        EnsureBlockingCollider();
        LoadSprites();
    }

    private void EnsureBlockingCollider()
    {
        Transform blocker = transform.Find("DolapHallwayBlocker");
        if (blocker == null)
        {
            GameObject bObj = new GameObject("DolapHallwayBlocker");
            bObj.transform.SetParent(transform, false);
            bObj.transform.localPosition = Vector3.zero;
            blockingCollider = bObj.AddComponent<BoxCollider2D>();
            blockingCollider.isTrigger = false;
            blockingCollider.size = new Vector2(2.5f, 10.0f);
            blockingCollider.offset = Vector2.zero;
        }
        else
        {
            blockingCollider = blocker.GetComponent<BoxCollider2D>();
            if (blockingCollider == null) blockingCollider = blocker.gameObject.AddComponent<BoxCollider2D>();
            blockingCollider.isTrigger = false;
            blockingCollider.size = new Vector2(2.5f, 10.0f);
        }
        blockingCollider.enabled = false;
    }

    private void SetBlockerActive(bool active)
    {
        if (blockingCollider == null) EnsureBlockingCollider();
        if (blockingCollider != null)
        {
            blockingCollider.enabled = active;
        }
    }

    private void Start()
    {
        SyncToTimelineState();
    }

    private void OnEnable()
    {
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.OnStageChanged += HandleStageChanged;
            GameTimelineManager.Instance.OnStateUpdated += SyncToTimelineState;
        }
        SyncToTimelineState();
    }

    private void OnDisable()
    {
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.OnStageChanged -= HandleStageChanged;
            GameTimelineManager.Instance.OnStateUpdated -= SyncToTimelineState;
        }
    }

    private void LoadSprites()
    {
        if (frame1Upright == null) frame1Upright = LoadDolapSprite(1);
        if (frame2Tilt == null) frame2Tilt = LoadDolapSprite(2);
        if (frame3Falling == null) frame3Falling = LoadDolapSprite(3);
        if (frame4Fallen == null) frame4Fallen = LoadDolapSprite(4);

        Debug.Log($"[MainSceneDolap] Sprites loaded: frame1={(frame1Upright != null)}, frame2={(frame2Tilt != null)}, frame3={(frame3Falling != null)}, frame4={(frame4Fallen != null)}");
    }

    private Sprite LoadDolapSprite(int index)
    {
        // 1. If looking for frame 1 and spriteRenderer already has the upright sprite, use it!
        if (index == 1 && spriteRenderer != null && spriteRenderer.sprite != null)
        {
            return spriteRenderer.sprite;
        }

        // 2. Try loading from Resources/Dolap/dusme_dolap{index}
        Sprite s = Resources.Load<Sprite>($"Dolap/dusme_dolap{index}");
        if (s != null) return s;

        // 3. Try loading directly from Resources
        s = Resources.Load<Sprite>($"dusme_dolap{index}");
        if (s != null) return s;

        // 4. Try FindObjectsOfTypeAll<Sprite> for any already loaded sprite matching dolap + index
        var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var sp in allSprites)
        {
            if (sp == null) continue;
            string name = sp.name;
            if (name.IndexOf("dolap", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (name.EndsWith(index.ToString()) || name.IndexOf("dolap" + index, StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("dolap_" + index, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return sp;
            }
        }

        // 5. Try disk read from Assets/Resources/Dolap or Assets/MainScene/Sprite
        string[] searchDirs = new string[]
        {
            Path.Combine(Application.dataPath, "Resources", "Dolap"),
            Path.Combine(Application.dataPath, "MainScene", "Sprite")
        };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            var files = Directory.GetFiles(dir, "*.png");
            foreach (var f in files)
            {
                string fn = Path.GetFileNameWithoutExtension(f);
                if (fn.IndexOf("dolap", StringComparison.OrdinalIgnoreCase) >= 0 && fn.EndsWith(index.ToString()))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(f);
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (tex.LoadImage(bytes))
                        {
                            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MainSceneDolap] Failed loading {f}: {ex.Message}");
                    }
                }
            }
        }

        return null;
    }

    public void HandleStageChanged(GameStage newStage)
    {
        if (newStage == GameStage.Stage2_Tremor)
        {
            // Earthquake strikes!
            if (!hasFallen && (GameTimelineManager.Instance == null || !GameTimelineManager.Instance.IsDolapSecured))
            {
                TriggerEarthquakeFall();
            }
        }
        else
        {
            SyncToTimelineState();
        }
    }

    public void SyncToTimelineState()
    {
        if (GameTimelineManager.Instance == null)
        {
            SetVisualUpright();
            SetInteractable(false);
            return;
        }

        var stage = GameTimelineManager.Instance.CurrentStage;
        bool isSecured = GameTimelineManager.Instance.IsDolapSecured;

        if (stage == GameStage.Stage1_Preparation)
        {
            // Inactive / uninteractable before the earthquake, standing upright
            hasFallen = false;
            SetVisualUpright();
            SetInteractable(false);
            SetBlockerActive(false);
        }
        else if (stage == GameStage.Stage2_Tremor)
        {
            // If already fallen or secured, show appropriate frame
            if (isSecured)
            {
                SetVisualUpright();
                SetInteractable(false);
                SetBlockerActive(false);
            }
            else if (!hasFallen)
            {
                TriggerEarthquakeFall();
            }
            else
            {
                SetBlockerActive(true);
            }
        }
        else if (stage == GameStage.Stage3_Utilities)
        {
            if (isSecured)
            {
                // Secured! Standing safely upright back against the wall
                hasFallen = false;
                SetVisualUpright();
                SetInteractable(false);
                SetBlockerActive(false);
            }
            else
            {
                // Fallen! Waiting for player to interact and lift it in DolapScene
                hasFallen = true;
                SetVisualFallen();
                SetInteractable(true);
                SetBlockerActive(true);
            }
        }
        else
        {
            if (isSecured)
            {
                SetVisualUpright();
                SetBlockerActive(false);
            }
            else
            {
                SetVisualFallen();
                SetBlockerActive(hasFallen);
            }
            SetInteractable(false);
        }
    }

    private void SetVisualUpright()
    {
        transform.position = new Vector3(ExpectedDolapX, ExpectedDolapY, transform.position.z);
        transform.localRotation = Quaternion.identity;

        if (spriteRenderer != null)
        {
            if (frame1Upright != null) spriteRenderer.sprite = frame1Upright;
            if (spriteRenderer.transform != transform)
            {
                spriteRenderer.transform.localPosition = Vector3.zero;
                spriteRenderer.transform.localRotation = Quaternion.identity;
            }
        }
    }

    private void SetVisualFallen()
    {
        transform.position = new Vector3(ExpectedDolapX, ExpectedDolapY, transform.position.z);
        transform.localRotation = Quaternion.identity;

        if (spriteRenderer != null)
        {
            if (frame4Fallen != null)
            {
                spriteRenderer.sprite = frame4Fallen;
                if (spriteRenderer.transform != transform)
                {
                    spriteRenderer.transform.localPosition = Vector3.zero;
                    spriteRenderer.transform.localRotation = Quaternion.identity;
                }
            }
            else if (frame3Falling != null)
            {
                spriteRenderer.sprite = frame3Falling;
                if (spriteRenderer.transform != transform)
                {
                    spriteRenderer.transform.localPosition = Vector3.zero;
                    spriteRenderer.transform.localRotation = Quaternion.identity;
                }
            }
            else
            {
                // Fallback: rotate wardrobe flat on floor
                transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                transform.position = new Vector3(ExpectedDolapX, -1.0f, transform.position.z);
            }
        }
    }

    private void SetInteractable(bool interactable)
    {
        if (interactionZone != null)
        {
            interactionZone.SetInteractable(interactable);
        }
        if (triggerCollider != null)
        {
            triggerCollider.enabled = interactable;
        }
    }

    public void TriggerEarthquakeFall()
    {
        if (hasFallen) return;
        hasFallen = true;

        if (fallCoroutine != null) StopCoroutine(fallCoroutine);
        fallCoroutine = StartCoroutine(FallSequenceRoutine());
    }

    private IEnumerator FallSequenceRoutine()
    {
        // Check if player / camera is in hallway (x > 0)
        Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
        bool isPlayerInRoom = (cam != null && cam.transform.position.x > 0f);

        if (!isPlayerInRoom)
        {
            // Player is in bedroom or kitchen; fall immediately without animation
            SetVisualFallen();
            AudioManager.PlayWardrobeCrash();
            yield break;
        }

        // Player is present: Play dramatic falling animation!
        Debug.Log("<color=orange>[MainSceneDolap] Earthquake tremors cause the hallway cupboard to topple!</color>");
        AudioManager.PlayDolapCreak();

        // 1. Shaking upright (düşme dolap1)
        SetVisualUpright();
        Vector3 origPos = new Vector3(ExpectedDolapX, ExpectedDolapY, transform.position.z);
        transform.position = origPos;
        float elapsed = 0f;
        while (elapsed < 0.8f)
        {
            elapsed += Time.deltaTime;
            float shakeX = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * 0.15f;
            transform.position = origPos + new Vector3(shakeX, 0f, 0f);
            yield return null;
        }
        transform.position = origPos;

        // 2. Frame 2: Tilting forward
        if (frame2Tilt != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = frame2Tilt;
            AudioManager.PlayDolapCreak();
        }
        yield return new WaitForSeconds(0.18f);

        // 3. Frame 3: Heavy tilt about to slam
        if (frame3Falling != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = frame3Falling;
        }
        yield return new WaitForSeconds(0.18f);

        // 4. Frame 4: Crashed flat onto floor with impact!
        SetVisualFallen();
        SetBlockerActive(true);
        AudioManager.PlayWardrobeCrash();
        Debug.Log("<color=red>[MainSceneDolap] Cupboard crashed onto the floor!</color>");

        // Camera impact shake
        var roomCam = FindFirstObjectByType<RoomCamera>();
        if (roomCam != null)
        {
            roomCam.StartScreenShake(0.35f, 0.45f);
        }

        // In Stage 3, becomes interactable
        if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.CurrentStage == GameStage.Stage3_Utilities)
        {
            SetInteractable(true);
        }
    }

    public void ResetDolap()
    {
        hasFallen = false;
        if (fallCoroutine != null)
        {
            StopCoroutine(fallCoroutine);
            fallCoroutine = null;
        }
        SetVisualUpright();
        SetInteractable(false);
        SetBlockerActive(false);
    }
}
