using UnityEngine;

public class FlickeringLight : MonoBehaviour
{
    [Header("Light Settings")]
    [SerializeField] private Light pointLight;

    [SerializeField] private float minIntensity = 1.5f;
    [SerializeField] private float maxIntensity = 3.5f;

    [Header("Flicker Speed")]
    [SerializeField] private float flickerSpeed = 8f;

    private float baseIntensity;

    void Start()
    {
        if (pointLight == null)
            pointLight = GetComponent<Light>();

        baseIntensity = pointLight.intensity;
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(
            Time.time * flickerSpeed,
            0f
        );

        pointLight.intensity = Mathf.Lerp(
            minIntensity,
            maxIntensity,
            noise
        );
    }
}