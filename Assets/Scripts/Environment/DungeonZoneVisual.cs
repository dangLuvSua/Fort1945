using TMPro;
using UnityEngine;

public class DungeonZoneVisual : MonoBehaviour
{
    [Header("Zone Floor")]
    [SerializeField] private Renderer zoneFloorRenderer;

    [Header("Colors")]
    [SerializeField]
    private Color redColor =
        new Color(1f, 0.05f, 0.05f, 0.45f);

    [SerializeField]
    private Color greenColor =
        new Color(0.1f, 1f, 0.3f, 0.45f);

    [Header("Optional")]
    [SerializeField] private bool hideWhenInactive = true;

    private Material runtimeMaterial;
    private bool isGreen;

    private void Awake()
    {
        InitializeMaterial();

        SetRed();
    }

    private void InitializeMaterial()
    {
        if (zoneFloorRenderer == null)
        {
            Debug.LogWarning(
                "[DUNGEON ZONE VISUAL] Zone floor Renderer is not assigned.",
                this
            );

            return;
        }

        // Creates a material instance for this object.
        // This prevents changing the original shared material.
        runtimeMaterial =
            zoneFloorRenderer.material;
    }

    public void SetActive(bool active)
    {
        if (hideWhenInactive)
        {
            gameObject.SetActive(active);
        }
    }

    public void SetRed()
    {
        isGreen = false;

        ApplyColor(redColor);

        Debug.Log(
            "[DUNGEON ZONE VISUAL] Zone is RED."
        );
    }

    public void SetGreen()
    {
        isGreen = true;

        ApplyColor(greenColor);

        Debug.Log(
            "[DUNGEON ZONE VISUAL] Zone is GREEN."
        );
    }

    private void ApplyColor(Color color)
    {
        if (zoneFloorRenderer == null)
            return;

        if (runtimeMaterial == null)
            runtimeMaterial = zoneFloorRenderer.material;

        // Standard / Built-in
        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.color = color;
        }

        // URP Lit / Unlit
        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor(
                "_BaseColor",
                color
            );
        }
    }
}