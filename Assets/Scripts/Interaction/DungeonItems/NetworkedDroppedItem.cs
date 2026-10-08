using Fusion;
using UnityEngine;

public class NetworkedDroppedItem : NetworkBehaviour
{
    [Networked]
    public int ItemId { get; private set; }

    public void Initialize(int itemId)
    {
        if (!Object.HasStateAuthority)
            return;

        ItemId = itemId;
    }

    public ItemData GetItemData(
        PlayerInventory inventory)
    {
        if (inventory == null)
            return null;

        return inventory.GetItemData(ItemId);
    }
}