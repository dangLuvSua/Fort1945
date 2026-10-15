using UnityEngine;

public class PuzzleTableInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField]
    private string prompt = "Press E to Assemble Map";

    [SerializeField]
    private bool canInteract = true;

    [SerializeField]
    private int priority = 20;

    public string Prompt => prompt;

    public bool CanInteract => canInteract;

    public int Priority => priority;

    public void Interact(PlayerInventory inventory)
    {
        Debug.Log(
            "[PUZZLE TABLE] Player interacted with the puzzle table."
        );
    }
}