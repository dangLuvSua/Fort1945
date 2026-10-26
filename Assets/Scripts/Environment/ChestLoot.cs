using System.Collections.Generic;
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

        List<ItemData> phaseItems = null;
        ItemRandomSpawner itemSpawner = FindFirstObjectByType<ItemRandomSpawner>();
        if (itemSpawner != null)
            phaseItems = itemSpawner.GetCurrentPhaseRequiredItems();

        ItemData[] choices = possibleItems;
        if (phaseItems != null && phaseItems.Count > 0)
            choices = phaseItems.ToArray();

        if (choices == null || choices.Length == 0)
        {
            Debug.LogWarning($"[CHEST LOOT] {name} has no items eligible for the active phase.", this);
            return;
        }

        ItemData selectedItem = choices[Random.Range(0, choices.Length)];

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
                        selectedItem.itemId,
                        Object
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
