using UnityEngine;
using UnityEngine.Rendering.Universal;

public class EarthquakeLightController : MonoBehaviour
{
    [SerializeField] private Light2D globalLight;

    [Header("Light Settings")]
    [SerializeField] private float normalIntensity = 1f;
    [SerializeField] private float flickerInterval = 0.08f;

    private float flickerTimer;

    private void Awake()
    {
        if (globalLight == null)
        {
            globalLight = GetComponent<Light2D>();
        }
    }

    private void Update()
    {
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

        // Deprem sonrası: elektrik tamamen kesiliyor
        else if (stage == GameStage.Stage3_Utilities)
        {
            globalLight.intensity = 0f;
        }
    }
}