using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

/// <summary>
/// Manages high-impact, fullscreen educational death/hazard visuals for all failure reasons in the game.
/// Restores the authentic world/camera visual architecture from commit 2faca7d0ae0a7d89dfc0937f7a71e06ca1f900f1:
/// - Real, high-resolution comic artwork loaded from project assets (Color.white, unlit, no color tints)
/// - Centered directly in front of the active camera on Sorting Order 20
/// - Backed with a solid black letterbox underlay on Sorting Order 19 that completely fills the entire screen
/// - If an image has not been added yet, displays a clean placeholder: "Hazard: {type} to be added"
/// - Freeze time (Time.timeScale = 0f, timers paused, player frozen, lights disabled)
/// - Plays hazard-specific sound effects and holds for dramatic recognition
/// - Seamless transition into TimeRewindController loop
/// </summary>
public class HazardVisualManager : MonoBehaviour
{
    private static HazardVisualManager s_instance;
    public static HazardVisualManager Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = FindFirstObjectByType<HazardVisualManager>();
                if (s_instance == null)
                {
                    GameObject go = new GameObject("HazardVisualManager");
                    s_instance = go.AddComponent<HazardVisualManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return s_instance;
        }
        private set => s_instance = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (s_instance == null)
        {
            var go = new GameObject("HazardVisualManager");
            s_instance = go.AddComponent<HazardVisualManager>();
            DontDestroyOnLoad(go);
        }
    }

    [System.Serializable]
    public class HazardConfig
    {
        public string id;
        public string title;
        public string[] matchKeywords;
        [TextArea(2, 4)]
        public string educationalReason;
        public float rewindDuration;
        public string resourcePath;
    }

    [Header("Registered Hazards Configuration")]
    [SerializeField] private List<HazardConfig> hazardConfigs = new List<HazardConfig>();

    private readonly Dictionary<string, SwitchInteraction> activeHazardInteractions = new Dictionary<string, SwitchInteraction>(StringComparer.OrdinalIgnoreCase);
    private Transform hazardRoot;

    public bool IsShowing => SwitchInteraction.IsAnyHazardActive;

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeDefaultConfigs(forceRefresh: true);
        BuildHazardGameObjects();
    }

    [ContextMenu("Build / Refresh Hazard GameObjects in Scene")]
    public void ManualBuildInEditor()
    {
        InitializeDefaultConfigs(forceRefresh: true);
        BuildHazardGameObjects();
    }

    private void InitializeDefaultConfigs(bool forceRefresh = false)
    {
        if (!forceRefresh && hazardConfigs != null && hazardConfigs.Count > 0)
        {
            return;
        }

        hazardConfigs = new List<HazardConfig>
        {
            // 1. Stage 2 - Candle fire (comic 2)
            new HazardConfig
            {
                id = "Hazard_CandleFire",
                title = "APARTMENT FIRE",
                matchKeywords = new[] { "candle", "APARTMENT FIRE", "flames" },
                educationalReason = "APARTMENT FIRE: The lit candle on the desk toppled over during the tremor, igniting the room! Always extinguish open flames and candles before an earthquake strikes!",
                rewindDuration = 15f,
                resourcePath = "Hazards/comic_panel_2_earthquake_fire"
            },

            // 2. Stage 2 - Heavy wall painting falls (comic 1)
            new HazardConfig
            {
                id = "Hazard_PaintingFall",
                title = "FALLING WALL PAINTING",
                matchKeywords = new[] { "painting", "wall hangings", "shattered glass" },
                educationalReason = "FATAL INJURY: The heavy wall painting above the desk fell and shattered glass! Always remove or securely anchor heavy wall hangings over desks/beds!",
                rewindDuration = 15f,
                resourcePath = "Hazards/comic_panel_1_picture_frame"
            },

            // 3. Stage 2 - Ran to the door during tremor (1000000107)
            new HazardConfig
            {
                id = "Hazard_DoorRush",
                title = "DOOR RUSH FALL",
                matchKeywords = new[] { "lateral forces", "RunDoor", "FATAL FALL", "violent lateral" },
                educationalReason = "FATAL FALL: Violent lateral forces threw you into walls and falling debris! Never run while the building is shaking—Drop, Cover, and Hold On!",
                rewindDuration = 5f,
                resourcePath = "Hazards/1000000107"
            },

            // 4. Stage 2 - Tremor timeout / panic freeze (1000000107)
            new HazardConfig
            {
                id = "Hazard_TremorPanicTimeout",
                title = "PANIC FREEZE / DEBRIS",
                matchKeywords = new[] { "Frozen in panic", "drop under sturdy", "knocks you down" },
                educationalReason = "TIME RAN OUT: Frozen in panic! In an earthquake, drop under sturdy furniture within seconds before violent shaking knocks you down!",
                rewindDuration = 5f,
                resourcePath = "Hazards/1000000107"
            },

            // 5. Stage 3 - Cabinet fall during aftershock (To be added)
            new HazardConfig
            {
                id = "Hazard_CabinetFall",
                title = "OVERHEAD CABINET FALL",
                matchKeywords = new[] { "cabinet fell", "shifting cabinet", "overhead obstacles" },
                educationalReason = "FATAL INJURY: The shifting cabinet fell on you during the aftershock! React quickly to secure loose overhead obstacles!",
                rewindDuration = 5f,
                resourcePath = "Hazards/hazard_cabinet_fall"
            },

            // 6. Stage 3 - Heavy wardrobe / dolap collapse (To be added)
            new HazardConfig
            {
                id = "Hazard_WardrobeFall",
                title = "WARDROBE COLLAPSE",
                matchKeywords = new[] { "wardrobe", "dolap", "crushed", "fell on you" },
                educationalReason = "WARDROBE COLLAPSE: The heavy unanchored wardrobe toppled over and crushed you! Always lift or secure fallen obstacles before moving forward!",
                rewindDuration = 5f,
                resourcePath = "Hazards/hazard_wardrobe_fall"
            },

            // 7. Stage 3 - Light switch spark explosion (explosion)
            new HazardConfig
            {
                id = "Hazard_ElectricSwitch",
                title = "GAS EXPLOSION (ELECTRIC SPARK)",
                matchKeywords = new[] { "electrical light switch", "switch sparked", "FATAL EXPLOSION", "leaking gas" },
                educationalReason = "FATAL EXPLOSION: The electrical light switch sparked and ignited the leaking gas! NEVER flip light switches or create electrical sparks when you smell gas!",
                rewindDuration = 5f,
                resourcePath = "Hazards/explosion"
            },

            // 8. Stage 3 - Gas valve in total darkness (comic 3)
            new HazardConfig
            {
                id = "Hazard_GasInDark",
                title = "VALVE IN TOTAL DARKNESS",
                matchKeywords = new[] { "gas valve in total darkness", "packed flashlight is essential", "broken fixtures" },
                educationalReason = "IN THE DARK: You couldn't locate the gas valve in total darkness and tripped over broken fixtures! A packed flashlight is essential for utility shut-off!",
                rewindDuration = 15f,
                resourcePath = "Hazards/comic_panel_3_gas_leak"
            },

            // 9. Stage 3 - Gas leak timeout (comic 3)
            new HazardConfig
            {
                id = "Hazard_GasTimeout",
                title = "TOXIC GAS SUFFOCATION",
                matchKeywords = new[] { "Lingered too long", "leaking natural gas" },
                educationalReason = "TIME RAN OUT: Lingered too long in an apartment with leaking natural gas!",
                rewindDuration = 15f,
                resourcePath = "Hazards/comic_panel_3_gas_leak"
            },

            // 10. Stage 4 - Tripped over stairs planter (To be added)
            new HazardConfig
            {
                id = "Hazard_StairsPlanter",
                title = "STAIRS PLANTER FALL",
                matchKeywords = new[] { "heavy planter", "fell down the stairs" },
                educationalReason = "INJURY: In total darkness without a flashlight, you tripped over the heavy planter and fell down the stairs!",
                rewindDuration = 15f,
                resourcePath = "Hazards/hazard_stairs_fall"
            },

            // 11. Stage 4 - Blind search for neighbor in dark corridor (comic 3)
            new HazardConfig
            {
                id = "Hazard_CorridorSearch",
                title = "BLIND CORRIDOR SEARCH",
                matchKeywords = new[] { "Mr. Ahmet", "BLIND SEARCH", "dark corridor" },
                educationalReason = "BLIND SEARCH: Without a flashlight, you couldn't locate or treat Mr. Ahmet in the dark corridor.",
                rewindDuration = 15f,
                resourcePath = "Hazards/comic_panel_3_gas_leak"
            },

            // 12. Stage 5 - Falling storefront facade (To be added)
            new HazardConfig
            {
                id = "Hazard_StreetFacade",
                title = "FALLING STOREFRONT FACADE",
                matchKeywords = new[] { "storefront facade", "falling facade", "overhead hazards" },
                educationalReason = "FATAL TRAUMA: Struck by falling storefront facade! Watch overhead hazards when stepping into the street!",
                rewindDuration = 5f,
                resourcePath = "Hazards/hazard_street_facade"
            },

            // 13. Stage 5 - Mountain road gridlock in car (To be added)
            new HazardConfig
            {
                id = "Hazard_CarGridlock",
                title = "MOUNTAIN ROAD GRIDLOCK",
                matchKeywords = new[] { "gridlocked", "GRIDLOCK TRAP", "Stay out of cars", "mountain road" },
                educationalReason = "GRIDLOCK TRAP: You and hundreds of panicked drivers gridlocked the mountain road, blocking emergency rescue vehicles! Stay out of cars!",
                rewindDuration = 5f,
                resourcePath = "Hazards/hazard_car_gridlock"
            },

            // 14. Stage 5 - Tsunami hazard (comic 4)
            new HazardConfig
            {
                id = "Hazard_TsunamiEnding",
                title = "TSUNAMI ZONE",
                matchKeywords = new[] { "tsunami", "coast", "wave" },
                educationalReason = "TSUNAMI HAZARD: Moving towards the sea or coastline after a major undersea earthquake is lethal! Move immediately to high ground!",
                rewindDuration = 5f,
                resourcePath = "Hazards/comic_panel_4_tsunami_ending"
            }
        };
    }

    public void BuildHazardGameObjects()
    {
        if (hazardRoot == null)
        {
            var existingRoot = GameObject.Find("HazardVisuals");
            if (existingRoot != null)
            {
                hazardRoot = existingRoot.transform;
            }
            else
            {
                var rootObj = new GameObject("HazardVisuals");
                hazardRoot = rootObj.transform;
            }
        }

        hazardRoot.SetParent(transform, true);

        Sprite backdropSprite = GetSolidBlackSprite();
        var artworkMat = SwitchInteraction.GetUnlitArtworkMaterial();
        var backdropMat = SwitchInteraction.GetUnlitBackdropMaterial();

        foreach (var config in hazardConfigs)
        {
            Transform existingChild = hazardRoot.Find(config.id);
            GameObject hazardObj;
            SwitchInteraction switchComp;

            if (existingChild != null)
            {
                hazardObj = existingChild.gameObject;
                switchComp = hazardObj.GetComponent<SwitchInteraction>();
                if (switchComp == null) switchComp = hazardObj.AddComponent<SwitchInteraction>();
            }
            else
            {
                hazardObj = new GameObject(config.id);
                hazardObj.transform.SetParent(hazardRoot, false);
                switchComp = hazardObj.AddComponent<SwitchInteraction>();
            }

            // Configure SwitchInteraction
            switchComp.SetTriggerOnInteract(false);
            switchComp.SetEducationalReason(config.educationalReason);
            switchComp.SetRewindDuration(config.rewindDuration);
            switchComp.SetSortingOrder(20);
            switchComp.SetImageDuration(2.2f);

            // Attempt loading real artwork sprite
            Sprite realSprite = LoadHazardSprite(config.resourcePath);

            // Ensure visual child exists (Visual / TempVisual)
            Transform visualTransform = hazardObj.transform.Find("Visual") ?? hazardObj.transform.Find("TempVisual");
            GameObject visualObj;
            SpriteRenderer sr;

            if (visualTransform != null)
            {
                visualObj = visualTransform.gameObject;
                visualObj.name = "Visual";
                sr = visualObj.GetComponent<SpriteRenderer>();
                if (sr == null) sr = visualObj.AddComponent<SpriteRenderer>();
            }
            else
            {
                visualObj = new GameObject("Visual");
                visualObj.transform.SetParent(hazardObj.transform, false);
                sr = visualObj.AddComponent<SpriteRenderer>();
            }

            // 1. Ensure solid black letterbox backdrop on Layer 18 (ALWAYS active)
            Transform bdTransform = visualObj.transform.Find("Backdrop");
            GameObject bdObj;
            SpriteRenderer bdSr;

            if (bdTransform != null)
            {
                bdObj = bdTransform.gameObject;
                bdSr = bdObj.GetComponent<SpriteRenderer>();
                if (bdSr == null) bdSr = bdObj.AddComponent<SpriteRenderer>();
            }
            else
            {
                bdObj = new GameObject("Backdrop");
                bdObj.transform.SetParent(visualObj.transform, false);
                bdSr = bdObj.AddComponent<SpriteRenderer>();
            }

            bdSr.sprite = backdropSprite;
            bdSr.color = Color.black;
            bdSr.sortingLayerName = "Default";
            bdSr.sortingOrder = 18; // Strictly behind artwork and placeholder
            if (backdropMat != null) bdSr.sharedMaterial = backdropMat;
            bdObj.transform.localPosition = new Vector3(0f, 0f, 0.5f);

            // 2. Artwork vs. Placeholder
            if (realSprite != null)
            {
                // Artwork is added
                sr.enabled = true;
                sr.sprite = realSprite;
                sr.sortingLayerName = "Default";
                sr.sortingOrder = 20;
                sr.color = Color.white;
                if (artworkMat != null) sr.sharedMaterial = artworkMat;

                // Deactivate placeholder label
                Transform oldLabel = visualObj.transform.Find("PlaceholderLabel") ?? visualObj.transform.Find("TempLabel");
                if (oldLabel != null)
                {
                    oldLabel.gameObject.SetActive(false);
                }
            }
            else
            {
                // Image is NOT added: write "Hazard: {type} to be added"
                sr.enabled = false;
                sr.sprite = null;

                BuildPlaceholderLabel(visualObj.transform, config.title, config.educationalReason);
            }

            visualObj.SetActive(false);
            switchComp.SetSwitchImage(visualObj);

            // Register in lookup
            activeHazardInteractions[config.id] = switchComp;
        }

        Debug.Log($"<color=cyan>[HazardVisualManager] Successfully configured {hazardConfigs.Count} hazard visuals.</color>");
    }

    private static TMP_FontAsset s_defaultFont;
    public static TMP_FontAsset GetDefaultFont()
    {
        if (s_defaultFont != null) return s_defaultFont;
        s_defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (s_defaultFont == null) s_defaultFont = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
        if (s_defaultFont == null)
        {
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (all != null && all.Length > 0) s_defaultFont = all[0];
        }
#if UNITY_EDITOR
        if (s_defaultFont == null)
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:TMP_FontAsset");
            if (guids != null && guids.Length > 0)
            {
                string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                s_defaultFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            }
        }
