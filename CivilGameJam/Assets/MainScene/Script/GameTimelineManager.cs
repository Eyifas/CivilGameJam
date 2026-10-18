using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public enum GameStage
{
    Stage1_Preparation,    // 60s pre-earthquake prep
    Stage2_Tremor,         // 5s earthquake tremor choice
    Stage3_Utilities,      // Post-shaking dark apartment, gas leak, cabinet QTE
    Stage4_Stairwell,      // Dark stairs, planter hazard, Mr. Ahmet triage
    Stage5_StreetFinale,   // Street aftershock, sign QTE, rumor choice, assembly area
    Rewinding,             // Time loop rewind transition
    Victory                // Safe ending at assembly area
}

[System.Serializable]
public struct TimelineSnapshot
{
    public string actionDescription;
    public GameStage stage;
    public float stageTimer;
    public Vector3 playerPosition;

    // Stage 1
    public bool isBookshelfAnchored;
    public bool isPaintingRemoved;
    public bool isCandleExtinguished;
    public bool hasFlashlight;
    public bool hasFirstAidKit;
    public List<string> inventoryItems;

    // Stage 3
    public bool isCabinetSecured;
    public bool isDolapSecured;
    public bool isGasShutOff;
    public bool isBreakerShutOff;
    public bool isElectricityRestored;

    // Stage 4 & 5
    public bool isPlanterMoved;
    public bool isNeighborTreated;
    public bool isSignDodged;
}

/// <summary>
/// Unified Game State & Timeline Manager for Sıfırıncı Dakika.
/// Tracks player inventory, hazards fixed, stage progression, timers, and time-loop rewinds.
/// </summary>
public class GameTimelineManager : MonoBehaviour
{
    public static GameTimelineManager Instance { get; private set; }

    [Header("Current Timeline Stage")]
    [SerializeField] private GameStage currentStage = GameStage.Stage1_Preparation;

    [Header("Stage 1: State & Inventory")]
    [SerializeField] private bool isBookshelfAnchored = false;
    [SerializeField] private bool isPaintingRemoved = false;
    [SerializeField] private bool isCandleExtinguished = false;
    [SerializeField] private bool hasFlashlight = false;
    [SerializeField] private bool hasFirstAidKit = false;

    [Header("Stage 3: State")]
    [SerializeField] private bool isCabinetSecured = false;
    [SerializeField] private bool isDolapSecured = false;
    [SerializeField] private bool isGasShutOff = false;
    [SerializeField] private bool isBreakerShutOff = false;
    [SerializeField] private bool isElectricityRestored = false;
    public bool IsElectricityRestored => isElectricityRestored;
    public void SetElectricityRestored(bool restored)
    {
        isElectricityRestored = restored;
        OnStateUpdated?.Invoke();
    }

    [Header("Stage 4 & 5: State")]
    [SerializeField] private bool isPlanterMoved = false;
    [SerializeField] private bool isNeighborTreated = false;
    [SerializeField] private bool isSignDodged = false;

    [Header("Stage Timers")]
    [SerializeField] private float stage1Duration = 60f;
    [SerializeField] private float stage2TremorDuration = 5f;
    [SerializeField] private float stage3Duration = 30f;
    private float currentStageTimer = 0f;
    private bool isTimerRunning = false;
    public float CurrentStageTimer => currentStageTimer;

    [Header("Time Loop Limits & Settings")]
    [SerializeField] private int maxRewindsPerGame = 3;
    [SerializeField] private int rewindsUsed = 0;

    public int RewindsRemaining => Mathf.Max(0, maxRewindsPerGame - rewindsUsed);
    public int MaxRewinds => maxRewindsPerGame;

    private Stack<TimelineSnapshot> actionHistory = new Stack<TimelineSnapshot>();

    [Header("HUD References")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text stageObjectiveText;
    [SerializeField] private TMP_Text inventoryHUDText;

    [Header("Time Loop Rewind UI")]
    [SerializeField] private GameObject rewindPanel;
    [SerializeField] private TMP_Text rewindReasonText;
    [SerializeField] private TMP_Text rewindCountdownText;
    [SerializeField] private float rewindDelay = 3.5f;

    [Header("Intro Rewind Settings")]
    [Tooltip("If true, plays the time rewind clock animation when entering the game before Stage 1 starts")]
    [SerializeField] private bool playIntroRewind = true;
    [TextArea(2, 4)]
    [SerializeField] private string introRewindMessage = "60 SECONDS BEFORE EARTHQUAKE — BE READY!\nSecure loose hazards & pack emergency supplies!";

    [Header("System References")]
    [SerializeField] private RoomCamera roomCamera;
    [SerializeField] private WorldChoiceGroup stage2ChoiceGroup;
    [SerializeField] private PlayerMovement playerMovement;

    // Events
    public event Action<GameStage> OnStageChanged;
    public event Action OnStateUpdated;

    private bool hasStarted = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        RebindSceneReferences();
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
        RebindSceneReferences();
        if (scene.name == "MainScene")
        {
            EnsureTimerTextReference();
            SetHUDVisibility(true);
            UpdateTimerHUD();

            if (currentStage == GameStage.Stage3_Utilities)
            {
                EnableStage3Interactions();
            }
            else if (currentStage == GameStage.Stage1_Preparation)
            {
                ReEnableStage1Interactions();
            }
        }
    }

    private void RebindSceneReferences()
    {
        if (roomCamera == null) roomCamera = FindFirstObjectByType<RoomCamera>();
        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (roomCamera != null && playerMovement != null && roomCamera.player == null)
        {
            roomCamera.player = playerMovement.transform;
        }
        if (stage2ChoiceGroup == null) stage2ChoiceGroup = FindFirstObjectByType<WorldChoiceGroup>(FindObjectsInactive.Include);
        EnsureCupboardTriggerZone();
        EnsureMainSceneDolap();
        EnsureHazardVisualManager();
        EnsureApartmentExitDoor();
        EnsureTimerTextReference();
    }

