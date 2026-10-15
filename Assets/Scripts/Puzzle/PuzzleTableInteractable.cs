using UnityEngine;

public class PuzzleTableInteractable :
    MonoBehaviour,
    IInteractable
{
    [Header("Interaction")]
    [SerializeField]
    private string prompt =
        "Press E to Assemble Map";

    [SerializeField]
    private bool canInteract = true;

    [SerializeField]
    private int priority = 20;

    [Header("Puzzle")]
    [SerializeField]
    private Transform puzzleCameraPoint;

    [SerializeField]
    private PuzzleTableController puzzleTableController;

    public string Prompt => prompt;
    public bool CanInteract => canInteract;
    public int Priority => priority;

    public void Interact(PlayerInventory inventory)
    {
        if (inventory == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] PlayerInventory is null."
            );

            return;
        }

        if (puzzleCameraPoint == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] PuzzleCameraPoint is missing."
            );

            return;
        }

        if (puzzleTableController == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] PuzzleTableController is missing."
            );

            return;
        }

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

        puzzleMode.EnterPuzzleMode(
            puzzleCameraPoint,
            puzzleTableController,
            inventory
        );
    }
}