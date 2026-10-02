using UnityEngine;
using UnityEngine.Serialization;

public class MenuCamera : MonoBehaviour
{
    [Header("Movement")]
    [FormerlySerializedAs("panSpeed")]
    [SerializeField] private float rotationSpeed = 0.15f;

    [Header("Rotation")]
    [SerializeField] private float rotationAmount = 2f;

    [Header("Horror Shake")]
    [SerializeField] private float shakeAmount = 0.025f;
    [SerializeField] private float shakeSpeed = 1.5f;
    [SerializeField] private float rotationShakeAmount = 0.4f;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void Update()
    {
        float time = Time.time;
        float yawSway = Mathf.Sin(time * rotationSpeed);

        // =====================================
        // SUBTLE HORROR SHAKE
        // =====================================

        float noiseX = Mathf.PerlinNoise(
            time * shakeSpeed,
            0f
        );

        float noiseY = Mathf.PerlinNoise(
            0f,
            time * shakeSpeed
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

        Vector3 shakeOffset = new Vector3(
            shakeX,
            shakeY,
            0f
        );

        transform.position =
            startPosition +
            shakeOffset;

        // =====================================
        // CAMERA ROTATION
        // =====================================

        float baseRotation =
            yawSway * rotationAmount;

        // Horror shake rotation
        float shakeRotation =
            (Mathf.PerlinNoise(
                time * shakeSpeed,
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