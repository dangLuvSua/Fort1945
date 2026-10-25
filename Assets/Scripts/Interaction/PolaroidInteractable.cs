using UnityEngine;

[RequireComponent(typeof(PolaroidPhotoDisplay))]
public sealed class PolaroidInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private EvidenceBoardManager board;
    private PolaroidPhotoDisplay display;
    public string Prompt => Completed ? "Photograph Already Placed" : "Click to Place Photograph";
    public bool CanInteract => board != null && board.Ready;
    public int Priority => 10;
    private bool Completed => display != null &&
        (display.IsCompleted || board != null && board.IsPlaced(board.IndexOf(display)));

    private void Awake()
    {
        display = GetComponent<PolaroidPhotoDisplay>();
        if (board == null) board = GetComponentInParent<EvidenceBoardManager>();
    }

    public void Interact(PlayerInventory inventory)
    {
        if (!CanInteract || Completed || inventory == null) return;
        foreach (PlayerInventoryUI ui in FindObjectsByType<PlayerInventoryUI>(FindObjectsSortMode.None))
            if (ui.IsForInventory(inventory))
            { ui.BeginEvidencePlacement(board, board.IndexOf(display)); return; }
        Debug.LogWarning("[EVIDENCE] Click received, but no active PlayerInventoryUI is bound to this player's inventory.", this);
    }
}
