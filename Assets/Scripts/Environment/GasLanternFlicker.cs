using UnityEngine;

public class GasLanternFlicker : MonoBehaviour
{
    [Header("Light Settings")]
    [SerializeField] private Light lanternLight;

    [Header("Flicker")]
    [SerializeField] private float baseIntensity = 2f;
    [SerializeField] private float flickerStrength = 0.25f;
    [SerializeField] private float flickerSpeed = 8f;

    [Header("Optional Range Flicker")]
    [SerializeField] private bool flickerRange = false;
    [SerializeField] private float baseRange = 6f;
    [SerializeField] private float rangeStrength = 0.3f;

    private float noiseOffset;

    private void Start()
    {
        if (lanternLight == null)
            lanternLight = GetComponent<Light>();

        noiseOffset = Random.Range(0f, 100f);

        lanternLight.intensity = baseIntensity;
        lanternLight.range = baseRange;
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(
            noiseOffset,
            Time.time * flickerSpeed
        );

        // Convert 0-1 noise to -1 to 1
        float variation = (noise - 0.5f) * 2f;

        // Change intensity
        lanternLight.intensity =
            baseIntensity + variation * flickerStrength;

        // Optional range variation
        if (flickerRange)
        {
            lanternLight.range =
                baseRange + variation * rangeStrength;
        }
    }
}