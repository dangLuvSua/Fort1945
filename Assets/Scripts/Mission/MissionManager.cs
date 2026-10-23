using System;
using UnityEngine;

// =========================================================
//  MissionManager  —  MonoBehaviour Singleton
//  Local-only controller for the mission-objective panel.
//  No networking — purely a UI driver.
//
//  Static API:
//      MissionManager.Show(MissionData data)
//      MissionManager.ShowRaw(title, description, autoDuration)
//      MissionManager.Hide()
// =========================================================

/// <summary>
/// Singleton MonoBehaviour that manages the mission-objective HUD panel.
/// Place one instance in the scene alongside the <see cref="MissionPanel"/>
/// component. All interaction is through the static methods so that any
/// gameplay script can trigger mission messages without a direct reference.
/// </summary>
public class MissionManager : MonoBehaviour
{
    // =========================================================
    //  Singleton
    // =========================================================

    /// <summary>The single active instance of <see cref="MissionManager"/>.</summary>
    public static MissionManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MISSION MANAGER] Duplicate instance detected — destroying this GameObject.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("[MISSION MANAGER] Instance initialised.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Debug.Log("[MISSION MANAGER] Instance cleared.");
        }
    }

    // =========================================================
    //  Inspector Fields
    // =========================================================

    [Header("Panel")]

    [Tooltip("Reference to the MissionPanel component that owns the UI elements.")]
    [SerializeField] private MissionPanel missionPanel;

    // =========================================================
    //  Static API — Show (from ScriptableObject)
    // =========================================================

    /// <summary>
    /// Populates and shows the mission panel using a <see cref="MissionData"/> asset.
    /// </summary>
    /// <param name="data">The mission data asset to display.</param>
    public static bool HasMissionPanel => ResolveMissionPanel() != null;

    public static void Show(MissionData data, Action onAcknowledge = null)
    {
        if (data == null)
        {
            Debug.LogWarning("[MISSION MANAGER] Show() called with a null MissionData asset — ignoring.");
            return;
        }

        MissionPanel panel = ResolveMissionPanel();
        if (panel == null)
        {
            Debug.LogWarning("[MISSION MANAGER] No MissionPanel exists in the loaded scene.");
            return;
        }

        panel.Show(
            data.title,
            data.description,
            data.icon,
            data.autoDuration,
            data.titleColor,
            data.descriptionColor,
            onAcknowledge
        );
    }

    // =========================================================
    //  Static API — ShowRaw (inline strings)
    // =========================================================

    /// <summary>
    /// Populates and shows the mission panel with raw strings.
    /// Uses default white / light-grey colours.
    /// </summary>
    /// <param name="title">Heading text.</param>
    /// <param name="description">Body text.</param>
    /// <param name="autoDuration">Seconds before auto-hide; 0 = persistent.</param>
    public static void ShowRaw(string title, string description, float autoDuration = 0f, Action onAcknowledge = null)
    {
        MissionPanel panel = ResolveMissionPanel();
        if (panel == null)
        {
            Debug.LogWarning("[MISSION MANAGER] No MissionPanel exists in the loaded scene.");
            return;
        }

        panel.Show(
            title,
            description,
            null,
            autoDuration,
            Color.white,
            new Color(0.85f, 0.85f, 0.85f, 1f),
            onAcknowledge
        );
    }

    // =========================================================
    //  Static API — Hide
    // =========================================================

    /// <summary>
    /// Hides the mission panel immediately (or triggers its hide animation).
    /// </summary>
    public static void Hide()
    {
        MissionPanel panel = ResolveMissionPanel();
        if (panel != null)
            panel.Hide();
    }

    private static MissionPanel ResolveMissionPanel()
    {
        if (Instance != null && Instance.missionPanel != null)
            return Instance.missionPanel;

        return UnityEngine.Object.FindFirstObjectByType<MissionPanel>(FindObjectsInactive.Include);
    }
}
