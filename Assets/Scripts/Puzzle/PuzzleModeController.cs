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

    private Vector3 originalCameraLocalPosition;
    private Quaternion originalCameraLocalRotation;

    private bool playerControllerWasEnabled;
    private bool playerInteractorWasEnabled;
    private bool cameraControllerWasEnabled;

    private PuzzleTableController activePuzzleTable;

    // Information temporarily stored while
    // the instruction page is visible.
    private Transform pendingPuzzleCameraPoint;
    private PuzzleTableController pendingPuzzleTable;
    private PlayerInventory pendingInventory;

    private Coroutine cursorRelockRoutine;

    public bool IsInPuzzleMode => isInPuzzleMode;
    public bool IsShowingPuzzleIntro => isShowingPuzzleIntro;

    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        // Only the local player should control this UI.
        if (Object != null &&
            !Object.HasInputAuthority)
        {
            return;
        }

        // Intro should start hidden.
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
        // ESC WHILE ASSEMBLING PUZZLE
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
    // PLAYER PRESSES E ON TABLE
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

        // Save current component states.
        SavePlayerControlStates();

        // Player cannot move/look while reading instructions.
        DisablePlayerControls();

        // Cancel any old cursor relock.
        if (cursorRelockRoutine != null)
        {
            StopCoroutine(
                cursorRelockRoutine
            );

            cursorRelockRoutine = null;
        }

        // We are now in the instruction state.
        isShowingPuzzleIntro = true;

        // -----------------------------------------------------
        // SHOW INTRO
        // -----------------------------------------------------

        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "[PUZZLE MODE] Puzzle Intro Panel is missing. " +
                "Starting puzzle immediately."
            );

            BeginPuzzleFromIntro();
            return;
        }

        // Unlock mouse so BEGIN can be clicked.
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

        if (pendingPuzzleCameraPoint == null ||
            pendingPuzzleTable == null)
        {
            Debug.LogError(
                "[PUZZLE MODE] Pending puzzle information is missing."
            );

            ClosePuzzleIntro();
            return;
        }

        // -----------------------------------------------------
        // HIDE INSTRUCTIONS
        // -----------------------------------------------------

        if (puzzleIntroPanel != null)
        {
            puzzleIntroPanel.SetActive(false);
        }

        isShowingPuzzleIntro = false;

        // -----------------------------------------------------
        // SAVE NORMAL CAMERA POSITION
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

            RestorePlayerControls();
            ClearPendingPuzzle();

            return;
        }

        // =====================================================
        // ACTUAL PUZZLE STARTS HERE
        //
        // Timer starts here.
        // Timer SFX starts here.
        // World timer appears here.
        // Puzzle pieces appear here.
        // =====================================================

        activePuzzleTable.BeginPuzzle(
            pendingInventory,
            cameraComponent,
            puzzleProgressText,
            puzzleTimerText
        );

        // Puzzle needs unlocked cursor for dragging.
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible =
            true;

        isInPuzzleMode =
            true;

        Debug.Log(
            "[PUZZLE MODE] BEGIN clicked. Puzzle started."
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
            "[PUZZLE MODE] Instructions closed without starting puzzle."
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

        // Restore controls.
        RestorePlayerControls();

        ClearPendingPuzzle();

        // Relock cursor after Unity processes Escape.
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
    }

    // =========================================================
    // DISABLE PLAYER
    // =========================================================

    private void DisablePlayerControls()
    {
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
    }

    // =========================================================
    // CLEAR TEMPORARY PUZZLE DATA
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
        // Escape also affects the Unity Editor cursor,
        // so wait before locking it again.
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