    private void EnsureMainSceneDolap()
    {
        if (MainSceneDolap.Instance == null)
        {
            var dolap = GameObject.Find("düşme dolap1");
            if (dolap == null)
            {
                var allSr = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var sr in allSr)
                {
                    if (sr.name.IndexOf("dolap", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        (sr.name.IndexOf("düşme", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         sr.name.IndexOf("dsme", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        dolap = sr.gameObject;
                        break;
                    }
                }
            }

            if (dolap != null)
            {
                if (!dolap.TryGetComponent<MainSceneDolap>(out var msd))
                {
                    msd = dolap.AddComponent<MainSceneDolap>();
                }
                msd.SyncToTimelineState();
            }
        }
    }

    private void EnsureApartmentExitDoor()
    {
        if (ApartmentExitDoor.Instance == null)
        {
            var existing = FindFirstObjectByType<ApartmentExitDoor>();
            if (existing == null)
            {
                var doorObj = new GameObject("ApartmentExitDoor");
                doorObj.transform.position = new Vector3(16.8f, -1.8f, 0f);
                doorObj.AddComponent<ApartmentExitDoor>();
            }
        }
    }

    private void EnsureHazardVisualManager()
    {
        if (HazardVisualManager.Instance == null)
        {
            var go = GameObject.Find("HazardVisualManager");
            if (go == null)
            {
                go = new GameObject("HazardVisualManager");
            }
            if (!go.TryGetComponent<HazardVisualManager>(out var hvm))
            {
                hvm = go.AddComponent<HazardVisualManager>();
            }
        }
    }

    public void PauseTimer()
    {
        isTimerRunning = false;
    }

    public void ResumeTimer()
    {
        isTimerRunning = true;
    }

    private void EnsureCupboardTriggerZone()
    {
        var dolap = GameObject.Find("dolap mutfak1");
        if (dolap != null)
        {
            var zone = dolap.transform.Find("InteractionZone");
            if (zone != null)
            {
                if (!zone.TryGetComponent<CupboardTriggerZone>(out var ctz))
                {
                    ctz = zone.gameObject.AddComponent<CupboardTriggerZone>();
                }
                ctz.CheckStatus();
            }
        }
    }

    private void Start()
    {
        if (!hasStarted)
        {
            hasStarted = true;
            EnsureTimeRewindController();
            EnsureHazardVisualManager();
            if (rewindPanel != null) rewindPanel.SetActive(false);

            if (playIntroRewind)
            {
                StartCoroutine(PlayIntroAndStartGame());
            }
            else
            {
                StartStage1();
            }
        }
    }

    private IEnumerator PlayIntroAndStartGame()
    {
        if (playerMovement != null) playerMovement.enabled = false;
        SetHUDVisibility(false);

        // Wait a frame for scene and system initialization
        yield return null;

        EnsureTimeRewindController();

        TimeRewindController.Instance.PlayIntroRewind(
            introMessage: introRewindMessage,
            onComplete: () =>
            {
                SetHUDVisibility(true);
                StartStage1();
            }
        );
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            TriggerRewind("TEST REWIND: Electric spark caused gas explosion! Undoing fatal action and adding 5 seconds.");
        }
#endif

        if (isTimerRunning)
        {
            currentStageTimer -= Time.deltaTime;
            currentStageTimer = Mathf.Max(0f, currentStageTimer);

            UpdateTimerHUD();

            if (currentStageTimer <= 0f)
            {
                OnStageTimerExpired();
            }
        }
    }

    #region --- ACTION SNAPSHOT SYSTEM ---

    public void RecordActionSnapshot(string actionDescription)
    {
        Vector3 pos = playerMovement != null ? playerMovement.transform.position : new Vector3(-9.6f, -2.2f, 0f);
        TimelineSnapshot snapshot = new TimelineSnapshot
        {
            actionDescription = actionDescription,
            stage = currentStage,
            stageTimer = currentStageTimer,
            playerPosition = pos,

            isBookshelfAnchored = isBookshelfAnchored,
            isPaintingRemoved = isPaintingRemoved,
            isCandleExtinguished = isCandleExtinguished,
            hasFlashlight = hasFlashlight,
            hasFirstAidKit = hasFirstAidKit,
            inventoryItems = InventoryManager.GetItemsCopy(),

            isCabinetSecured = isCabinetSecured,
            isDolapSecured = isDolapSecured,
            isGasShutOff = isGasShutOff,
            isBreakerShutOff = isBreakerShutOff,
            isElectricityRestored = isElectricityRestored,

            isPlanterMoved = isPlanterMoved,
            isNeighborTreated = isNeighborTreated,
            isSignDodged = isSignDodged
        };
        actionHistory.Push(snapshot);
        Debug.Log($"[Timeline] Recorded action snapshot: '{actionDescription}' (History depth: {actionHistory.Count})");
    }

    public float CalculateRewindDuration(GameStage failedStage, string educationalReason)
    {
        return CalculateRewindDuration(failedStage, educationalReason, currentStageTimer);
    }

    public float CalculateRewindDuration(GameStage failedStage, string educationalReason, float timeLeft)
    {
        // If the user fails while the time left is less than 10 seconds, give 15 seconds
        // because 5 seconds would be too short since the countdown starts at 5 seconds.
        if (timeLeft < 10f)
        {
            return 15f;
        }

        // Major preparation mistakes (unaddressed painting / candle) or stage timeouts get 15s.
        // Quick reaction mistakes (switch spark, door rush, cabinet QTE) get 5s.
        bool isMajorPrepOrTimeout =
            failedStage == GameStage.Stage1_Preparation ||
            educationalReason.IndexOf("painting", StringComparison.OrdinalIgnoreCase) >= 0 ||
            educationalReason.IndexOf("candle", StringComparison.OrdinalIgnoreCase) >= 0 ||
            educationalReason.IndexOf("TIME RAN OUT", StringComparison.OrdinalIgnoreCase) >= 0 ||
            educationalReason.IndexOf("Lingered too long", StringComparison.OrdinalIgnoreCase) >= 0;

        return isMajorPrepOrTimeout ? 15f : 5f;
    }

    #endregion

    #region --- STAGE 1: PREPARATION ---

    public void StartStage1()
    {
        currentStage = GameStage.Stage1_Preparation;
        currentStageTimer = stage1Duration;
        isTimerRunning = true;

        EnsureTimerTextReference();
        SetHUDVisibility(true);
        UpdateTimerHUD();

        if (playerMovement != null) playerMovement.enabled = true;

        if (actionHistory.Count == 0)
        {
            RecordActionSnapshot("Stage 1 Initial Start");
        }

        SetObjectiveText("STAGE 1: Fix room hazards & pack flashlight + first-aid kit!");
        UpdateInventoryHUD();
        OnStageChanged?.Invoke(currentStage);

        Debug.Log("[Timeline] Stage 1 Started: 60s Room Preparation.");
    }

    public void SetBookshelfAnchored(bool anchored)
    {
        if (currentStage != GameStage.Rewinding && isBookshelfAnchored != anchored)
        {
            RecordActionSnapshot($"Bookshelf Anchored: {anchored}");
        }
        isBookshelfAnchored = anchored;
        Debug.Log($"[Timeline] Bookshelf Anchored: {anchored}");
        UpdateInventoryHUD();
        CheckStage1EarlyCompletion();
    }

    public void SetPaintingRemoved(bool removed)
    {
        if (currentStage != GameStage.Rewinding && isPaintingRemoved != removed)
        {
            RecordActionSnapshot($"Painting Removed: {removed}");
        }
        isPaintingRemoved = removed;
        Debug.Log($"[Timeline] Painting Removed: {removed}");
        UpdateInventoryHUD();
        CheckStage1EarlyCompletion();
        OnStateUpdated?.Invoke();
    }

    public void SetCandleExtinguished(bool extinguished)
    {
        if (currentStage != GameStage.Rewinding && isCandleExtinguished != extinguished)
        {
            RecordActionSnapshot($"Candle Extinguished: {extinguished}");
        }
        isCandleExtinguished = extinguished;
        Debug.Log($"[Timeline] Candle Extinguished: {extinguished}");
        UpdateInventoryHUD();
        CheckStage1EarlyCompletion();
        OnStateUpdated?.Invoke();
    }

    public void CollectFlashlight()
    {
        if (currentStage != GameStage.Rewinding && !hasFlashlight)
        {
            RecordActionSnapshot("Collected Flashlight");
        }
        hasFlashlight = true;
        Debug.Log("[Timeline] Flashlight collected!");
        UpdateInventoryHUD();
        CheckStage1EarlyCompletion();
    }

    public void CollectFirstAidKit()
    {
        if (currentStage != GameStage.Rewinding && !hasFirstAidKit)
        {
            RecordActionSnapshot("Collected First Aid Kit");
        }
        hasFirstAidKit = true;
        Debug.Log("[Timeline] First-Aid Kit collected!");
        UpdateInventoryHUD();
        CheckStage1EarlyCompletion();
    }

    private void CheckStage1EarlyCompletion()
    {
        // If player finished all 5 Stage 1 tasks early
        if (isBookshelfAnchored && isPaintingRemoved && isCandleExtinguished && hasFlashlight && hasFirstAidKit)
        {
            Debug.Log("[Timeline] All Stage 1 preparations complete! Early trigger ready if needed.");
        }
    }

    #endregion

    #region --- STAGE 2: THE TREMOR ---

    public void StartStage2()
    {
        if (currentStage != GameStage.Rewinding)
        {
            RecordActionSnapshot("Earthquake Tremor Began");
        }

        currentStage = GameStage.Stage2_Tremor;
        isTimerRunning = false;

        Debug.Log("[Timeline] STAGE 2: Destructive S-waves strike!");
        SetObjectiveText("EARTHQUAKE STRIKES! MAKE YOUR DECISION!");

        // Start violent screen shake
        if (roomCamera != null)
        {
            roomCamera.StartEarthquake();
        }

        // Freeze player movement so they make their choice
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        // Cancel any active QTE (e.g. if player was anchoring bookshelf when earthquake struck)
        if (QTEManager.Instance != null)
        {
            QTEManager.Instance.CancelAll();
        }

        // Disable all Stage 1 interaction zones, prompts, and room items
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.DisableAllPrompts();
        }

        var zones = FindObjectsByType<InteractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var z in zones) z.SetInteractable(false);

        var roomItems = FindObjectsByType<RoomItemInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var r in roomItems) r.SetInteractable(false);

