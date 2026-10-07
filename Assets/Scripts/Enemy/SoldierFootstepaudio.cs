using UnityEngine;

public class SoldierFootstepAudio : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Footstep Sounds")]
    [SerializeField] private AudioClip[] footstepSounds;

    [Header("Movement")]
    [SerializeField] private float stepInterval = 0.5f;
    [SerializeField] private float movementThreshold = 0.05f;

    [Header("Surface Detection")]
    [SerializeField] private float raycastDistance = 2f;
    [SerializeField] private LayerMask groundLayers;

    [Header("Audio Variation")]
    [SerializeField] private float minPitch = 0.95f;
    [SerializeField] private float maxPitch = 1.05f;

    private Transform movementRoot;
    private Vector3 previousPosition;
    private float stepTimer;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // SoldierGuideModel is the child.
        // SoldierGuide is the object actually moving.
        movementRoot = transform.parent;
    }

    private void Start()
    {
        if (movementRoot != null)
        {
            previousPosition = movementRoot.position;
        }
    }

    private void Update()
    {
        if (movementRoot == null || audioSource == null)
            return;

        // -------------------------------------------------
        // GET SOLDIER MOVEMENT
        // -------------------------------------------------

        Vector3 movement =
            movementRoot.position - previousPosition;

        movement.y = 0f;

        float speed =
            movement.magnitude /
            Mathf.Max(Time.deltaTime, 0.0001f);

        previousPosition = movementRoot.position;


        // -------------------------------------------------
        // SOLDIER IS NOT MOVING
        // -------------------------------------------------

        if (speed <= movementThreshold)
        {
            stepTimer = 0f;
            return;
        }


        // -------------------------------------------------
        // CHECK GROUND
        // -------------------------------------------------

        if (!DetectGround())
        {
            stepTimer = 0f;
            return;
        }


        // -------------------------------------------------
        // FOOTSTEP TIMER
        // -------------------------------------------------

        stepTimer += Time.deltaTime;

        if (stepTimer >= stepInterval)
        {
            PlayFootstep();
            stepTimer = 0f;
        }
    }

    private bool DetectGround()
    {
        Vector3 origin =
            movementRoot.position + Vector3.up * 0.3f;

        return Physics.Raycast(
            origin,
            Vector3.down,
            raycastDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    private void PlayFootstep()
    {
        if (footstepSounds == null ||
            footstepSounds.Length == 0)
        {
            return;
        }

        AudioClip clip =
            footstepSounds[
                Random.Range(
                    0,
                    footstepSounds.Length
                )
            ];

        if (clip == null)
            return;

        audioSource.pitch =
            Random.Range(
                minPitch,
                maxPitch
            );

        audioSource.PlayOneShot(clip);
    }
}