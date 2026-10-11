/// <summary>
/// An interactable that requires the player to HOLD the interact key
/// for a set duration instead of a single tap.
///
/// PlayerInteractor drives the hold progress slider automatically:
///  - UpdateContext is called every frame while aiming at the object
///  - HoldStarted when the player presses the interact key
///  - HoldCancelled when the player releases the key or loses the target
///  - HoldCompleted when the key was held for the full HoldDuration
/// </summary>
public interface IHoldInteractable : IInteractable
{
    /// <summary>Seconds the player must hold the interact key to complete the action.</summary>
    float HoldDuration { get; }

    /// <summary>
    /// Called every frame while the player is aiming at this object so it
    /// can show context-sensitive prompts (e.g. whether a key is equipped).
    /// </summary>
    void UpdateContext(PlayerInventory inv);

    /// <summary>Called when the player starts holding the interact key.</summary>
    void HoldStarted(PlayerInventory inv);

    /// <summary>Called when the player releases the key or loses the target before completing.</summary>
    void HoldCancelled();

    /// <summary>Called when the player held the key for the full HoldDuration.</summary>
    void HoldCompleted(PlayerInventory inv);
}
