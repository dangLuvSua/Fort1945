
using Fusion;
using UnityEngine;

public class ItemPickup : NetworkBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private ItemData itemData;

    [Header("Optional Source Chest")]
    [SerializeField] private ChestState sourceChest;

    [Networked]
    public int ItemId { get; private set; }

    [Networked]
    private NetworkObject SourceChestObject { get; set; }

    [Networked]
    private NetworkBool IsCollecting { get; set; }

    private PlayerRef pendingCollector;

    public int NetworkItemId => ItemId;
    public ItemData Item => itemData;

    public string Prompt =>
        itemData != null && !string.IsNullOrWhiteSpace(itemData.itemName)
            ? $"Press E to collect {itemData.itemName}"
            : "Press E to collect";

    public bool CanInteract => CanCollect;
    public int Priority => 10;

    public void Interact(PlayerInventory inventory)
    {
        RequestCollect();
    }

    public bool CanCollect
    {
        get
        {
            if (ItemId <= 0)
                return false;

            if (SourceChestObject != null)
            {
                ChestState chest =
                    SourceChestObject.GetComponent<ChestState>();

                return chest != null && chest.IsOpen;
            }

            if (sourceChest == null)
                sourceChest = GetComponentInParent<ChestState>();

            return sourceChest == null || sourceChest.IsOpen;
        }
    }

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        if (ItemId <= 0 && itemData != null)
        {
            if (itemData.itemId <= 0)
            {
                Debug.LogError(
                    $"[ITEM PICKUP] {name} has an invalid ItemData ID."
                );
                return;
            }

            ItemId = itemData.itemId;
        }

        if (ItemId <= 0)
        {
            Debug.LogError(
                $"[ITEM PICKUP] {name} has no valid Item ID."
            );
        }
    }

    public void Initialize(
        int itemId,
        NetworkObject sourceChest = null)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (itemId <= 0)
        {
            Debug.LogError(
                $"[ITEM PICKUP] Invalid item ID {itemId}."
            );
            return;
        }

        ItemId = itemId;
        SourceChestObject = sourceChest;
    }

    public void RequestCollect()
    {
        if (Object == null || !Object.IsValid)
            return;

        RPC_RequestCollect();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCollect(RpcInfo info = default)
    {
        if (IsCollecting || ItemId <= 0 || !CanCollect)
            return;

        if (info.Source == PlayerRef.None)
            return;

        pendingCollector = info.Source;
        IsCollecting = true;

        // Candle state is stored on the dropped world object.
        NetworkedDroppedItem dropped =
            GetComponent<NetworkedDroppedItem>();

        bool hasCandleState =
            dropped != null && dropped.HasCandleState;

        float remaining =
            hasCandleState ? dropped.CandleRemaining : 0f;

        float maxLife =
            hasCandleState ? dropped.CandleMaxLife : 0f;

        RPC_DeliverItem(
            info.Source,
            ItemId,
            hasCandleState,
            remaining,
            maxLife
        );
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DeliverItem(
        [RpcTarget] PlayerRef targetPlayer,
        int itemId,
        bool hasCandleState,
        float remaining,
        float maxLife)
    {
        if (Runner.LocalPlayer != targetPlayer)
            return;

        if (!Runner.TryGetPlayerObject(
                targetPlayer,
                out NetworkObject playerObject) ||
            playerObject == null)
        {
            RPC_CollectResult(false);
            return;
        }

        PlayerInventory inventory =
            playerObject.GetComponent<PlayerInventory>();

        if (inventory == null)
        {
            Debug.LogError(
                "[ITEM PICKUP] PlayerInventory is missing."
            );
            RPC_CollectResult(false);
            return;
        }

        bool added;

        if (hasCandleState)
        {
            added = inventory.TryAddNetworkedById(
                itemId,
                remaining,
                maxLife
            );
        }
        else
        {
            added = inventory.TryAddNetworkedById(itemId);
        }

        if (added)
        {
            Debug.Log(
                $"[ITEM PICKUP] Item {itemId} collected by {targetPlayer}."
            );
        }
        else
        {
            Debug.Log(
                $"[ITEM PICKUP] Could not add item {itemId}; inventory is full."
            );
        }

        RPC_CollectResult(added);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_CollectResult(
        bool success,
        RpcInfo info = default)
    {
        if (!IsCollecting || info.Source != pendingCollector)
            return;

        IsCollecting = false;

        if (!success)
        {
            pendingCollector = PlayerRef.None;
            return;
        }

        pendingCollector = PlayerRef.None;

        if (Object != null && Object.IsValid)
            Runner.Despawn(Object);
    }
}