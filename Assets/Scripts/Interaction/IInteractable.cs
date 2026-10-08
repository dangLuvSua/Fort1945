public interface IInteractable
{
    string Prompt { get; }
    bool CanInteract { get; }
    int Priority { get; } // mas mataas = mas inuuna
    void Interact(PlayerInventory inv);
}