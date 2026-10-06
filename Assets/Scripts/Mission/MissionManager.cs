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
    public static void Show(MissionData data)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[MISSION MANAGER] Show() called but no Instance exists in the scene.");
            return;
        }

        if (data == null)
        {
            Debug.LogWarning("[MISSION MANAGER] Show() called with a null MissionData asset — ignoring.");
            return;
        }

        if (Instance.missionPanel == null)
        {
            Debug.LogWarning("[MISSION MANAGER] missionPanel reference is not assigned.");
            return;
        }

        Instance.missionPanel.Show(
            data.title,
            data.description,
            data.icon,
            data.autoDuration,
            data.titleColor,
            data.descriptionColor
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
    public static void ShowRaw(string title, string description, float autoDuration = 0f)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[MISSION MANAGER] ShowRaw() called but no Instance exists in the scene.");
            return;
        }

        if (Instance.missionPanel == null)
        {
            Debug.LogWarning("[MISSION MANAGER] missionPanel reference is not assigned.");
            return;
        }

        Instance.missionPanel.Show(
            title,
            description,
            null,
            autoDuration,
            Color.white,
            new Color(0.85f, 0.85f, 0.85f, 1f)
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
        if (Instance == null)
        {
            Debug.LogWarning("[MISSION MANAGER] Hide() called but no Instance exists in the scene.");
            return;
        }

        if (Instance.missionPanel == null)
        {
            Debug.LogWarning("[MISSION MANAGER] missionPanel reference is not assigned.");
            return;
        }

        Instance.missionPanel.Hide();
    }
}
