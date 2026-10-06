using UnityEngine;

public class KeyPickup : MonoBehaviour
{
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
        inv.AddKey();
        Destroy(gameObject);
    }
}