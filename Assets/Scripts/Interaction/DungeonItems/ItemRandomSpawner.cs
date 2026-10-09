
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class ItemRandomSpawner : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PhaseManager phaseManager;
    [SerializeField] private DifficultyItemHandler itemHandler;
    [SerializeField] private DungeonGenerator dungeonGenerator;

    [Header("Standalone Testing")]
    [Tooltip("Allows item spawning without NetworkGameManager for direct Dungeon scene testing.")]
    [SerializeField] private bool allowStandaloneTesting = true;

    [Tooltip("Difficulty used when NetworkGameManager is unavailable.")]
    [SerializeField]
    private GameDifficulty standaloneDifficulty =
        GameDifficulty.Normal;

    [Tooltip("Phase used when PhaseManager is unavailable during standalone testing.")]
    [SerializeField, Min(1)] private int standalonePhase = 1;

    [Header("Spawn Settings")]
    [SerializeField] private bool spawnOnCurrentPhase = true;
    [SerializeField] private float verticalOffset = 0.1f;

    private readonly List<ItemSpawnPoint> availablePoints =
        new List<ItemSpawnPoint>();

    private bool dungeonReady;
    private bool isSubscribed;
    private int lastProcessedPhase = -1;

    public override void Spawned()
    {
        lastProcessedPhase = -1;

        SubscribeToDungeonEvent();

        if (phaseManager == null)
            phaseManager = PhaseManager.Instance;

        if (dungeonGenerator == null)
            dungeonGenerator = FindFirstObjectByType<DungeonGenerator>();

        if (dungeonGenerator != null && dungeonGenerator.IsGenerated)
            HandleDungeonGenerated(dungeonGenerator);

        Debug.Log(
            $"[ITEM SPAWNER] Network object spawned. " +
            $"State Authority: {Object.HasStateAuthority}"
        );
    }

    private void OnEnable()
    {
        SubscribeToDungeonEvent();
    }

    private void OnDisable()
    {
        UnsubscribeFromDungeonEvent();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnsubscribeFromDungeonEvent();
    }

    private void SubscribeToDungeonEvent()
    {
        if (isSubscribed)
            return;

        DungeonGenerator.OnDungeonGenerated += HandleDungeonGenerated;
        isSubscribed = true;
    }

    private void UnsubscribeFromDungeonEvent()
    {
        if (!isSubscribed)
            return;

        DungeonGenerator.OnDungeonGenerated -= HandleDungeonGenerated;
        isSubscribed = false;
    }

    private void HandleDungeonGenerated(DungeonGenerator generator)
    {
        if (generator == null)
            return;

        if (dungeonGenerator != null && generator != dungeonGenerator)
            return;

        dungeonGenerator = generator;
        availablePoints.Clear();

        ItemSpawnPoint[] markers =
            generator.GetComponentsInChildren<ItemSpawnPoint>(true);

        foreach (ItemSpawnPoint marker in markers)
        {
            if (marker == null || !marker.canSpawnItems)
                continue;

            if (!marker.gameObject.activeInHierarchy)
                continue;

            availablePoints.Add(marker);
        }

        ShufflePoints();

        dungeonReady = true;
        lastProcessedPhase = -1;

        Debug.Log(
            $"[ITEM SPAWNER] Dungeon ready. " +
            $"Found {availablePoints.Count} item spawn points."
        );
    }

    public override void FixedUpdateNetwork()
    {
        // This script must be attached to a spawned NetworkObject.
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (!spawnOnCurrentPhase || !dungeonReady)
            return;

        if (Runner == null || !Runner.IsRunning)
            return;

        if (phaseManager == null)
            phaseManager = PhaseManager.Instance;

        if (itemHandler == null)
        {
            Debug.LogWarning(
                "[ITEM SPAWNER] DifficultyItemHandler is missing."
            );
            return;
        }

        // Resolve difficulty and phase, supporting standalone testing.
        GameDifficulty difficulty;
        int phase;

        NetworkGameManager gameManager = NetworkGameManager.Instance;

        if (gameManager != null && gameManager.IsNetworkStateReady)
        {
            difficulty = gameManager.Difficulty;
        }
        else if (allowStandaloneTesting)
        {
            difficulty = standaloneDifficulty;
        }
        else
        {
            return;
        }

        if (phaseManager != null && phaseManager.PhaseInitialized)
        {
            phase = phaseManager.CurrentPhase;
        }
        else if (allowStandaloneTesting)
        {
            phase = Mathf.Max(1, standalonePhase);
        }
        else
        {
            return;
        }

        if (phase == lastProcessedPhase)
            return;

        // Do not mark the phase processed until a valid rule is found.
        ItemSpawnProfile.PhaseSpawnRule rule =
            itemHandler.GetSpawnRule(difficulty, phase);

        if (rule == null)
        {
            Debug.LogWarning(
                $"[ITEM SPAWNER] No matching rule for " +
                $"{difficulty}, Phase {phase}. Will retry."
            );
            return;
        }

        bool completed = SpawnPhaseItems(difficulty, phase, rule);

        if (completed)
        {
            lastProcessedPhase = phase;
        }
    }

    private bool SpawnPhaseItems(
        GameDifficulty difficulty,
        int phase,
        ItemSpawnProfile.PhaseSpawnRule rule)
    {
        if (rule.items == null || rule.items.Count == 0)
        {
            Debug.LogWarning(
                $"[ITEM SPAWNER] No item entries configured for " +
                $"{difficulty}, Phase {phase}."
            );

            return false;
        }

        int spawnRolls = Mathf.Max(0, rule.totalItemsToSpawn);
        int spawnedCount = 0;
        bool hadFailure = false;

        for (int i = 0; i < spawnRolls; i++)
        {
            if (availablePoints.Count == 0)
            {
                Debug.LogWarning(
                    "[ITEM SPAWNER] No unused item spawn points remain."
                );

                break;
            }

            ItemSpawnProfile.ItemSpawnEntry entry =
                SelectWeightedItem(rule.items);

            if (entry == null || entry.item == null)
            {
                Debug.LogWarning(
                    "[ITEM SPAWNER] No valid weighted item entry found."
                );

                hadFailure = true;
                continue;
            }

            int min = Mathf.Max(0, entry.minQuantity);
            int max = Mathf.Max(min, entry.maxQuantity);

            int quantity = Random.Range(min, max + 1);

            for (int q = 0; q < quantity; q++)
            {
                if (availablePoints.Count == 0)
                    break;

                if (SpawnAtRandomPoint(entry.item))
                {
                    spawnedCount++;
                }
                else
                {
                    hadFailure = true;
                }
            }
        }

        Debug.Log(
            $"[ITEM SPAWNER] {difficulty}, Phase {phase}: " +
            $"spawned {spawnedCount} item(s)."
        );

        // A failed attempt can be retried. Successful spawns from a
        // partially failed attempt are not rolled back.
        return !hadFailure;
    }

    private bool SpawnAtRandomPoint(ItemData item)
    {
        if (item == null || item.worldPrefab == null)
        {
            Debug.LogWarning(
                "[ITEM SPAWNER] Item or World Prefab is missing."
            );
            return false;
        }

        if (availablePoints.Count == 0)
            return false;

        int index = Random.Range(0, availablePoints.Count);
        ItemSpawnPoint point = availablePoints[index];

        if (point == null)
        {
            availablePoints.RemoveAt(index);
            return false;
        }

        Vector3 position =
            point.Position + Vector3.up * verticalOffset;

        Quaternion rotation = point.Rotation;

        NetworkObject spawned = Runner.Spawn(
            item.worldPrefab,
            position,
            rotation
        );

        if (spawned == null)
        {
            Debug.LogWarning(
                $"[ITEM SPAWNER] Failed to spawn {item.itemName}."
            );

            // Keep the point available so a later attempt can retry.
            return false;
        }

        // Consume the marker only after a successful network spawn.
        availablePoints.RemoveAt(index);

        NetworkedDroppedItem droppedItem =
            spawned.GetComponent<NetworkedDroppedItem>();

        if (droppedItem != null)
            droppedItem.Initialize(item.itemId);

        ItemPickup pickup =
            spawned.GetComponent<ItemPickup>();

        if (pickup != null)
            pickup.Initialize(item.itemId);

        Debug.Log(
            $"[ITEM SPAWNER] Spawned {item.itemName} at {position}."
        );

        return true;
    }

    private ItemSpawnProfile.ItemSpawnEntry SelectWeightedItem(
        List<ItemSpawnProfile.ItemSpawnEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return null;

        float totalWeight = 0f;

        foreach (var entry in entries)
        {
            if (entry == null ||
                entry.item == null ||
                entry.item.worldPrefab == null)
                continue;

            totalWeight += Mathf.Max(0f, entry.weight);
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.Range(0f, totalWeight);

        foreach (var entry in entries)
        {
            if (entry == null ||
                entry.item == null ||
                entry.item.worldPrefab == null)
                continue;

            roll -= Mathf.Max(0f, entry.weight);

            if (roll <= 0f)
                return entry;
        }

        return null;
    }

    private void ShufflePoints()
    {
        for (int i = availablePoints.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            ItemSpawnPoint temp = availablePoints[i];
            availablePoints[i] = availablePoints[j];
            availablePoints[j] = temp;
        }
    }
}