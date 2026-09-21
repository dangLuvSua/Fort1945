using UnityEngine;

public class MenuCamera : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float panAmount = 2f;
    [SerializeField] private float panSpeed = 0.15f;

    [Header("Rotation")]
    [SerializeField] private float rotationAmount = 2f;

    [Header("Horror Shake")]
    [SerializeField] private float shakeAmount = 0.025f;
    [SerializeField] private float shakeSpeed = 1.5f;
    [SerializeField] private float rotationShakeAmount = 0.4f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 startRight;

    private void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        // Save the ORIGINAL right direction
        startRight = startRotation * Vector3.right;
    }

    private void Update()
    {
        // =====================================
        // SLOW CINEMATIC PAN
        // =====================================

        // Moves:
        // Original → Right → Original → Right...
        float pan = Mathf.PingPong(
            Time.time * panSpeed,
            panAmount
        );

        // =====================================
        // SUBTLE HORROR SHAKE
        // =====================================

        float noiseX = Mathf.PerlinNoise(
            Time.time * shakeSpeed,
            0f
        );

        float noiseY = Mathf.PerlinNoise(
            0f,
            Time.time * shakeSpeed
        );

        float shakeX =
            (noiseX - 0.5f) *
            2f *
            shakeAmount;

        float shakeY =
            (noiseY - 0.5f) *
            2f *
            shakeAmount;

        // =====================================
        // CAMERA POSITION
        // =====================================

        Vector3 panOffset = startRight * pan;

        Vector3 shakeOffset = new Vector3(
            shakeX,
            shakeY,
            0f
        );

        transform.position =
            startPosition +
            panOffset +
            shakeOffset;

        // =====================================
        // CAMERA ROTATION
        // =====================================

        // Convert pan into a -1 to +1 value.
        // This makes the camera rotate slightly
        // toward the direction of movement.
        float panDirection = 0f;

        if (panAmount > 0f)
        {
            panDirection =
                (pan / panAmount) * 2f - 1f;
        }

        float baseRotation =
            panDirection * rotationAmount;

        // Horror shake rotation
        float shakeRotation =
            (Mathf.PerlinNoise(
                Time.time * shakeSpeed,
                10f
            ) - 0.5f)
            * 2f
            * rotationShakeAmount;

        transform.rotation =
            startRotation *
            Quaternion.Euler(
                0f,
                baseRotation + shakeRotation,
                0f
            );
    }
}