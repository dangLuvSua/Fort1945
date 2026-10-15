
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns the pre-dungeon mission items (2 candles + 1 key)
/// when the soldier reaches the dungeon door.
///
/// Spawn priority:
///   1. MissionItemSpawnArea components (invisible Box Collider areas)
///   2. Manually assigned Transform spawn points
///   3. Scene ItemSpawnPoint markers
///   4. Random NavMesh fallback points
///
/// Only the state authority should call SpawnMissionItems.
/// Each client's instance can independently drive its local mission HUD.
/// </summary>
public class FortMissionItemSpawner : MonoBehaviour
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Items")]
    [Tooltip("Candle ItemData asset players must collect.")]
    [SerializeField] private ItemData candleItem;

    [Tooltip("Key ItemData asset that unlocks the gate.")]
    [SerializeField] private ItemData keyItem;

    [Tooltip("MissionData asset shown in the mission progress panel.")]
    [SerializeField] private MissionData fortMission;

    [Tooltip("Number of candles to spawn.")]
    [SerializeField, Min(1)] private int candleCount = 2;

    [Tooltip("Number of keys to spawn.")]
    [SerializeField, Min(1)] private int keyCount = 1;

    [Header("Spawn Areas")]
    [Tooltip(
        "If enabled spawn areas exist, they take priority over " +
        "markers and NavMesh fallback points."
    )]
    [SerializeField] private bool prioritizeSpawnAreas = true;

    [Tooltip(
        "Maximum attempts to find a valid position within the " +
        "designated spawn areas for each item."
    )]
    [SerializeField, Min(1)] private int spawnAreaAttempts = 30;

    [Header("Spawn Points")]
    [Tooltip("Optional manually assigned spawn markers.")]
    [SerializeField] private Transform[] manualSpawnPoints;

    [Tooltip("Radius used to find fallback NavMesh positions.")]
    [SerializeField, Min(1f)] private float fallbackSpawnRadius = 30f;

    [Tooltip(
        "Maximum vertical distance from the gate or reference " +
        "height allowed for fallback NavMesh points."
    )]
    [SerializeField, Min(0.5f)] private float heightTolerance = 3f;

    [Tooltip("Layers treated as ground for the legacy fallback.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("Raycast distance used by the legacy ground snap.")]
    [SerializeField, Min(1f)] private float groundSnapDistance = 6f;

    [Tooltip("Height above ground for fallback-spawned items.")]
    [SerializeField, Range(0f, 1f)] private float verticalOffset = 0.15f;

    [Header("Reference Heights")]
    [Tooltip(
        "Optional ground Y values for the legacy NavMesh fallback. " +
        "Automatically supplemented by scene markers and existing items."
    )]
    [SerializeField] private float[] manualReferenceHeights;

    // =========================================================
    // INTERNAL STATE
    // =========================================================

    private NetworkRunner runner;

    private readonly List<MissionItemSpawnArea> spawnAreas =
        new List<MissionItemSpawnArea>();

    private readonly List<Transform> spawnMarkers =
        new List<Transform>();

    private readonly List<Vector3> fallbackPoints =
        new List<Vector3>();

    private readonly List<float> referenceHeights =
        new List<float>();

    private bool spawned;

    private int candleItemId;
    private int keyItemId;

    // Local mission-progress tracking.
    private bool missionActive;

    private MissionProgressPanelUI progressPanel;

    private int lastCandleCount = -1;
    private int lastKeyCount = -1;

    public bool IsMissionActive => missionActive;

    // =========================================================
    // SPAWN MISSION ITEMS
    // =========================================================

    /// <summary>
    /// Spawns the required candles and keys.
    /// Call only from the Fusion state authority.
    /// </summary>
    public bool SpawnMissionItems(
      NetworkRunner networkRunner,
      ItemData candle,
      ItemData key,
      int candles,
      int keys,
      float[] referenceHeights)
    {
        // Prevent duplicate spawning.
        if (spawned)
        {
            return true;
        }

        // Validate the runner first.
        if (networkRunner == null)
        {
            Debug.LogError(
                "[FORT SPAWNER] No NetworkRunner. " +
                "Cannot spawn mission items."
            );

            return false;
        }

        // This project uses Fusion Shared Mode: only its Master Client should spawn.
        NetworkGameManager manager = NetworkGameManager.Instance;
        if (manager != null && !manager.IsHost)
        {
            Debug.LogWarning(
                "[FORT SPAWNER] SpawnMissionItems was called on a non-host client. " +
                "The Shared Mode Master Client must spawn mission items.", this);
            return false;
        }

        runner = networkRunner;

        candleItem = candle;
        keyItem = key;

        candleCount = candles;
        keyCount = keys;

        manualReferenceHeights = referenceHeights;

        // Validate item data.
        if (candleItem == null || candleItem.worldPrefab == null)
        {
            Debug.LogError(
                "[FORT SPAWNER] Candle ItemData is missing " +
                "or has no world prefab."
            );

            return false;
        }

        if (keyItem == null || keyItem.worldPrefab == null)
        {
            Debug.LogError(
                "[FORT SPAWNER] Key ItemData is missing " +
                "or has no world prefab."
            );

            return false;
        }

        candleItemId = candleItem.itemId;
        keyItemId = keyItem.itemId;

        // Build the existing spawn pool.
        BuildSpawnPool();

        if (spawnAreas.Count == 0 &&
            spawnMarkers.Count == 0 &&
            fallbackPoints.Count == 0)
        {
            Debug.LogError(
                "[FORT SPAWNER] No spawn positions available. " +
                "Check MissionItemSpawnArea components, " +
                "ItemSpawnPoint markers, and NavMesh."
            );

            return false;
        }

        int candlesSpawned = 0;
        int keysSpawned = 0;

        // Spawn candles.
        for (int i = 0; i < candleCount; i++)
        {
            if (SpawnItemAtRandomPoint(candleItem))
            {
                candlesSpawned++;
            }
        }

        // Spawn keys.
        for (int i = 0; i < keyCount; i++)
        {
            if (SpawnItemAtRandomPoint(keyItem))
            {
                keysSpawned++;
            }
        }

        bool complete =
            candlesSpawned == candleCount &&
            keysSpawned == keyCount;

        Debug.Log(
            $"[FORT SPAWNER] Spawned " +
            $"{candlesSpawned}/{candleCount} candles and " +
            $"{keysSpawned}/{keyCount} keys. " +
            $"Complete: {complete}"
        );

        // Preserve one-time spawning behavior only if items were successfully spawned.
        // If 0 items were spawned, allow retrying.
        if (complete || candlesSpawned > 0 || keysSpawned > 0)
        {
            spawned = true;
        }

        return complete;
    }

    // =========================================================
    // BUILD SPAWN POOL
    // =========================================================

    private void BuildSpawnPool()
    {
        spawnAreas.Clear();
        spawnMarkers.Clear();
        fallbackPoints.Clear();
        referenceHeights.Clear();

        // -----------------------------------------------------
        // 1. Collect designated invisible spawn areas.
        // -----------------------------------------------------

        MissionItemSpawnArea[] sceneAreas =
            Object.FindObjectsByType<MissionItemSpawnArea>(
                FindObjectsInactive.Exclude
            );

        foreach (MissionItemSpawnArea area in sceneAreas)
        {
            if (area == null || !area.isActiveAndEnabled)
            {
                continue;
            }

            spawnAreas.Add(area);

            // Also collect any child spawn markers under this area
            foreach (Transform child in area.transform)
            {
                if (child != null && !spawnMarkers.Contains(child))
                {
                    spawnMarkers.Add(child);
                }
            }
        }

        // -----------------------------------------------------
        // 2. Collect manually assigned markers.
        // -----------------------------------------------------

        if (manualSpawnPoints != null)
        {
            foreach (Transform marker in manualSpawnPoints)
            {
                if (marker != null)
                {
                    spawnMarkers.Add(marker);
                }
            }
        }

        // -----------------------------------------------------
        // 3. Collect scene ItemSpawnPoint markers.
        // -----------------------------------------------------

        ItemSpawnPoint[] sceneMarkers =
            Object.FindObjectsByType<ItemSpawnPoint>(
                FindObjectsInactive.Exclude
            );

        foreach (ItemSpawnPoint marker in sceneMarkers)
        {
            if (marker == null || !marker.canSpawnItems)
            {
                continue;
            }

            // Avoid adding the same Transform twice.
            if (!spawnMarkers.Contains(marker.transform))
            {
                spawnMarkers.Add(marker.transform);
            }
        }

        // Check for "ItemSpawnSections" or transforms named "ItemSpawn" in the scene
        GameObject itemSpawnSections = GameObject.Find("ItemSpawnSections");
        if (itemSpawnSections != null)
        {
            Transform[] sectionChildren = itemSpawnSections.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in sectionChildren)
            {
                if (t != null && t.name.IndexOf("ItemSpawn", System.StringComparison.OrdinalIgnoreCase) >= 0 && t.name != "ItemSpawnSections")
                {
                    if (!spawnMarkers.Contains(t))
                    {
                        spawnMarkers.Add(t);
                    }
                }
            }
        }

        CollectReferenceHeights();

        Shuffle(spawnMarkers);

        // -----------------------------------------------------
        // 4. Build legacy NavMesh fallback points.
        // -----------------------------------------------------

        // These points are retained for backward compatibility.
        // They are not used when spawn areas or markers are available.

        GameObject zoneObject =
            GameObject.Find("DungeonEntryZone");

        Vector3 center =
            zoneObject != null
                ? zoneObject.transform.position
                : Vector3.zero;

        float gateHeight = center.y;

        if (zoneObject == null)
        {
            Debug.LogWarning(
                "[FORT SPAWNER] DungeonEntryZone not found. " +
                "Using world origin as fallback center."
            );
        }

        int attemptsLeft = 100;

        while (fallbackPoints.Count < 8 && attemptsLeft > 0)
        {
            attemptsLeft--;

            Vector2 offset =
                Random.insideUnitCircle * fallbackSpawnRadius;

            Vector3 sample =
                center + new Vector3(offset.x, 0f, offset.y);

            if (!NavMesh.SamplePosition(
                    sample,
                    out NavMeshHit hit,
                    fallbackSpawnRadius,
                    NavMesh.AllAreas))
            {
                continue;
            }

            // Keep the original reference-height restrictions.
            if (!IsNearReferenceHeight(hit.position.y) &&
                Mathf.Abs(hit.position.y - gateHeight) >
                heightTolerance)
            {
                continue;
            }

            Vector3 spawn = hit.position;
            spawn.y = ResolveSpawnHeight(hit.position);

            fallbackPoints.Add(spawn);
        }

        Debug.Log(
            $"[FORT SPAWNER] Spawn pool built: " +
            $"{spawnAreas.Count} spawn areas, " +
            $"{spawnMarkers.Count} markers, " +
            $"{fallbackPoints.Count} fallback positions."
        );
    }

    // =========================================================
    // REFERENCE HEIGHTS
    // =========================================================

    private void CollectReferenceHeights()
    {
        if (manualReferenceHeights != null)
        {
            foreach (float height in manualReferenceHeights)
            {
                AddReferenceHeight(height);
            }
        }

        foreach (Transform marker in spawnMarkers)
        {
            if (marker != null)
            {
                AddReferenceHeight(marker.position.y);
            }
        }

        CollectItemPickupHeights<ItemPickup>();
        CollectItemPickupHeights<KeyPickup>();
        CollectItemPickupHeights<NetworkedDroppedItem>();
    }

    private void CollectItemPickupHeights<T>()
        where T : Component
    {
        T[] items =
            Object.FindObjectsByType<T>(
                FindObjectsInactive.Exclude
            );

        foreach (T item in items)
        {
            if (item != null)
            {
                AddReferenceHeight(item.transform.position.y);
            }
        }
    }

    private void AddReferenceHeight(float height)
    {
        if (!referenceHeights.Contains(height))
        {
            referenceHeights.Add(height);
        }
    }

    private bool IsNearReferenceHeight(float y)
    {
        foreach (float height in referenceHeights)
        {
            if (Mathf.Abs(y - height) <= heightTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private float ResolveSpawnHeight(Vector3 position)
    {
        // Legacy fallback: snap to the nearest reference height.
        float bestHeight = position.y;
        float bestDelta = float.MaxValue;

        foreach (float height in referenceHeights)
        {
            float delta = Mathf.Abs(position.y - height);

            if (delta <= heightTolerance && delta < bestDelta)
            {
                bestDelta = delta;
                bestHeight = height;
            }
        }

        if (bestDelta <= heightTolerance)
        {
            return bestHeight + verticalOffset;
        }

        Vector3 snapped;

        if (TrySnapToGround(position, out snapped))
        {
            return snapped.y;
        }

        return position.y + verticalOffset;
    }

    private bool TrySnapToGround(
        Vector3 origin,
        out Vector3 groundPoint)
    {
        groundPoint = origin;

        Vector3 rayOrigin =
            origin + Vector3.up * (groundSnapDistance * 0.5f);

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundSnapDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            groundPoint =
                hit.point + Vector3.up * verticalOffset;

            return true;
        }

        return false;
    }

    // =========================================================
    // RANDOM ITEM SPAWNING
    // =========================================================

    private bool SpawnItemAtRandomPoint(ItemData itemData)
    {
        // -----------------------------------------------------
        // PRIORITY 1: Invisible spawn areas.
        // -----------------------------------------------------

        if (prioritizeSpawnAreas && spawnAreas.Count > 0)
        {
            for (int attempt = 0;
                 attempt < spawnAreaAttempts;
                 attempt++)
            {
                int index = Random.Range(0, spawnAreas.Count);

                MissionItemSpawnArea area = spawnAreas[index];

                if (area == null || !area.isActiveAndEnabled)
                {
                    continue;
                }

                if (!area.TryGetRandomSpawnPosition(
                        out Vector3 position))
                {
                    continue;
                }

                Quaternion rotation = Quaternion.Euler(
                    0f,
                    Random.Range(0f, 360f),
                    0f
                );

                if (SpawnItem(itemData, position, rotation))
                {
                    return true;
                }
            }

            Debug.LogWarning(
                $"[FORT SPAWNER] Could not find a valid position " +
                $"in any MissionItemSpawnArea for " +
                $"{itemData.itemName}. Check area placement, " +
                $"collider bounds, and ground layer settings. " +
                "Falling back to markers or NavMesh."
            );
        }

        // -----------------------------------------------------
        // PRIORITY 2: Manually assigned / scene markers.
        // -----------------------------------------------------

        while (spawnMarkers.Count > 0)
        {
            int index = Random.Range(0, spawnMarkers.Count);

            Transform marker = spawnMarkers[index];

            spawnMarkers.RemoveAt(index);

            if (marker == null)
            {
                continue;
            }

            if (SpawnItem(
                itemData,
                marker.position,
                marker.rotation
            ))
            {
                return true;
            }
        }

        // -----------------------------------------------------
        // PRIORITY 3: NavMesh fallback.
        // -----------------------------------------------------

        while (fallbackPoints.Count > 0)
        {
            int index = Random.Range(0, fallbackPoints.Count);

            Vector3 position = fallbackPoints[index];

            fallbackPoints.RemoveAt(index);

            if (SpawnItem(
                itemData,
                position,
                Quaternion.identity
            ))
            {
                return true;
            }
        }

        // -----------------------------------------------------
        // PRIORITY 4: Emergency fallback near DungeonEntryZone
        // -----------------------------------------------------

        GameObject zoneObject =
            GameObject.Find("DungeonEntryZone");

        Vector3 emergencyOrigin =
            zoneObject != null
                ? zoneObject.transform.position
                : transform.position;

        if (NavMesh.SamplePosition(
                emergencyOrigin,
                out NavMeshHit navHit,
                30f,
                NavMesh.AllAreas))
        {
            if (SpawnItem(
                itemData,
                navHit.position + Vector3.up * verticalOffset,
                Quaternion.identity
            ))
            {
                return true;
            }
        }

        Debug.LogWarning(
            $"[FORT SPAWNER] No remaining spawn position for " +
            $"{itemData.itemName}."
        );

        return false;
    }

    // =========================================================
    // NETWORKED ITEM CREATION
    // =========================================================

    private bool SpawnItem(
        ItemData itemData,
        Vector3 position,
        Quaternion rotation)
    {
        if (runner == null)
        {
            Debug.LogError(
                "[FORT SPAWNER] Cannot spawn item: " +
                "NetworkRunner is missing."
            );

            return false;
        }

        NetworkGameManager manager = NetworkGameManager.Instance;
        if (manager != null && !manager.IsHost)
        {
            Debug.LogError(
                "[FORT SPAWNER] Only the Shared Mode Master Client may spawn networked mission items.", this);
            return false;
        }

        if (itemData == null || itemData.worldPrefab == null)
        {
            Debug.LogError(
                "[FORT SPAWNER] ItemData or world prefab is missing."
            );

            return false;
        }

        if (itemData.worldPrefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError(
                $"[FORT SPAWNER] Prefab for '{itemData.itemName}' has no NetworkObject. " +
                "Add NetworkObject and register the prefab in Fusion.", itemData.worldPrefab);
            return false;
        }

        NetworkObject spawnedObject =
            runner.Spawn(
                itemData.worldPrefab,
                position,
                rotation
            );

        if (spawnedObject == null)
        {
            Debug.LogWarning(
                $"[FORT SPAWNER] Failed to spawn " +
                $"{itemData.itemName} at {position}."
            );

            return false;
        }

        NetworkedDroppedItem droppedItem =
            spawnedObject.GetComponent<NetworkedDroppedItem>();

        if (droppedItem != null)
        {
            droppedItem.Initialize(itemData.itemId);
        }

        ItemPickup pickup =
            spawnedObject.GetComponent<ItemPickup>();

        if (pickup != null)
        {
            pickup.Initialize(itemData.itemId);
        }

        Debug.Log(
            $"[FORT SPAWNER] Spawned {itemData.itemName} " +
            $"at {position}."
        );

        return true;
    }

    // =========================================================
    // MISSION PROGRESS
    // =========================================================

    public void SetMissionActive(bool active)
    {
        missionActive = active;

        if (!active)
        {
            if (progressPanel != null)
            {
                progressPanel.Hide();
            }

            return;
        }

        EnsureProgressPanel();
    }

    /// <summary>Called after the player acknowledges the mission briefing.</summary>
    public void BeginProgressTracking()
    {
        if (missionActive)
            return;

        missionActive = true;
        EnsureProgressPanel();
    }

    /// <summary>
    /// Configures the mission and required items on each client.
    /// </summary>
    public void ConfigureFortMission(
        MissionData mission,
        ItemData candle,
        ItemData key,
        int candles,
        int keys,
        float[] referenceHeights)
    {
        fortMission = mission;
        candleItem = candle;
        keyItem = key;

        candleCount = candles;
        keyCount = keys;

        manualReferenceHeights = referenceHeights;

        candleItemId = candle != null ? candle.itemId : 0;
        keyItemId = key != null ? key.itemId : 0;
    }

    private void Update()
    {
        if (!missionActive)
        {
            return;
        }

        // Hide the panel after the dungeon gate is unlocked.
        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager != null &&
            manager.IsNetworkStateReady &&
            manager.DungeonGateUnlocked)
        {
            SetMissionActive(false);
            return;
        }

        UpdateProgress(false);
    }

    private void UpdateProgress(bool force)
    {
        PlayerInventory inventory =
            GetLocalPlayerInventory();
        NetworkGameManager manager = NetworkGameManager.Instance;
        bool hasTeamNetwork = manager != null && manager.IsNetworkStateReady;

        // In a network session, mission progress is the combined quantity
        // carried by every active player's networked inventory. A local
        // inventory reference is only needed for offline fallback mode.
        if (!hasTeamNetwork && inventory == null)
            return;

        int candleCountOwned = hasTeamNetwork
            ? manager.GetTeamItemCount(candleItemId)
            : inventory.GetQuantityForItemId(candleItemId);

        int keyCountOwned = hasTeamNetwork
            ? manager.GetTeamItemCount(keyItemId)
            : inventory.GetQuantityForItemId(keyItemId);

        if (!force &&
            candleCountOwned == lastCandleCount &&
            keyCountOwned == lastKeyCount)
        {
            return;
        }

        lastCandleCount = candleCountOwned;
        lastKeyCount = keyCountOwned;

        if (progressPanel != null)
        {
            if (candleItemId > 0)
            {
                progressPanel.SetProgress(
                    candleItemId,
                    candleCountOwned,
                    candleCount
                );
            }

            if (keyItemId > 0)
            {
                progressPanel.SetProgress(
                    keyItemId,
                    keyCountOwned,
                    keyCount
                );
            }

            return;
        }

        // Fallback: text-based mission panel.
        MissionManager.ShowRaw(
            GetMissionTitle(),
            GetMissionDescription(
                candleCountOwned,
                keyCountOwned
            ),
            0f
        );
    }

    private void EnsureProgressPanel()
    {
        if (candleItem == null || keyItem == null || fortMission == null)
        {
            NetworkGameManager mg = NetworkGameManager.Instance;
            if (mg != null)
            {
                if (candleItem == null) candleItem = mg.CandleItem;
                if (keyItem == null) keyItem = mg.KeyItem;
                if (fortMission == null) fortMission = mg.FortMission;
            }
        }

        if (candleItemId == 0 && candleItem != null)
        {
            candleItemId = candleItem.itemId;
        }

        if (keyItemId == 0 && keyItem != null)
        {
            keyItemId = keyItem.itemId;
        }

        if (progressPanel == null)
        {
            progressPanel =
                MissionProgressPanelUI.Create();
        }

        if (progressPanel == null)
        {
            lastCandleCount = -1;
            lastKeyCount = -1;

            UpdateProgress(true);

            return;
        }

        MissionManager.Hide();

        progressPanel.SetMission(GetMissionTitle());

        if (candleItem != null)
        {
            progressPanel.AddItemRow(
                candleItem,
                candleCount
            );
        }

        if (keyItem != null)
        {
            progressPanel.AddItemRow(
                keyItem,
                keyCount
            );
        }

        progressPanel.Show(GetMissionTitle());

        lastCandleCount = -1;
        lastKeyCount = -1;

        UpdateProgress(true);
    }

    private string GetMissionTitle()
    {
        return fortMission != null
            ? fortMission.title
            : "FIND 2 CANDLES AND 1 KEY";
    }

    private string GetMissionDescription(
        int candles,
        int keys)
    {
        if (fortMission != null)
        {
            return
                fortMission.description +
                $"\n\nCandles: {candles}/{candleCount} | " +
                $"Key: {keys}/{keyCount}";
        }

        return
            $"Candles: {candles}/{candleCount}\n" +
            $"Key: {keys}/{keyCount}\n\n" +
            "Equip the key, then hold [E] at the dungeon gate " +
            "to unlock it.";
    }

    // =========================================================
    // RUNNER AND INVENTORY HELPERS
    // =========================================================

    private NetworkRunner GetRunner()
    {
        if (runner != null)
        {
            return runner;
        }

        // The component may be added to NetworkGameManager at runtime.
        NetworkGameManager manager =
            GetComponent<NetworkGameManager>();

        if (manager != null && manager.Runner != null)
        {
            runner = manager.Runner;
        }

        return runner;
    }

    private PlayerInventory GetLocalPlayerInventory()
    {
        NetworkRunner currentRunner = GetRunner();

        if (currentRunner == null)
        {
            return null;
        }

        PlayerRef localPlayer = currentRunner.LocalPlayer;

        if (!localPlayer.IsValid)
        {
            return null;
        }

        if (!currentRunner.TryGetPlayerObject(
                localPlayer,
                out NetworkObject playerObject))
        {
            return null;
        }

        return playerObject != null
            ? playerObject.GetComponent<PlayerInventory>()
            : null;
    }

    // =========================================================
    // UTILITY
    // =========================================================

    private void Shuffle(List<Transform> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            Transform temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
