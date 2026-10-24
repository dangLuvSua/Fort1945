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
    // FOOTSTEPS & AUDIO
    // =========================================================

    [Header("Footsteps")]
    [SerializeField]
    private AudioSource footstepsAudioSource;

    [Header("Puzzle Flow Audio")]
    [SerializeField]
    private AudioSource puzzleAudioSource;

    [Tooltip("Sound played when entering or opening the puzzle table view.")]
    [SerializeField]
    private AudioClip tableOpenClip;

    [Tooltip("Sound played when leaving the puzzle table.")]
    [SerializeField]
    private AudioClip tableCloseClip;

    [Tooltip("Sound played when the Lost Map intro parchment panel appears.")]
    [SerializeField]
    private AudioClip parchmentOpenClip;

    [Tooltip("Sound played when clicking Begin, Retry, or Continue.")]
    [SerializeField]
    private AudioClip buttonClickClip;

    [Range(0f, 1f)]
    [SerializeField]
    private float flowAudioVolume = 0.8f;

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

    [Tooltip("Panel shown when the puzzle is completed or failed.")]
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

    private bool resultIsFailure;

    // Once BEGIN is pressed, the intro will not appear again
    // during this player's current session.
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

    // =========================================================
    // PUBLIC STATE
    // =========================================================

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
        // Only the local player's copy controls UI/input.
        if (Object != null &&
            !Object.HasInputAuthority)
        {
            return;
        }

        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(false);
        }

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(false);
        }

        if (beginButton != null)
        {
            beginButton.onClick.RemoveListener(
                BeginPuzzleFromIntro
            );

            beginButton.onClick.AddListener(
                BeginPuzzleFromIntro
            );
        }

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

        UnsubscribeFromActiveTable();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        // ESC also exits from the result screen so normal player controls
        // and interaction prompts return without requiring the UI button.
        if (isShowingPuzzleResult)
        {
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ExitPuzzleMode();
            }

            return;
        }

        // ESC while reading instructions.
        if (isShowingPuzzleIntro)
        {
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ClosePuzzleIntro();
            }

            return;
        }

        // ESC while assembling puzzle.
        if (!isInPuzzleMode)
        {
            return;
        }

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ExitPuzzleMode();
        }
    }

    // =========================================================
    // ENTER PUZZLE MODE
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

        pendingPuzzleCameraPoint =
            puzzleCameraPoint;

        pendingPuzzleTable =
            puzzleTable;

        pendingInventory =
            inventory;

        SavePlayerControlStates();

        DisablePlayerControls();

        if (cursorRelockRoutine != null)
        {
            StopCoroutine(
                cursorRelockRoutine
            );

            cursorRelockRoutine = null;
        }

        // Intro already completed before.
        if (hasSeenPuzzleIntro)
        {
            Debug.Log(
                "[PUZZLE MODE] Intro already completed. " +
                "Opening puzzle directly."
            );

            StartActualPuzzle();

            return;
        }

        // First interaction: show parchment.
        isShowingPuzzleIntro = true;

        PlayFlowAudio(parchmentOpenClip);

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

            hasSeenPuzzleIntro = true;
            isShowingPuzzleIntro = false;

            StartActualPuzzle();

            return;
        }

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

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
        {
            return;
        }

        PlayFlowAudio(buttonClickClip, 1.05f);

        hasSeenPuzzleIntro = true;

        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(false);
        }

        isShowingPuzzleIntro = false;

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

        // Save normal camera transform.
        originalCameraLocalPosition =
            playerCamera.localPosition;

        originalCameraLocalRotation =
            playerCamera.localRotation;

        // Move camera to board.
        playerCamera.SetPositionAndRotation(
            pendingPuzzleCameraPoint.position,
            pendingPuzzleCameraPoint.rotation
        );

        PlayFlowAudio(tableOpenClip);

        // Remove subscriptions from any previous table.
        UnsubscribeFromActiveTable();

        activePuzzleTable =
            pendingPuzzleTable;

        // Subscribe to shared/local puzzle events.
        activePuzzleTable.PuzzleCompleted +=
            HandlePuzzleCompleted;

        activePuzzleTable.PuzzleFailed +=
            HandlePuzzleFailed;

        activePuzzleTable.PuzzleRetried +=
            HandlePuzzleRetried;

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

        resultIsFailure = false;

        // Must be true before BeginPuzzle because a shared
        // completed/failed puzzle can fire an event immediately.
        isInPuzzleMode = true;

        activePuzzleTable.BeginPuzzle(
            pendingInventory,
            cameraComponent,
            puzzleProgressText,
            puzzleTimerText
        );

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

        Debug.Log(
            "[PUZZLE MODE] Puzzle started."
        );
    }

    // =========================================================
    // PUZZLE COMPLETED
    // =========================================================

    private void HandlePuzzleCompleted()
    {
        if (!isInPuzzleMode)
        {
            return;
        }

        resultIsFailure = false;
        isShowingPuzzleResult = true;

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

        SetResultButtonText(
            "CONTINUE"
        );

        HidePlayerPuzzleHUD();

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(true);
        }

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

        Debug.Log(
            "[PUZZLE MODE] Shared/local puzzle complete result shown."
        );
    }

    // =========================================================
    // PUZZLE FAILED
    // =========================================================

    private void HandlePuzzleFailed()
    {
        if (!isInPuzzleMode)
        {
            return;
        }

        resultIsFailure = true;
        isShowingPuzzleResult = true;

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

        SetResultButtonText(
            "RETRY"
        );

        HidePlayerPuzzleHUD();

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(true);
        }

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

        Debug.Log(
            "[PUZZLE MODE] Shared/local TIME'S UP result shown."
        );
    }

    // =========================================================
    // ANOTHER PLAYER RETRIED
    // =========================================================

    private void HandlePuzzleRetried()
    {
        if (!isInPuzzleMode ||
            !isShowingPuzzleResult ||
            !resultIsFailure)
        {
            return;
        }

        isShowingPuzzleResult = false;
        resultIsFailure = false;

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(false);
        }

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

        Debug.Log(
            "[PUZZLE MODE] Another player retried. " +
            "Failure panel closed and puzzle resumed."
        );
    }

    // =========================================================
    // RESULT BUTTON
    // =========================================================

    private void ContinueAfterPuzzleResult()
    {
        if (!isShowingPuzzleResult)
        {
            return;
        }

        PlayFlowAudio(buttonClickClip, 1.0f);

        if (resultIsFailure)
        {
            RetryPuzzle();

            return;
        }

        isShowingPuzzleResult = false;

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
    // RETRY
    // =========================================================

    private void RetryPuzzle()
    {
        PlayFlowAudio(buttonClickClip, 0.95f);

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

        isShowingPuzzleResult = false;
        resultIsFailure = false;

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(false);
        }

        // In multiplayer this requests a shared retry.
        // Offline mode resets locally.
        activePuzzleTable.ResetFailedAttempt();

        activePuzzleTable.BeginPuzzle(
            pendingInventory,
            cameraComponent,
            puzzleProgressText,
            puzzleTimerText
        );

        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

        Debug.Log(
            "[PUZZLE MODE] RETRY clicked. " +
            "Fresh shared attempt requested."
        );
    }

    // =========================================================
    // RESULT BUTTON TEXT
    // =========================================================

    private void SetResultButtonText(
        string text)
    {
        if (continueButton == null)
        {
            return;
        }

        TMP_Text buttonText =
            continueButton.GetComponentInChildren<TMP_Text>(
                true
            );

        if (buttonText != null)
        {
            buttonText.text = text;
        }
    }

    // =========================================================
    // HIDE LOCAL PUZZLE HUD
    // =========================================================

    private void HidePlayerPuzzleHUD()
    {
        if (puzzleProgressText != null)
        {
            puzzleProgressText.gameObject.SetActive(false);
        }

        if (puzzleTimerText != null)
        {
            puzzleTimerText.gameObject.SetActive(false);
        }
    }

    // =========================================================
    // CLOSE INTRO WITH ESC
    // =========================================================

    private void ClosePuzzleIntro()
    {
        if (!isShowingPuzzleIntro)
        {
            return;
        }

        PlayFlowAudio(tableCloseClip);

        isShowingPuzzleIntro = false;

        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(false);
        }

        // BEGIN was not pressed, so do not mark intro as seen.
        ClearPendingPuzzle();

        RestorePlayerControls();

        StartCursorRelock();

        Debug.Log(
            "[PUZZLE MODE] Instructions closed without BEGIN."
        );
    }

    // =========================================================
    // CANCEL FAILED PUZZLE START
    // =========================================================

    private void CancelPuzzleStart()
    {
        isShowingPuzzleIntro = false;
        isShowingPuzzleResult = false;
        resultIsFailure = false;
        isInPuzzleMode = false;

        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(false);
        }

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(false);
        }

        UnsubscribeFromActiveTable();

        activePuzzleTable = null;

        RestorePlayerControls();

        ClearPendingPuzzle();

        StartCursorRelock();
    }

    // =========================================================
    // EXIT PUZZLE MODE
    // =========================================================

    public void ExitPuzzleMode()
    {
        if (!isInPuzzleMode)
        {
            return;
        }

        PlayFlowAudio(tableCloseClip);

        isInPuzzleMode = false;
        isShowingPuzzleResult = false;
        resultIsFailure = false;

        if (puzzleResultPanel != null)
        {
            puzzleResultPanel.SetActive(false);
        }

        if (activePuzzleTable != null)
        {
            UnsubscribeFromActiveTable();

            activePuzzleTable.EndPuzzle();

            activePuzzleTable = null;
        }

        // Restore normal camera.
        if (playerCamera != null)
        {
            playerCamera.localPosition =
                originalCameraLocalPosition;

            playerCamera.localRotation =
                originalCameraLocalRotation;
        }

        RestorePlayerControls();

        ClearPendingPuzzle();

        StartCursorRelock();

        Debug.Log(
            "[PUZZLE MODE] Exited puzzle mode."
        );
    }

    // =========================================================
    // TABLE EVENT CLEANUP
    // =========================================================

    private void UnsubscribeFromActiveTable()
    {
        if (activePuzzleTable == null)
        {
            return;
        }

        activePuzzleTable.PuzzleCompleted -=
            HandlePuzzleCompleted;

        activePuzzleTable.PuzzleFailed -=
            HandlePuzzleFailed;

        activePuzzleTable.PuzzleRetried -=
            HandlePuzzleRetried;
    }

    // =========================================================
    // SAVE PLAYER CONTROL STATES
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
    // DISABLE PLAYER CONTROLS
    // =========================================================

    private void DisablePlayerControls()
    {
        if (footstepsAudioSource != null)
        {
            footstepsAudioSource.Stop();

            footstepsAudioSource.mute = true;
        }

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled = false;
        }

        if (cameraController != null)
        {
            cameraController.enabled = false;
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
        pendingPuzzleCameraPoint = null;
        pendingPuzzleTable = null;
        pendingInventory = null;
    }

    // =========================================================
    // CURSOR
    // =========================================================

    private void StartCursorRelock()
    {
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

    private IEnumerator RelockCursorNextFrame()
    {
        yield return null;

        yield return new WaitForEndOfFrame();

        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;

        cursorRelockRoutine = null;

        Debug.Log(
            "[PUZZLE MODE] Mouse locked again."
        );
    }

    // =========================================================
    // AUDIO HELPER
    // =========================================================

    private void PlayFlowAudio(AudioClip clip, float pitch = 1f)
    {
        if (clip == null)
            return;

        if (puzzleAudioSource == null)
        {
            puzzleAudioSource = GetComponent<AudioSource>();
        }

        if (puzzleAudioSource != null)
        {
            puzzleAudioSource.pitch = pitch;
            puzzleAudioSource.PlayOneShot(clip, flowAudioVolume);
        }
    }
}
