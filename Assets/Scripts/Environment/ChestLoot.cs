using Fusion;
using UnityEngine;

public class ChestLoot : NetworkBehaviour
{
    [Header("Loot")]
    [Tooltip("Possible ItemData assets that can appear in this chest.")]
    [SerializeField] private ItemData[] possibleItems;

    [Tooltip("Networked prefab used for ALL key/item types.")]
    [SerializeField] private NetworkObject itemPrefab;

    [Tooltip("Where the item appears inside the chest.")]
    [SerializeField] private Transform itemSpawnPoint;

    [Range(0f, 1f)]
    [Tooltip("1 = always has an item. 0.5 = 50% chance.")]
    [SerializeField] private float spawnChance = 1f;

    [Header("Debug")]
    [SerializeField] private bool showGizmo = true;

    private NetworkObject spawnedItem;

    // =========================================================
    // SPAWN
    // =========================================================

    public override void Spawned()
    {
        // Only the State Authority should generate the loot.
        if (!Object.HasStateAuthority)
            return;

        SpawnItem();
    }

    // =========================================================
    // LOOT GENERATION
    // =========================================================

    private void SpawnItem()
    {
        if (possibleItems == null ||
            possibleItems.Length == 0)
        {
            Debug.LogWarning(
                $"[CHEST LOOT] {name} has no possible items.",
                this
            );

            return;
        }

        if (itemPrefab == null)
        {
            Debug.LogError(
                $"[CHEST LOOT] {name} has no item NetworkObject prefab.",
                this
            );

            return;
        }

        if (itemSpawnPoint == null)
        {
            Debug.LogError(
                $"[CHEST LOOT] {name} has no itemSpawnPoint.",
                this
            );

            return;
        }

        // =====================================================
        // SPAWN CHANCE
        // =====================================================

        if (Random.value > spawnChance)
        {
            Debug.Log(
                $"[CHEST LOOT] {name} spawned empty."
            );

            return;
        }

        // =====================================================
        // CHOOSE ITEM
        // =====================================================

        ItemData selectedItem =
            possibleItems[
                Random.Range(
                    0,
                    possibleItems.Length
                )
            ];

        if (selectedItem == null)
        {
            Debug.LogWarning(
                $"[CHEST LOOT] {name} selected a null ItemData."
            );

            return;
        }

        if (selectedItem.itemId <= 0)
        {
            Debug.LogError(
                $"[CHEST LOOT] " +
                $"{selectedItem.name} has invalid Item ID " +
                $"{selectedItem.itemId}."
            );

            return;
        }

        // =====================================================
        // NETWORK SPAWN
        // =====================================================

        spawnedItem = Runner.Spawn(
            itemPrefab,
            itemSpawnPoint.position,
            itemSpawnPoint.rotation,
            null,
            (runner, networkObject) =>
            {
                KeyPickup pickup =
                    networkObject.GetComponent<KeyPickup>();

                if (pickup != null)
                {
                    pickup.Initialize(
                        selectedItem.itemId
                    );
                }
                else
                {
                    Debug.LogError(
                        "[CHEST LOOT] " +
                        "Spawned item has no KeyPickup component."
                    );
                }
            }
        );

        if (spawnedItem == null)
        {
            Debug.LogError(
                $"[CHEST LOOT] Failed to spawn " +
                $"{selectedItem.itemName}."
            );

            return;
        }

        Debug.Log(
            $"[CHEST LOOT] {name} spawned " +
            $"{selectedItem.itemName} " +
            $"(ID {selectedItem.itemId})"
        );
    }

    // =========================================================
    // GIZMO
    // =========================================================

    private void OnDrawGizmos()
    {
        if (!showGizmo ||
            itemSpawnPoint == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            itemSpawnPoint.position,
            0.1f
        );

        Gizmos.DrawRay(
            itemSpawnPoint.position,
            itemSpawnPoint.up * 0.3f
        );
    }
}