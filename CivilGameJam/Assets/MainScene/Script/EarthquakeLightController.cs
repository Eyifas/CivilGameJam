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

    public float NormalIntensity => normalIntensity;
    public Light2D GlobalLight => globalLight;

    private void Awake()
    {
        Instance = this;
        if (globalLight == null)
        {
            globalLight = GetComponent<Light2D>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetCustomControlled(bool custom)
    {
        isCustomControlled = custom;
    }

    public void SetLightIntensity(float intensity)
    {
        if (globalLight != null)
        {
            globalLight.intensity = intensity;
        }
    }

    private void Update()
    {
        if (isCustomControlled)
        {
            return;
        }

        if (GameTimelineManager.Instance == null || globalLight == null)
        {
            return;
        }

        GameStage stage = GameTimelineManager.Instance.CurrentStage;

        // Hazırlık aşaması: ışık normal
        if (stage == GameStage.Stage1_Preparation)
        {
            globalLight.intensity = normalIntensity;
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

                globalLight.intensity = lightOn
                    ? normalIntensity
                    : 0f;
            }
        }

        // Deprem sonrası: elektrik tamamen kesiliyor (gaz kapatılıp şalter açılınca tekrar gelir)
        else if (stage == GameStage.Stage3_Utilities)
        {
            if (GameTimelineManager.Instance != null && GameTimelineManager.Instance.IsElectricityRestored)
            {
                globalLight.intensity = normalIntensity;
            }
            else
            {
                globalLight.intensity = 0f;
            }
        }
    }
}