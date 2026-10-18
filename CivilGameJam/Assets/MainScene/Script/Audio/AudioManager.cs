using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum BgmTrack
{
    None,
    Menu,
    Stage1_Preparation,
    Earthquake,
    Stage3_PostQuake,
    MinigameFocus,
    MinigameTension,
    VictoryOutro,
    VictoryLoop
}

public enum SFXType
{
    None,
    Footstep,
    FlashlightOn,
    FlashlightOff,
    LightSwitch,
    ItemPickup,
    ToolPlace,
    HammerHit,
    AnchorSuccess,
    DrawerOpen,
    DrawerClose,
    CandlePuff,
    PaintingPlace,
    PaintingRattle,
    WardrobeCrash,
    CabinetCrash,
    Explosion,
    DoorLocked,
    DoorOpen,
    ValveTurn,
    ValveLatch,
    DolapStrain,
    DolapCreak,
    UIHover,
    UIClick,
    QTEHit,
    QTEMiss,
    PageTurn,
    TimeRewindWhoosh,
    TimeRewindTick
}

/// <summary>
/// Centralized Audio Manager for Disaster Survival Game.
/// Provides high-fidelity procedural audio synthesis for all BGM tracks, ambient loops, and SFX,
/// with automatic scene/stage synchronization, multi-channel crossfading, and zero external asset dependencies.
/// Supports runtime overrides if custom audio clips are assigned.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Master Volume Settings")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
    [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.75f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.95f;
    [Range(0f, 1f)] [SerializeField] private float ambientVolume = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float uiVolume = 0.85f;

    [Header("Optional Custom Audio Overrides (Inspector)")]
    [Tooltip("Drag custom AudioClips here in the Inspector if desired.")]
    [SerializeField] private AudioClip customMenuBgm;
    [SerializeField] private AudioClip customStage1Bgm;
    [SerializeField] private AudioClip customEarthquakeBgm;
    [SerializeField] private AudioClip customStage3Bgm;
    [SerializeField] private AudioClip customVictoryBgm;
    [SerializeField] private AudioClip customFootstep;
    [SerializeField] private AudioClip customExplosion;
    [SerializeField] private AudioClip customHammer;
    [SerializeField] private AudioClip customDoor;

    [Header("BGM Track Speed Multipliers")]
    [Tooltip("Playback speed multiplier for MinigameTension (speedup by 2 = 2.0f)")]
    [SerializeField] private float minigameTensionSpeed = 1.3f;

    [Header("BGM Channels (Crossfade)")]
    private AudioSource bgmSourceA;
    private AudioSource bgmSourceB;
    private bool isBgmSourceAActive = true;
    private Coroutine bgmCrossfadeCoroutine;
    private BgmTrack currentBgmTrack = BgmTrack.None;

    [Header("Ambient Channels")]
    private AudioSource ambientSource;
    private AudioSource gasHissSource;
    private AudioSource earthquakeRumbleSource;

    [Header("SFX & Foley Channels")]
    private AudioSource sfxSource;
    private AudioSource footstepSource;
    private AudioSource uiSource;

    // Cache of procedural/synthesized audio clips
    private readonly Dictionary<BgmTrack, AudioClip> bgmClips = new Dictionary<BgmTrack, AudioClip>();
    private readonly Dictionary<SFXType, AudioClip> sfxClips = new Dictionary<SFXType, AudioClip>();

    private const int SAMPLE_RATE = 44100;
    private bool isInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (Instance == null)
        {
            var existing = FindFirstObjectByType<AudioManager>();
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            GameObject audioObj = new GameObject("AudioManager_Central");
            Instance = audioObj.AddComponent<AudioManager>();
            DontDestroyOnLoad(audioObj);
        }
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

        if (!isInitialized)
        {
            SetupAudioSources();
            PreloadProceduralClips();
            isInitialized = true;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void Start()
    {
        // Initial scene BGM check
        HandleSceneMusic(SceneManager.GetActiveScene().name);
        HookTimelineEvents();
    }

    private void Update()
    {
        // Periodic check to ensure timeline events remain hooked
        HookTimelineEvents();
    }

    private void SetupAudioSources()
    {
        // 1. Dual BGM sources for crossfading
        bgmSourceA = gameObject.AddComponent<AudioSource>();
        bgmSourceA.loop = true;
        bgmSourceA.playOnAwake = false;
        bgmSourceA.spatialBlend = 0f;
        bgmSourceA.volume = 0f;

        bgmSourceB = gameObject.AddComponent<AudioSource>();
        bgmSourceB.loop = true;
        bgmSourceB.playOnAwake = false;
        bgmSourceB.spatialBlend = 0f;
        bgmSourceB.volume = 0f;

        // 2. Ambient room loop
        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.loop = true;
        ambientSource.playOnAwake = false;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = ambientVolume * masterVolume;

        // 3. Dedicated Gas Hiss loop
        gasHissSource = gameObject.AddComponent<AudioSource>();
        gasHissSource.loop = true;
        gasHissSource.playOnAwake = false;
        gasHissSource.spatialBlend = 0f;
        gasHissSource.volume = 0f;

        // 4. Dedicated Earthquake sub-bass rumble loop
        earthquakeRumbleSource = gameObject.AddComponent<AudioSource>();
        earthquakeRumbleSource.loop = true;
        earthquakeRumbleSource.playOnAwake = false;
        earthquakeRumbleSource.spatialBlend = 0f;
        earthquakeRumbleSource.volume = 0f;

        // 5. One-Shot SFX
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = sfxVolume * masterVolume;

        // 6. Footsteps
        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.loop = false;
        footstepSource.playOnAwake = false;
        footstepSource.spatialBlend = 0f;
        footstepSource.volume = sfxVolume * 0.7f * masterVolume;

        // 7. UI Clicks
        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.loop = false;
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;
        uiSource.volume = uiVolume * masterVolume;
        uiSource.ignoreListenerPause = true;
    }

    private void HookTimelineEvents()
    {
        if (GameTimelineManager.Instance != null)
        {
            GameTimelineManager.Instance.OnStageChanged -= HandleTimelineStageChanged;
            GameTimelineManager.Instance.OnStageChanged += HandleTimelineStageChanged;

            GameTimelineManager.Instance.OnStateUpdated -= HandleTimelineStateUpdated;
            GameTimelineManager.Instance.OnStateUpdated += HandleTimelineStateUpdated;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleSceneMusic(scene.name);
        HookTimelineEvents();
    }

    private void HandleSceneMusic(string sceneName)
    {
        switch (sceneName)
        {
            case "StartScene":
                PlayBGM(BgmTrack.Menu, 0.8f);
                SetGasHissActive(false);
                SetEarthquakeRumbleActive(false);
                break;

            case "MainScene":
                if (GameTimelineManager.Instance != null)
                {
                    HandleTimelineStageChanged(GameTimelineManager.Instance.CurrentStage);
                }
                else
                {
                    PlayBGM(BgmTrack.Stage1_Preparation, 1.0f);
                }
                break;

            case "BookshelfScene":
            case "TableScene":
            case "DrawerScene":
                PlayBGM(BgmTrack.MinigameFocus, 0.8f);
                SetEarthquakeRumbleActive(false);
                break;

            case "CupboardScene":
            case "DolapScene":
                PlayBGM(BgmTrack.MinigameTension, 0.6f);
                break;

            case "PipeScene":
                PlayBGM(BgmTrack.MinigameFocus, 0.6f);
                // Gas leak hiss is prominent in pipe scene if not shut off
                if (GameTimelineManager.Instance != null && !GameTimelineManager.Instance.IsGasShutOff)
                {
                    SetGasHissActive(true, 0.85f);
                }
                break;
        }
    }

    private void HandleTimelineStageChanged(GameStage stage)
    {
        switch (stage)
        {
            case GameStage.Stage1_Preparation:
                PlayBGM(BgmTrack.Stage1_Preparation, 0.8f);
                SetGasHissActive(false);
                SetEarthquakeRumbleActive(false);
                break;

            case GameStage.Stage2_Tremor:
                PlayBGM(BgmTrack.Earthquake, 0.2f);
                SetEarthquakeRumbleActive(true, 0.95f);
                SetGasHissActive(false);
                break;

            case GameStage.Stage3_Utilities:
                PlayBGM(BgmTrack.Stage3_PostQuake, 1.2f);
                SetEarthquakeRumbleActive(false);
                // In stage 3, gas leaks until shut off
                bool isGasLeaking = GameTimelineManager.Instance != null && !GameTimelineManager.Instance.IsGasShutOff;
                SetGasHissActive(isGasLeaking, 0.7f);
                break;

            case GameStage.Stage4_Stairwell:
            case GameStage.Stage5_StreetFinale:
                PlayBGM(BgmTrack.Stage3_PostQuake, 1.0f);
                SetEarthquakeRumbleActive(false);
                break;

            case GameStage.Victory:
                PlayBGM(BgmTrack.VictoryOutro, 0.5f);
                SetGasHissActive(false);
                SetEarthquakeRumbleActive(false);
                break;
        }
    }

    private void HandleTimelineStateUpdated()
    {
        if (GameTimelineManager.Instance != null)
        {
            if (GameTimelineManager.Instance.IsGasShutOff)
            {
                SetGasHissActive(false);
            }
        }
    }

    #region --- Public Static Audio API ---

    public static void PlayBGM(BgmTrack track, float fadeDuration = 0.5f, float pitch = -1f)
    {
        if (Instance != null) Instance.InternalPlayBGM(track, fadeDuration, pitch);
    }

    public static void StopBGM(float fadeDuration = 0.5f)
    {
        if (Instance != null) Instance.InternalStopBGM(fadeDuration);
    }

    public static void PlaySFX(SFXType type, float volumeMultiplier = 1.0f, float pitch = 1.0f)
    {
        if (Instance != null) Instance.InternalPlaySFX(type, volumeMultiplier, pitch);
    }

    public static void PlayFootstep()
    {
        if (Instance != null) Instance.InternalPlayFootstep();
    }

    public static void PlayUIClick()
    {
        if (Instance != null) Instance.InternalPlaySFX(SFXType.UIClick, 0.8f, 1.0f, Instance.uiSource);
    }

    public static void PlayUIHover()
    {
        if (Instance != null) Instance.InternalPlaySFX(SFXType.UIHover, 0.5f, 1.0f, Instance.uiSource);
    }

    public static void PlayItemPickup()
    {
        PlaySFX(SFXType.ItemPickup, 0.9f);
    }

    public static void PlayFlashlightToggle(bool isOn)
    {
        PlaySFX(isOn ? SFXType.FlashlightOn : SFXType.FlashlightOff, 0.85f);
    }

    public static void PlaySwitchToggle(bool isOn)
    {
        PlaySFX(SFXType.LightSwitch, 0.9f, isOn ? 1.05f : 0.95f);
    }

    public static void PlayExplosion()
    {
        PlaySFX(SFXType.Explosion, 1.0f);
    }

    public static void PlayDoorLocked()
    {
        PlaySFX(SFXType.DoorLocked, 0.85f);
    }

    public static void PlayDoorOpen()
    {
        PlaySFX(SFXType.DoorOpen, 0.95f);
    }

    public static void PlayDrawer(bool isOpen)
    {
        PlaySFX(isOpen ? SFXType.DrawerOpen : SFXType.DrawerClose, 0.8f);
    }

    public static void PlayCandlePuff()
    {
        PlaySFX(SFXType.CandlePuff, 0.9f);
    }

    public static void PlayPaintingPlace()
    {
        PlaySFX(SFXType.PaintingPlace, 0.85f);
    }

    public static void PlayPaintingRattle()
    {
        PlaySFX(SFXType.PaintingRattle, 0.8f);
    }

    public static void PlayHammerHit()
    {
        PlaySFX(SFXType.HammerHit, 0.95f, UnityEngine.Random.Range(0.96f, 1.04f));
    }

    public static void PlayAnchorSuccess()
    {
        PlaySFX(SFXType.AnchorSuccess, 0.9f);
    }

    public static void PlayWardrobeCrash()
    {
        PlaySFX(SFXType.WardrobeCrash, 1.0f);
    }

    public static void PlayCabinetCrash()
    {
        PlaySFX(SFXType.CabinetCrash, 0.95f);
    }

    public static void PlayPaintingFall()
    {
        PlaySFX(SFXType.PaintingRattle, 1.0f);
        PlaySFX(SFXType.CabinetCrash, 0.7f, 1.4f);
    }

    public static void PlayDoorRushFall()
    {
        PlaySFX(SFXType.WardrobeCrash, 0.85f, 1.2f);
    }

    public static void PlayGasSuffocation()
    {
        PlaySFX(SFXType.QTEMiss, 1.0f, 0.7f);
    }

    public static void PlayValveTurn()
    {
        PlaySFX(SFXType.ValveTurn, 0.85f, UnityEngine.Random.Range(0.94f, 1.06f));
    }

    public static void PlayValveLatch()
    {
        PlaySFX(SFXType.ValveLatch, 0.95f);
    }

    public static void PlayDolapStrain()
    {
        PlaySFX(SFXType.DolapStrain, 0.8f, UnityEngine.Random.Range(0.92f, 1.08f));
    }

    public static void PlayDolapCreak()
    {
        PlaySFX(SFXType.DolapCreak, 0.85f, UnityEngine.Random.Range(0.95f, 1.05f));
    }

    public static void PlayQTEHit()
    {
        PlaySFX(SFXType.QTEHit, 0.85f, UnityEngine.Random.Range(0.98f, 1.02f));
    }

    public static void PlayQTEMiss()
    {
        PlaySFX(SFXType.QTEMiss, 0.85f);
    }

    public static void PlayPageTurn()
    {
        PlaySFX(SFXType.PageTurn, 0.8f, UnityEngine.Random.Range(0.95f, 1.05f));
    }

    public static void PlayVictoryFanfare()
    {
        PlaySFX(SFXType.AnchorSuccess, 1.0f);
    }

    public static void SetGasHissActive(bool active, float targetVolume = 0.65f)
    {
        if (Instance != null) Instance.InternalSetGasHiss(active, targetVolume);
    }

    public static void SetEarthquakeRumbleActive(bool active, float targetVolume = 0.9f)
    {
        if (Instance != null) Instance.InternalSetEarthquakeRumble(active, targetVolume);
    }

    /// <summary>
    /// Play any custom AudioClip once on the SFX channel.
    /// </summary>
    public static void PlayClip(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
    {
        if (Instance != null && clip != null && Instance.sfxSource != null)
        {
            Instance.sfxSource.pitch = pitch;
            Instance.sfxSource.PlayOneShot(clip, Instance.sfxVolume * Instance.masterVolume * volume);
        }
    }

    /// <summary>
    /// Play an audio file from Resources by name or subpath (e.g. "Audio/SFX/MySound" or "MySound").
    /// </summary>
    public static void PlaySound(string resourcePath, float volume = 1.0f, float pitch = 1.0f)
    {
        if (Instance == null || string.IsNullOrEmpty(resourcePath)) return;
        AudioClip clip = Resources.Load<AudioClip>(resourcePath) ??
                         Resources.Load<AudioClip>("Audio/SFX/" + resourcePath) ??
                         Resources.Load<AudioClip>("Audio/BGM/" + resourcePath) ??
                         Resources.Load<AudioClip>("Audio/" + resourcePath);
        if (clip != null)
        {
            PlayClip(clip, volume, pitch);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] Could not find AudioClip in Resources at: '{resourcePath}'");
        }
    }

    /// <summary>
    /// Override a specific SFX type with a custom AudioClip at runtime.
    /// </summary>
    public static void SetCustomSFX(SFXType type, AudioClip clip)
    {
        if (Instance != null && clip != null)
        {
            Instance.sfxClips[type] = clip;
        }
    }

    /// <summary>
    /// Override a specific BGM track with a custom AudioClip at runtime.
    /// </summary>
    public static void SetCustomBGM(BgmTrack track, AudioClip clip)
    {
        if (Instance != null && clip != null)
        {
            Instance.bgmClips[track] = clip;
        }
    }

    /// <summary>
    /// Playback speed multiplier for MinigameTension (default 2.0f).
    /// </summary>
    public static float MinigameTensionSpeed
    {
        get => Instance != null ? Instance.minigameTensionSpeed : 2.0f;
        set
        {
            if (Instance != null)
            {
                Instance.minigameTensionSpeed = value;
                if (Instance.currentBgmTrack == BgmTrack.MinigameTension)
                {
                    AudioSource activeSource = Instance.isBgmSourceAActive ? Instance.bgmSourceA : Instance.bgmSourceB;
                    if (activeSource != null) activeSource.pitch = value;
                }
            }
        }
    }

    #endregion

    #region --- Internal Playback Implementations ---

    private void InternalPlayBGM(BgmTrack track, float fadeDuration, float pitch = -1f)
    {
        if (track == currentBgmTrack && (bgmSourceA.isPlaying || bgmSourceB.isPlaying))
        {
            return;
        }

        currentBgmTrack = track;

        if (track == BgmTrack.None)
        {
            InternalStopBGM(fadeDuration);
            return;
        }

        if (!bgmClips.TryGetValue(track, out AudioClip clip) || clip == null)
        {
            clip = TryLoadCustomClip("BGM", track.ToString()) ?? SynthesizeBgmClip(track);
            if (clip != null) bgmClips[track] = clip;
        }

        if (clip == null) return;

        if (bgmCrossfadeCoroutine != null)
        {
            StopCoroutine(bgmCrossfadeCoroutine);
        }

        float targetPitch = pitch > 0f ? pitch : ((track == BgmTrack.MinigameTension) ? minigameTensionSpeed : 1.0f);
        bgmCrossfadeCoroutine = StartCoroutine(CrossfadeBgmRoutine(clip, fadeDuration, targetPitch));
    }

    private void InternalStopBGM(float fadeDuration)
    {
        currentBgmTrack = BgmTrack.None;
        if (bgmCrossfadeCoroutine != null) StopCoroutine(bgmCrossfadeCoroutine);
        bgmCrossfadeCoroutine = StartCoroutine(FadeOutBgmRoutine(fadeDuration));
    }

    private IEnumerator CrossfadeBgmRoutine(AudioClip newClip, float duration, float pitch = 1.0f)
    {
        AudioSource incoming = isBgmSourceAActive ? bgmSourceB : bgmSourceA;
        AudioSource outgoing = isBgmSourceAActive ? bgmSourceA : bgmSourceB;
        isBgmSourceAActive = !isBgmSourceAActive;

        incoming.clip = newClip;
        incoming.pitch = pitch;
        incoming.volume = 0f;
        incoming.Play();

        float targetVol = bgmVolume * masterVolume;
        float elapsed = 0f;

        float startOutgoingVol = outgoing.volume;

        if (duration <= 0.05f)
        {
            incoming.volume = targetVol;
            outgoing.volume = 0f;
            outgoing.Stop();
            outgoing.pitch = 1.0f;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            incoming.volume = Mathf.Lerp(0f, targetVol, t);
            outgoing.volume = Mathf.Lerp(startOutgoingVol, 0f, t);

            yield return null;
        }

        incoming.volume = targetVol;
        outgoing.volume = 0f;
        outgoing.Stop();
        outgoing.pitch = 1.0f;
    }

    private IEnumerator FadeOutBgmRoutine(float duration)
    {
        float startA = bgmSourceA.volume;
        float startB = bgmSourceB.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            bgmSourceA.volume = Mathf.Lerp(startA, 0f, t);
            bgmSourceB.volume = Mathf.Lerp(startB, 0f, t);

            yield return null;
        }

        bgmSourceA.volume = 0f;
        bgmSourceB.volume = 0f;
        bgmSourceA.Stop();
        bgmSourceB.Stop();
        bgmSourceA.pitch = 1.0f;
        bgmSourceB.pitch = 1.0f;
    }

    private void InternalPlaySFX(SFXType type, float volumeMultiplier = 1.0f, float pitch = 1.0f, AudioSource targetSource = null)
    {
        if (type == SFXType.None) return;

        if (!sfxClips.TryGetValue(type, out AudioClip clip) || clip == null)
        {
            clip = TryLoadCustomClip("SFX", type.ToString()) ?? SynthesizeSfxClip(type);
            if (clip != null) sfxClips[type] = clip;
        }

        if (clip == null) return;

        AudioSource src = targetSource != null ? targetSource : sfxSource;
        if (src != null)
        {
            src.pitch = pitch;
            float finalVol = sfxVolume * masterVolume * volumeMultiplier;
            src.PlayOneShot(clip, finalVol);
        }
    }

    private bool footstepLeftFoot = false;
    private AudioClip footstepClipL;
    private AudioClip footstepClipR;

    private void InternalPlayFootstep()
    {
        if (footstepSource == null) return;

        if (footstepClipL == null) footstepClipL = SynthesizeFootstepClip(true);
        if (footstepClipR == null) footstepClipR = SynthesizeFootstepClip(false);

        footstepLeftFoot = !footstepLeftFoot;
        AudioClip clip = footstepLeftFoot ? footstepClipL : footstepClipR;

        if (clip != null)
        {
            footstepSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
            footstepSource.PlayOneShot(clip, sfxVolume * 0.32f * masterVolume);
        }
    }

    private Coroutine gasHissFadeCoroutine;
    private void InternalSetGasHiss(bool active, float targetVolume)
    {
        if (gasHissSource == null) return;

        if (active)
        {
            if (gasHissSource.clip == null)
            {
                if (!sfxClips.TryGetValue(SFXType.None, out AudioClip hissClip) || hissClip == null)
                {
                    hissClip = CreateContinuousGasHissClip();
                }
                gasHissSource.clip = hissClip;
            }

            if (!gasHissSource.isPlaying) gasHissSource.Play();
        }

        if (gasHissFadeCoroutine != null) StopCoroutine(gasHissFadeCoroutine);
        gasHissFadeCoroutine = StartCoroutine(FadeAudioSource(gasHissSource, active ? targetVolume * ambientVolume * masterVolume : 0f, 0.6f, stopOnZero: !active));
    }

    private Coroutine rumbleFadeCoroutine;
    private void InternalSetEarthquakeRumble(bool active, float targetVolume)
    {
        if (earthquakeRumbleSource == null) return;

        if (active)
        {
            if (earthquakeRumbleSource.clip == null)
            {
                earthquakeRumbleSource.clip = SynthesizeEarthquakeRumbleClip();
            }
            if (!earthquakeRumbleSource.isPlaying) earthquakeRumbleSource.Play();
        }

        if (rumbleFadeCoroutine != null) StopCoroutine(rumbleFadeCoroutine);
        rumbleFadeCoroutine = StartCoroutine(FadeAudioSource(earthquakeRumbleSource, active ? targetVolume * ambientVolume * masterVolume : 0f, 0.4f, stopOnZero: !active));
    }

    private IEnumerator FadeAudioSource(AudioSource src, float targetVol, float duration, bool stopOnZero)
    {
        if (src == null) yield break;

        float startVol = src.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (src != null) src.volume = Mathf.Lerp(startVol, targetVol, t);
            yield return null;
        }

        if (src != null)
        {
            src.volume = targetVol;
            if (stopOnZero && targetVol <= 0.001f)
            {
                src.Stop();
            }
        }
    }

    #endregion

    #region --- Procedural Sound Synthesis Engine ---

    private static AudioClip TryLoadCustomClip(string subfolder, string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return null;

        string[] candidates = new string[]
        {
            $"Audio/{subfolder}/{clipName}",
            $"Audio/{clipName}",
            $"{subfolder}/{clipName}",
            clipName
        };

        foreach (var path in candidates)
        {
            var loaded = Resources.Load<AudioClip>(path);
            if (loaded != null) return loaded;
        }

        return null;
    }

    private void PreloadProceduralClips()
    {
        // 1. Inspector Overrides (if assigned on AudioManager component)
        if (customMenuBgm != null) bgmClips[BgmTrack.Menu] = customMenuBgm;
        if (customStage1Bgm != null) bgmClips[BgmTrack.Stage1_Preparation] = customStage1Bgm;
        if (customEarthquakeBgm != null) bgmClips[BgmTrack.Earthquake] = customEarthquakeBgm;
        if (customStage3Bgm != null) bgmClips[BgmTrack.Stage3_PostQuake] = customStage3Bgm;
        if (customVictoryBgm != null) bgmClips[BgmTrack.VictoryOutro] = customVictoryBgm;

        if (customFootstep != null) { footstepClipL = customFootstep; footstepClipR = customFootstep; }
        if (customExplosion != null) sfxClips[SFXType.Explosion] = customExplosion;
        if (customHammer != null) sfxClips[SFXType.HammerHit] = customHammer;
        if (customDoor != null) sfxClips[SFXType.DoorOpen] = customDoor;

        // 2. Pre-generate or load custom clips for SFX
        foreach (SFXType type in (SFXType[])Enum.GetValues(typeof(SFXType)))
        {
            if (type == SFXType.None) continue;
            if (sfxClips.ContainsKey(type) && sfxClips[type] != null) continue;

            string sfxName = type.ToString();
            AudioClip clip = TryLoadCustomClip("SFX", sfxName) ?? SynthesizeSfxClip(type);
            if (clip != null) sfxClips[type] = clip;
        }

        // 3. Pre-generate or load custom clips for BGM
        foreach (BgmTrack track in (BgmTrack[])Enum.GetValues(typeof(BgmTrack)))
        {
            if (track == BgmTrack.None) continue;
            if (bgmClips.ContainsKey(track) && bgmClips[track] != null) continue;

            string bgmName = track.ToString();
            AudioClip clip = TryLoadCustomClip("BGM", bgmName) ?? SynthesizeBgmClip(track);
            if (clip != null) bgmClips[track] = clip;
        }

        // Footstep custom stereo check
        if (footstepClipL == null)
        {
            footstepClipL = TryLoadCustomClip("SFX", "Footstep_L") ??
                            TryLoadCustomClip("SFX", "Footstep1") ??
                            TryLoadCustomClip("SFX", "Footstep");
        }
        if (footstepClipR == null)
        {
            footstepClipR = TryLoadCustomClip("SFX", "Footstep_R") ??
                            TryLoadCustomClip("SFX", "Footstep2") ??
                            footstepClipL;
        }
    }

    private AudioClip SynthesizeBgmClip(BgmTrack track)
    {
        switch (track)
        {
            case BgmTrack.Menu:
                return SynthesizeMenuBgm();
            case BgmTrack.Stage1_Preparation:
                return SynthesizeStage1PrepBgm();
            case BgmTrack.Earthquake:
                return SynthesizeEarthquakeRumbleClip();
            case BgmTrack.Stage3_PostQuake:
                return SynthesizeStage3PostQuakeBgm();
            case BgmTrack.MinigameFocus:
                return SynthesizeMinigameFocusBgm();
            case BgmTrack.MinigameTension:
                return SynthesizeMinigameTensionBgm();
            case BgmTrack.VictoryOutro:
                return SynthesizeVictoryOutroBgm();
            case BgmTrack.VictoryLoop:
                return SynthesizeVictoryLoopBgm();
            default:
                return null;
        }
    }

    private AudioClip SynthesizeSfxClip(SFXType type)
    {
        switch (type)
        {
            case SFXType.Footstep:
                return SynthesizeFootstepClip();
            case SFXType.FlashlightOn:
                return SynthesizeFlashlightClickClip(true);
            case SFXType.FlashlightOff:
                return SynthesizeFlashlightClickClip(false);
            case SFXType.LightSwitch:
                return SynthesizeLightSwitchClip();
            case SFXType.ItemPickup:
                return SynthesizeItemPickupClip();
            case SFXType.ToolPlace:
                return SynthesizeToolPlaceClip();
            case SFXType.HammerHit:
                return SynthesizeHammerHitClip();
            case SFXType.AnchorSuccess:
                return SynthesizeAnchorSuccessClip();
            case SFXType.DrawerOpen:
                return SynthesizeDrawerSlideClip(true);
            case SFXType.DrawerClose:
                return SynthesizeDrawerSlideClip(false);
            case SFXType.CandlePuff:
                return SynthesizeCandlePuffClip();
            case SFXType.PaintingPlace:
                return SynthesizePaintingPlaceClip();
            case SFXType.PaintingRattle:
                return SynthesizePaintingRattleClip();
            case SFXType.WardrobeCrash:
                return SynthesizeWardrobeCrashClip();
            case SFXType.CabinetCrash:
                return SynthesizeCabinetCrashClip();
            case SFXType.Explosion:
                return SynthesizeExplosionClip();
            case SFXType.DoorLocked:
                return SynthesizeDoorLockedClip();
            case SFXType.DoorOpen:
                return SynthesizeDoorOpenClip();
            case SFXType.ValveTurn:
                return SynthesizeValveTurnClip();
            case SFXType.ValveLatch:
                return SynthesizeValveLatchClip();
            case SFXType.DolapStrain:
                return SynthesizeDolapStrainClip();
            case SFXType.DolapCreak:
                return SynthesizeDolapCreakClip();
            case SFXType.UIHover:
                return SynthesizeUIHoverClip();
            case SFXType.UIClick:
                return SynthesizeUIClickClip();
            case SFXType.QTEHit:
                return SynthesizeQTEHitClip();
            case SFXType.QTEMiss:
                return SynthesizeQTEMissClip();
            case SFXType.PageTurn:
                return SynthesizePageTurnClip();
            default:
                return null;
        }
    }

    // -------------------------------------------------------------
    // BGM SYNTHESIS ROUTINES (Seamless Loop Mathematical Waveforms)
    // -------------------------------------------------------------

    private AudioClip SynthesizeMenuBgm()
    {
        // 12.0s seamless loop: Atmospheric D minor warm ambient chord progression
        float duration = 12.0f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        // Chords: Dm (0-3s), Bbmaj7 (3-6s), Gm (6-9s), Asus4-A (9-12s)
        float[][] chordNotes = new float[][]
        {
            new float[] { 73.42f, 146.83f, 174.61f, 220.00f },       // D2, D3, F3, A3
            new float[] { 58.27f, 116.54f, 174.61f, 220.00f, 293.66f }, // Bb1, Bb2, F3, A3, D4
            new float[] { 49.00f, 98.00f, 146.83f, 233.08f },        // G1, G2, D3, Bb3
            new float[] { 55.00f, 110.00f, 164.81f, 220.00f, 277.18f }  // A1, A2, E3, A3, C#4
        };

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            int chordIdx = Mathf.Clamp((int)(t / 3.0f), 0, 3);
            float chordT = (t % 3.0f) / 3.0f;

            // Envelope per chord
            float chordEnv = Mathf.Sin(chordT * Mathf.PI);

            float chordMix = 0f;
            float[] notes = chordNotes[chordIdx];
            for (int n = 0; n < notes.Length; n++)
            {
                float freq = notes[n];
                // Chorus / subtle detuning
                float v1 = Mathf.Sin(2f * Mathf.PI * freq * t);
                float v2 = Mathf.Sin(2f * Mathf.PI * (freq * 1.002f) * t) * 0.5f;
                chordMix += (v1 + v2) / notes.Length;
            }

            // Low tape noise warmth
            float tapeNoise = (UnityEngine.Random.value * 2f - 1f) * 0.02f;

            // Overall volume
            samples[i] = Mathf.Clamp((chordMix * chordEnv * 0.28f) + tapeNoise, -1f, 1f);
        }

        ApplyLoopCrossfade(samples, (int)(SAMPLE_RATE * 0.25f));

        var clip = AudioClip.Create("BGM_Menu", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeStage1PrepBgm()
    {
        // 8.0s loop: Ticking countdown clock + tense pulsing bass ostinato
        float duration = 8.0f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;

            // 1. Clock Ticks (1 second cadence: high tick on 0, 2, 4, 6s, lower tock on 1, 3, 5, 7s)
            float secPhase = t % 1.0f;
            int secNum = (int)t;
            float tickSound = 0f;
            if (secPhase < 0.06f)
            {
                float tickT = secPhase / 0.06f;
                float tickDecay = Mathf.Exp(-tickT * 18f);
                float tickFreq = (secNum % 2 == 0) ? 1400f : 900f;
                float tickSine = Mathf.Sin(2f * Mathf.PI * tickFreq * secPhase);
                float tickNoise = (UnityEngine.Random.value * 2f - 1f) * 0.4f;
                tickSound = (tickSine * 0.7f + tickNoise) * tickDecay * 0.45f;
            }

            // 2. Tense bass heartbeat pulse on every half-second
            float beatPhase = t % 0.5f;
            float beatDecay = Mathf.Exp(-beatPhase * 14f);
            float bassPulse = Mathf.Sin(2f * Mathf.PI * 65.4f * t) * beatDecay * 0.35f;

            // 3. Subtle background minor shimmer (E minor high drone)
            float shimmer = (Mathf.Sin(2f * Mathf.PI * 659.25f * t) + Mathf.Sin(2f * Mathf.PI * 783.99f * t)) * 0.04f;

            samples[i] = Mathf.Clamp(tickSound + bassPulse + shimmer, -1f, 1f);
        }

        ApplyLoopCrossfade(samples, (int)(SAMPLE_RATE * 0.2f));

        var clip = AudioClip.Create("BGM_Stage1Prep", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeEarthquakeRumbleClip()
    {
        // 4.5s loop: Massive tectonic sub-bass rumble with ground shockwaves
        float duration = 4.5f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        float noiseAcc = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;

            // Low frequency chaotic earthquake frequencies: 28Hz, 38Hz, 52Hz
            float sub1 = Mathf.Sin(2f * Mathf.PI * 28f * t + 0.8f * Mathf.Sin(2f * Mathf.PI * 3f * t));
            float sub2 = Mathf.Sin(2f * Mathf.PI * 42f * t) * 0.65f;
            float sub3 = Mathf.Sin(2f * Mathf.PI * 64f * t) * 0.4f;

            // Tremor amplitude modulation
            float swell = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 1.8f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * 5.2f * t);

            // Filtered brown rumble noise
            float white = (UnityEngine.Random.value * 2f - 1f);
            noiseAcc = Mathf.Lerp(noiseAcc, white, 0.04f); // heavily low-passed

            samples[i] = Mathf.Clamp(((sub1 + sub2 + sub3) * 0.4f + noiseAcc * 0.6f) * swell * 0.75f, -1f, 1f);
        }

        ApplyLoopCrossfade(samples, (int)(SAMPLE_RATE * 0.3f));

        var clip = AudioClip.Create("BGM_EarthquakeRumble", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeStage3PostQuakeBgm()
    {
        // 8.0s loop: Desolate post-earthquake silence, ominous low drone + subtle debris settling
        float duration = 8.0f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;

            // Dark cluster drone: C2 (65.4 Hz) + Db2 (69.3 Hz) creates a slow 3.9Hz dissonance beating
            float drone1 = Mathf.Sin(2f * Mathf.PI * 65.41f * t) * 0.28f;
            float drone2 = Mathf.Sin(2f * Mathf.PI * 69.30f * t) * 0.22f;

            // Occasional distant settling dust flutter
            float airHiss = (UnityEngine.Random.value * 2f - 1f) * 0.025f;

            samples[i] = Mathf.Clamp(drone1 + drone2 + airHiss, -1f, 1f);
        }

        ApplyLoopCrossfade(samples, (int)(SAMPLE_RATE * 0.25f));

        var clip = AudioClip.Create("BGM_Stage3PostQuake", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeMinigameFocusBgm()
    {
        // 6.0s loop: Focused, warm, minimal room ambiance
        float duration = 6.0f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float pad = Mathf.Sin(2f * Mathf.PI * 130.81f * t) * 0.15f + // C3
                        Mathf.Sin(2f * Mathf.PI * 196.00f * t) * 0.10f;  // G3
            float breath = (UnityEngine.Random.value * 2f - 1f) * 0.015f;
            samples[i] = Mathf.Clamp(pad + breath, -1f, 1f);
        }

        ApplyLoopCrossfade(samples, (int)(SAMPLE_RATE * 0.2f));

        var clip = AudioClip.Create("BGM_MinigameFocus", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeMinigameTensionBgm()
    {
        // 5.0s loop: Tense groaning building tension during cupboard/dolap QTE
        float duration = 5.0f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float tension = Mathf.Sin(2f * Mathf.PI * 92.5f * t) * 0.25f +
                            Mathf.Sin(2f * Mathf.PI * 123.47f * t + Mathf.Sin(8f * t) * 0.5f) * 0.18f;
            samples[i] = Mathf.Clamp(tension, -1f, 1f);
        }

        ApplyLoopCrossfade(samples, (int)(SAMPLE_RATE * 0.2f));

        var clip = AudioClip.Create("BGM_MinigameTension", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeVictoryOutroBgm()
    {
        // 6.0s loop: Soft breeze of relief after surviving disaster
        float duration = 6.0f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float reliefChord = Mathf.Sin(2f * Mathf.PI * 261.63f * t) * 0.18f + // C4
                                Mathf.Sin(2f * Mathf.PI * 329.63f * t) * 0.14f + // E4
                                Mathf.Sin(2f * Mathf.PI * 392.00f * t) * 0.12f;  // G4
            float wind = (UnityEngine.Random.value * 2f - 1f) * 0.03f;
            samples[i] = Mathf.Clamp(reliefChord + wind, -1f, 1f);
        }

        ApplyLoopCrossfade(samples, (int)(SAMPLE_RATE * 0.25f));

        var clip = AudioClip.Create("BGM_VictoryOutro", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeVictoryLoopBgm()
    {
        // 8.0s loop: Uplifting, warm victory progression for the success screen
        float duration = 8.0f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        float[][] notes = new float[][]
        {
            new float[] { 261.63f, 329.63f, 392.00f, 523.25f }, // C Major
            new float[] { 293.66f, 349.23f, 440.00f, 587.33f }, // D Minor
            new float[] { 392.00f, 493.88f, 587.33f, 783.99f }, // G Major
            new float[] { 261.63f, 329.63f, 392.00f, 659.25f }  // C Add9
        };

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            int step = Mathf.Clamp((int)(t / 2.0f), 0, 3);
            float stepT = (t % 2.0f) / 2.0f;
            float env = Mathf.Sin(stepT * Mathf.PI);

            float mix = 0f;
            float[] chord = notes[step];
            for (int n = 0; n < chord.Length; n++)
            {
                mix += Mathf.Sin(2f * Mathf.PI * chord[n] * t) / chord.Length;
            }

            samples[i] = Mathf.Clamp(mix * env * 0.32f, -1f, 1f);
        }

        ApplyLoopCrossfade(samples, (int)(SAMPLE_RATE * 0.25f));

        var clip = AudioClip.Create("BGM_VictoryLoop", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateContinuousGasHissClip()
    {
        // 2.0s seamless high-pressure gas hiss loop
        float duration = 2.0f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        float filter = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float white = (UnityEngine.Random.value * 2f - 1f);
            filter = Mathf.Lerp(filter, white, 0.45f);
            float hiss = white - filter * 0.72f;
            float flutter = 1.0f + 0.10f * Mathf.Sin(2f * Mathf.PI * 8.5f * t);
            samples[i] = Mathf.Clamp(hiss * flutter * 0.55f, -1f, 1f);
        }

        ApplyLoopCrossfade(samples, (int)(SAMPLE_RATE * 0.2f));

        var clip = AudioClip.Create("Ambient_GasHissLoop", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // -------------------------------------------------------------
    // FOLEY & SFX SYNTHESIS ROUTINES (Punchy, Clean, Responsive)
    // -------------------------------------------------------------

    private AudioClip SynthesizeFootstepClip(bool isLeft = true)
    {
        // 0.065s: Light, crisp indoor shoe/floor contact tap + gentle highpass floor friction (zero sub-bass boom)
        float duration = 0.065f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        float baseFreq = isLeft ? 410f : 470f;
        float harmFreq = baseFreq * 2.15f;

        float prevNoise = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 70f); // Fast natural tap decay

            // Dry wooden floor / sock contact tap (mid frequencies: ~410-470 Hz, absolutely zero sub-bass)
            float tap = Mathf.Sin(2f * Mathf.PI * baseFreq * t) * 0.40f
                      + Mathf.Sin(2f * Mathf.PI * harmFreq * t) * 0.16f;

            // Bandpassed / highpassed subtle friction scuff (strips all low bass rumble)
            float rawNoise = UnityEngine.Random.value * 2f - 1f;
            float hpNoise = rawNoise - prevNoise * 0.85f;
            prevNoise = rawNoise;

            float friction = hpNoise * Mathf.Exp(-t * 90f) * 0.22f;

            samples[i] = Mathf.Clamp((tap + friction) * env * 0.65f, -1f, 1f);
        }

        var clip = AudioClip.Create(isLeft ? "SFX_Footstep_L" : "SFX_Footstep_R", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }


    private AudioClip SynthesizeFlashlightClickClip(bool turnOn)
    {
        // 0.08s: Tactile plastic toggle switch click
        float duration = 0.08f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        float freq1 = turnOn ? 1800f : 1400f;
        float freq2 = turnOn ? 2600f : 1900f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 45f);
            float click = Mathf.Sin(2f * Mathf.PI * freq1 * t) * 0.5f +
                          Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.35f;
            float snap = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
            samples[i] = Mathf.Clamp((click + snap) * env * 0.95f, -1f, 1f);
        }

        var clip = AudioClip.Create(turnOn ? "SFX_FlashlightOn" : "SFX_FlashlightOff", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeLightSwitchClip()
    {
        // 0.10s: Heavy mechanical wall switch snap
        float duration = 0.10f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 32f);
            float snap = Mathf.Sin(2f * Mathf.PI * 920f * t) * 0.6f +
                         Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.3f;
            float impact = (UnityEngine.Random.value * 2f - 1f) * 0.3f;
            samples[i] = Mathf.Clamp((snap + impact) * env * 0.95f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_LightSwitch", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeItemPickupClip()
    {
        // 0.24s: Cheerful ascending 2-note chime + fabric grab
        float duration = 0.24f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float noteFreq = t < 0.10f ? 659.25f : 987.77f; // E5 -> B5
            float noteT = t < 0.10f ? t : t - 0.10f;
            float env = Mathf.Exp(-noteT * 18f);

            float chime = Mathf.Sin(2f * Mathf.PI * noteFreq * t) * 0.65f +
                          Mathf.Sin(2f * Mathf.PI * (noteFreq * 2f) * t) * 0.25f;
            float rustle = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 22f) * 0.2f;

            samples[i] = Mathf.Clamp((chime + rustle) * env * 0.85f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_ItemPickup", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeToolPlaceClip()
    {
        // 0.15s: Solid wooden shelf contact click
        float duration = 0.15f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 26f);
            float thud = Mathf.Sin(2f * Mathf.PI * 165f * t) * 0.7f;
            float clatter = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
            samples[i] = Mathf.Clamp((thud + clatter) * env * 0.8f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_ToolPlace", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeHammerHitClip()
    {
        // 0.30s: Sharp metallic ping on steel nail head + wooden stud punch
        float duration = 0.30f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float punchEnv = Mathf.Exp(-t * 40f);
            float ringEnv = Mathf.Exp(-t * 12f);

            // Metal ping harmonics
            float metal = Mathf.Sin(2f * Mathf.PI * 2280f * t) * 0.55f +
                          Mathf.Sin(2f * Mathf.PI * 4650f * t) * 0.35f;

            // Wood thump
            float wood = Mathf.Sin(2f * Mathf.PI * 135f * t) * 0.75f;

            float impactGrit = (UnityEngine.Random.value * 2f - 1f) * punchEnv * 0.4f;

            samples[i] = Mathf.Clamp((wood * punchEnv + metal * ringEnv + impactGrit) * 0.95f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_HammerHit", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeAnchorSuccessClip()
    {
        // 0.75s: Bright 3-note celebration arpeggio (C6 - E6 - G6)
        float duration = 0.75f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        float[] freqs = new float[] { 1046.50f, 1318.51f, 1567.98f };
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            int noteIdx = Mathf.Clamp((int)(t / 0.16f), 0, 2);
            float noteT = t - (noteIdx * 0.16f);
            float env = Mathf.Exp(-noteT * 7f);

            float freq = freqs[noteIdx];
            float bell = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f +
                         Mathf.Sin(2f * Mathf.PI * (freq * 2f) * t) * 0.25f;

            samples[i] = Mathf.Clamp(bell * env * 0.8f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_AnchorSuccess", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeDrawerSlideClip(bool open)
    {
        // 0.26s: Wooden drawer sliding friction + stop
        float duration = 0.26f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float slideEnv = Mathf.Sin((t / duration) * Mathf.PI);

            float woodRumble = Mathf.Sin(2f * Mathf.PI * 115f * t) * 0.45f;
            float friction = (UnityEngine.Random.value * 2f - 1f) * 0.35f;

            // Stop click at end of slide
            float stopClick = 0f;
            if (t > duration * 0.85f)
            {
                float stopT = (t - duration * 0.85f) / (duration * 0.15f);
                stopClick = Mathf.Sin(2f * Mathf.PI * (open ? 260f : 140f) * t) * Mathf.Exp(-stopT * 12f) * 0.65f;
            }

            samples[i] = Mathf.Clamp(((woodRumble + friction) * slideEnv * 0.6f + stopClick) * 0.85f, -1f, 1f);
        }

        var clip = AudioClip.Create(open ? "SFX_DrawerOpen" : "SFX_DrawerClose", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeCandlePuffClip()
    {
        // 0.38s: Sharp air blow puff + smoke sizzle
        float duration = 0.38f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float puffEnv = Mathf.Sin(Mathf.Clamp01(t / 0.18f) * Mathf.PI);
            float sizzleEnv = t > 0.16f ? Mathf.Exp(-(t - 0.16f) * 16f) : 0f;

            float puff = (UnityEngine.Random.value * 2f - 1f) * puffEnv * 0.65f;
            float sizzle = (UnityEngine.Random.value * 2f - 1f) * sizzleEnv * 0.25f;

            samples[i] = Mathf.Clamp(puff + sizzle, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_CandlePuff", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizePaintingPlaceClip()
    {
        // 0.28s: Heavy painting frame resting safely on floor
        float duration = 0.28f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 16f);
            float thud = Mathf.Sin(2f * Mathf.PI * 72f * t) * 0.8f +
                         Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.3f;
            float friction = (UnityEngine.Random.value * 2f - 1f) * 0.2f;
            samples[i] = Mathf.Clamp((thud + friction) * env * 0.9f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_PaintingPlace", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizePaintingRattleClip()
    {
        // 0.32s: Frame slipping and rattling against wall
        float duration = 0.32f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Sin(t / duration * Mathf.PI);
            float rattle = Mathf.Sin(2f * Mathf.PI * 340f * t + Mathf.Sin(40f * t) * 3f) * 0.5f;
            float noise = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
            samples[i] = Mathf.Clamp((rattle + noise) * env * 0.8f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_PaintingRattle", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeWardrobeCrashClip()
    {
        // 1.25s: Heavy wardrobe falling crash with wood splintering & massive floor thud
        float duration = 1.25f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float punchEnv = Mathf.Exp(-t * 8f);
            float subThud = Mathf.Sin(2f * Mathf.PI * 48f * t) * punchEnv * 0.95f;
            float timberCrack = Mathf.Sin(2f * Mathf.PI * 170f * t) * Mathf.Exp(-t * 14f) * 0.6f;
            float debris = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 6f) * 0.45f;

            samples[i] = Mathf.Clamp(subThud + timberCrack + debris, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_WardrobeCrash", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeCabinetCrashClip()
    {
        // 0.95s: Kitchen cabinet toppling crash
        float duration = 0.95f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float punchEnv = Mathf.Exp(-t * 10f);
            float thud = Mathf.Sin(2f * Mathf.PI * 65f * t) * punchEnv * 0.85f;
            float woodClatter = Mathf.Sin(2f * Mathf.PI * 230f * t) * Mathf.Exp(-t * 18f) * 0.5f;
            float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 7f) * 0.4f;

            samples[i] = Mathf.Clamp(thud + woodClatter + noise, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_CabinetCrash", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeExplosionClip()
    {
        // 1.8s: Electric arc ignition zap followed by violent gas explosion blast
        float duration = 1.8f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;

            float zap = 0f;
            if (t < 0.12f)
            {
                zap = Mathf.Sin(2f * Mathf.PI * 2800f * t + Mathf.Sin(120f * t) * 6f) * 0.8f;
            }

            float blastT = Mathf.Max(0f, t - 0.10f);
            float blastEnv = Mathf.Exp(-blastT * 4.5f);
            float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 32f, Mathf.Clamp01(blastT / 0.5f)) * blastT) * blastEnv * 0.9f;
            float blastNoise = (UnityEngine.Random.value * 2f - 1f) * blastEnv * 0.65f;

            samples[i] = Mathf.Clamp(zap + boom + blastNoise, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_Explosion", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeDoorLockedClip()
    {
        // 0.28s: Door handle jiggle and locked strike
        float duration = 0.28f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float jiggle1 = Mathf.Sin(2f * Mathf.PI * 380f * t) * Mathf.Exp(-t * 28f);
            float jiggle2 = t > 0.09f ? Mathf.Sin(2f * Mathf.PI * 460f * t) * Mathf.Exp(-(t - 0.09f) * 28f) : 0f;
            float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.2f;

            samples[i] = Mathf.Clamp((jiggle1 + jiggle2 + noise) * 0.85f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_DoorLocked", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeDoorOpenClip()
    {
        // 0.70s: Heavy latch unhook + wooden door swinging open
        float duration = 0.70f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;

            // Unlatch click at start
            float latch = t < 0.12f ? Mathf.Sin(2f * Mathf.PI * 820f * t) * Mathf.Exp(-t * 35f) * 0.7f : 0f;

            // Door creak swing
            float creakEnv = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            float creakFreq = Mathf.Lerp(180f, 320f, t / duration);
            float creak = Mathf.Sin(2f * Mathf.PI * creakFreq * t) * creakEnv * 0.45f;
            float airWhoosh = (UnityEngine.Random.value * 2f - 1f) * creakEnv * 0.2f;

            samples[i] = Mathf.Clamp(latch + creak + airWhoosh, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_DoorOpen", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeValveTurnClip()
    {
        // 0.18s: Pipe valve metal friction squeak
        float duration = 0.18f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Sin(t / duration * Mathf.PI);
            float mod = Mathf.Sin(2f * Mathf.PI * 130f * t) * 90f;
            float metal = Mathf.Sin(2f * Mathf.PI * (540f + mod) * t) * 0.65f;
            float grit = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
            samples[i] = Mathf.Clamp((metal + grit) * env * 0.75f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_ValveTurn", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeValveLatchClip()
    {
        // 0.45s: Solid metal valve cutoff latch CLANK
        float duration = 0.45f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float punchEnv = Mathf.Exp(-t * 26f);
            float ringEnv = Mathf.Exp(-t * 9f);
            float thud = Mathf.Sin(2f * Mathf.PI * 92f * t) * punchEnv * 0.75f;
            float metal = (Mathf.Sin(2f * Mathf.PI * 1320f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 2580f * t)) * ringEnv * 0.65f;
            float grit = (UnityEngine.Random.value * 2f - 1f) * punchEnv * 0.3f;
            samples[i] = Mathf.Clamp(thud + metal + grit, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_ValveLatch", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeDolapStrainClip()
    {
        // 0.16s: Straining physical grunt / effort push
        float duration = 0.16f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 20f);
            float thump = Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 125f * t) * 0.3f;
            samples[i] = Mathf.Clamp(thump * env * 0.85f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_DolapStrain", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeDolapCreakClip()
    {
        // 0.26s: Heavy wood groan as wardrobe is lifted upright
        float duration = 0.26f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Sin(t / duration * Mathf.PI);
            float freq = 210f + Mathf.Sin(t * 70f) * 55f;
            float wood = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.55f;
            float friction = (UnityEngine.Random.value * 2f - 1f) * 0.15f;
            samples[i] = Mathf.Clamp((wood + friction) * env * 0.75f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_DolapCreak", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeUIHoverClip()
    {
        // 0.035s: Ultra soft UI hover tick
        float duration = 0.035f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 80f);
            samples[i] = Mathf.Clamp(Mathf.Sin(2f * Mathf.PI * 850f * t) * env * 0.4f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_UIHover", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeUIClickClip()
    {
        // 0.06s: Crisp UI button click
        float duration = 0.06f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 50f);
            float ping = Mathf.Sin(2f * Mathf.PI * 1150f * t) * 0.6f +
                         Mathf.Sin(2f * Mathf.PI * 1650f * t) * 0.4f;
            samples[i] = Mathf.Clamp(ping * env * 0.75f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_UIClick", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeQTEHitClip()
    {
        // 0.14s: Punchy successful target hit tone
        float duration = 0.14f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 25f);
            float hit = Mathf.Sin(2f * Mathf.PI * 520f * t) * 0.7f +
                        Mathf.Sin(2f * Mathf.PI * 1040f * t) * 0.35f;
            samples[i] = Mathf.Clamp(hit * env * 0.85f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_QTEHit", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizeQTEMissClip()
    {
        // 0.22s: Discordant fail buzz
        float duration = 0.22f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 14f);
            float buzz = Mathf.Sin(2f * Mathf.PI * 145f * t) * 0.5f +
                         Mathf.Sin(2f * Mathf.PI * 154f * t) * 0.5f; // dissonance
            samples[i] = Mathf.Clamp(buzz * env * 0.8f, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_QTEMiss", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip SynthesizePageTurnClip()
    {
        // 0.22s: Paper rustle / slide sound
        float duration = 0.22f;
        int sampleCount = (int)(SAMPLE_RATE * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Sin(t / duration * Mathf.PI);
            float paper = (UnityEngine.Random.value * 2f - 1f) * env * 0.45f;
            samples[i] = Mathf.Clamp(paper, -1f, 1f);
        }

        var clip = AudioClip.Create("SFX_PageTurn", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // -------------------------------------------------------------
    // AUDIO UTILITIES: Seamless Loop Edge Crossfade
    // -------------------------------------------------------------

    private static void ApplyLoopCrossfade(float[] samples, int crossfadeLength)
    {
        if (samples == null || samples.Length <= crossfadeLength * 2) return;

        int totalSamples = samples.Length;
        for (int i = 0; i < crossfadeLength; i++)
        {
            float t = (float)i / crossfadeLength;
            // Crossfade: start blends into end, end blends into start
            int endIdx = totalSamples - crossfadeLength + i;
            float origStart = samples[i];
            float origEnd = samples[endIdx];

            samples[i] = Mathf.Lerp(origEnd, origStart, t);
            samples[endIdx] = Mathf.Lerp(origEnd, origStart, t);
        }
    }

    #endregion
}
