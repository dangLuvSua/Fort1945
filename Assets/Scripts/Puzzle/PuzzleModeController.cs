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

    // =========================================================
    // FOOTSTEPS
    // =========================================================

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
    // PUZZLE INTRO
    // =========================================================

    [Header("Puzzle Instructions")]

    [Tooltip("The panel containing the Lost Map parchment.")]
    [SerializeField]
    private GameObject puzzleIntroPanel;

    [Tooltip("The BEGIN button inside the intro panel.")]
    [SerializeField]
    private Button beginButton;

    // =========================================================
    // PUZZLE RESULT
    // =========================================================

    [Header("Puzzle Result")]

    [Tooltip("Panel used for puzzle success and failure.")]
    [SerializeField]
    private GameObject puzzleResultPanel;

    [SerializeField]
    private TMP_Text resultTitleText;

    [SerializeField]
    private TMP_Text resultMessageText;

    [SerializeField]
    private Button continueButton;

    // =========================================================
    // STATE
    // =========================================================

    private bool isInPuzzleMode;

    private bool isShowingPuzzleIntro;

    private bool isShowingPuzzleResult;

    // True when result panel currently represents
    // TIME'S UP instead of success.
    private bool resultIsFailure;

    // Once BEGIN is pressed, intro does not appear again.
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

    public bool IsShowingPuzzleResult =>
        isShowingPuzzleResult;

    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        // Only local player controls their own puzzle UI.
        if (Object != null &&
            !Object.HasInputAuthority)
        {
            return;
        }

        // -----------------------------------------------------
        // HIDE INTRO
        // -----------------------------------------------------

        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(false);
        }

        // -----------------------------------------------------
        // HIDE RESULT
        // -----------------------------------------------------

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(false);
        }

        // -----------------------------------------------------
        // BEGIN BUTTON
        // -----------------------------------------------------

        if (beginButton != null)
        {
            beginButton.onClick.RemoveListener(
                BeginPuzzleFromIntro
            );

            beginButton.onClick.AddListener(
                BeginPuzzleFromIntro
            );
        }

        // -----------------------------------------------------
        // RESULT BUTTON
        // -----------------------------------------------------

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(
                ContinueAfterPuzzleResult
            );

            continueButton.onClick.AddListener(
                ContinueAfterPuzzleResult
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

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(
                ContinueAfterPuzzleResult
            );
        }

        if (activePuzzleTable != null)
        {
            activePuzzleTable.PuzzleCompleted -=
                HandlePuzzleCompleted;

            activePuzzleTable.PuzzleFailed -=
                HandlePuzzleFailed;
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        // -----------------------------------------------------
        // RESULT SCREEN
        // -----------------------------------------------------

        if (isShowingPuzzleResult)
        {
            // Player must use CONTINUE or RETRY.
            return;
        }

        // -----------------------------------------------------
        // ESC WHILE READING INTRO
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
            isShowingPuzzleIntro ||
            isShowingPuzzleResult)
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
        // STORE DATA
        // -----------------------------------------------------

        pendingPuzzleCameraPoint =
            puzzleCameraPoint;

        pendingPuzzleTable =
            puzzleTable;

        pendingInventory =
            inventory;

        // Save normal player states.
        SavePlayerControlStates();

        // Disable movement/look and silence footsteps.
        DisablePlayerControls();

        // Stop pending cursor relock.
        if (cursorRelockRoutine != null)
        {
            StopCoroutine(
                cursorRelockRoutine
            );

            cursorRelockRoutine =
                null;
        }

        // =====================================================
        // INTRO ALREADY SEEN
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
        // FIRST TIME
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
                "[PUZZLE MODE] Puzzle Intro Panel missing. " +
                "Starting puzzle directly."
            );

            hasSeenPuzzleIntro =
                true;

            isShowingPuzzleIntro =
                false;

            StartActualPuzzle();

            return;
        }

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

        hasSeenPuzzleIntro =
            true;

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
                "[PUZZLE MODE] Pending PuzzleCameraPoint missing."
            );

            CancelPuzzleStart();

            return;
        }

        if (pendingPuzzleTable == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Pending PuzzleTableController missing."
            );

            CancelPuzzleStart();

            return;
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Player Camera missing."
            );

            CancelPuzzleStart();

            return;
        }

        // -----------------------------------------------------
        // SAVE CAMERA
        // -----------------------------------------------------

        originalCameraLocalPosition =
            playerCamera.localPosition;

        originalCameraLocalRotation =
            playerCamera.localRotation;

        // -----------------------------------------------------
        // MOVE CAMERA
        // -----------------------------------------------------

        playerCamera.SetPositionAndRotation(
            pendingPuzzleCameraPoint.position,
            pendingPuzzleCameraPoint.rotation
        );

        // -----------------------------------------------------
        // REMOVE OLD EVENT SUBSCRIPTIONS
        // -----------------------------------------------------

        if (activePuzzleTable != null)
        {
            activePuzzleTable.PuzzleCompleted -=
                HandlePuzzleCompleted;

            activePuzzleTable.PuzzleFailed -=
                HandlePuzzleFailed;
        }

        activePuzzleTable =
            pendingPuzzleTable;

        // -----------------------------------------------------
        // SUBSCRIBE TO RESULT EVENTS
        // -----------------------------------------------------

        activePuzzleTable.PuzzleCompleted -=
            HandlePuzzleCompleted;

        activePuzzleTable.PuzzleFailed -=
            HandlePuzzleFailed;

        activePuzzleTable.PuzzleCompleted +=
            HandlePuzzleCompleted;

        activePuzzleTable.PuzzleFailed +=
            HandlePuzzleFailed;

        // -----------------------------------------------------
        // CAMERA COMPONENT
        // -----------------------------------------------------

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
        // START
        // =====================================================

        activePuzzleTable.BeginPuzzle(
            pendingInventory,
            cameraComponent,
            puzzleProgressText,
            puzzleTimerText
        );

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible =
            true;

        isInPuzzleMode =
            true;

        resultIsFailure =
            false;

        Debug.Log(
            "[PUZZLE MODE] Puzzle started."
        );
    }

    // =========================================================
    // PUZZLE COMPLETE
    // =========================================================

    private void HandlePuzzleCompleted()
    {
        if (!isInPuzzleMode)
            return;

        resultIsFailure =
            false;

        isShowingPuzzleResult =
            true;

        // -----------------------------------------------------
        // TEXT
        // -----------------------------------------------------

        if (resultTitleText != null)
        {
            resultTitleText.text =
                "PUZZLE COMPLETE";
        }

        if (resultMessageText != null)
        {
            resultMessageText.text =
                "The map has been restored.";
        }

        // -----------------------------------------------------
        // BUTTON
        // -----------------------------------------------------

        SetResultButtonText(
            "CONTINUE"
        );

        // -----------------------------------------------------
        // HIDE PUZZLE HUD
        // -----------------------------------------------------

        HidePlayerPuzzleHUD();

        // -----------------------------------------------------
        // SHOW RESULT
        // -----------------------------------------------------

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "[PUZZLE MODE] Puzzle Result Panel is missing."
            );
        }

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible =
            true;

        Debug.Log(
            "[PUZZLE MODE] PUZZLE COMPLETE result shown."
        );
    }

    // =========================================================
    // PUZZLE FAILED
    // =========================================================

    private void HandlePuzzleFailed()
    {
        if (!isInPuzzleMode)
            return;

        resultIsFailure =
            true;

        isShowingPuzzleResult =
            true;

        // -----------------------------------------------------
        // TEXT
        // -----------------------------------------------------

        if (resultTitleText != null)
        {
            resultTitleText.text =
                "TIME'S UP";
        }

        if (resultMessageText != null)
        {
            resultMessageText.text =
                "The map was not completed in time.";
        }

        // -----------------------------------------------------
        // BUTTON
        // -----------------------------------------------------

        SetResultButtonText(
            "RETRY"
        );

        // -----------------------------------------------------
        // HIDE PUZZLE HUD
        // -----------------------------------------------------

        HidePlayerPuzzleHUD();

        // -----------------------------------------------------
        // SHOW RESULT
        // -----------------------------------------------------

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "[PUZZLE MODE] Puzzle Result Panel is missing."
            );
        }

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible =
            true;

        Debug.Log(
            "[PUZZLE MODE] TIME'S UP result shown."
        );
    }

    // =========================================================
    // RESULT BUTTON
    // =========================================================

    private void ContinueAfterPuzzleResult()
    {
        if (!isShowingPuzzleResult)
            return;

        // =====================================================
        // FAILURE → RETRY
        // =====================================================

        if (resultIsFailure)
        {
            RetryPuzzle();

            return;
        }

        // =====================================================
        // SUCCESS → CONTINUE
        // =====================================================

        isShowingPuzzleResult =
            false;

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(false);
        }

        Debug.Log(
            "[PUZZLE MODE] CONTINUE clicked."
        );

        ExitPuzzleMode();
    }

    // =========================================================
    // RETRY PUZZLE
    // =========================================================

    private void RetryPuzzle()
    {
        if (activePuzzleTable == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Cannot retry. " +
                "Active puzzle table is missing."
            );

            return;
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Cannot retry. " +
                "Player camera is missing."
            );

            return;
        }

        Camera cameraComponent =
            playerCamera.GetComponent<Camera>();

        if (cameraComponent == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Cannot retry. " +
                "Camera component is missing."
            );

            return;
        }

        // -----------------------------------------------------
        // CLOSE FAILURE PANEL
        // -----------------------------------------------------

        isShowingPuzzleResult =
            false;

        resultIsFailure =
            false;

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(false);
        }

        // -----------------------------------------------------
        // ERASE FAILED ATTEMPT
        // -----------------------------------------------------

        activePuzzleTable.ResetFailedAttempt();

        // -----------------------------------------------------
        // START FRESH ATTEMPT
        // -----------------------------------------------------

        activePuzzleTable.BeginPuzzle(
            pendingInventory,
            cameraComponent,
            puzzleProgressText,
            puzzleTimerText
        );

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible =
            true;

        Debug.Log(
            "[PUZZLE MODE] RETRY clicked. " +
            "New attempt started from 0/8."
        );
    }

    // =========================================================
    // SET RESULT BUTTON LABEL
    // =========================================================

    private void SetResultButtonText(
        string text)
    {
        if (continueButton == null)
            return;

        TMP_Text buttonText =
            continueButton.GetComponentInChildren<TMP_Text>(
                true
            );

        if (buttonText != null)
        {
            buttonText.text =
                text;
        }
    }

    // =========================================================
    // HIDE PUZZLE HUD
    // =========================================================

    private void HidePlayerPuzzleHUD()
    {
        if (puzzleProgressText != null)
        {
            puzzleProgressText.gameObject.SetActive(
                false
            );
        }

        if (puzzleTimerText != null)
        {
            puzzleTimerText.gameObject.SetActive(
                false
            );
        }
    }

    // =========================================================
    // CANCEL FAILED START
    // =========================================================

    private void CancelPuzzleStart()
    {
        isShowingPuzzleIntro =
            false;

        isShowingPuzzleResult =
            false;

        resultIsFailure =
            false;

        isInPuzzleMode =
            false;

        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(false);
        }

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(false);
        }

        if (activePuzzleTable != null)
        {
            activePuzzleTable.PuzzleCompleted -=
                HandlePuzzleCompleted;

            activePuzzleTable.PuzzleFailed -=
                HandlePuzzleFailed;

            activePuzzleTable =
                null;
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
    // ESC WHILE INTRO OPEN
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

        // Player did NOT press BEGIN,
        // so intro should still appear next time.

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
    // EXIT PUZZLE
    // =========================================================

    public void ExitPuzzleMode()
    {
        if (!isInPuzzleMode)
            return;

        isInPuzzleMode =
            false;

        isShowingPuzzleResult =
            false;

        resultIsFailure =
            false;

        // -----------------------------------------------------
        // HIDE RESULT
        // -----------------------------------------------------

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(false);
        }

        // -----------------------------------------------------
        // END ACTIVE PUZZLE
        // -----------------------------------------------------

        if (activePuzzleTable != null)
        {
            activePuzzleTable.PuzzleCompleted -=
                HandlePuzzleCompleted;

            activePuzzleTable.PuzzleFailed -=
                HandlePuzzleFailed;

            activePuzzleTable.EndPuzzle();

            activePuzzleTable =
                null;
        }

        // -----------------------------------------------------
        // RESTORE CAMERA
        // -----------------------------------------------------

        if (playerCamera != null)
        {
            playerCamera.localPosition =
                originalCameraLocalPosition;

            playerCamera.localRotation =
                originalCameraLocalRotation;
        }

        // -----------------------------------------------------
        // RESTORE PLAYER
        // -----------------------------------------------------

        RestorePlayerControls();

        ClearPendingPuzzle();

        // -----------------------------------------------------
        // RELOCK CURSOR
        // -----------------------------------------------------

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
    // SAVE PLAYER STATE
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

        if (footstepsAudioSource != null)
        {
            footstepsWasMuted =
                footstepsAudioSource.mute;
        }
    }

    // =========================================================
    // DISABLE PLAYER
    // =========================================================

    private void DisablePlayerControls()
    {
        // Stop and mute footsteps immediately.
        if (footstepsAudioSource != null)
        {
            footstepsAudioSource.Stop();

            footstepsAudioSource.mute =
                true;
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
    // RESTORE PLAYER
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

        if (footstepsAudioSource != null)
        {
            footstepsAudioSource.Stop();

            footstepsAudioSource.mute =
                footstepsWasMuted;
        }
    }

    // =========================================================
    // CLEAR PENDING DATA
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