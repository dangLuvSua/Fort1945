using UnityEngine;

public class KeyGlow : MonoBehaviour
{
    [SerializeField] Renderer rend;
    [SerializeField] Color glowColor = new Color(1f, 0.7f, 0.2f);
    [SerializeField] float minIntensity = 1f;
    [SerializeField] float maxIntensity = 4f;
    [SerializeField] float speed = 2f;

    Material mat;

    void Awake()
    {
        mat = rend.material; // gumagawa ng sariling instance ng material
        mat.EnableKeyword("_EMISSION");
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        mat.SetColor("_EmissionColor", glowColor * Mathf.Lerp(minIntensity, maxIntensity, t));
    }
}