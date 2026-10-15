
using UnityEngine;

public class CompassUI : MonoBehaviour
{
    [Header("References")]
    public Transform playerCamera;
    public RectTransform compassBackground;
    public RectTransform compassArrow;

    [Header("Settings")]
    public float rotationSpeed = 10f;

    void Update()
    {
        // Automatically find the camera
        if (playerCamera == null)
        {
            Camera mainCam = Camera.main;

            if (mainCam != null && mainCam.isActiveAndEnabled)
            {
                playerCamera = mainCam.transform;
            }

            return;
        }

        if (compassArrow == null)
            return;

        // Get the camera's horizontal rotation
        float heading = playerCamera.eulerAngles.y;

        // Your arrow image originally points NW (315 degrees).
        // Offset by 45 degrees to align it with North.
        float arrowOffset = -45f;

        // Rotate ONLY the arrow
        Quaternion targetRotation =
            Quaternion.Euler(0, 0, -heading + arrowOffset);

        compassArrow.localRotation =
            Quaternion.Lerp(
                compassArrow.localRotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
    }
}
