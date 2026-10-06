using UnityEngine;

/// <summary>
/// Procedural cube used for glowing marker visuals:
/// - ghost walk-path dots
/// - soldier guide beacon column
/// - expanding ping rings
/// - dungeon entry zone markers
///
/// Supports:
/// - movement using velocity
/// - pulsing scale
/// - automatic lifetime destruction
/// - optional destruction when the SoldierGuide walks through it
///
/// Soldier-pass detection uses horizontal/XZ distance only,
/// because soldier path dots are intentionally positioned above
/// the ground.
/// </summary>
public class GhostDot : MonoBehaviour
{
    // =========================================================
    // MOTION
    // =========================================================

    [Header("Motion")]

    [Tooltip("World units per second. Zero keeps the dot in place.")]
    public Vector3 velocity = Vector3.zero;


    // =========================================================
    // LIFE
    // =========================================================

    [Header("Life")]

    [Tooltip("Seconds before the dot destroys itself. 0 = lives forever.")]
    public float lifeSeconds = 0f;


    // =========================================================
    // SOLDIER DETECTION
    // =========================================================

    [Header("Soldier Detection")]

    [Tooltip(
        "If enabled, this dot destroys itself when the SoldierGuide " +
        "gets close enough."
    )]
    public bool destroyWhenSoldierPasses = false;

    [Tooltip(
        "Horizontal distance from the dot at which the SoldierGuide " +
        "is considered to have walked through it."
    )]
    public float soldierPassDistance = 0.8f;

    [Tooltip(
        "Automatically search for the SoldierGuide if no direct " +
        "reference was assigned."
    )]
    public bool autoFindSoldier = true;

    [Tooltip("Optional direct reference to the SoldierGuide root.")]
    public Transform soldierTransform;


    // =========================================================
    // COLOR
    // =========================================================

    [Header("Color")]

    [Tooltip("Color of the generated marker.")]
    public Color dotColor =
        new Color(
            0f,
            1f,
            0.15f,
            1f
        );


    // =========================================================
    // PULSE
    // =========================================================

    [Header("Pulse")]

    [Tooltip("How fast the dot pulses, in cycles per second.")]
    public float pulseSpeed = 3f;

    [Tooltip("How much the scale swells. 0 = static size.")]
    [Range(0f, 1f)]
    public float pulseAmount = 0.35f;

    [Tooltip("Base local scale applied to the dot.")]
    public float baseScale = 0.4f;

    [Tooltip(
        "Offsets the pulse phase so dots do not all pulse together."
    )]
    public float phaseOffset = 0f;


    // =========================================================
    // INTERNAL
    // =========================================================

    private float age;

    private GameObject meshInstance;

    private MeshRenderer meshRenderer;

    private bool initialized;

    private bool soldierSearchAttempted;


    // =========================================================
    // CONFIGURE
    // =========================================================

    /// <summary>
    /// Existing generic configuration method.
    ///
    /// Kept compatible with the rest of NetworkGameManager.
    /// </summary>
    public void Configure(
        Vector3 dotVelocity,
        float life,
        float pulseCyclesPerSecond,
        float swell,
        float scale,
        float phase = 0f)
    {
        velocity = dotVelocity;

        lifeSeconds = life;

        pulseSpeed = pulseCyclesPerSecond;

        pulseAmount = swell;

        baseScale = scale;

        phaseOffset = phase;
    }


    // =========================================================
    // SOLDIER PASS CONFIGURATION
    // =========================================================

    /// <summary>
    /// Enables destruction when the soldier reaches this dot.
    /// </summary>
    public void ConfigureSoldierPass(
        Transform soldier,
        float passDistance = 0.8f)
    {
        soldierTransform = soldier;

        soldierPassDistance =
            Mathf.Max(
                0.05f,
                passDistance
            );

        destroyWhenSoldierPasses = true;

        soldierSearchAttempted = true;
    }


    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        // Create the visible cube.
        meshInstance =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        if (meshInstance == null)
            return;

