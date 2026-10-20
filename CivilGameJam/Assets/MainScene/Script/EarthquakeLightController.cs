using UnityEngine;
using UnityEngine.Rendering.Universal;

public class EarthquakeLightController : MonoBehaviour
{
    public static EarthquakeLightController Instance { get; private set; }

    [SerializeField] private Light2D globalLight;

    [Header("Light Settings")]
    [SerializeField] private float normalIntensity = 1f;
    [SerializeField] private float flickerInterval = 0.08f;

    private float flickerTimer;
    private bool isCustomControlled = false;

    public float NormalIntensity => normalIntensity > 0f ? normalIntensity : 1f;
    public Light2D GlobalLight => globalLight;

    private void Awake()
    {
        Instance = this;
        isCustomControlled = false;
        EnsureGlobalLight();
        ApplyNormalLighting();
    }

    private void Start()
    {
        ApplyNormalLighting();
    }

    private void OnEnable()
    {
        ApplyNormalLighting();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void EnsureGlobalLight()
    {
        if (globalLight == null)
        {
            globalLight = GetComponent<Light2D>();
        }
        if (globalLight == null)
        {
            var lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l != null && l.lightType == Light2D.LightType.Global)
                {
                    globalLight = l;
                    break;
                }
            }
        }
    }

    public void ApplyNormalLighting()
    {
        EnsureGlobalLight();
        isCustomControlled = false;
        if (globalLight != null)
        {
            globalLight.enabled = true;
            globalLight.intensity = NormalIntensity;
        }
    }

    public void SetCustomControlled(bool custom)
    {
        isCustomControlled = custom;
    }

    public void SetLightIntensity(float intensity)
    {
        EnsureGlobalLight();
        if (globalLight != null)
        {
            if (intensity > 0f && !globalLight.enabled)
            {
                globalLight.enabled = true;
            }
            globalLight.intensity = intensity;
        }
    }

    private void Update()
    {
        if (isCustomControlled)
        {
            return;
        }

        EnsureGlobalLight();
        if (globalLight == null)
        {
            return;
        }

        GameStage stage = GameTimelineManager.Instance != null
            ? GameTimelineManager.Instance.CurrentStage
            : GameStage.Stage1_Preparation;

        // Hazırlık aşaması veya geri sarma: ışık her zaman normal açık
        if (stage == GameStage.Stage1_Preparation || stage == GameStage.Rewinding)
        {
            if (!globalLight.enabled) globalLight.enabled = true;
            globalLight.intensity = NormalIntensity;
        }

        // Deprem sırasında: ışık gidip geliyor
        else if (stage == GameStage.Stage2_Tremor)
        {
            flickerTimer -= Time.unscaledDeltaTime;

            if (flickerTimer <= 0f)
            {
                flickerTimer = flickerInterval;

                // Bazen tamamen sön, bazen normal yan
                bool lightOn = Random.value > 0.45f;

                if (!globalLight.enabled) globalLight.enabled = true;
                globalLight.intensity = lightOn
                    ? NormalIntensity
                    : 0f;
            }
        }

        // Deprem sonrası: elektrik tamamen kesiliyor (gaz kapatılıp şalter açılınca tekrar gelir)
        else if (stage == GameStage.Stage3_Utilities)
        {
            if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsElectricityRestored)
            {
                if (!globalLight.enabled) globalLight.enabled = true;
                globalLight.intensity = NormalIntensity;
            }
            else
            {
                globalLight.intensity = 0f;
            }
        }
    }
}
