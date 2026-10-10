using UnityEngine;

/// <summary>
/// Procedural cube used for glowing marker visuals:
/// - expanding ping rings
/// - dungeon entry zone markers
///
/// Supports:
/// - movement using velocity
/// - pulsing scale
/// - automatic lifetime destruction
///
/// Generated cubes never carry a collider, so players can walk
/// straight through them.
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

        // Generated cubes must never block players walking past.
        Collider dotCollider =
            meshInstance.GetComponent<Collider>();

        if (dotCollider != null)
        {
            Destroy(dotCollider);
        }

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