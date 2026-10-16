
using UnityEngine;

public class CompassUI : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform playerCamera;
    public RectTransform compassNeedle;

    [Header("Compass Calibration")]
    [Tooltip("Adjust if the needle does not point north correctly.")]
    public float needleOffset = 0f;

    [Header("Realistic Needle Movement")]
    [Min(0f)] public float springStrength = 25f;
    [Min(0f)] public float damping = 7f;
    [Min(0f)] public float maxAngularSpeed = 540f;

    [Header("Walking and Running Shake")]
    public bool enableShake = true;

    [Range(0f, 10f)]
    public float shakeAmount = 3.5f;

    [Min(0f)] public float shakeSpeed = 10f;

    [Tooltip("Speed at which walking shake reaches full strength.")]
    public float movementSpeedForMaxShake = 2f;

    [Header("Turning Shake")]
    public bool shakeWhenTurning = true;

    [Range(0f, 5f)]
    public float turningShakeAmount = 1.5f;

    [Header("Shake Timing")]
    [Tooltip("How quickly shaking begins.")]
    public float shakeFadeInSpeed = 10f;

    [Tooltip("How slowly shaking fades after stopping.")]
    public float shakeFadeOutSpeed = 3f;

    [Tooltip("Smoothness of the needle vibration.")]
    public float shakeSmoothness = 12f;

    private float currentAngle;
    private float angularVelocity;

    private float currentShake;
    private float shakeIntensity;
    private float previousHeading;
    private float shakeTime;

    private Vector3 previousPosition;
    private bool initialized = false;

    void Start()
    {
        FindReferences();
    }

    void FindReferences()
    {
        // Automatically find the player.
        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }

        // Automatically find the main camera.
        if (playerCamera == null)
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
                playerCamera = mainCamera.transform;
        }

        if (player == null ||
            playerCamera == null ||
            compassNeedle == null)
        {
            initialized = false;
            return;
        }

        previousPosition = player.position;

        currentAngle = GetTargetAngle();
        previousHeading = currentAngle;

        angularVelocity = 0f;
        currentShake = 0f;
        shakeIntensity = 0f;
        shakeTime = 0f;

        compassNeedle.localRotation =
            Quaternion.Euler(0f, 0f, currentAngle);

        initialized = true;
    }

    void LateUpdate()
    {
        // Wait until player and camera exist.
        if (!initialized ||
            player == null ||
            playerCamera == null)
        {
            initialized = false;
            FindReferences();
            return;
        }

        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        if (dt <= 0f)
            return;

        // =====================================
        // 1. CAMERA DIRECTION
        // =====================================

        float targetAngle = GetTargetAngle();

        // =====================================
        // 2. MAGNETIC NEEDLE SPRING
        // =====================================

        float angleDifference =
            Mathf.DeltaAngle(currentAngle, targetAngle);

        float acceleration =
            angleDifference * springStrength
            - angularVelocity * damping;

        angularVelocity += acceleration * dt;

        angularVelocity = Mathf.Clamp(
            angularVelocity,
            -maxAngularSpeed,
            maxAngularSpeed
        );

        currentAngle += angularVelocity * dt;

        // Keep rotation within 0-360 degrees.
        currentAngle = Mathf.Repeat(currentAngle, 360f);

        // =====================================
        // 3. DETECT PLAYER MOVEMENT
        // =====================================

        Vector3 movement =
            player.position - previousPosition;

        // Ignore vertical movement.
        movement.y = 0f;

        float movementSpeed =
            movement.magnitude / dt;

        previousPosition = player.position;

        float walkingFactor = Mathf.Clamp01(
            movementSpeed /
            Mathf.Max(0.01f, movementSpeedForMaxShake)
        );

        // =====================================
        // 4. DETECT CAMERA TURNING
        // =====================================

        float headingChange = Mathf.DeltaAngle(
            previousHeading,
            targetAngle
        );

        float turningSpeed =
            Mathf.Abs(headingChange) / dt;

        previousHeading = targetAngle;

        float turningFactor = 0f;

        if (shakeWhenTurning)
        {
            turningFactor = Mathf.Clamp01(
                turningSpeed / 120f
            );
        }

        // =====================================
        // 5. CALCULATE SHAKE INTENSITY
        // =====================================

        float targetIntensity = 0f;

        if (enableShake)
        {
            targetIntensity = Mathf.Max(
                walkingFactor,
                turningFactor
            );
        }

        // Shake starts quickly and fades slowly.
        float fadeSpeed =
            targetIntensity > shakeIntensity
            ? shakeFadeInSpeed
            : shakeFadeOutSpeed;

        shakeIntensity = Mathf.Lerp(
            shakeIntensity,
            targetIntensity,
            1f - Mathf.Exp(-fadeSpeed * dt)
        );

        // =====================================
        // 6. REALISTIC NEEDLE VIBRATION
        // =====================================

        float targetShake = 0f;

        if (enableShake)
        {
            shakeTime += dt * shakeSpeed;

            // Main gentle wobble.
            float primaryWobble =
                Mathf.Sin(shakeTime) * shakeAmount;

            // Secondary irregular wobble.
            float secondaryWobble =
                Mathf.Sin(shakeTime * 1.73f) *
                shakeAmount * 0.3f;

            // Extra movement when turning.
            float turningWobble =
                Mathf.Sin(shakeTime * 1.2f) *
                turningShakeAmount *
                turningFactor;

            targetShake =
                (primaryWobble + secondaryWobble) *
                shakeIntensity +
                turningWobble;
        }

        // Smooth vibration.
        currentShake = Mathf.Lerp(
            currentShake,
            targetShake,
            1f - Mathf.Exp(-shakeSmoothness * dt)
        );

        // =====================================
        // 7. APPLY FINAL NEEDLE ROTATION
        // =====================================

        compassNeedle.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                currentAngle + currentShake
            );
    }

    float GetTargetAngle()
    {
        // Get camera direction on horizontal plane.
        Vector3 forward = Vector3.ProjectOnPlane(
            playerCamera.forward,
            Vector3.up
        );

        if (forward.sqrMagnitude < 0.0001f)
            return currentAngle;

        forward.Normalize();

        // Unity world directions:
        // +Z = North
        // +X = East
        // -Z = South
        // -X = West

        float heading = Mathf.Atan2(
            forward.x,
            forward.z
        ) * Mathf.Rad2Deg;

        return heading + needleOffset;
    }
}
