using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controller for the Table & Wall zoom scene.
/// Manages:
/// 1. Extinguishing the candle with a direct click.
/// 2. Lowering the heavy painting to the floor using a Hold & Release gauge.
/// 3. Auto-returning to MainScene once both are secured.
/// </summary>
public class TableSceneController : MonoBehaviour
{
    [Header("Table Objects")]
    [SerializeField] private GameObject litTable;               // Original lit table (Masa)
    [SerializeField] private GameObject unlitTable;             // Unlit table (Masa - unlit)

    [Header("Candle Setup")]
    [SerializeField] private GameObject candleFlame;            // Flame visual (SpriteRenderer, Particle, or Light)
    [SerializeField] private Collider2D candleCollider;         // Clickable area for candle
    [SerializeField] private bool candleExtinguished = false;

    [Header("Painting Setup")]
    [SerializeField] private Transform paintingTransform;       // Painting object
    [SerializeField] private Collider2D paintingCollider;       // Click/hold area for painting
    [SerializeField] private Vector3 wallPosition = new Vector3(-0.06f, 3.15f, 0f);
    [SerializeField] private Vector3 floorPosition = new Vector3(-0.06f, -3.8f, 0f);
    [SerializeField] private bool paintingSafe = false;

    [Header("Hold & Release Gauge Settings")]
    [SerializeField] private float chargeSpeed = 0.8f;          // Time to full charge ~1.25s
    [Range(0.4f, 0.9f)]
    [SerializeField] private float sweetSpotMin = 0.65f;
    [Range(0.5f, 0.95f)]
    [SerializeField] private float sweetSpotMax = 0.85f;

    [Header("Scene Return Settings")]
    [SerializeField] private string returnSceneName = "MainScene";
    [SerializeField] private float autoReturnDelay = 1.2f;
    [SerializeField] private TMP_Text instructionText;

    public static TableSceneController Instance { get; private set; }

    // Public state for external queries (e.g. ReturnToRoom)
    public bool IsBusy => isHolding || loweringCoroutine != null || isTransitioningBack;

    // Hold & Release internal state
    private bool isHolding = false;
    private float currentCharge = 0f;
    private bool chargeAscending = true;
    private bool isTransitioningBack = false;
    private string currentInstruction = "";
    private Coroutine loweringCoroutine = null;

