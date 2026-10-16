using UnityEngine;

public class PuzzleWorldTimerBillboard : MonoBehaviour
{
    [Header("Billboard Settings")]
    [SerializeField]
    private bool keepUpright = true;

    [SerializeField]
    private bool flipFacing = false;

    private Camera targetCamera;

    private void LateUpdate()
    {
        // Find the local player's active Main Camera.
        if (targetCamera == null ||
            !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
            return;

        Vector3 direction =
            transform.position -
            targetCamera.transform.position;

        // Keep the text standing upright instead of
        // tilting when the player looks up/down.
        if (keepUpright)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up
            );

        if (flipFacing)
        {
            targetRotation *=
                Quaternion.Euler(
                    0f,
                    180f,
                    0f
                );
        }

        transform.rotation =
            targetRotation;
    }
}