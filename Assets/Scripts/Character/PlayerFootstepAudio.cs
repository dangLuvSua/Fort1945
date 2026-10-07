using UnityEngine;

public class PlayerFootstepAudio : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private AudioSource audioSource;


    // =========================================================
    // SURFACE DETECTION
    // =========================================================

    [Header("Surface Detection")]
    [SerializeField] private float raycastDistance = 1.5f;

    [Tooltip("Layers that can be detected as walkable ground.")]
    [SerializeField] private LayerMask groundLayers;


    // =========================================================
    // TERRAIN SURFACE MAPPING
    // =========================================================

    [Header("Terrain Surface Mapping")]
    [Tooltip("Terrain Layer assets used by your Unity Terrain.")]
    [SerializeField] private TerrainLayer[] terrainLayers;

    [Tooltip("Surface type corresponding to each Terrain Layer above.")]
    [SerializeField] private SurfaceType[] terrainSurfaceTypes;


    // =========================================================
    // FOOTSTEP SOUNDS
    // =========================================================

    [Header("Footstep Sounds")]
    [SerializeField] private AudioClip[] defaultSteps;
    [SerializeField] private AudioClip[] grassSteps;
    [SerializeField] private AudioClip[] dirtSteps;
    [SerializeField] private AudioClip[] brickSteps;
    [SerializeField] private AudioClip[] stoneSteps;
    [SerializeField] private AudioClip[] woodSteps;


    // =========================================================
    // JUMP / LANDING SOUNDS
    // =========================================================

    [Header("Jump & Landing")]
    [Tooltip("Sound played when the player performs a jump.")]
    [SerializeField] private AudioClip jumpSound;

    [Tooltip("Random landing sounds.")]
    [SerializeField] private AudioClip[] landSounds;


    // =========================================================
    // TIMING
    // =========================================================

    [Header("Footstep Timing")]
    [SerializeField] private float walkStepInterval = 0.5f;
    [SerializeField] private float runStepInterval = 0.32f;


    // =========================================================
    // MOVEMENT
    // =========================================================

    [Header("Movement Detection")]
    [SerializeField] private float movementThreshold = 0.1f;

    [Tooltip("Horizontal speed at which running footsteps begin.")]
    [SerializeField] private float runThreshold = 4f;


    // =========================================================
    // AUDIO VARIATION
    // =========================================================

    [Header("Audio Variation")]
    [SerializeField] private float minPitch = 0.95f;
    [SerializeField] private float maxPitch = 1.05f;


    // =========================================================
    // JUMP DETECTION
    // =========================================================

    [Header("Jump Detection")]
    [Tooltip("Minimum upward velocity required to identify a jump.")]
    [SerializeField] private float jumpVelocityThreshold = 0.1f;


    // =========================================================
    // INTERNAL VARIABLES
    // =========================================================

    private float stepTimer;

    private bool wasGrounded;

    private bool jumpSoundPlayed;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        // Automatically find CharacterController if not assigned.
        if (controller == null)
        {
            controller = GetComponentInParent<CharacterController>();
        }

        // Automatically find AudioSource on this object if not assigned.
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }


    private void Start()
    {
        if (controller != null)
        {
            wasGrounded = controller.isGrounded;
        }
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (controller == null || audioSource == null)
            return;

        bool isGrounded = controller.isGrounded;

        Vector3 velocity = controller.velocity;

        // =====================================================
        // JUMP DETECTION
        // =====================================================

        if (!isGrounded)
        {
            DetectJump(velocity);
        }

        // =====================================================
        // LANDING DETECTION
        // =====================================================

        if (isGrounded && !wasGrounded)
        {
            PlayLandingSound();

            // Allow another jump sound after landing.
            jumpSoundPlayed = false;
        }

        // Store current grounded state.
        wasGrounded = isGrounded;


        // =====================================================
        // STOP FOOTSTEPS WHILE AIRBORNE
        // =====================================================

        if (!isGrounded)
        {
            stepTimer = 0f;
            return;
        }


        // =====================================================
        // HORIZONTAL MOVEMENT
        // =====================================================

        Vector3 horizontalVelocity = velocity;

        horizontalVelocity.y = 0f;

        float speed = horizontalVelocity.magnitude;


        // =====================================================
        // PLAYER IS NOT MOVING
        // =====================================================

        if (speed <= movementThreshold)
        {
            stepTimer = 0f;
            return;
        }


        // =====================================================
        // WALK / RUN
        // =====================================================

        bool isRunning = speed >= runThreshold;

        float interval = isRunning
            ? runStepInterval
            : walkStepInterval;


        // =====================================================
        // FOOTSTEP TIMER
        // =====================================================

        stepTimer += Time.deltaTime;

        if (stepTimer >= interval)
        {
            PlayFootstep();

            stepTimer = 0f;
        }
    }


    // =========================================================
    // JUMP DETECTION
    // =========================================================

    private void DetectJump(Vector3 velocity)
    {
        // Already played the jump sound for this jump.
        if (jumpSoundPlayed)
            return;

        // We only consider an upward velocity a jump.
        if (velocity.y > jumpVelocityThreshold)
        {
            PlayJumpSound();

            jumpSoundPlayed = true;
        }
    }


    // =========================================================
    // PLAY JUMP SOUND
    // =========================================================

    private void PlayJumpSound()
    {
        if (jumpSound == null)
            return;

        PlayClipWithRandomPitch(jumpSound);
    }


    // =========================================================
    // PLAY LANDING SOUND
    // =========================================================

    private void PlayLandingSound()
    {
        if (landSounds == null || landSounds.Length == 0)
            return;

        AudioClip clip = GetRandomValidClip(landSounds);

        if (clip == null)
            return;

        PlayClipWithRandomPitch(clip);
    }


    // =========================================================
    // PLAY FOOTSTEP
    // =========================================================

    private void PlayFootstep()
    {
        SurfaceType surface = DetectSurface();

        AudioClip[] clips = GetClipsForSurface(surface);

        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip = GetRandomValidClip(clips);

        if (clip == null)
            return;

        PlayClipWithRandomPitch(clip);
    }


    // =========================================================
    // PLAY AUDIO
    // =========================================================

    private void PlayClipWithRandomPitch(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);

        audioSource.PlayOneShot(clip);
    }


    // =========================================================
    // GET RANDOM VALID CLIP
    // =========================================================

    private AudioClip GetRandomValidClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        // Try several times to find a non-null clip.
        for (int i = 0; i < 10; i++)
        {
            AudioClip clip = clips[Random.Range(0, clips.Length)];

            if (clip != null)
                return clip;
        }

        // Fallback search.
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return clips[i];
        }

        return null;
    }


    // =========================================================
    // SURFACE DETECTION
    // =========================================================

    private SurfaceType DetectSurface()
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f;

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                raycastDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            return SurfaceType.Default;
        }


        // =====================================================
        // TERRAIN
        // =====================================================

        Terrain terrain = hit.collider.GetComponent<Terrain>();

        if (terrain == null)
        {
            TerrainCollider terrainCollider =
                hit.collider.GetComponent<TerrainCollider>();

            if (terrainCollider != null)
            {
                terrain =
                    terrainCollider.GetComponent<Terrain>();
            }
        }

        if (terrain != null)
        {
            return DetectTerrainSurface(
                terrain,
                hit.point
            );
        }


        // =====================================================
        // POLYSHAPE / MESH SURFACE
        // =====================================================

        SurfaceIdentifier surfaceIdentifier =
            hit.collider.GetComponent<SurfaceIdentifier>();

        if (surfaceIdentifier == null)
        {
            surfaceIdentifier =
                hit.collider.GetComponentInParent<SurfaceIdentifier>();
        }

        if (surfaceIdentifier != null)
        {
            return surfaceIdentifier.SurfaceType;
        }


        // =====================================================
        // DEFAULT
        // =====================================================

        return SurfaceType.Default;
    }


    // =========================================================
    // TERRAIN SURFACE DETECTION
    // =========================================================

    private SurfaceType DetectTerrainSurface(
        Terrain terrain,
        Vector3 worldPosition)
    {
        if (terrain == null || terrain.terrainData == null)
            return SurfaceType.Default;

        TerrainData terrainData = terrain.terrainData;

        if (terrainData.terrainLayers == null ||
            terrainData.terrainLayers.Length == 0)
        {
            return SurfaceType.Default;
        }


        // =====================================================
        // WORLD → TERRAIN LOCAL POSITION
        // =====================================================

        Vector3 terrainPosition =
            worldPosition - terrain.transform.position;


        // =====================================================
        // NORMALIZE POSITION
        // =====================================================

        float normalizedX =
            terrainPosition.x / terrainData.size.x;

        float normalizedZ =
            terrainPosition.z / terrainData.size.z;

        normalizedX = Mathf.Clamp01(normalizedX);
        normalizedZ = Mathf.Clamp01(normalizedZ);


        // =====================================================
        // ALPHAMAP POSITION
        // =====================================================

        int mapX = Mathf.RoundToInt(
            normalizedX *
            (terrainData.alphamapWidth - 1)
        );

        int mapZ = Mathf.RoundToInt(
            normalizedZ *
            (terrainData.alphamapHeight - 1)
        );


        // =====================================================
        // GET TERRAIN LAYER WEIGHTS
        // =====================================================

        float[,,] weights =
            terrainData.GetAlphamaps(
                mapX,
                mapZ,
                1,
                1
            );


        int layerCount =
            terrainData.terrainLayers.Length;


        int strongestLayer = 0;

        float strongestWeight = 0f;


        // =====================================================
        // FIND STRONGEST PAINTED TERRAIN LAYER
        // =====================================================

        for (int i = 0; i < layerCount; i++)
        {
            float weight = weights[0, 0, i];

            if (weight > strongestWeight)
            {
                strongestWeight = weight;
                strongestLayer = i;
            }
        }


        // =====================================================
        // GET ACTUAL TERRAIN LAYER
        // =====================================================

        TerrainLayer detectedLayer =
            terrainData.terrainLayers[strongestLayer];


        // =====================================================
        // MAP TERRAIN LAYER → SURFACE TYPE
        // =====================================================

        if (terrainLayers != null &&
            terrainSurfaceTypes != null)
        {
            int count = Mathf.Min(
                terrainLayers.Length,
                terrainSurfaceTypes.Length
            );

            for (int i = 0; i < count; i++)
            {
                if (terrainLayers[i] == detectedLayer)
                {
                    return terrainSurfaceTypes[i];
                }
            }
        }


        // =====================================================
        // NO MAPPING FOUND
        // =====================================================

        return SurfaceType.Default;
    }


    // =========================================================
    // GET FOOTSTEP CLIPS FOR SURFACE
    // =========================================================

    private AudioClip[] GetClipsForSurface(
        SurfaceType surface)
    {
        switch (surface)
        {
            case SurfaceType.Grass:
                return grassSteps;

            case SurfaceType.Dirt:
                return dirtSteps;

            case SurfaceType.Brick:
                return brickSteps;

            case SurfaceType.Stone:
                return stoneSteps;

            case SurfaceType.Wood:
                return woodSteps;

            case SurfaceType.Default:
            default:
                return defaultSteps;
        }
    }


    // =========================================================
    // OPTIONAL PUBLIC JUMP METHOD
    // =========================================================
    //
    // You can call this from PlayerController later if you
    // want the jump sound to happen at the exact jump input.
    //
    // Example:
    //
    // footstepAudio.PlayJump();
    //
    // =========================================================

    public void PlayJump()
    {
        if (jumpSoundPlayed)
            return;

        PlayJumpSound();

        jumpSoundPlayed = true;
    }
}