#endif
        return s_defaultFont;
    }

    private void BuildPlaceholderLabel(Transform parent, string title, string reason)
    {
        Transform existing = parent.Find("PlaceholderLabel") ?? parent.Find("TempLabel");
        GameObject labelObj;
        TextMeshPro tmp;

        if (existing != null)
        {
            labelObj = existing.gameObject;
            labelObj.name = "PlaceholderLabel";
            labelObj.SetActive(true);
            tmp = labelObj.GetComponent<TextMeshPro>();
            if (tmp == null) tmp = labelObj.AddComponent<TextMeshPro>();
        }
        else
        {
            labelObj = new GameObject("PlaceholderLabel");
            labelObj.transform.SetParent(parent, false);
            tmp = labelObj.AddComponent<TextMeshPro>();
        }

        labelObj.transform.localPosition = new Vector3(0f, 0f, -0.2f);
        labelObj.transform.localScale = Vector3.one;

        // Clean, readable placeholder notice requested by user
        var font = GetDefaultFont();
        if (font != null)
        {
            tmp.font = font;
        }
        tmp.text = $"<size=130%><color=#FFD700><b>Hazard: {title} to be added</b></color></size>\n\n<size=65%><color=#D8D8D8>{reason}</color></size>";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 6.5f;
        tmp.sortingLayerID = SortingLayer.NameToID("Default");
        tmp.sortingOrder = 22; // In front of backdrop (18) and sprite (20)
        tmp.rectTransform.sizeDelta = new Vector2(16f, 8f);
    }

    private static Sprite s_solidBlackSprite;
    public static Sprite GetSolidBlackSprite()
    {
        if (s_solidBlackSprite != null) return s_solidBlackSprite;
        int size = 2;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, Color.black);
            }
        }
        tex.Apply();
        // 2x2 sprite with 1 pixel per unit = exactly 2x2 world units
        s_solidBlackSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
        return s_solidBlackSprite;
    }

    /// <summary>
    /// Finds a registered SwitchInteraction matching the given educational reason or keyword.
    /// </summary>
    public SwitchInteraction GetHazard(string educationalReason)
    {
        if (string.IsNullOrEmpty(educationalReason)) return null;

        foreach (var config in hazardConfigs)
        {
            if (educationalReason.IndexOf(config.title, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (activeHazardInteractions.TryGetValue(config.id, out var switchComp))
                {
                    return switchComp;
                }
            }

            foreach (var keyword in config.matchKeywords)
            {
                if (educationalReason.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (activeHazardInteractions.TryGetValue(config.id, out var switchComp))
                    {
                        return switchComp;
                    }
                }
            }
        }

        // Fallback: Check if any active hazard matches directly
        foreach (var kvp in activeHazardInteractions)
        {
            if (kvp.Value != null && kvp.Value.EducationalReason.IndexOf(educationalReason, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return kvp.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Triggers the death visual sequence for the matching educational reason.
    /// Returns true if a visual was found and triggered.
    /// </summary>
    public bool TriggerHazard(string educationalReason)
    {
        var hazard = GetHazard(educationalReason);
        if (hazard != null && !hazard.IsTriggered)
        {
            hazard.TriggerSequence();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Unified visual player with completion callback.
    /// </summary>
    public void ShowHazardVisual(string educationalReason, Action onDone = null)
    {
        var hazard = GetHazard(educationalReason);
        if (hazard != null && !hazard.IsTriggered)
        {
            hazard.TriggerSequence(educationalReason, 5f, 2.2f, onDone);
        }
        else
        {
            onDone?.Invoke();
        }
    }

    /// <summary>
    /// Hides all active hazard visuals immediately.
    /// </summary>
    public void Hide()
    {
        foreach (var kvp in activeHazardInteractions)
        {
            if (kvp.Value != null)
            {
                kvp.Value.HideVisual();
            }
        }
    }

    /// <summary>
    /// Loads a sprite by resource path or relative asset path from disk/Resources/AssetDatabase.
    /// Supports single sprites, multi-sprite sprite sheets (e.g. 1000000107), Texture2Ds, and editor assets.
    /// </summary>
    public static Sprite LoadHazardSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // 1. Direct single sprite via Resources.Load
        Sprite spr = Resources.Load<Sprite>(path);
        if (spr != null) return spr;

        // 2. Multi-sprite sheet via Resources.LoadAll (e.g. 1000000107_0)
        Sprite[] allSpr = Resources.LoadAll<Sprite>(path);
        if (allSpr != null && allSpr.Length > 0)
        {
            foreach (var s in allSpr)
            {
                if (s != null) return s;
            }
        }

        // 3. Texture2D in Resources converted to Sprite
        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

#if UNITY_EDITOR
        // 4. In Unity Editor, try direct AssetDatabase loading
        string[] extensions = new[] { ".png", ".jpg", ".jpeg" };
        foreach (var ext in extensions)
        {
            string p1 = "Assets/Resources/" + path + ext;
            var s1 = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p1);
            if (s1 != null) return s1;

            var all1 = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p1);
            if (all1 != null)
            {
                foreach (var a in all1)
                {
                    if (a is Sprite s) return s;
                }
            }

            string p2 = "Assets/" + path + ext;
            var s2 = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p2);
            if (s2 != null) return s2;

            var all2 = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p2);
            if (all2 != null)
            {
                foreach (var a in all2)
                {
                    if (a is Sprite s) return s;
                }
            }
        }
#endif

        // 5. File system raw bytes fallback (ensures guaranteed loading even outside Resources cache)
        string[] exts = new[] { ".png", ".jpg", ".jpeg" };
        foreach (var ext in exts)
        {
            string diskPath = System.IO.Path.Combine(Application.dataPath, "Resources", path + ext);
            if (!System.IO.File.Exists(diskPath))
            {
                diskPath = System.IO.Path.Combine(Application.dataPath, path + ext);
            }
            if (System.IO.File.Exists(diskPath))
            {
                try
                {
                    byte[] fileData = System.IO.File.ReadAllBytes(diskPath);
                    Texture2D diskTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (diskTex.LoadImage(fileData))
                    {
                        return Sprite.Create(diskTex, new Rect(0, 0, diskTex.width, diskTex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
                catch { }
            }
        }

        return null;
    }

    /// <summary>
    /// Multi-tier sprite loader kept for backwards compatibility.
    /// </summary>
    public static Sprite LoadProjectSprite(string resourcePath, string[] relativeFilePaths)
    {
        Sprite s = LoadHazardSprite(resourcePath);
        if (s != null) return s;

        if (relativeFilePaths != null)
        {
            foreach (var rel in relativeFilePaths)
            {
                s = LoadHazardSprite(rel);
                if (s != null) return s;
            }
        }

        return null;
    }
}
