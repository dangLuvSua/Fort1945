using UnityEngine;

public class MenuCamera : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float panAmount = 2f;
    [SerializeField] private float panSpeed = 0.15f;

    [Header("Rotation")]
    [SerializeField] private float rotationAmount = 2f;

    private Vector3 startPosition;
    private Quaternion startRotation;

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Update()
    {
        // Smooth left and right movement
        float movement = Mathf.Sin(Time.time * panSpeed) * panAmount;

        transform.position = startPosition + transform.right * movement;

        // Slightly rotate the camera as it moves
        float rotation = Mathf.Sin(Time.time * panSpeed) * rotationAmount;

        transform.rotation =
            startRotation * Quaternion.Euler(0f, rotation, 0f);
    }
}