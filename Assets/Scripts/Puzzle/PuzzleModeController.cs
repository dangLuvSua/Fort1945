using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PuzzleModeController : NetworkBehaviour
{
    [Header("Player Components")]
    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private PlayerInteractor playerInteractor;

    [SerializeField]
    private CameraController cameraController;

    [Header("Camera")]
    [SerializeField]
    private Transform playerCamera;

    private bool isInPuzzleMode;

    private Vector3 originalCameraLocalPosition;
    private Quaternion originalCameraLocalRotation;

    private bool playerControllerWasEnabled;
    private bool playerInteractorWasEnabled;
    private bool cameraControllerWasEnabled;

    private PuzzleTableController activePuzzleTable;

    private Coroutine cursorRelockRoutine;

    public bool IsInPuzzleMode => isInPuzzleMode;

    private void Update()
    {
        if (!isInPuzzleMode)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ExitPuzzleMode();
        }
    }

    public void EnterPuzzleMode(
        Transform puzzleCameraPoint,
        PuzzleTableController puzzleTable,
        PlayerInventory inventory)
    {
        if (isInPuzzleMode)
            return;

        if (Object != null &&
            !Object.HasInputAuthority)
        {
            return;
        }

        if (puzzleCameraPoint == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] PuzzleCameraPoint is missing."
            );
            return;
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Player Camera is missing."
            );
            return;
        }

        // Stop any pending cursor relock.
        if (cursorRelockRoutine != null)
        {
            StopCoroutine(cursorRelockRoutine);
            cursorRelockRoutine = null;
        }

        // Save normal camera transform.
        originalCameraLocalPosition =
            playerCamera.localPosition;

        originalCameraLocalRotation =
            playerCamera.localRotation;

        // Remember enabled states.
        if (playerController != null)
        {
            playerControllerWasEnabled =
                playerController.enabled;
        }

        if (playerInteractor != null)
        {
            playerInteractorWasEnabled =
                playerInteractor.enabled;
        }

        if (cameraController != null)
        {
            cameraControllerWasEnabled =
                cameraController.enabled;
        }

        // Disable normal controls.
        if (playerController != null)
            playerController.enabled = false;

        if (playerInteractor != null)
            playerInteractor.enabled = false;

        if (cameraController != null)
            cameraController.enabled = false;

        // Move camera to puzzle view.
        playerCamera.SetPositionAndRotation(
            puzzleCameraPoint.position,
            puzzleCameraPoint.rotation
        );

        activePuzzleTable = puzzleTable;

        Camera cameraComponent =
            playerCamera.GetComponent<Camera>();

        if (cameraComponent == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Main Camera does not have " +
                "a Camera component."
            );
        }

        if (activePuzzleTable != null)
        {
            activePuzzleTable.BeginPuzzle(
                inventory,
                cameraComponent
            );
        }
        else
        {
            Debug.LogError(
                "[PUZZLE MODE] PuzzleTableController is missing."
            );
        }

        // Unlock mouse for puzzle.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isInPuzzleMode = true;

        Debug.Log(
            "[PUZZLE MODE] Entered puzzle mode."
        );
    }

    public void ExitPuzzleMode()
    {
        if (!isInPuzzleMode)
            return;

        isInPuzzleMode = false;

        // Close puzzle.
        if (activePuzzleTable != null)
        {
            activePuzzleTable.EndPuzzle();
            activePuzzleTable = null;
        }

        // Return camera.
        if (playerCamera != null)
        {
            playerCamera.localPosition =
                originalCameraLocalPosition;

            playerCamera.localRotation =
                originalCameraLocalRotation;
        }

        // Restore player controls.
        if (playerController != null)
        {
            playerController.enabled =
                playerControllerWasEnabled;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled =
                playerInteractorWasEnabled;
        }

        if (cameraController != null)
        {
            cameraController.enabled =
                cameraControllerWasEnabled;
        }

        // IMPORTANT:
        // Wait until the next frame before locking the cursor.
        // Unity Editor also reacts to Escape.
        if (cursorRelockRoutine != null)
            StopCoroutine(cursorRelockRoutine);

        cursorRelockRoutine =
            StartCoroutine(RelockCursorNextFrame());

        Debug.Log(
            "[PUZZLE MODE] Exited puzzle mode."
        );
    }

    private IEnumerator RelockCursorNextFrame()
    {
        // Let Unity finish processing the Escape key first.
        yield return null;
        yield return new WaitForEndOfFrame();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        cursorRelockRoutine = null;

        Debug.Log(
            "[PUZZLE MODE] Mouse locked again."
        );
    }
}