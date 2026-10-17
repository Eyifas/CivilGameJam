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
    public bool isGasShutOff;
    public bool isBreakerShutOff;

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
    [SerializeField] private bool isGasShutOff = false;
    [SerializeField] private bool isBreakerShutOff = false;

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
    }

    private void RebindSceneReferences()
    {
        if (roomCamera == null) roomCamera = FindFirstObjectByType<RoomCamera>();
        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (stage2ChoiceGroup == null) stage2ChoiceGroup = FindFirstObjectByType<WorldChoiceGroup>(FindObjectsInactive.Include);
        EnsureCupboardTriggerZone();
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
            if (rewindPanel != null) rewindPanel.SetActive(false);
            StartStage1();
        }
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
            isGasShutOff = isGasShutOff,
            isBreakerShutOff = isBreakerShutOff,

            isPlanterMoved = isPlanterMoved,
            isNeighborTreated = isNeighborTreated,
            isSignDodged = isSignDodged
        };
        actionHistory.Push(snapshot);
        Debug.Log($"[Timeline] Recorded action snapshot: '{actionDescription}' (History depth: {actionHistory.Count})");
    }

    public float CalculateRewindDuration(GameStage failedStage, string educationalReason)
    {
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
        currentStageTimer = stage3Duration;
        isTimerRunning = true;

        if (playerMovement != null) playerMovement.enabled = true;

        SetObjectiveText("STAGE 3: Gas leak detected! Secure cabinet & shut off utilities safely.");
        Debug.Log("[Timeline] Stage 3 Started: Post-Shaking Utilities & Hazards.");
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
        }
    }

    #endregion

    #region --- TIME LOOP REWIND SYSTEM ---

    public void TriggerRewind(string educationalReason)
    {
        if (currentStage == GameStage.Rewinding) return;

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

        if (playerMovement != null)
        {
            playerMovement.ResetFlashlight();
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
                UndoLastFatalActionAndRestore(failedStage, educationalReason, addedDuration);
            },
            onComplete: () =>
            {
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

        if (playerMovement != null)
        {
            playerMovement.ResetFlashlight();
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
        isGasShutOff = snapshot.isGasShutOff;
        isBreakerShutOff = snapshot.isBreakerShutOff;

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
        bool wasCandleOrPaintingTremorFail = failedStage == GameStage.Stage2_Tremor &&
            (educationalReason.IndexOf("candle", StringComparison.OrdinalIgnoreCase) >= 0 ||
             educationalReason.IndexOf("painting", StringComparison.OrdinalIgnoreCase) >= 0);

        if (wasCandleOrPaintingTremorFail)
        {
            // Hazard was left unaddressed in Stage 1 -> Rewind to Stage 1 Preparation with variable bonus (e.g. +15s)
            currentStage = GameStage.Stage1_Preparation;
            currentStageTimer = Mathf.Max(snapshot.stageTimer + addedDuration, addedDuration);
            isTimerRunning = true;
            SetObjectiveText($"TIME REWOUND (+{addedDuration:0}s)! Fix room hazards before the earthquake strikes!");
            ReEnableStage1Interactions();
            if (playerMovement != null)
            {
                playerMovement.enabled = true;
                playerMovement.transform.position = new Vector3(-9.6f, -2.2f, 0f);
            }
        }
        else if (failedStage == GameStage.Stage1_Preparation)
        {
            currentStage = GameStage.Stage1_Preparation;
            currentStageTimer = Mathf.Max(snapshot.stageTimer + addedDuration, addedDuration);
            isTimerRunning = true;
            SetObjectiveText($"STAGE 1 (+{addedDuration:0}s): Fix room hazards & pack flashlight + first-aid kit!");
            ReEnableStage1Interactions();
            if (playerMovement != null)
            {
                playerMovement.enabled = true;
                playerMovement.transform.position = snapshot.playerPosition;
            }
        }
        else if (failedStage == GameStage.Stage2_Tremor)
        {
            // Player ran for the door or froze: That fatal action never happened!
            // Re-present choice with added decision time
            currentStage = GameStage.Stage2_Tremor;
            isTimerRunning = false;
            stage2TremorDuration += addedDuration;
            SetObjectiveText("EARTHQUAKE STRIKES! MAKE YOUR DECISION!");

            if (roomCamera != null)
            {
                roomCamera.StartEarthquake();
            }

            if (playerMovement != null)
            {
                playerMovement.enabled = false;
                playerMovement.transform.position = snapshot.playerPosition;
            }

            if (stage2ChoiceGroup != null)
            {
                stage2ChoiceGroup.gameObject.SetActive(true);
                stage2ChoiceGroup.PresentChoices(
                    headerTitle: "EARTHQUAKE HITS! CHOOSE FAST!",
                    duration: stage2TremorDuration,
                    onChoice: HandleStage2Choice,
                    onTimeout: HandleStage2Timeout
                );
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
        foreach (var z in zones) z.SetInteractable(true);

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

    public void ResetToCheckpoint()
    {
        // Reset time loop counters & history
        rewindsUsed = 0;
        actionHistory.Clear();

        // Reset flags
        isBookshelfAnchored = false;
        isPaintingRemoved = false;
        isCandleExtinguished = false;
        hasFlashlight = false;
        hasFirstAidKit = false;
        isCabinetSecured = false;
        isGasShutOff = false;
        isBreakerShutOff = false;

        var cupboardTriggers = FindObjectsByType<CupboardTriggerZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ctz in cupboardTriggers) ctz.CheckStatus();

        // Reset Inventory & selection
        InventorySelection.ResetSelection();
        InventoryManager.ResetInventory();
        if (playerMovement != null)
        {
            playerMovement.ResetFlashlight();
        }

        // Reset any physical room items (Flashlight, Medkit, Painting)
        var items = FindObjectsByType<RoomItemInteractable>(FindObjectsSortMode.None);
        foreach (var item in items)
        {
            item.ResetItem();
        }

        // Reset Bookshelf state
        var bookshelf = FindFirstObjectByType<BookshelfAnchorInteractable>(FindObjectsInactive.Include);
        if (bookshelf != null)
        {
            bookshelf.ResetState();
        }
        BookshelfSceneController.ResetToolState();

        // Re-enable all interaction zones
        ReEnableStage1Interactions();

        PlayerMovement.ClearSavedPosition();
        if (playerMovement != null)
        {
            playerMovement.transform.position = new Vector3(-9.6f, -2.2f, 0f);
        }

        // Restart Stage 1
        StartStage1();
        OnStateUpdated?.Invoke();
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
            TriggerRewind("TIME RAN OUT: Lingered too long in an apartment with leaking natural gas!");
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

    private void UpdateTimerHUD()
    {
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
        if (timerText != null && timerText.gameObject != null)
        {
            timerText.gameObject.SetActive(visible);
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
            if (c != null) c.enabled = visible;
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
    public bool IsGasShutOff => isGasShutOff;
    public bool IsBreakerShutOff => isBreakerShutOff;

    public void HandleGasValveShutOff()
    {
        if (isGasShutOff) return;
        RecordActionSnapshot("Gas Valve Closed (Semicircle Turn)");
        isGasShutOff = true;
        Debug.Log("<color=green>[Timeline] Gas valve safely turned shut via QTE!</color>");
        OnStateUpdated?.Invoke();

        if (currentStage == GameStage.Stage3_Utilities && isBreakerShutOff)
        {
            AdvanceToStage4();
        }
    }

    #endregion
}