        // Put the generated cube at the same position
        // as this GhostDot object.
        meshInstance.transform.position =
            transform.position;

        meshInstance.transform.rotation =
            transform.rotation;

        // Get renderer.
        meshRenderer =
            meshInstance.GetComponent<MeshRenderer>();

        // Apply color.
        if (meshRenderer != null)
        {
            Material material =
                meshRenderer.material;

            if (material != null)
            {
                material.color =
                    dotColor;
            }
        }

        UpdateVisual();
    }


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (!destroyWhenSoldierPasses)
            return;

        // If a soldier was already assigned,
        // there is nothing else to search for.
        if (soldierTransform != null)
        {
            soldierSearchAttempted = true;
            return;
        }

        // Otherwise try to find the SoldierGuide automatically.
        if (autoFindSoldier)
        {
            FindSoldier();
        }
    }


    // =========================================================
    // FIND SOLDIER
    // =========================================================

    private void FindSoldier()
    {
        if (soldierTransform != null)
            return;

        SoldierGuideController controller =
            FindFirstObjectByType<SoldierGuideController>(
                FindObjectsInactive.Include
            );

        if (controller != null)
        {
            soldierTransform =
                controller.transform;

            soldierSearchAttempted = true;

            return;
        }

        // Fallback search by GameObject name.
        GameObject soldier =
            GameObject.Find(
                "SoldierGuide"
            );

        if (soldier != null)
        {
            soldierTransform =
                soldier.transform;

            soldierSearchAttempted = true;

            return;
        }

        soldierSearchAttempted = true;
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!initialized)
            initialized = true;

        float deltaTime =
            Time.deltaTime;

        age += deltaTime;


        // -----------------------------------------------------
        // MOVEMENT
        // -----------------------------------------------------

        if (velocity != Vector3.zero)
        {
            transform.position +=
                velocity *
                deltaTime;
        }


        // -----------------------------------------------------
        // SOLDIER PASS DETECTION
        // -----------------------------------------------------

        if (destroyWhenSoldierPasses)
        {
            // Try to find soldier if we don't have one yet.
            if (soldierTransform == null &&
                autoFindSoldier &&
                !soldierSearchAttempted)
            {
                FindSoldier();
            }

            if (soldierTransform != null)
            {
                // IMPORTANT:
                // Ignore Y because the path dots are elevated
                // above the ground.
                Vector3 dotPosition =
                    transform.position;

                Vector3 soldierPosition =
                    soldierTransform.position;

                dotPosition.y = 0f;

                soldierPosition.y = 0f;

                float distance =
                    Vector3.Distance(
                        dotPosition,
                        soldierPosition
                    );

                if (distance <= soldierPassDistance)
                {
                    Destroy(gameObject);

                    return;
                }
            }
        }


        // -----------------------------------------------------
        // LIFE
        // -----------------------------------------------------

        if (lifeSeconds > 0f &&
            age >= lifeSeconds)
        {
            Destroy(gameObject);

            return;
        }


        // -----------------------------------------------------
        // VISUAL
        // -----------------------------------------------------

        UpdateVisual();
    }


    // =========================================================
    // VISUAL
    // =========================================================

    private void UpdateVisual()
    {
        if (meshInstance == null)
            return;


        // Follow the GhostDot object.
        meshInstance.transform.position =
            transform.position;

        meshInstance.transform.rotation =
            transform.rotation;


        // Calculate pulse.
        float wave =
            Mathf.Sin(
                (
                    age *
                    pulseSpeed +
                    phaseOffset
                ) *
                Mathf.PI *
                2f
            );


        float scale =
            baseScale *
            (
                1f +
                wave *
                pulseAmount
            );


        meshInstance.transform.localScale =
            new Vector3(
                scale,
                scale,
                scale
            );


        // Keep color synchronized.
        if (meshRenderer != null)
        {
            Material material =
                meshRenderer.material;

            if (material != null)
            {
                material.color =
                    dotColor;
            }
        }
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (meshInstance != null)
        {
            Destroy(meshInstance);

            meshInstance = null;
        }
    }
}