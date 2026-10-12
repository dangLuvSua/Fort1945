using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Hold the selected Cross on an incapacitated teammate to revive them.</summary>
public class CrossReviveTarget : MonoBehaviour, IHoldInteractable, IKeyHoldInteractable
{
    [SerializeField, Min(0.1f)] private float holdDuration = 4f;

    private PlayerSanity sanity;
    private PlayerInventory reviverInventory;

    public Key HoldKey => Key.R;
    public string Prompt => sanity != null && sanity.IsInsane
        ? "[R] HOLD TO REVIVE"
        : "[R] HOLD TO HEAL TEAMMATE";
    public bool CanInteract => sanity != null && sanity.CanBeHealedByCross && HasSelectedCross();
    public int Priority => 20;
    public float HoldDuration => holdDuration;

    private void Awake()
    {
        sanity = GetComponentInParent<PlayerSanity>();
        if (sanity == null)
            sanity = GetComponent<PlayerSanity>();
    }

    public void UpdateContext(PlayerInventory inventory)
    {
        reviverInventory = inventory;
    }

    public void Interact(PlayerInventory inventory) { }
    public void HoldStarted(PlayerInventory inventory) => reviverInventory = inventory;
    public void HoldCancelled() { }

    public void HoldCompleted(PlayerInventory inventory)
    {
        reviverInventory = inventory;
        if (!CanInteract || inventory == null || sanity.Object == null ||
            !sanity.Object.IsValid || inventory.Object == null ||
            !inventory.Object.IsValid)
            return;

        sanity.RPC_RequestCrossRevive(inventory.Object.InputAuthority);
    }

    private bool HasSelectedCross()
    {
        return reviverInventory != null &&
               reviverInventory.SelectedSlot >= 0 &&
               reviverInventory.GetQuickBarItemIdAt(reviverInventory.SelectedSlot) == 201;
    }
}
