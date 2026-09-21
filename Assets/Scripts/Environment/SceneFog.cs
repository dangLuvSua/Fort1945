using UnityEngine;

public class SceneFog : MonoBehaviour
{
    [Header("Fog Settings")]
    [SerializeField] private Color fogColor = new Color(0.12f, 0.08f, 0.07f);
    [SerializeField] [Range(0f, 0.1f)] private float fogDensity = 0.02f;

    private void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    private void OnDestroy()
    {
        RenderSettings.fog = false;
    }
}