using UnityEngine;

// =========================================================
//  MissionData  —  ScriptableObject
//  Holds all data required to populate one mission-objective
//  panel in the HUD.
//
//  Create via: Fort1945 ▸ Mission Data
// =========================================================

/// <summary>
/// A ScriptableObject asset that stores all content and display
/// settings for a single mission-objective panel entry.
/// </summary>
[CreateAssetMenu(fileName = "MissionData", menuName = "Fort1945/Mission Data")]
public class MissionData : ScriptableObject
{
    // =========================================================
    //  Text
    // =========================================================

    [Header("Text")]

    [Tooltip("Short heading displayed at the top of the panel, e.g. \"FOLLOW THE SOLDIER\".")]
    public string title;

    [Tooltip("Longer body text shown beneath the heading.")]
    [TextArea(2, 5)]
    public string description;

    // =========================================================
    //  Icon
    // =========================================================

    [Header("Icon")]

    [Tooltip("Optional icon sprite. Leave null to hide the icon container.")]
    public Sprite icon;

    // =========================================================
    //  Display
    // =========================================================

    [Header("Display")]

    [Tooltip("Seconds before the panel auto-hides. Set to 0 for a persistent panel that must be hidden manually.")]
    public float autoDuration = 0f;

    [Tooltip("Color applied to the title text.")]
    public Color titleColor = Color.white;

    [Tooltip("Color applied to the description text.")]
    public Color descriptionColor = new Color(0.85f, 0.85f, 0.85f, 1f);
}
