using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class PuzzleModeController : NetworkBehaviour
{
    // =========================================================
    // PLAYER COMPONENTS
    // =========================================================

    [Header("Player Components")]

    [SerializeField]
    private PlayerController playerController;

    [SerializeField]
    private PlayerInteractor playerInteractor;

    [SerializeField]
    private CameraController cameraController;

    [Header("Footsteps")]
    [SerializeField]
    private AudioSource footstepsAudioSource;

    // =========================================================
    // CAMERA
    // =========================================================

    [Header("Camera")]

    [SerializeField]
    private Transform playerCamera;

    // =========================================================
    // PUZZLE UI
    // =========================================================

    [Header("Puzzle UI")]

    [SerializeField]
    private TMP_Text puzzleProgressText;

    [SerializeField]
    private TMP_Text puzzleTimerText;

    // =========================================================
    // PUZZLE INTRO / INSTRUCTIONS
    // =========================================================

    [Header("Puzzle Instructions")]

    [Tooltip("The panel containing the Lost Map parchment.")]
    [SerializeField]
    private GameObject puzzleIntroPanel;

    [Tooltip("The BEGIN button inside the intro panel.")]
    [SerializeField]
    private Button beginButton;

    // =========================================================
    // STATE
    // =========================================================

    private bool isInPuzzleMode;

    private bool isShowingPuzzleIntro;

    // Becomes true after BEGIN is clicked once.
    // After this, pressing E opens the puzzle directly.
    private bool hasSeenPuzzleIntro;

    private Vector3 originalCameraLocalPosition;

    private Quaternion originalCameraLocalRotation;

    private bool playerControllerWasEnabled;

    private bool playerInteractorWasEnabled;

    private bool cameraControllerWasEnabled;

    private bool footstepsWasMuted;

    private PuzzleTableController activePuzzleTable;

    // =========================================================
    // PENDING PUZZLE DATA
    // =========================================================

    private Transform pendingPuzzleCameraPoint;

    private PuzzleTableController pendingPuzzleTable;

    private PlayerInventory pendingInventory;

    private Coroutine cursorRelockRoutine;

    public bool IsInPuzzleMode =>
        isInPuzzleMode;

    public bool IsShowingPuzzleIntro =>
        isShowingPuzzleIntro;

    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        // Only the local player controls their puzzle UI.
        if (Object != null &&
            !Object.HasInputAuthority)
        {
            return;
        }

        // Intro starts hidden.
        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(false);
        }

        // Connect BEGIN button automatically.
        if (beginButton != null)
        {
            beginButton.onClick.RemoveListener(
                BeginPuzzleFromIntro
            );

            beginButton.onClick.AddListener(
                BeginPuzzleFromIntro
            );
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (beginButton != null)
        {
            beginButton.onClick.RemoveListener(
                BeginPuzzleFromIntro
            );
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        // -----------------------------------------------------
        // ESC WHILE READING INSTRUCTIONS
        // -----------------------------------------------------

        if (isShowingPuzzleIntro)
        {
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ClosePuzzleIntro();
            }

            return;
        }

        // -----------------------------------------------------
        // ESC WHILE ASSEMBLING
        // -----------------------------------------------------

        if (!isInPuzzleMode)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ExitPuzzleMode();
        }
    }

    // =========================================================
    // PLAYER PRESSES E
    // =========================================================

    public void EnterPuzzleMode(
        Transform puzzleCameraPoint,
        PuzzleTableController puzzleTable,
        PlayerInventory inventory)
    {
        if (isInPuzzleMode ||
            isShowingPuzzleIntro)
        {
            return;
        }

        if (Object != null &&
            !Object.HasInputAuthority)
        {
            return;
        }

        // -----------------------------------------------------
        // VALIDATION
        // -----------------------------------------------------

        if (puzzleCameraPoint == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] PuzzleCameraPoint is missing."
            );

            return;
        }

        if (puzzleTable == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] PuzzleTableController is missing."
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

        // -----------------------------------------------------
        // STORE PUZZLE DATA
        // -----------------------------------------------------

        pendingPuzzleCameraPoint =
            puzzleCameraPoint;

        pendingPuzzleTable =
            puzzleTable;

        pendingInventory =
            inventory;

        // Save normal player states.
        SavePlayerControlStates();

        // Stop player movement/look and silence footsteps.
        DisablePlayerControls();

        // Cancel any pending cursor relock.
        if (cursorRelockRoutine != null)
        {
            StopCoroutine(
                cursorRelockRoutine
            );

            cursorRelockRoutine = null;
        }

        // =====================================================
        // INTRO WAS ALREADY COMPLETED
        // =====================================================

        if (hasSeenPuzzleIntro)
        {
            Debug.Log(
                "[PUZZLE MODE] Intro already completed. " +
                "Opening puzzle directly."
            );

            StartActualPuzzle();

            return;
        }

        // =====================================================
        // FIRST TIME - SHOW INSTRUCTIONS
        // =====================================================

        isShowingPuzzleIntro =
            true;

        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "[PUZZLE MODE] Puzzle Intro Panel is missing. " +
                "Starting puzzle directly."
            );

            hasSeenPuzzleIntro =
                true;

            isShowingPuzzleIntro =
                false;

            StartActualPuzzle();

            return;
        }

        // Cursor must be available for BEGIN.
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible =
            true;

        Debug.Log(
            "[PUZZLE MODE] Puzzle instructions opened."
        );
    }

    // =========================================================
    // BEGIN BUTTON
    // =========================================================

    public void BeginPuzzleFromIntro()
    {
        if (!isShowingPuzzleIntro)
            return;

        // The player has accepted/read the intro.
        hasSeenPuzzleIntro =
            true;

        // Hide parchment.
        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(false);
        }

        isShowingPuzzleIntro =
            false;

        Debug.Log(
            "[PUZZLE MODE] BEGIN clicked. " +
            "Intro will not appear again."
        );

        StartActualPuzzle();
    }

    // =========================================================
    // START ACTUAL PUZZLE
    // =========================================================

    private void StartActualPuzzle()
    {
        if (pendingPuzzleCameraPoint == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Pending PuzzleCameraPoint is missing."
            );

            CancelPuzzleStart();

            return;
        }

        if (pendingPuzzleTable == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Pending PuzzleTableController is missing."
            );

            CancelPuzzleStart();

            return;
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Player Camera is missing."
            );

            CancelPuzzleStart();

            return;
        }

        // -----------------------------------------------------
        // SAVE NORMAL CAMERA
        // -----------------------------------------------------

        originalCameraLocalPosition =
            playerCamera.localPosition;

        originalCameraLocalRotation =
            playerCamera.localRotation;

        // -----------------------------------------------------
        // MOVE CAMERA TO PUZZLE
        // -----------------------------------------------------

        playerCamera.SetPositionAndRotation(
            pendingPuzzleCameraPoint.position,
            pendingPuzzleCameraPoint.rotation
        );

        activePuzzleTable =
            pendingPuzzleTable;

        Camera cameraComponent =
            playerCamera.GetComponent<Camera>();

        if (cameraComponent == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Main Camera has no Camera component."
            );

            CancelPuzzleStart();

            return;
        }

        // =====================================================
        // START PUZZLE
        //
        // PuzzleTableController handles:
        // - spawning pieces
        // - progress UI
        // - timer
        // - timer SFX
        // - world-space timer
        // - saved puzzle progress
        // =====================================================

        activePuzzleTable.BeginPuzzle(
            pendingInventory,
            cameraComponent,
            puzzleProgressText,
            puzzleTimerText
        );

        // Mouse remains unlocked so pieces can be dragged.
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible =
            true;

        isInPuzzleMode =
            true;

        Debug.Log(
            "[PUZZLE MODE] Puzzle started."
        );
    }

    // =========================================================
    // CANCEL FAILED PUZZLE START
    // =========================================================

    private void CancelPuzzleStart()
    {
        isShowingPuzzleIntro =
            false;

        isInPuzzleMode =
            false;

        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(false);
        }

        RestorePlayerControls();

        ClearPendingPuzzle();

        if (cursorRelockRoutine != null)
        {
            StopCoroutine(
                cursorRelockRoutine
            );
        }

        cursorRelockRoutine =
            StartCoroutine(
                RelockCursorNextFrame()
            );
    }

    // =========================================================
    // ESC WHILE READING INTRO
    // =========================================================

    private void ClosePuzzleIntro()
    {
        if (!isShowingPuzzleIntro)
            return;

        isShowingPuzzleIntro =
            false;

        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(false);
        }

        // IMPORTANT:
        //
        // We DO NOT set hasSeenPuzzleIntro here.
        //
        // If the player closes the instructions
        // without pressing BEGIN, pressing E again
        // should show the instructions again.

        ClearPendingPuzzle();

        RestorePlayerControls();

        if (cursorRelockRoutine != null)
        {
            StopCoroutine(
                cursorRelockRoutine
            );
        }

        cursorRelockRoutine =
            StartCoroutine(
                RelockCursorNextFrame()
            );

        Debug.Log(
            "[PUZZLE MODE] Instructions closed without BEGIN."
        );
    }

    // =========================================================
    // EXIT ACTUAL PUZZLE
    // =========================================================

    public void ExitPuzzleMode()
    {
        if (!isInPuzzleMode)
            return;

        isInPuzzleMode =
            false;

        // -----------------------------------------------------
        // CLOSE PUZZLE VIEW
        // -----------------------------------------------------

        if (activePuzzleTable != null)
        {
            activePuzzleTable.EndPuzzle();

            activePuzzleTable =
                null;
        }

        // -----------------------------------------------------
        // RESTORE NORMAL CAMERA
        // -----------------------------------------------------

        if (playerCamera != null)
        {
            playerCamera.localPosition =
                originalCameraLocalPosition;

            playerCamera.localRotation =
                originalCameraLocalRotation;
        }

        RestorePlayerControls();

        ClearPendingPuzzle();

        // Wait before locking because Unity also
        // processes Escape for the Editor cursor.
        if (cursorRelockRoutine != null)
        {
            StopCoroutine(
                cursorRelockRoutine
            );
        }

        cursorRelockRoutine =
            StartCoroutine(
                RelockCursorNextFrame()
            );

        Debug.Log(
            "[PUZZLE MODE] Exited puzzle mode."
        );
    }

    // =========================================================
    // SAVE PLAYER STATES
    // =========================================================

    private void SavePlayerControlStates()
    {
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

        // Remember the previous footstep mute state so we
        // can restore it exactly after leaving the puzzle.
        if (footstepsAudioSource != null)
        {
            footstepsWasMuted =
                footstepsAudioSource.mute;
        }
    }

    // =========================================================
    // DISABLE PLAYER CONTROLS
    // =========================================================

    private void DisablePlayerControls()
    {
        // Stop the current footstep immediately and keep the
        // source muted for the whole intro/puzzle state.
        // This prevents the footstep system from replaying audio
        // if E is pressed while the player is still walking.
        if (footstepsAudioSource != null)
        {
            footstepsAudioSource.Stop();
            footstepsAudioSource.mute = true;
        }

        if (playerController != null)
        {
            playerController.enabled =
                false;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled =
                false;
        }

        if (cameraController != null)
        {
            cameraController.enabled =
                false;
        }
    }

    // =========================================================
    // RESTORE PLAYER CONTROLS
    // =========================================================

    private void RestorePlayerControls()
    {
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

        // Stop any stale footstep and restore the mute state
        // that existed before entering the puzzle.
        if (footstepsAudioSource != null)
        {
            footstepsAudioSource.Stop();
            footstepsAudioSource.mute =
                footstepsWasMuted;
        }
    }

    // =========================================================
    // CLEAR PENDING PUZZLE DATA
    // =========================================================

    private void ClearPendingPuzzle()
    {
        pendingPuzzleCameraPoint =
            null;

        pendingPuzzleTable =
            null;

        pendingInventory =
            null;
    }

    // =========================================================
    // CURSOR
    // =========================================================

    private IEnumerator RelockCursorNextFrame()
    {
        yield return null;

        yield return
            new WaitForEndOfFrame();

        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible =
            false;

        cursorRelockRoutine =
            null;

        Debug.Log(
            "[PUZZLE MODE] Mouse locked again."
        );
    }
}