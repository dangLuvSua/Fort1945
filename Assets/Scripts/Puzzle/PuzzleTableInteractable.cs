using UnityEngine;

public class PuzzleTableInteractable :
    MonoBehaviour,
    IInteractable
{
    // =========================================================
    // INTERACTION
    // =========================================================

    [Header("Interaction")]

    [SerializeField]
    private string prompt =
        "Press E to Assemble Map";

    [SerializeField]
    private bool canInteract = true;

    [SerializeField]
    private int priority = 20;

    // =========================================================
    // PUZZLE
    // =========================================================

    [Header("Puzzle")]

    [SerializeField]
    private Transform puzzleCameraPoint;

    [SerializeField]
    private PuzzleTableController puzzleTableController;

    // =========================================================
    // STATE
    // =========================================================

    private bool puzzleHasBeenCompleted;

    // =========================================================
    // IINTERACTABLE
    // =========================================================

    public string Prompt =>
        prompt;

    public bool CanInteract
    {
        get
        {
            // Disabled manually in Inspector.
            if (!canInteract)
                return false;

            // No puzzle controller assigned.
            if (puzzleTableController == null)
                return false;

            // Puzzle already successfully completed.
            if (puzzleHasBeenCompleted)
                return false;

            return true;
        }
    }

    public int Priority =>
        priority;

    // =========================================================
    // ENABLE
    // =========================================================

    private void OnEnable()
    {
        SubscribeToPuzzleEvents();
    }

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        // Subscribe again safely in case the reference
        // was not available during OnEnable.
        SubscribeToPuzzleEvents();
    }

    // =========================================================
    // DISABLE
    // =========================================================

    private void OnDisable()
    {
        UnsubscribeFromPuzzleEvents();
    }

    // =========================================================
    // DESTROY
    // =========================================================

    private void OnDestroy()
    {
        UnsubscribeFromPuzzleEvents();
    }

    // =========================================================
    // SUBSCRIBE
    // =========================================================

    private void SubscribeToPuzzleEvents()
    {
        if (puzzleTableController == null)
            return;

        // Remove first so we never subscribe twice.
        puzzleTableController.PuzzleCompleted -=
            HandlePuzzleCompleted;

        puzzleTableController.PuzzleCompleted +=
            HandlePuzzleCompleted;
    }

    // =========================================================
    // UNSUBSCRIBE
    // =========================================================

    private void UnsubscribeFromPuzzleEvents()
    {
        if (puzzleTableController == null)
            return;

        puzzleTableController.PuzzleCompleted -=
            HandlePuzzleCompleted;
    }

    // =========================================================
    // PUZZLE COMPLETED
    // =========================================================

    private void HandlePuzzleCompleted()
    {
        puzzleHasBeenCompleted =
            true;

        PhaseManager phaseManager = PhaseManager.Instance;
        if (phaseManager != null && phaseManager.PhaseInitialized &&
            phaseManager.CurrentPhase >= 3)
        {
            phaseManager.ReportExternalPhaseComplete();
        }

        Debug.Log(
            "[PUZZLE TABLE] Puzzle completed. " +
            "Table interaction has been disabled."
        );
    }

    // =========================================================
    // INTERACT
    // =========================================================

    public void Interact(
        PlayerInventory inventory)
    {
        // -----------------------------------------------------
        // ALREADY COMPLETED
        // -----------------------------------------------------

        if (puzzleHasBeenCompleted)
        {
            Debug.Log(
                "[PUZZLE TABLE] Puzzle is already completed."
            );

            return;
        }

        // -----------------------------------------------------
        // CONTROLLER
        // -----------------------------------------------------

        if (puzzleTableController == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] PuzzleTableController is missing."
            );

            return;
        }

        // -----------------------------------------------------
        // INVENTORY
        // -----------------------------------------------------

        if (inventory == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] PlayerInventory is null."
            );

            return;
        }

        // -----------------------------------------------------
        // CAMERA
        // -----------------------------------------------------

        if (puzzleCameraPoint == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] PuzzleCameraPoint is missing."
            );

            return;
        }

        // -----------------------------------------------------
        // FIND PLAYER PUZZLE MODE
        // -----------------------------------------------------

        PuzzleModeController puzzleMode =
            inventory.GetComponentInParent<PuzzleModeController>();

        if (puzzleMode == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] PuzzleModeController " +
                "was not found on PlayerRoot."
            );

            return;
        }

        // -----------------------------------------------------
        // ENTER PUZZLE
        // -----------------------------------------------------

        puzzleMode.EnterPuzzleMode(
            puzzleCameraPoint,
            puzzleTableController,
            inventory
        );
    }
}