    private void Awake()
    {
        Instance = this;

        // Auto-detect Table objects if unassigned
        if (litTable == null)
        {
            litTable = GameObject.Find("Masa");
        }

        if (unlitTable == null)
        {
            var found = GameObject.Find("Masa - unlit");
            if (found != null)
            {
                unlitTable = found;
            }
            else
            {
                // Inactive objects won't be found by GameObject.Find, search scene roots
                var rootObjs = gameObject.scene.GetRootGameObjects();
                foreach (var root in rootObjs)
                {
                    if (root.name.IndexOf("unlit", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        unlitTable = root;
                        break;
                    }
                }
            }
        }

        // Auto-detect Painting components if unassigned
        if (paintingTransform == null)
        {
            var tablo = GameObject.Find("Tablo");
            if (tablo == null) tablo = GameObject.Find("Painting");
            if (tablo != null) paintingTransform = tablo.transform;
        }

        if (paintingCollider == null && paintingTransform != null)
        {
            paintingCollider = paintingTransform.GetComponent<Collider2D>();
        }

        // Auto-detect Candle & Flame components if unassigned
        if (candleFlame == null)
        {
            candleFlame = GameObject.Find("flame");
            if (candleFlame == null) candleFlame = GameObject.Find("Flame");
            if (candleFlame == null) candleFlame = GameObject.Find("Fire");
            if (candleFlame == null) candleFlame = GameObject.Find("CandleFlame");
        }

        if (candleCollider == null)
        {
            if (candleFlame != null)
            {
                candleCollider = candleFlame.GetComponent<Collider2D>();
            }

            if (candleCollider == null)
            {
                var candleObj = GameObject.Find("Candle");
                if (candleObj == null) candleObj = GameObject.Find("Mum");
                if (candleObj != null) candleCollider = candleObj.GetComponent<Collider2D>();
            }
        }
    }

    private void Start()
    {
        // 1. Check existing state from GameTimelineManager
        if (GameTimelineManager.Instance != null)
        {
            if (GameTimelineManager.Instance.IsCandleExtinguished)
            {
                candleExtinguished = true;
            }

            if (GameTimelineManager.Instance.IsPaintingRemoved)
            {
                paintingSafe = true;
            }
        }

        // 2. Set Table Visuals (lit vs unlit)
        SetTableVisuals(candleExtinguished);

        // 3. Set Painting Position
        if (paintingTransform != null)
        {
            if (paintingSafe)
            {
                paintingTransform.localPosition = new Vector3(paintingTransform.localPosition.x, floorPosition.y, paintingTransform.localPosition.z);
            }
            else
            {
                wallPosition = paintingTransform.localPosition;
            }
        }

        // 4. Both already done?
        if (candleExtinguished && paintingSafe)
        {
            UpdateInstruction("<color=green>Table and painting already secured! Returning to room...</color>");
            isTransitioningBack = true;
            StartCoroutine(ReturnToRoomRoutine());
            return;
        }
    }

    private void SetTableVisuals(bool extinguished)
    {
        if (litTable != null) litTable.SetActive(!extinguished);
        if (unlitTable != null) unlitTable.SetActive(extinguished);
        if (candleFlame != null) candleFlame.SetActive(!extinguished);
    }

    private void Update()
    {
        if (isTransitioningBack) return;

        HandleCandleClick();
        HandlePaintingHoldAndRelease();
    }

    #region --- CANDLE LOGIC (POINT & CLICK) ---

    private void HandleCandleClick()
    {
        if (candleExtinguished) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mouseWorld = Camera.main != null ? 
                (Vector2)Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()) : Vector2.zero;

            bool hitCandle = false;

            if (candleCollider != null && candleCollider.OverlapPoint(mouseWorld))
            {
                hitCandle = true;
            }
            else if (candleFlame != null && Vector2.Distance(mouseWorld, candleFlame.transform.position) < 1.5f)
            {
                // Generous proximity fallback
                hitCandle = true;
            }

            if (hitCandle)
            {
                ExtinguishCandle();
            }
        }
    }

    public void ExtinguishCandle()
    {
        if (candleExtinguished) return;

        candleExtinguished = true;

        AudioManager.PlayCandlePuff();
        SetTableVisuals(true);

        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.SetCandleExtinguished(true);
        }

        Debug.Log("<color=green>[TableScene] Candle extinguished successfully! Swapped to unlit table.</color>");

        if (!paintingSafe)
        {
            UpdateInstruction("<color=green>Candle extinguished!</color> Now hold & release in GREEN to lower the heavy painting!");
        }
        else
        {
            CheckBothCompleted();
        }
    }

    #endregion

    #region --- PAINTING LOGIC (HOLD & RELEASE GAUGE) ---

    private void HandlePaintingHoldAndRelease()
    {
        if (paintingSafe || loweringCoroutine != null) return;

        bool isMouseHovering = false;
        Vector2 mouseWorld = Vector2.zero;

        if (Camera.main != null && Mouse.current != null)
        {
            mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            if (paintingCollider != null)
            {
                isMouseHovering = paintingCollider.OverlapPoint(mouseWorld);
            }
            else if (paintingTransform != null)
            {
                isMouseHovering = Vector2.Distance(mouseWorld, paintingTransform.position) < 2.5f;
            }
        }

        // Start holding: Mouse click down over painting OR Space key pressed
        bool startPress = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && isMouseHovering) ||
                          (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);

        // Continuous holding
        bool isPressed = (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
                         (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);

        // Release
        bool released = (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) ||
                        (Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame);

        if (startPress && !isHolding)
        {
            isHolding = true;
            currentCharge = 0f;
            chargeAscending = true;
            UpdateInstruction("Lifting frame... <color=yellow>RELEASE in the GREEN zone!</color>");
        }

        if (isHolding)
        {
            if (isPressed)
            {
                // Ping-pong charge between 0 and 1
                float delta = chargeSpeed * Time.unscaledDeltaTime;
                if (chargeAscending)
                {
                    currentCharge += delta;
                    if (currentCharge >= 1f)
                    {
                        currentCharge = 1f;
                        chargeAscending = false;
                    }
                }
                else
                {
                    currentCharge -= delta;
                    if (currentCharge <= 0f)
                    {
                        currentCharge = 0f;
                        chargeAscending = true;
                    }
                }
            }

            if (released)
            {
                EvaluateRelease();
            }
        }
    }

    private void EvaluateRelease()
    {
        isHolding = false;

        // Check if charge is within the sweet spot
        if (currentCharge >= sweetSpotMin && currentCharge <= sweetSpotMax)
        {
            // SUCCESS
            paintingSafe = true;
            currentCharge = 0f;

            if (GameTimelineManager.Instance != null)
            {
                GameTimelineManager.Instance.SetPaintingRemoved(true);
            }

            Debug.Log("<color=green>[TableScene] Painting successfully lifted and placed on floor!</color>");
            loweringCoroutine = StartCoroutine(LowerPaintingSmoothly());
        }
        else
        {
            // FAILED ATTEMPT
            string feedback = currentCharge < sweetSpotMin ? 
                "<color=red>Too weak! The heavy frame slipped back onto the hooks.</color>" : 
                "<color=red>Pulled too hard! Lost grip. Release inside the GREEN zone!</color>";

            currentCharge = 0f;
            UpdateInstruction(feedback + " Hold again to retry.");
            StartCoroutine(WobblePainting());
        }
    }

    private IEnumerator LowerPaintingSmoothly()
    {
        UpdateInstruction("<color=green>Lifting off wall... placing safely on the floor!</color>");

        if (paintingTransform != null)
        {
            Vector3 startPos = paintingTransform.localPosition;
            Vector3 targetFloorPos = new Vector3(startPos.x, floorPosition.y, startPos.z);
            float duration = 0.8f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                paintingTransform.localPosition = Vector3.Lerp(startPos, targetFloorPos, t);
                yield return null;
            }

            paintingTransform.localPosition = targetFloorPos;
            AudioManager.PlayPaintingPlace();
        }

        loweringCoroutine = null;

        if (!candleExtinguished)
        {
            UpdateInstruction("<color=green>Painting safe on the floor!</color> Now click the candle to extinguish the flame!");
        }
        else
        {
            CheckBothCompleted();
        }
    }

    private IEnumerator WobblePainting()
    {
        if (paintingTransform == null) yield break;

        AudioManager.PlayPaintingRattle();
        Vector3 basePos = paintingTransform.localPosition;
        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float offset = Mathf.Sin(elapsed * 40f) * 0.08f;
            paintingTransform.localPosition = new Vector3(basePos.x + offset, basePos.y, basePos.z);
            yield return null;
        }

        paintingTransform.localPosition = basePos;
    }

    #endregion

    private void CheckBothCompleted()
    {
        if (candleExtinguished && paintingSafe)
        {
            isTransitioningBack = true;
            AudioManager.PlayAnchorSuccess();
            UpdateInstruction("<color=green>All secured! Table flame extinguished and painting safe. Returning...</color>");
            StartCoroutine(ReturnToRoomRoutine());
        }
    }

    private IEnumerator ReturnToRoomRoutine()
    {
        yield return new WaitForSeconds(autoReturnDelay);
        SceneManager.LoadScene(returnSceneName);
    }

    private void UpdateInstruction(string message)
    {
        currentInstruction = message;
        if (instructionText != null)
        {
            instructionText.text = message;
        }
        Debug.Log("[TableScene] " + message);
    }

    private void OnGUI()
    {
        // Draw Hold & Release Gauge while holding
        if (isHolding && !paintingSafe)
        {
            DrawHoldGauge();
        }
    }

    private void DrawHoldGauge()
    {
        float gaugeWidth = 360f;
        float gaugeHeight = 32f;
        float x = (Screen.width - gaugeWidth) * 0.5f;
        float y = Screen.height * 0.68f;

        // Background box framing the gauge
        GUI.Box(new Rect(x - 5, y - 5, gaugeWidth + 10, gaugeHeight + 10), "");

        // Gauge track (dark)
        Texture2D darkTex = Texture2D.whiteTexture;
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, gaugeWidth, gaugeHeight), darkTex);

        // Sweet spot band (Green)
        float sweetX = x + gaugeWidth * sweetSpotMin;
        float sweetW = gaugeWidth * (sweetSpotMax - sweetSpotMin);
        GUI.color = new Color(0.1f, 0.85f, 0.2f, 0.75f);
        GUI.DrawTexture(new Rect(sweetX, y, sweetW, gaugeHeight), darkTex);

        // Fill bar (Cyan / Gold)
        float fillW = gaugeWidth * currentCharge;
        bool inSweetSpot = currentCharge >= sweetSpotMin && currentCharge <= sweetSpotMax;
        GUI.color = inSweetSpot ? new Color(0.2f, 1f, 0.4f, 1f) : new Color(1f, 0.8f, 0.1f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, fillW, gaugeHeight), darkTex);

        // Target bracket lines
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(sweetX, y - 2, 2, gaugeHeight + 4), darkTex);
        GUI.DrawTexture(new Rect(sweetX + sweetW, y - 2, 2, gaugeHeight + 4), darkTex);

        GUI.color = Color.white;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

