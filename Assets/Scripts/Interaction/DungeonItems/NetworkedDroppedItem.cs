
using Fusion;
using UnityEngine;

public class NetworkedDroppedItem : NetworkBehaviour
{
    [Networked]
    public int ItemId { get; private set; }

    [Networked]
    public NetworkBool HasCandleState { get; private set; }

    [Networked]
    public float CandleRemaining { get; private set; }

    [Networked]
    public float CandleMaxLife { get; private set; }

    [Networked]
    public NetworkBool CandleWasLit { get; private set; }

    public void Initialize(int itemId)
    {
        Initialize(itemId, false, 0f, 0f, false);
    }

    public void Initialize(
        int itemId,
        bool hasCandleState,
        float remaining,
        float maxLife,
        bool wasLit)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        ItemId = itemId;
        HasCandleState = hasCandleState;
        CandleRemaining = Mathf.Max(0f, remaining);
        CandleMaxLife = Mathf.Max(0f, maxLife);

        // A dropped candle is always extinguished.
        // It retains its remaining life while on the ground.
        CandleWasLit = false;
    }

    public void SetCandleState(
        float remaining,
        float maxLife,
        bool wasLit)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        HasCandleState = true;
        CandleRemaining = Mathf.Max(0f, remaining);
        CandleMaxLife = Mathf.Max(0f, maxLife);

        // Dropped candles pause their timer.
        CandleWasLit = false;
    }

    public ItemData GetItemData(PlayerInventory inventory)
    {
        if (inventory == null)
            return null;

        return inventory.GetItemData(ItemId);
    }
}