        var bookshelves = FindObjectsByType<BookshelfAnchorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in bookshelves) b.SetInteractable(false);

        // Present world-space choices: [S] Desk vs [E] Door
        if (stage2ChoiceGroup != null)
        {
            stage2ChoiceGroup.PresentChoices(
                headerTitle: "EARTHQUAKE HITS! CHOOSE FAST!",
                duration: stage2TremorDuration,
                onChoice: HandleStage2Choice,
                onTimeout: HandleStage2Timeout
            );
        }
        else
        {
            Debug.LogError("[Timeline] Stage2ChoiceGroup is not assigned!");
        }

        OnStageChanged?.Invoke(currentStage);
    }

    private void HandleStage2Choice(string actionId)
    {
        if (currentStage != GameStage.Rewinding)
        {
            RecordActionSnapshot($"Tremor Choice: {actionId}");
        }

        bool choseDesk = actionId.Equals("desk", StringComparison.OrdinalIgnoreCase) || 
                         actionId.Equals("DropCover", StringComparison.OrdinalIgnoreCase) ||
                         actionId.Equals("cover", StringComparison.OrdinalIgnoreCase);

        bool choseDoor = actionId.Equals("exit", StringComparison.OrdinalIgnoreCase) || 
                         actionId.Equals("RunDoor", StringComparison.OrdinalIgnoreCase) ||
                         actionId.Equals("door", StringComparison.OrdinalIgnoreCase);

        if (choseDesk)
        {
            // Player chose Drop, Cover, Hold On under the desk
            if (!isPaintingRemoved)
            {
                // Painting falls from above the desk!
                TriggerRewind("FATAL INJURY: The heavy wall painting above the desk fell and shattered glass! Always remove or securely anchor heavy wall hangings over desks/beds!");
            }
            else if (!isCandleExtinguished)
            {
                // Candle tipped over and ignited apartment!
                TriggerRewind("APARTMENT FIRE: The lit candle on the desk toppled over during the tremor, igniting the room! Always extinguish open flames and candles before an earthquake strikes!");
            }
            else
            {
                // Survived safely!
                Debug.Log("<color=green>[Timeline] Survived the tremor safely under the desk!</color>");
                StartCoroutine(TransitionToStage3Sequence());
            }
        }
        else if (choseDoor)
        {
            // Player panicked and tried to run
            TriggerRewind("FATAL FALL: Violent lateral forces threw you into walls and falling debris! Never run while the building is shaking—Drop, Cover, and Hold On!");
        }
    }

    private void HandleStage2Timeout()
    {
        if (currentStage != GameStage.Rewinding)
        {
            RecordActionSnapshot("Tremor Decision Timed Out");
        }
        TriggerRewind("TIME RAN OUT: Frozen in panic! In an earthquake, drop under sturdy furniture within seconds before violent shaking knocks you down!");
    }

    private IEnumerator TransitionToStage3Sequence()
    {
        SetObjectiveText("Main tremor subsides... Power cuts out! Total darkness.");
        yield return new WaitForSeconds(2.0f);
        StartStage3();
    }

    #endregion

    #region --- STAGE 3: POST-SHAKING UTILITIES ---

    public void StartStage3()
    {
        if (currentStage != GameStage.Rewinding)
        {
            RecordActionSnapshot("Stage 3 Utilities Began");
        }

        currentStage = GameStage.Stage3_Utilities;
        float carriedOverBonus = Mathf.Max(0f, currentStageTimer);
        currentStageTimer = stage3Duration + carriedOverBonus;
        isTimerRunning = true;

        if (playerMovement != null) playerMovement.enabled = true;

        SetObjectiveText("STAGE 3: Gas leak detected! Secure cabinet & shut off utilities safely.");
        Debug.Log($"[Timeline] Stage 3 Started: Post-Shaking Utilities & Hazards. Timer: {currentStageTimer:F1}s (including +{carriedOverBonus:F1}s bonus).");
        UpdateTimerHUD();
        EnableStage3Interactions();
        OnStageChanged?.Invoke(currentStage);
    }

    public void HandleCabinetQTEResult(bool success)
    {
        if (currentStage != GameStage.Rewinding)
        {
            RecordActionSnapshot($"Cabinet QTE: {success}");
        }

        isCabinetSecured = success;
        if (success)
        {
            Debug.Log("<color=green>[Timeline] Kitchen cabinet secured safely!</color>");
        }
        else
        {
            TriggerRewind("FATAL INJURY: The shifting cabinet fell on you during the aftershock! React quickly to secure loose overhead obstacles!");
        }
    }

    public void HandleDolapQTEResult(bool success)
    {
        if (currentStage != GameStage.Rewinding)
        {
            RecordActionSnapshot($"Hallway Dolap QTE: {success}");
        }

        isDolapSecured = success;
        if (success)
        {
            Debug.Log("<color=green>[Timeline] Hallway wardrobe safely lifted and placed upright!</color>");
            if (MainSceneDolap.Instance != null)
            {
                MainSceneDolap.Instance.SyncToTimelineState();
            }
        }
        else
        {
            TriggerRewind("FATAL INJURY: The heavy wardrobe fell back onto you! You must brace and hold your footing firmly!");
        }
        OnStateUpdated?.Invoke();
    }

    public void HandleUtilityAction(string action)
    {
        if (currentStage != GameStage.Rewinding)
        {
            RecordActionSnapshot($"Utility Action: {action}");
        }

        if (action == "LightSwitch")
        {
            TriggerRewind("FATAL EXPLOSION: The electrical light switch sparked and ignited the leaking gas! NEVER flip light switches or create electrical sparks when you smell gas!");
        }
        else if (action == "RushOut")
        {
            Debug.LogWarning("[Timeline] Building Fire Ending: You escaped, but leaking gas caused the building to burn down!");
            AdvanceToStage4();
        }
        else if (action == "ShutOffValves")
        {
            if (!hasFlashlight)
            {
                TriggerRewind("IN THE DARK: You couldn't locate the gas valve in total darkness and tripped over broken fixtures! A packed flashlight is essential for utility shut-off!");
                return;
            }

            isGasShutOff = true;
            isBreakerShutOff = true;
            Debug.Log("<color=green>[Timeline] Gas valve and electrical breaker safely shut off! Explosion prevented!</color>");
            AdvanceToStage4();
        }
    }

    #endregion

    #region --- STAGE 4: STAIRWELL & NEIGHBOR ---

    public void AdvanceToStage4()
    {
        if (currentStage != GameStage.Rewinding)
        {
            RecordActionSnapshot("Stage 4 Stairwell Began");
        }

        currentStage = GameStage.Stage4_Stairwell;
        isTimerRunning = false;
        SetObjectiveText("STAGE 4: Navigate dark stairwell & assist Mr. Ahmet.");
        Debug.Log("[Timeline] Stage 4: Stairwell & Neighbor Triage.");
        OnStageChanged?.Invoke(currentStage);
    }

    public void HandleStairwellPlanterHazard()
    {
        if (hasFlashlight)
        {
            isPlanterMoved = true;
            Debug.Log("<color=green>[Timeline] Flashlight illuminated fallen planter! Moved aside safely.</color>");
        }
        else
        {
            TriggerRewind("INJURY: In total darkness without a flashlight, you tripped over the heavy planter and fell down the stairs!");
        }
    }

    public void HandleNeighborTriage()
    {
        if (hasFlashlight && hasFirstAidKit)
        {
            isNeighborTreated = true;
            Debug.Log("<color=green>[Timeline] Applied pressure bandage with First-Aid Kit! Mr. Ahmet stabilized!</color>");
            AdvanceToStage5();
        }
        else if (hasFlashlight && !hasFirstAidKit)
        {
            Debug.LogWarning("[Timeline] Tragic Outcome: Left the First-Aid Kit behind. Mr. Ahmet bled out on the sidewalk.");
            AdvanceToStage5();
        }
        else
        {
            TriggerRewind("BLIND SEARCH: Without a flashlight, you couldn't locate or treat Mr. Ahmet in the dark corridor.");
        }
    }

    #endregion

    #region --- STAGE 5: STREET & ASSEMBLY AREA ---

    public void AdvanceToStage5()
    {
        if (currentStage != GameStage.Rewinding)
        {
            RecordActionSnapshot("Stage 5 Street Began");
        }

        currentStage = GameStage.Stage5_StreetFinale;
        isTimerRunning = false;
        SetObjectiveText("STAGE 5: Navigate street aftershock & reach Toplanma Alanı!");
        Debug.Log("[Timeline] Stage 5: Street, Misinformation & Finale.");
        OnStageChanged?.Invoke(currentStage);
    }

    public void HandleStreetSignQTEResult(bool success)
    {
        if (currentStage != GameStage.Rewinding)
        {
            RecordActionSnapshot($"Sign QTE: {success}");
        }

        isSignDodged = success;
        if (success)
        {
            Debug.Log("<color=green>[Timeline] Successfully dodged the falling storefront sign!</color>");
        }
        else
        {
            TriggerRewind("FATAL TRAUMA: Struck by falling storefront facade! Watch overhead hazards when stepping into the street!");
        }
    }

    public void HandleStreetRumorChoice(bool choseCar)
    {
        if (currentStage != GameStage.Rewinding)
        {
            RecordActionSnapshot($"Rumor Choice: ChoseCar={choseCar}");
        }
        if (choseCar)
        {
            TriggerRewind("GRIDLOCK TRAP: You and hundreds of panicked drivers gridlocked the mountain road, blocking emergency rescue vehicles! Stay out of cars!");
        }
        else
        {
            Debug.Log("<color=green>[VICTORY] Checked official e-Devlet alert and led neighbors on foot to the Toplanma Alanı!</color>");
            currentStage = GameStage.Victory;
            SetObjectiveText("VICTORY! Safely reached the Official Disaster Assembly Area!");
            InventorySelection.ResetSelection();
            if (playerMovement != null) playerMovement.ResetFlashlight();
            OnStageChanged?.Invoke(currentStage);

            // Display Comic Panel 4 Cliffhanger Ending
            PlayEndingCutsceneSequence();
        }
    }

    private void PlayEndingCutsceneSequence()
    {
        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.TurnOffAllLights();
            playerMovement.enabled = false;
            var rb = playerMovement.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        var roomCam = FindFirstObjectByType<RoomCamera>();
        if (roomCam != null) roomCam.StopEarthquake();

        SetHUDVisibility(false);

        if (PersistentCanvas.Instance != null)
        {
            var pcCanvas = PersistentCanvas.Instance.GetComponent<Canvas>();
            if (pcCanvas != null) pcCanvas.enabled = false;
        }

        if (QTEManager.Instance != null) QTEManager.Instance.CancelAll();
        var choiceGroup = FindFirstObjectByType<WorldChoiceGroup>(FindObjectsInactive.Include);
        if (choiceGroup != null) choiceGroup.gameObject.SetActive(false);

        Debug.Log("<color=green>[VICTORY] Starting comic outro cutscene followed by hand-drawn success screen...</color>");

        VictorySuccessDisplay.PlayOutroThenSuccess(
            onRestart: () =>
            {
                ResetToCheckpoint();
            },
            onMainMenu: () =>
            {
                StartMenuController.ClearDontDestroyOnLoadObjects();
                UnityEngine.SceneManagement.SceneManager.LoadScene("StartScene");
            }
        );
    }

    #endregion

    #region --- TIME LOOP REWIND SYSTEM ---

    public void TriggerRewind(string educationalReason)
    {
        TriggerRewind(educationalReason, false, null, null);
    }

    public void TriggerRewind(string educationalReason, Action onStateRestore, Action onComplete = null)
    {
        TriggerRewind(educationalReason, false, onStateRestore, onComplete);
    }

    public void TriggerRewind(string educationalReason, bool isFromVisual, Action onStateRestore = null, Action onComplete = null)
    {
        if (currentStage == GameStage.Rewinding) return;

        // If not already triggered from a hazard visual, display the comic failure visual first!
        if (!isFromVisual)
        {
            if (HazardVisualManager.Instance != null)
            {
                HazardVisualManager.Instance.ShowHazardVisual(educationalReason, () =>
                {
                    TriggerRewind(educationalReason, isFromVisual: true, onStateRestore, onComplete);
                });
                return;
            }
        }

        // Check if all available time loop rewinds have been exhausted
        if (rewindsUsed >= maxRewindsPerGame)
        {
            Debug.LogWarning($"<color=red>[TIME LOOP] Max rewinds reached ({rewindsUsed}/{maxRewindsPerGame})! Timeline Collapsed.</color>");
            TriggerGameOverSequence(educationalReason);
            return;
        }

        rewindsUsed++;
        int loopsRemaining = maxRewindsPerGame - rewindsUsed;

        GameStage failedStage = currentStage;
        currentStage = GameStage.Rewinding;
        isTimerRunning = false;

        Debug.Log($"<color=red>[TIME LOOP REWIND] (Used: {rewindsUsed}/{maxRewindsPerGame}, Remaining: {loopsRemaining}) {educationalReason}</color>");

        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.TurnOffAllLights();
            playerMovement.enabled = false;
        }
        InventorySelection.ResetSelection();
        if (QTEManager.Instance != null) QTEManager.Instance.CancelAll();
        if (roomCamera != null) roomCamera.StopEarthquake();
        if (stage2ChoiceGroup != null) stage2ChoiceGroup.gameObject.SetActive(false);

        // Calculate dynamic rewind duration:
        // Major prep mistakes (unaddressed painting / candle) or stage timeouts get 15s.
        // Quick reaction mistakes (switch spark, door rush, cabinet QTE) get 5s.
        float addedDuration = CalculateRewindDuration(failedStage, educationalReason);

        EnsureTimeRewindController();
        TimeRewindController.Instance.PlayTimeRewind(
            educationalReason: educationalReason,
            addedSeconds: addedDuration,
            remainingLoops: loopsRemaining,
            maxLoops: maxRewindsPerGame,
            onRewindStateRestore: () =>
            {
                if (HazardVisualManager.Instance != null)
                {
                    HazardVisualManager.Instance.Hide();
                }
                SwitchInteraction.ResetAllHazardStates();
                onStateRestore?.Invoke();
                UndoLastFatalActionAndRestore(failedStage, educationalReason, addedDuration);
                Time.timeScale = 1f;
            },
            onComplete: () =>
            {
                if (HazardVisualManager.Instance != null)
                {
                    HazardVisualManager.Instance.Hide();
                }
                Time.timeScale = 1f;
                onComplete?.Invoke();
                if (playerMovement != null && currentStage != GameStage.Stage2_Tremor)
                {
                    playerMovement.enabled = true;
                }
            }
        );
    }

    private void TriggerGameOverSequence(string educationalReason)
    {
        currentStage = GameStage.Rewinding;
        isTimerRunning = false;

        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.TurnOffAllLights();
            playerMovement.enabled = false;
        }
        InventorySelection.ResetSelection();
        if (QTEManager.Instance != null) QTEManager.Instance.CancelAll();
        if (roomCamera != null) roomCamera.StopEarthquake();
        if (stage2ChoiceGroup != null) stage2ChoiceGroup.gameObject.SetActive(false);

        EnsureTimeRewindController();
        TimeRewindController.Instance.PlayGameOverSequence(
            educationalReason: educationalReason,
            onRestart: () =>
            {
                ResetToCheckpoint();
            }
        );
    }

    private void EnsureTimeRewindController()
    {
        if (TimeRewindController.Instance == null)
        {
            GameObject obj = new GameObject("TimeRewindController");
            obj.AddComponent<TimeRewindController>();
        }
    }

    private void UndoLastFatalActionAndRestore(GameStage failedStage, string educationalReason, float addedDuration)
    {
        // If the player failed while inside a sub-scene (like CupboardScene), load MainScene behind closed eyes
        if (SceneManager.GetActiveScene().name != "MainScene")
        {
            SceneManager.LoadScene("MainScene");
            RebindSceneReferences();
        }

        // 1. Retrieve the snapshot before the last fatal action was performed
        TimelineSnapshot snapshot;
        if (actionHistory.Count > 1)
        {
            snapshot = actionHistory.Pop();
            Debug.Log($"<color=yellow>[Timeline] Reverting last action '{snapshot.actionDescription}' as if it never happened!</color>");
        }
        else if (actionHistory.Count == 1)
        {
            snapshot = actionHistory.Peek();
            Debug.Log($"<color=yellow>[Timeline] Restoring to initial baseline state '{snapshot.actionDescription}'</color>");
        }
        else
        {
            snapshot = new TimelineSnapshot
            {
                actionDescription = "Baseline State",
                stage = GameStage.Stage1_Preparation,
                stageTimer = stage1Duration,
                playerPosition = new Vector3(-9.6f, -2.2f, 0f),
                inventoryItems = new List<string>()
            };
        }

        // 2. Restore logical flags from snapshot
        isBookshelfAnchored = snapshot.isBookshelfAnchored;
        isPaintingRemoved = snapshot.isPaintingRemoved;
        isCandleExtinguished = snapshot.isCandleExtinguished;
        hasFlashlight = snapshot.hasFlashlight;
        hasFirstAidKit = snapshot.hasFirstAidKit;

        isCabinetSecured = snapshot.isCabinetSecured;
        isDolapSecured = snapshot.isDolapSecured;
        isGasShutOff = snapshot.isGasShutOff;
        isBreakerShutOff = snapshot.isBreakerShutOff;
        isElectricityRestored = snapshot.isElectricityRestored;

        isPlanterMoved = snapshot.isPlanterMoved;
        isNeighborTreated = snapshot.isNeighborTreated;
        isSignDodged = snapshot.isSignDodged;

        // 3. Restore inventory items
        InventoryManager.SetItems(snapshot.inventoryItems);
        InventorySelection.ResetSelection();

        // 4. Physical / Visual restoration of room objects
        // (a) Restore RoomItemInteractable items (Painting, Flashlight, Medkit)
        var roomItems = FindObjectsByType<RoomItemInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var item in roomItems)
        {
            if (item.ItemType == ItemPickupType.RemovePainting)
            {
                item.RestoreState(isPaintingRemoved);
            }
            else if (item.ItemType == ItemPickupType.Flashlight)
            {
                item.RestoreState(hasFlashlight);
            }
            else if (item.ItemType == ItemPickupType.FirstAidKit)
            {
                item.RestoreState(hasFirstAidKit);
            }
        }

        // (b) Restore Bookshelf state
        var bookshelves = FindObjectsByType<BookshelfAnchorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in bookshelves)
        {
            b.RestoreState(isBookshelfAnchored);
        }

        // (c) Sync Table & Painting position and sprite via InteractionZones
        var zones = FindObjectsByType<InteractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var z in zones)
        {
            z.CheckCompletionStatus();
        }

        // (d) Sync Cupboard trigger zone in MainScene
        var cupboardTriggers = FindObjectsByType<CupboardTriggerZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ctz in cupboardTriggers)
        {
            ctz.CheckStatus();
        }

        // 5. Restore Stage & Timer with dynamic added time
        if (failedStage == GameStage.Stage2_Tremor || failedStage == GameStage.Stage1_Preparation)
        {
            // Hazard left unaddressed, door rush, or panic freeze -> Rewind to Stage 1 Preparation
            // The Stage 2 earthquake choice will only trigger based on the game timer expiring
            currentStage = GameStage.Stage1_Preparation;
            currentStageTimer = Mathf.Max(snapshot.stageTimer + addedDuration, addedDuration);
            isTimerRunning = true;
            stage2TremorDuration = 5f;

            if (roomCamera != null)
            {
                roomCamera.StopEarthquake();
            }

            if (stage2ChoiceGroup != null)
            {
                stage2ChoiceGroup.gameObject.SetActive(false);
            }

            if (failedStage == GameStage.Stage2_Tremor)
            {
                SetObjectiveText($"TIME REWOUND (+{addedDuration:0}s)! Fix room hazards & take cover before earthquake strikes!");
            }
            else
            {
                SetObjectiveText($"STAGE 1 (+{addedDuration:0}s): Fix room hazards & pack flashlight + first-aid kit!");
            }

            ReEnableStage1Interactions();

            if (playerMovement != null)
            {
                playerMovement.enabled = true;
                playerMovement.transform.position = snapshot.playerPosition != Vector3.zero
                    ? snapshot.playerPosition
                    : new Vector3(-9.6f, -2.2f, 0f);
            }
        }
        else if (failedStage == GameStage.Stage3_Utilities)
        {
            currentStage = GameStage.Stage3_Utilities;
            currentStageTimer = Mathf.Max(snapshot.stageTimer + addedDuration, addedDuration);
            isTimerRunning = true;
            SetObjectiveText($"STAGE 3 (+{addedDuration:0}s): Gas leak detected! Secure cabinet & shut off utilities safely.");

            if (playerMovement != null)
            {
                playerMovement.enabled = true;
                playerMovement.transform.position = snapshot.playerPosition;
            }

            EnableStage3Interactions();
        }
        else if (failedStage == GameStage.Stage4_Stairwell)
        {
            currentStage = GameStage.Stage4_Stairwell;
            currentStageTimer = Mathf.Max(snapshot.stageTimer + addedDuration, addedDuration);
            isTimerRunning = true;
            SetObjectiveText($"STAGE 4 (+{addedDuration:0}s): Navigate dark stairwell & assist Mr. Ahmet.");
            if (playerMovement != null)
            {
                playerMovement.enabled = true;
                playerMovement.transform.position = snapshot.playerPosition;
            }
        }
        else if (failedStage == GameStage.Stage5_StreetFinale)
        {
            currentStage = GameStage.Stage5_StreetFinale;
            currentStageTimer = Mathf.Max(snapshot.stageTimer + addedDuration, addedDuration);
            isTimerRunning = true;
            SetObjectiveText($"STAGE 5 (+{addedDuration:0}s): Navigate street aftershock & reach Toplanma Alanı!");
            if (playerMovement != null)
            {
                playerMovement.enabled = true;
                playerMovement.transform.position = snapshot.playerPosition;
            }
        }

        UpdateTimerHUD();
        UpdateInventoryHUD();
        OnStateUpdated?.Invoke();
        OnStageChanged?.Invoke(currentStage);
    }

    private void ReEnableStage1Interactions()
    {
        var zones = FindObjectsByType<InteractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var z in zones)
        {
            // Kitchen pipe zone must NOT be interactable in Stage 1!
            if (z.IsPipeZone())
            {
                z.SetInteractable(false);
            }
            else
            {
                z.SetInteractable(true);
            }
        }

        var roomItems = FindObjectsByType<RoomItemInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var r in roomItems) r.SetInteractable(true);

        var bookshelves = FindObjectsByType<BookshelfAnchorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in bookshelves) b.SetInteractable(true);

        var cupboardTriggers = FindObjectsByType<CupboardTriggerZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ctz in cupboardTriggers) ctz.CheckStatus();

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.ResetPromptsSystem();
        }
    }

    public void EnableStage3Interactions()
    {
        // 1. Enable Pipe InteractionZone in kitchen (if gas is not already shut off)
        var zones = FindObjectsByType<InteractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var z in zones)
        {
            if (z.IsPipeZone())
            {
                z.SetInteractable(!isGasShutOff);
            }
            else
            {
                z.SetInteractable(false);
            }
        }

        // 2. Enable Cupboard trigger zone in kitchen
        var cupboardTriggers = FindObjectsByType<CupboardTriggerZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ctz in cupboardTriggers)
        {
            ctz.CheckStatus();
        }

        // 3. Update hallway dolap state
        if (MainSceneDolap.Instance != null)
        {
            MainSceneDolap.Instance.SyncToTimelineState();
        }

        // 4. Ensure prompts system refreshed
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.ResetPromptsSystem();
        }
    }

    public void ResetToCheckpoint()
    {
        // Reset time loop counters & history
        rewindsUsed = 0;
        actionHistory.Clear();
        stage2TremorDuration = 5f;

        // Reset flags
        isBookshelfAnchored = false;
        isPaintingRemoved = false;
        isCandleExtinguished = false;
        hasFlashlight = false;
        hasFirstAidKit = false;
        isCabinetSecured = false;
        isDolapSecured = false;
        isGasShutOff = false;
        isBreakerShutOff = false;
        isElectricityRestored = false;

        InventorySelection.ResetSelection();
        InventoryManager.ResetInventory();
        PlayerMovement.ClearSavedPosition();
        SwitchInteraction.ResetAllHazardStates();

        Time.timeScale = 1f;

        // Cleanly reload MainScene and rebind all references including top-right timer
        StartCoroutine(RestartMainSceneRoutine());
    }

    private IEnumerator RestartMainSceneRoutine()
    {
        Time.timeScale = 1f;

        var op = SceneManager.LoadSceneAsync("MainScene");
        while (!op.isDone)
        {
            yield return null;
        }

        yield return null; // Wait one frame for Awake/Start in MainScene

        RebindSceneReferences();
        EnsureTimerTextReference();
        SetHUDVisibility(true);

        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.transform.position = new Vector3(-9.6f, -2.2f, 0f);
            playerMovement.enabled = true;
            playerMovement.ResetFlashlight();
        }

        if (roomCamera == null) roomCamera = FindFirstObjectByType<RoomCamera>();
        if (roomCamera != null)
        {
            if (playerMovement != null) roomCamera.player = playerMovement.transform;
            roomCamera.StopEarthquake();
        }

        StartStage1();
        SetHUDVisibility(true);
        UpdateTimerHUD();
        OnStateUpdated?.Invoke();

        Debug.Log("<color=green>[Timeline] Successfully restarted run into fresh MainScene Stage 1 with top-right timer!</color>");
    }

    #endregion

    #region --- HELPER METHODS & UI ---

    private void OnStageTimerExpired()
    {
        isTimerRunning = false;

        if (currentStage == GameStage.Stage1_Preparation)
        {
            // If the player is in DrawerScene when the timer ends, force return to MainScene
            if (SceneManager.GetActiveScene().name != "MainScene")
            {
                Debug.Log("[Timeline] Timer expired while in DrawerScene! Returning to MainScene for earthquake!");
                StartCoroutine(LoadMainSceneAndStartTremor());
                return;
            }

            // 60s ran out -> Earth tremors strike!
            StartStage2();
        }
        else if (currentStage == GameStage.Stage3_Utilities)
        {
            if (isGasShutOff)
            {
                TriggerRewind("BUILDING COLLAPSE: You lingered inside the damaged apartment after the earthquake! Buildings can collapse at any moment from structural weakening and aftershocks. Once utilities are secured, evacuate immediately!");
            }
            else
            {
                TriggerRewind("TIME RAN OUT: Lingered too long in an apartment with leaking natural gas!");
            }
        }
    }

    private IEnumerator LoadMainSceneAndStartTremor()
    {
        var op = SceneManager.LoadSceneAsync("MainScene");
        while (!op.isDone)
        {
            yield return null;
        }

        yield return null; // wait one extra frame for all Awake/Start in MainScene
        RebindSceneReferences();
        StartStage2();
    }

    public void EnsureTimerTextReference()
    {
        if (timerText != null && timerText.gameObject != null)
        {
            ActivateTimerHierarchy(timerText);
            return;
        }

        // 1. Search for active or inactive GameObject named "FirstTime" (standard scene timer)
        var allTmps = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var tmp in allTmps)
        {
            if (tmp != null && (tmp.name == "FirstTime" || tmp.gameObject.name == "FirstTime"))
            {
                timerText = tmp;
                ActivateTimerHierarchy(timerText);
                Debug.Log("<color=cyan>[GameTimelineManager] Rebound timerText to 'FirstTime'.</color>");
                return;
            }
        }

        // 2. Search for any GameObject named "TimerText" or "Timer"
        foreach (var tmp in allTmps)
        {
            if (tmp != null && (tmp.name == "TimerText" || tmp.name == "Timer" || tmp.name == "TimeText"))
            {
                if (tmp.transform.IsChildOf(transform)) continue;
                if (tmp.GetComponentInParent<WorldChoiceGroup>() != null) continue;
                if (tmp.GetComponentInParent<QTEMashPrompt>() != null) continue;

                timerText = tmp;
                ActivateTimerHierarchy(timerText);
                Debug.Log($"<color=cyan>[GameTimelineManager] Rebound timerText to '{tmp.name}'.</color>");
                return;
            }
        }

        // 3. Procedural creation fallback in top-right of Canvas or PersistentCanvas if missing
        EnsureProceduralTopRightTimer();
    }

    private void ActivateTimerHierarchy(TMP_Text txt)
    {
        if (txt == null) return;
        txt.gameObject.SetActive(true);
        txt.enabled = true;

        Transform curr = txt.transform.parent;
        while (curr != null)
        {
            curr.gameObject.SetActive(true);
            var c = curr.GetComponent<Canvas>();
            if (c != null)
            {
                c.enabled = true;
                if (c.sortingOrder < 100)
                {
                    c.sortingOrder = 100;
                }
            }
            curr = curr.parent;
        }
    }

    private void EnsureProceduralTopRightTimer()
    {
        Canvas targetCanvas = null;
        if (PersistentCanvas.Instance != null)
        {
            targetCanvas = PersistentCanvas.Instance.GetComponent<Canvas>();
        }
        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Exclude);
        }
        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        }

        if (targetCanvas == null)
        {
            GameObject canvasObj = new GameObject("HUDCanvas");
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 200;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        targetCanvas.enabled = true;
        targetCanvas.gameObject.SetActive(true);
        if (targetCanvas.sortingOrder < 100) targetCanvas.sortingOrder = 100;

        Transform existing = targetCanvas.transform.Find("ProceduralTimerText");
        if (existing != null)
        {
            timerText = existing.GetComponent<TMP_Text>();
            if (timerText != null)
            {
                timerText.gameObject.SetActive(true);
                timerText.enabled = true;
                return;
            }
        }

        GameObject timerObj = new GameObject("ProceduralTimerText");
        timerObj.transform.SetParent(targetCanvas.transform, false);

        var rect = timerObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -30f);
        rect.sizeDelta = new Vector2(250f, 60f);

        var tmpComp = timerObj.AddComponent<TextMeshProUGUI>();
        tmpComp.fontSize = 38f;
        tmpComp.fontStyle = FontStyles.Bold;
        tmpComp.alignment = TextAlignmentOptions.Right;
        tmpComp.color = Color.white;
        tmpComp.text = "01:00";

        var outline = timerObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        timerText = tmpComp;
        timerText.gameObject.SetActive(true);
        Debug.Log("<color=cyan>[GameTimelineManager] Created procedural top-right timer HUD.</color>");
    }

    private void UpdateTimerHUD()
    {
        if (timerText == null || timerText.gameObject == null)
        {
            EnsureTimerTextReference();
        }

        if (timerText != null)
        {
            int totalSeconds = Mathf.CeilToInt(currentStageTimer);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void SetObjectiveText(string text)
    {
        if (stageObjectiveText != null)
        {
            stageObjectiveText.text = text;
        }
    }

    private void UpdateInventoryHUD()
    {
        if (inventoryHUDText != null)
        {
            string shelfStatus = isBookshelfAnchored ? "<color=green>[✓] Bookshelf Anchored</color>" : "<color=red>[ ] Bookshelf</color>";
            string paintStatus = isPaintingRemoved ? "<color=green>[✓] Painting Safe</color>" : "<color=red>[ ] Heavy Painting</color>";
            string candleStatus = isCandleExtinguished ? "<color=green>[✓] Candle Out</color>" : "<color=red>[ ] Lit Candle</color>";
            string flashStatus = hasFlashlight ? "<color=green>[✓] Flashlight</color>" : "<color=red>[ ] Flashlight</color>";
            string medStatus = hasFirstAidKit ? "<color=green>[✓] First-Aid Kit</color>" : "<color=red>[ ] First-Aid Kit</color>";

            inventoryHUDText.text = $"{shelfStatus}\n{paintStatus}\n{candleStatus}\n{flashStatus}\n{medStatus}";
        }
    }

    public void SetHUDVisibility(bool visible)
    {
        if (visible)
        {
            EnsureTimerTextReference();
        }

        if (timerText != null && timerText.gameObject != null)
        {
            timerText.gameObject.SetActive(visible);
            timerText.enabled = visible;
            if (visible)
            {
                ActivateTimerHierarchy(timerText);
            }
        }
        if (stageObjectiveText != null && stageObjectiveText.gameObject != null)
        {
            stageObjectiveText.gameObject.SetActive(visible);
        }
        if (inventoryHUDText != null && inventoryHUDText.gameObject != null)
        {
            inventoryHUDText.gameObject.SetActive(visible);
        }
        if (PersistentCanvas.Instance != null)
        {
            var c = PersistentCanvas.Instance.GetComponent<Canvas>();
            if (c != null)
            {
                c.enabled = visible;
                if (visible && c.sortingOrder < 100) c.sortingOrder = 100;
            }
        }
    }

    // Public Getters
    public GameStage CurrentStage => currentStage;
    public bool HasFlashlight => hasFlashlight;
    public bool HasFirstAidKit => hasFirstAidKit;
    public bool IsBookshelfAnchored => isBookshelfAnchored;
    public bool IsPaintingRemoved => isPaintingRemoved;
    public bool IsCandleExtinguished => isCandleExtinguished;
    public bool IsCabinetSecured => isCabinetSecured;
    public bool IsDolapSecured => isDolapSecured;
    public bool IsGasShutOff => isGasShutOff;
    public bool IsBreakerShutOff => isBreakerShutOff;

    public void HandleGasValveShutOff()
    {
        if (isGasShutOff) return;
        RecordActionSnapshot("Gas Valve Closed (Semicircle Turn)");
        isGasShutOff = true;
        Debug.Log("<color=green>[Timeline] Gas valve safely turned shut via QTE!</color>");

        // Disable pipe zone once gas is shut off
        var zones = FindObjectsByType<InteractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var z in zones)
        {
            if (z.IsPipeZone()) z.SetInteractable(false);
        }

        SetObjectiveText("OBJECTIVE: Gas shut off! Now open the entrance door and evacuate to the street before aftershocks!");

        OnStateUpdated?.Invoke();
    }

    public void CompleteGameAndEvacuate()
    {
        Debug.Log("<color=green>[VICTORY] Player safely shut off gas and evacuated through the entrance door into the street!</color>");
        currentStage = GameStage.Victory;
        isTimerRunning = false;
        SetObjectiveText("VICTORY! Safely evacuated the building into the street!");
        InventorySelection.ResetSelection();
        if (playerMovement != null) playerMovement.ResetFlashlight();
        OnStageChanged?.Invoke(currentStage);

        PlayEndingCutsceneSequence();
    }

    public void ShowUrgentMessage(string msg)
    {
        SetObjectiveText(msg);
    }

    #endregion
}

