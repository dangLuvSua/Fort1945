using UnityEngine;

public class KeyPickup : MonoBehaviour
{
    public ItemData item;
    public ChestState chest; // pwedeng i-set ng spawner

    public bool CanCollect
    {
        get
        {
            if (chest == null) chest = GetComponentInParent<ChestState>();
            return chest == null || chest.IsOpen;
        }
    }

    public void Collect(PlayerInventory inv)
    {
        if (!CanCollect) return;
        if (inv.TryAdd(item)) Destroy(gameObject); // hindi mawawala ang item kapag puno
    }
}