using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Manages hazard death visuals for all failure reasons in the game.
/// Ensures each failure reason has a dedicated SwitchInteraction component with:
/// - Correct educational reason
/// - Correct rewind duration (15s for prep/timeouts, 5s for quick reactions)
/// - Temp image displayed on Sorting Order 20 (on top of everything)
/// - Full-screen freeze (Time.timeScale = 0f, timers paused, player frozen)
/// </summary>
public class HazardVisualManager : MonoBehaviour
{
    public static HazardVisualManager Instance { get; private set; }

    [System.Serializable]
    public class HazardConfig
    {
        public string id;
        public string title;
        public string[] matchKeywords;
        [TextArea(2, 4)]
        public string educationalReason;
        public float rewindDuration;
        public Color tempColorTint;
    }

    [Header("Registered Hazards Configuration")]
    [SerializeField] private List<HazardConfig> hazardConfigs = new List<HazardConfig>();

    private readonly Dictionary<string, SwitchInteraction> activeHazardInteractions = new Dictionary<string, SwitchInteraction>(StringComparer.OrdinalIgnoreCase);
    private Transform hazardRoot;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeDefaultConfigs();
        BuildHazardGameObjects();
    }

    [ContextMenu("Build / Refresh Hazard GameObjects in Scene")]
    public void ManualBuildInEditor()
    {
        InitializeDefaultConfigs();
        BuildHazardGameObjects();
    }

    private void InitializeDefaultConfigs()
    {
        if (hazardConfigs.Count > 0) return;

        hazardConfigs = new List<HazardConfig>
        {
            // 1. Stage 2 - Candle fire
            new HazardConfig
            {
                id = "Hazard_CandleFire",
                title = "APARTMENT FIRE",
                matchKeywords = new[] { "candle", "APARTMENT FIRE", "flames" },
                educationalReason = "APARTMENT FIRE: The lit candle on the desk toppled over during the tremor, igniting the room! Always extinguish open flames and candles before an earthquake strikes!",
                rewindDuration = 15f,
                tempColorTint = new Color(1f, 0.42f, 0.15f, 1f) // Fire Orange
            },

            // 2. Stage 2 - Heavy wall painting falls
            new HazardConfig
            {
                id = "Hazard_PaintingFall",
                title = "FALLING WALL PAINTING",
                matchKeywords = new[] { "painting", "wall hangings", "shattered glass" },
                educationalReason = "FATAL INJURY: The heavy wall painting above the desk fell and shattered glass! Always remove or securely anchor heavy wall hangings over desks/beds!",
                rewindDuration = 15f,
                tempColorTint = new Color(0.9f, 0.22f, 0.22f, 1f) // Crimson
            },

            // 3. Stage 2 - Ran to the door during tremor
            new HazardConfig
            {
                id = "Hazard_DoorRush",
                title = "DOOR RUSH FALL",
                matchKeywords = new[] { "lateral forces", "RunDoor", "FATAL FALL", "violent lateral" },
                educationalReason = "FATAL FALL: Violent lateral forces threw you into walls and falling debris! Never run while the building is shaking—Drop, Cover, and Hold On!",
                rewindDuration = 5f,
                tempColorTint = new Color(0.85f, 0.25f, 0.35f, 1f) // Deep Red
            },

            // 4. Stage 2 - Tremor timeout (panic / freeze)
            new HazardConfig
            {
                id = "Hazard_TremorPanicTimeout",
                title = "PANIC FREEZE / DEBRIS",
                matchKeywords = new[] { "Frozen in panic", "drop under sturdy", "knocks you down" },
                educationalReason = "TIME RAN OUT: Frozen in panic! In an earthquake, drop under sturdy furniture within seconds before violent shaking knocks you down!",
                rewindDuration = 5f,
                tempColorTint = new Color(1f, 0.72f, 0.2f, 1f) // Amber Warning
            },

            // 5. Stage 3 - Cabinet fall during aftershock
            new HazardConfig
            {
                id = "Hazard_CabinetFall",
                title = "OVERHEAD CABINET FALL",
                matchKeywords = new[] { "cabinet fell", "shifting cabinet", "overhead obstacles" },
                educationalReason = "FATAL INJURY: The shifting cabinet fell on you during the aftershock! React quickly to secure loose overhead obstacles!",
                rewindDuration = 5f,
                tempColorTint = new Color(0.92f, 0.5f, 0.2f, 1f) // Heavy Wood
            },

            // 6. Stage 3 - Light switch spark explosion
            new HazardConfig
            {
                id = "Hazard_ElectricSwitch",
                title = "GAS EXPLOSION (ELECTRIC SPARK)",
                matchKeywords = new[] { "electrical light switch", "switch sparked", "FATAL EXPLOSION", "leaking gas" },
                educationalReason = "FATAL EXPLOSION: The electrical light switch sparked and ignited the leaking gas! NEVER flip light switches or create electrical sparks when you smell gas!",
                rewindDuration = 5f,
                tempColorTint = new Color(1f, 0.95f, 0.45f, 1f) // Electric Yellow
            },

            // 7. Stage 3 - Gas valve in total darkness (no flashlight)
            new HazardConfig
            {
                id = "Hazard_GasInDark",
                title = "VALVE IN TOTAL DARKNESS",
                matchKeywords = new[] { "gas valve in total darkness", "packed flashlight is essential", "broken fixtures" },
                educationalReason = "IN THE DARK: You couldn't locate the gas valve in total darkness and tripped over broken fixtures! A packed flashlight is essential for utility shut-off!",
                rewindDuration = 15f,
                tempColorTint = new Color(0.28f, 0.35f, 0.7f, 1f) // Dark Violet
            },

            // 8. Stage 3 - Gas leak timeout
            new HazardConfig
            {
                id = "Hazard_GasTimeout",
                title = "TOXIC GAS SUFFOCATION",
                matchKeywords = new[] { "Lingered too long", "leaking natural gas" },
                educationalReason = "TIME RAN OUT: Lingered too long in an apartment with leaking natural gas!",
                rewindDuration = 15f,
                tempColorTint = new Color(0.45f, 0.85f, 0.3f, 1f) // Toxic Green
            },

            // 9. Stage 4 - Tripped over stairs planter
            new HazardConfig
            {
                id = "Hazard_StairsPlanter",
                title = "STAIRS PLANTER FALL",
                matchKeywords = new[] { "heavy planter", "fell down the stairs" },
                educationalReason = "INJURY: In total darkness without a flashlight, you tripped over the heavy planter and fell down the stairs!",
                rewindDuration = 15f,
                tempColorTint = new Color(0.78f, 0.3f, 0.3f, 1f) // Rust Red
            },

            // 10. Stage 4 - Blind search for neighbor
            new HazardConfig
            {
                id = "Hazard_CorridorSearch",
                title = "BLIND CORRIDOR SEARCH",
                matchKeywords = new[] { "Mr. Ahmet", "BLIND SEARCH", "dark corridor" },
                educationalReason = "BLIND SEARCH: Without a flashlight, you couldn't locate or treat Mr. Ahmet in the dark corridor.",
                rewindDuration = 15f,
                tempColorTint = new Color(0.25f, 0.32f, 0.65f, 1f) // Deep Navy
            },

            // 11. Stage 5 - Falling storefront facade
            new HazardConfig
            {
                id = "Hazard_StreetFacade",
                title = "FALLING STOREFRONT FACADE",
                matchKeywords = new[] { "storefront facade", "falling facade", "overhead hazards" },
                educationalReason = "FATAL TRAUMA: Struck by falling storefront facade! Watch overhead hazards when stepping into the street!",
                rewindDuration = 5f,
                tempColorTint = new Color(0.85f, 0.32f, 0.25f, 1f) // Brick Red
            },

            // 12. Stage 5 - Mountain road gridlock in car
            new HazardConfig
            {
                id = "Hazard_CarGridlock",
                title = "MOUNTAIN ROAD GRIDLOCK",
                matchKeywords = new[] { "gridlocked", "GRIDLOCK TRAP", "Stay out of cars", "mountain road" },
                educationalReason = "GRIDLOCK TRAP: You and hundreds of panicked drivers gridlocked the mountain road, blocking emergency rescue vehicles! Stay out of cars!",
                rewindDuration = 5f,
                tempColorTint = new Color(0.65f, 0.5f, 0.42f, 1f) // Smoky Gray
            }
        };
    }

    public void BuildHazardGameObjects()
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

        Sprite tempSprite = GetDefaultTempSprite();

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

            // Ensure visual child exists
            Transform visualTransform = hazardObj.transform.Find("TempVisual");
            GameObject visualObj;
            if (visualTransform != null)
            {
                visualObj = visualTransform.gameObject;
                var sr = visualObj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var unlitMat = SwitchInteraction.GetUnlitMaterial();
                    if (unlitMat != null) sr.material = unlitMat;
                }
            }
            else
            {
                visualObj = new GameObject("TempVisual");
                visualObj.transform.SetParent(hazardObj.transform, false);

                var sr = visualObj.AddComponent<SpriteRenderer>();
                sr.sprite = tempSprite;
                sr.sortingOrder = 20;
                sr.color = config.tempColorTint;
                var unlitMat = SwitchInteraction.GetUnlitMaterial();
                if (unlitMat != null)
                {
                    sr.material = unlitMat;
                }

                // Size to full 16:9 screen
                visualObj.transform.localScale = new Vector3(1f, 1f, 1f);

                // Add descriptive text label overlay
                BuildTempLabel(visualObj.transform, config.title);
            }

            visualObj.SetActive(false);
            switchComp.SetSwitchImage(visualObj);

            // Register in lookup
            activeHazardInteractions[config.id] = switchComp;
        }

        Debug.Log($"<color=cyan>[HazardVisualManager] Successfully built {hazardConfigs.Count} hazard visuals on Layer 20.</color>");
    }

    private void BuildTempLabel(Transform parent, string title)
    {
        GameObject textObj = new GameObject("TempLabel");
        textObj.transform.SetParent(parent, false);
        textObj.transform.localPosition = new Vector3(0f, 3.8f, -0.1f);

        var tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = $"<color=yellow>[TEMP HAZARD ARTWORK]</color>\n<b>{title}</b>";
        tmp.fontSize = 7f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 21; // Above the sprite
        tmp.rectTransform.sizeDelta = new Vector2(16f, 3f);
    }

    private Sprite GetDefaultTempSprite()
    {
        // 1. Try finding 1000000106_0 in the scene
        var existing = GameObject.Find("1000000106_0");
        if (existing != null)
        {
            var sr = existing.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                return sr.sprite;
            }
        }

        // 2. Generate a clean fallback 16:9 procedural sprite
        return GenerateFallbackSprite();
    }

    private Sprite GenerateFallbackSprite()
    {
        int width = 256;
        int height = 144;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color baseDark = new Color(0.12f, 0.12f, 0.14f, 0.95f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width;
                float v = (float)y / height;
                float edgeDist = Mathf.Min(u, 1f - u, v, 1f - v) * 2f;
                float vignette = Mathf.Clamp01(edgeDist);
                tex.SetPixel(x, y, baseDark * vignette);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 13.33f);
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
}
