using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class ChestLoot : NetworkBehaviour
{
    [Header("Loot")]
    [Tooltip("Where the item appears inside the chest.")]
    [SerializeField] private Transform itemSpawnPoint;

    [Range(0f, 1f)]
    [Tooltip("1 = always has an item. 0.5 = 50% chance.")]
    [SerializeField] private float spawnChance = 1f;

    [Header("Debug")]
    [SerializeField] private bool showGizmo = true;

    private NetworkObject spawnedItem;
    private ChestState chestState;
    private bool lootSpawned;

    // =========================================================
    // SPAWN
    // =========================================================

    public override void Spawned()
    {
        // Only the State Authority should generate the loot.
        if (!Object.HasStateAuthority)
            return;

        chestState = GetComponent<ChestState>();
        if (chestState == null)
            SpawnItem();
    }

    public override void FixedUpdateNetwork()
    {
        if (lootSpawned || Object == null || !Object.HasStateAuthority)
            return;

        // Pick from the active mission only when the chest is opened, so its
        // contents match the phase in which the team finds it.
        if (chestState == null)
            chestState = GetComponent<ChestState>();

        if (chestState != null && chestState.IsNetworkReady && chestState.IsOpen)
            SpawnItem();
    }

    // =========================================================
    // LOOT GENERATION
    // =========================================================

    private void SpawnItem()
    {
        if (lootSpawned)
            return;
        lootSpawned = true;

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

        ItemRandomSpawner itemSpawner = FindFirstObjectByType<ItemRandomSpawner>();
        List<ItemData> objectiveItems = itemSpawner != null
            ? itemSpawner.GetCurrentPhaseRequiredItems()
            : null;

        if (objectiveItems == null || objectiveItems.Count == 0)
        {
            Debug.LogWarning($"[CHEST LOOT] {name} skipped: no active phase objective items are available.", this);
            return;
        }

        ItemData selectedItem = objectiveItems[Random.Range(0, objectiveItems.Count)];

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

        if (selectedItem.worldPrefab == null)
        {
            Debug.LogError(
                $"[CHEST LOOT] Objective item '{selectedItem.itemName}' has no world prefab.",
                this);
            return;
        }

        // =====================================================
        // NETWORK SPAWN
        // =====================================================

        spawnedItem = Runner.Spawn(
            selectedItem.worldPrefab,
            itemSpawnPoint.position,
            itemSpawnPoint.rotation,
            null,
            (runner, networkObject) =>
            {
                NetworkedDroppedItem droppedItem = networkObject.GetComponent<NetworkedDroppedItem>();
                if (droppedItem != null)
                    droppedItem.Initialize(selectedItem.itemId);

                // Spawn the objective's own world prefab and initialize its
                // pickup with this chest, so it cannot be collected closed.
                ItemPickup itemPickup = networkObject.GetComponent<ItemPickup>();
                if (itemPickup != null)
                {
                    itemPickup.Initialize(
                        selectedItem.itemId,
                        Object
                    );
                }
                else if (networkObject.TryGetComponent(out KeyPickup keyPickup))
                {
                    keyPickup.Initialize(selectedItem.itemId, Object);
                }
                else
                {
                    Debug.LogError(
                        "[CHEST LOOT] " +
                        "Spawned item has neither ItemPickup nor KeyPickup component."
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
