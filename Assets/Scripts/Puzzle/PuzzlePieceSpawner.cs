using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class PuzzlePieceSpawner : NetworkBehaviour
{
    [Serializable]
    public class PuzzleDifficultySet
    {
        public GameDifficulty difficulty;

        [Tooltip("Every puzzle piece that must spawn for this difficulty.")]
        public ItemData[] pieces;
    }

    // =========================================================
    // PUZZLE SETS
    // =========================================================

    [Header("Puzzle Sets")]
    [SerializeField]
    private PuzzleDifficultySet[] puzzleSets;

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    [SerializeField]
    private DungeonGenerator dungeonGenerator;

    // =========================================================
    // SPAWN SETTINGS
    // =========================================================

    [Header("Spawn Settings")]

    [SerializeField]
    private float verticalOffset = 0.005f;

    [Tooltip("How high above the spawn point the floor ray starts.")]
    [SerializeField]
    private float floorRayStartHeight = 3f;

    [Tooltip("How far downward to search for the floor.")]
    [SerializeField]
    private float floorRayDistance = 10f;

    [Tooltip("Minimum distance between spawned puzzle pieces.")]
    [SerializeField]
    private float minDistanceBetweenPieces = 8f;

    [Tooltip(
        "How many spawn points to test while trying to maintain " +
        "distance between puzzle pieces."
    )]
    [SerializeField]
    private int maxSpawnPointAttempts = 30;

    // =========================================================
    // OBSTACLE AVOIDANCE
    // =========================================================

    [Header("Obstacle Avoidance")]

    [Tooltip(
        "Layers treated as obstacles. Everything is recommended " +
        "unless you have a dedicated environment layer setup."
    )]
    [SerializeField]
    private LayerMask obstacleMask = ~0;

    [Tooltip(
        "Free horizontal area required around the puzzle piece."
    )]
    [SerializeField]
    private float clearSpaceRadius = 0.4f;

    [Tooltip(
        "Free vertical area required immediately above the puzzle piece."
    )]
    [SerializeField]
    private float clearSpaceHeight = 1.1f;

    [Tooltip(
        "Keeps the free-space test slightly above the floor so " +
        "the floor collider does not block the spawn."
    )]
    [SerializeField]
    private float clearSpaceBottomOffset = 0.05f;

    [Tooltip(
        "Maximum difference between the intended walkable Y and " +
        "the physical floor Y."
    )]
    [SerializeField]
    private float maxFloorHeightDifference = 0.6f;

    [Tooltip(
        "Minimum upward-facing normal for a surface to count as floor."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float minimumFloorNormalY = 0.7f;

    // =========================================================
    // PLAYER REACHABILITY
    // =========================================================

    [Header("Player Reachability")]

    [Tooltip(
        "How far around an ItemSpawnPoint to search for a nearby " +
        "walkable NavMesh position."
    )]
    [SerializeField]
    private float navMeshSearchRadius = 1.25f;

    [Tooltip(
        "How far NavMesh.SamplePosition may search around each tested position."
    )]
    [SerializeField]
    private float navMeshSampleDistance = 0.4f;

    [Tooltip(
        "How many nearby positions are attempted around each ItemSpawnPoint."
    )]
    [SerializeField]
    private int navMeshAttemptsPerPoint = 12;

    [Tooltip(
        "Minimum distance from a NavMesh edge. Prevents spawning " +
        "inside tiny cracks beside furniture or walls."
    )]
    [SerializeField]
    private float minimumWalkableRadius = 0.65f;

    [Tooltip(
        "Approximate radius required for the player to stand beside the item."
    )]
    [SerializeField]
    private float playerClearanceRadius = 0.5f;

    [Tooltip(
        "Approximate player standing height required at the item location."
    )]
    [SerializeField]
    private float playerClearanceHeight = 1.7f;

    [Tooltip(
        "Print rejected spawn locations and the objects blocking them."
    )]
    [SerializeField]
    private bool logRejectedSpawnPoints = false;

    // =========================================================
    // STANDALONE TESTING
    // =========================================================

    [Header("Standalone Testing")]

    [SerializeField]
    private bool allowStandaloneTesting = true;

    [SerializeField]
    private GameDifficulty standaloneDifficulty =
        GameDifficulty.Normal;

    // =========================================================
    // RUNTIME
    // =========================================================

    private readonly List<ItemSpawnPoint> availablePoints =
        new List<ItemSpawnPoint>();

    private readonly List<Vector3> usedPuzzlePositions =
        new List<Vector3>();

    private bool dungeonReady;
    private bool puzzleSpawned;
    private bool isSubscribed;

    // =========================================================
    // FUSION SPAWN
    // =========================================================

    public override void Spawned()
    {
        Subscribe();

        if (dungeonGenerator == null)
        {
            dungeonGenerator =
                FindFirstObjectByType<DungeonGenerator>();
        }

        if (dungeonGenerator != null &&
            dungeonGenerator.IsGenerated)
        {
            HandleDungeonGenerated(
                dungeonGenerator
            );
        }

        Debug.Log(
            $"[PUZZLE SPAWNER] Spawned. " +
            $"State Authority = {Object.HasStateAuthority}"
        );
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        Unsubscribe();
    }

    // =========================================================
    // DUNGEON EVENTS
    // =========================================================

    private void Subscribe()
    {
        if (isSubscribed)
            return;

        DungeonGenerator.OnDungeonGenerated +=
            HandleDungeonGenerated;

        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed)
            return;

        DungeonGenerator.OnDungeonGenerated -=
            HandleDungeonGenerated;

        isSubscribed = false;
    }

    private void HandleDungeonGenerated(
        DungeonGenerator generator)
    {
        if (generator == null)
            return;

        if (dungeonGenerator != null &&
            generator != dungeonGenerator)
        {
            return;
        }

        dungeonGenerator =
            generator;

        availablePoints.Clear();
        usedPuzzlePositions.Clear();

        ItemSpawnPoint[] points =
            generator.GetComponentsInChildren<ItemSpawnPoint>(
                true
            );

        foreach (ItemSpawnPoint point in points)
        {
            if (point == null)
                continue;

            if (!point.canSpawnItems)
                continue;

            if (!point.gameObject.activeInHierarchy)
                continue;

            availablePoints.Add(
                point
            );
        }

        Shuffle(
            availablePoints
        );

        dungeonReady = true;
        puzzleSpawned = false;

        Debug.Log(
            $"[PUZZLE SPAWNER] Dungeon ready. " +
            $"Found {availablePoints.Count} spawn points."
        );
    }

    // =========================================================
    // NETWORK UPDATE
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        if (puzzleSpawned)
            return;

        if (!dungeonReady)
            return;

        if (Object == null ||
            !Object.HasStateAuthority)
        {
            return;
        }

        if (Runner == null ||
            !Runner.IsRunning)
        {
            return;
        }

        // -----------------------------------------------------
        // WAIT FOR NAVMESH
        // -----------------------------------------------------

        if (dungeonGenerator == null ||
            !dungeonGenerator.NavMeshReady)
        {
            return;
        }

        GameDifficulty difficulty;

        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager != null &&
            manager.IsNetworkStateReady)
        {
            difficulty =
                manager.Difficulty;
        }
        else if (allowStandaloneTesting)
        {
            difficulty =
                standaloneDifficulty;
        }
        else
        {
            return;
        }

        PuzzleDifficultySet set =
            GetSet(
                difficulty
            );

        if (set == null ||
            set.pieces == null ||
            set.pieces.Length == 0)
        {
            Debug.LogWarning(
                $"[PUZZLE SPAWNER] No puzzle configured " +
                $"for {difficulty}."
            );

            return;
        }

        if (availablePoints.Count <
            set.pieces.Length)
        {
            Debug.LogError(
                $"[PUZZLE SPAWNER] Not enough spawn points. " +
                $"Need {set.pieces.Length}, " +
                $"found {availablePoints.Count}."
            );

            return;
        }

        puzzleSpawned =
            SpawnPuzzleSet(
                set
            );
    }

    // =========================================================
    // GET DIFFICULTY SET
    // =========================================================

    private PuzzleDifficultySet GetSet(
        GameDifficulty difficulty)
    {
        if (puzzleSets == null)
            return null;

        foreach (PuzzleDifficultySet set
                 in puzzleSets)
        {
            if (set != null &&
                set.difficulty == difficulty)
            {
                return set;
            }
        }

        return null;
    }

    // =========================================================
    // SPAWN COMPLETE PUZZLE
    // =========================================================

    private bool SpawnPuzzleSet(
        PuzzleDifficultySet set)
    {
        HashSet<int> spawnedIds =
            new HashSet<int>();

        int successful =
            0;

        foreach (ItemData piece in set.pieces)
        {
            if (piece == null)
            {
                Debug.LogError(
                    "[PUZZLE SPAWNER] Missing ItemData."
                );

                return false;
            }

            if (piece.itemId <= 0)
            {
                Debug.LogError(
                    $"[PUZZLE SPAWNER] " +
                    $"{piece.name} has invalid Item ID."
                );

                return false;
            }

            if (!spawnedIds.Add(
                    piece.itemId))
            {
                Debug.LogError(
                    $"[PUZZLE SPAWNER] Duplicate Item ID: " +
                    $"{piece.itemId}"
                );

                return false;
            }

            if (piece.worldPrefab == null)
            {
                Debug.LogError(
                    $"[PUZZLE SPAWNER] " +
                    $"{piece.itemName} has no World Prefab."
                );

                return false;
            }

            if (!SpawnPiece(
                    piece))
            {
                Debug.LogError(
                    $"[PUZZLE SPAWNER] Could not find " +
                    $"a safe reachable spawn position for " +
                    $"{piece.itemName}."
                );

                return false;
            }

            successful++;
        }

        Debug.Log(
            $"[PUZZLE SPAWNER] SUCCESS: " +
            $"{successful}/{set.pieces.Length} pieces spawned."
        );

        return successful ==
               set.pieces.Length;
    }

    // =========================================================
    // SPAWN SINGLE PIECE
    // =========================================================

    private bool SpawnPiece(
        ItemData piece)
    {
        if (availablePoints.Count == 0)
            return false;

        ItemSpawnPoint point;
        int selectedIndex;
        Vector3 spawnPosition;
        float floorY;

        // -----------------------------------------------------
        // PASS 1
        // Safe location + desired piece spacing.
        // -----------------------------------------------------

        bool found =
            TryFindSafeSpawnPoint(
                true,
                out point,
                out selectedIndex,
                out spawnPosition,
                out floorY
            );

        // -----------------------------------------------------
        // PASS 2
        // Relax ONLY puzzle-to-puzzle spacing.
        //
        // NavMesh, obstacle and player-clearance checks remain.
        // -----------------------------------------------------

        if (!found)
        {
            Debug.LogWarning(
                $"[PUZZLE SPAWNER] Could not maintain " +
                $"{minDistanceBetweenPieces} units of spacing for " +
                $"{piece.itemName}. Trying another SAFE point " +
                $"with relaxed piece spacing."
            );

            found =
                TryFindSafeSpawnPoint(
                    false,
                    out point,
                    out selectedIndex,
                    out spawnPosition,
                    out floorY
                );
        }

        if (!found ||
            point == null)
        {
            Debug.LogError(
                $"[PUZZLE SPAWNER] No reachable free-space " +
                $"spawn location found for {piece.itemName}."
            );

            return false;
        }

        Quaternion spawnRotation =
            Quaternion.Euler(
                0f,
                UnityEngine.Random.Range(
                    0f,
                    360f
                ),
                0f
            );

        // =====================================================
        // FUSION SPAWN
        // =====================================================

        NetworkObject spawned =
            Runner.Spawn(
                piece.worldPrefab,
                spawnPosition,
                spawnRotation,
                PlayerRef.None,

                (runner, obj) =>
                {
                    obj.transform.SetPositionAndRotation(
                        spawnPosition,
                        spawnRotation
                    );

                    Renderer[] renderers =
                        obj.GetComponentsInChildren<Renderer>(
                            true
                        );

                    if (renderers == null ||
                        renderers.Length == 0)
                    {
                        Debug.LogWarning(
                            $"[PUZZLE SPAWNER] " +
                            $"{piece.itemName} has no Renderer."
                        );

                        return;
                    }

                    float lowestY =
                        float.PositiveInfinity;

                    foreach (Renderer renderer
                             in renderers)
                    {
                        if (renderer == null)
                            continue;

                        lowestY =
                            Mathf.Min(
                                lowestY,
                                renderer.bounds.min.y
                            );
                    }

                    if (float.IsInfinity(
                            lowestY))
                    {
                        return;
                    }

                    float correction =
                        floorY -
                        lowestY +
                        verticalOffset;

                    obj.transform.position +=
                        Vector3.up *
                        correction;
                }
            );

        if (spawned == null)
        {
            Debug.LogError(
                $"[PUZZLE SPAWNER] Failed to spawn " +
                $"{piece.itemName}."
            );

            return false;
        }

        // -----------------------------------------------------
        // CONSUME SPAWN POINT
        // -----------------------------------------------------

        if (selectedIndex >= 0 &&
            selectedIndex < availablePoints.Count &&
            availablePoints[selectedIndex] == point)
        {
            availablePoints.RemoveAt(
                selectedIndex
            );
        }
        else
        {
            availablePoints.Remove(
                point
            );
        }

        usedPuzzlePositions.Add(
            spawned.transform.position
        );

        // =====================================================
        // INITIALIZE ITEM
        // =====================================================

        NetworkedDroppedItem dropped =
            spawned.GetComponent<NetworkedDroppedItem>();

        if (dropped != null)
        {
            dropped.Initialize(
                piece.itemId
            );
        }

        ItemPickup pickup =
            spawned.GetComponent<ItemPickup>();

        if (pickup != null)
        {
            pickup.Initialize(
                piece.itemId
            );
        }

        Debug.Log(
            $"[PUZZLE SPAWNER] Spawned " +
            $"{piece.itemName} at " +
            $"{spawned.transform.position}"
        );

        return true;
    }

    // =========================================================
    // FIND SAFE SPAWN POINT
    // =========================================================

    private bool TryFindSafeSpawnPoint(
        bool requirePieceSpacing,
        out ItemSpawnPoint selectedPoint,
        out int selectedIndex,
        out Vector3 spawnPosition,
        out float floorY)
    {
        selectedPoint = null;
        selectedIndex = -1;
        spawnPosition = Vector3.zero;
        floorY = 0f;

        if (availablePoints.Count == 0)
            return false;

        List<int> indexes =
            new List<int>();

        for (int i = 0;
             i < availablePoints.Count;
             i++)
        {
            indexes.Add(
                i
            );
        }

        Shuffle(
            indexes
        );

        int checks;

        if (requirePieceSpacing)
        {
            checks =
                Mathf.Min(
                    indexes.Count,
                    Mathf.Max(
                        1,
                        maxSpawnPointAttempts
                    )
                );
        }
        else
        {
            // When spacing is relaxed, scan ALL remaining
            // points, but still require safety/reachability.
            checks =
                indexes.Count;
        }

        for (int i = 0;
             i < checks;
             i++)
        {
            int index =
                indexes[i];

            if (index < 0 ||
                index >= availablePoints.Count)
            {
                continue;
            }

            ItemSpawnPoint candidate =
                availablePoints[index];

            if (candidate == null)
                continue;

            if (!TryResolveSafePosition(
                    candidate,
                    out Vector3 candidatePosition,
                    out float candidateFloorY))
            {
                continue;
            }

            if (requirePieceSpacing &&
                !IsFarEnoughFromOtherPieces(
                    candidatePosition))
            {
                continue;
            }

            selectedPoint =
                candidate;

            selectedIndex =
                index;

            spawnPosition =
                candidatePosition;

            floorY =
                candidateFloorY;

            return true;
        }

        return false;
    }

    // =========================================================
    // RESOLVE SAFE + REACHABLE POSITION
    // =========================================================

    private bool TryResolveSafePosition(
        ItemSpawnPoint point,
        out Vector3 spawnPosition,
        out float floorY)
    {
        spawnPosition =
            Vector3.zero;

        floorY =
            0f;

        if (point == null)
            return false;

        int attempts =
            Mathf.Max(
                1,
                navMeshAttemptsPerPoint
            );

        // -----------------------------------------------------
        // SEARCH NEAR THE SPAWN MARKER
        // -----------------------------------------------------

        for (int attempt = 0;
             attempt < attempts;
             attempt++)
        {
            Vector3 probePosition =
                point.Position;

            // First attempt = exact ItemSpawnPoint.
            // Remaining attempts search nearby free space.
            if (attempt > 0)
            {
                Vector2 randomOffset =
                    UnityEngine.Random.insideUnitCircle *
                    navMeshSearchRadius;

                probePosition +=
                    new Vector3(
                        randomOffset.x,
                        0f,
                        randomOffset.y
                    );
            }

            // =================================================
            // MUST BE ON NAVMESH
            // =================================================

            if (!NavMesh.SamplePosition(
                    probePosition,
                    out NavMeshHit navHit,
                    navMeshSampleDistance,
                    NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 walkablePosition =
                navHit.position;

            // =================================================
            // DON'T SPAWN NEXT TO NAVMESH EDGE
            // =================================================

            if (NavMesh.FindClosestEdge(
                    walkablePosition,
                    out NavMeshHit edgeHit,
                    NavMesh.AllAreas))
            {
                if (edgeHit.distance <
                    minimumWalkableRadius)
                {
                    if (logRejectedSpawnPoints)
                    {
                        Debug.Log(
                            $"[PUZZLE SPAWNER] Rejected " +
                            $"{point.name}: too close to NavMesh edge " +
                            $"({edgeHit.distance:F2})."
                        );
                    }

                    continue;
                }
            }

            // =================================================
            // FIND PHYSICAL FLOOR
            // =================================================

            Vector3 rayStart =
                walkablePosition +
                Vector3.up *
                floorRayStartHeight;

            RaycastHit[] hits =
                Physics.RaycastAll(
                    rayStart,
                    Vector3.down,
                    floorRayDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore
                );

            if (hits == null ||
                hits.Length == 0)
            {
                continue;
            }

            bool foundFloor =
                false;

            RaycastHit bestHit =
                default;

            float bestDifference =
                float.PositiveInfinity;

            foreach (RaycastHit hit in hits)
            {
                // Reject walls / steep surfaces.
                if (hit.normal.y <
                    minimumFloorNormalY)
                {
                    continue;
                }

                float difference =
                    Mathf.Abs(
                        hit.point.y -
                        walkablePosition.y
                    );

                if (difference >
                    maxFloorHeightDifference)
                {
                    continue;
                }

                if (difference <
                    bestDifference)
                {
                    bestDifference =
                        difference;

                    bestHit =
                        hit;

                    foundFloor =
                        true;
                }
            }

            if (!foundFloor)
                continue;

            floorY =
                bestHit.point.y;

            Vector3 candidatePosition =
                new Vector3(
                    walkablePosition.x,
                    floorY,
                    walkablePosition.z
                );

            // =================================================
            // PIECE FREE SPACE
            // =================================================

            if (!HasClearSpace(
                    point,
                    candidatePosition))
            {
                continue;
            }

            // =================================================
            // PLAYER-SIZED FREE SPACE
            // =================================================

            if (!HasPlayerClearance(
                    point,
                    candidatePosition))
            {
                continue;
            }

            spawnPosition =
                candidatePosition;

            Debug.DrawLine(
                rayStart,
                bestHit.point,
                Color.green,
                5f
            );

            return true;
        }

        RejectPoint(
            point,
            "no reachable safe position nearby"
        );

        return false;
    }

    // =========================================================
    // PIECE CLEARANCE
    // =========================================================

    private bool HasClearSpace(
        ItemSpawnPoint point,
        Vector3 floorPosition)
    {
        float radius =
            Mathf.Max(
                0.05f,
                clearSpaceRadius
            );

        float height =
            Mathf.Max(
                0.1f,
                clearSpaceHeight
            );

        float bottomOffset =
            Mathf.Max(
                0.01f,
                clearSpaceBottomOffset
            );

        Vector3 center =
            floorPosition +
            Vector3.up *
            (
                bottomOffset +
                height * 0.5f
            );

        Vector3 halfExtents =
            new Vector3(
                radius,
                height * 0.5f,
                radius
            );

        Collider[] overlaps =
            Physics.OverlapBox(
                center,
                halfExtents,
                Quaternion.identity,
                obstacleMask,
                QueryTriggerInteraction.Ignore
            );

        if (overlaps == null ||
            overlaps.Length == 0)
        {
            return true;
        }

        foreach (Collider overlap in overlaps)
        {
            if (overlap == null)
                continue;

            if (ShouldIgnoreCollider(
                    point,
                    overlap,
                    floorPosition))
            {
                continue;
            }

            if (logRejectedSpawnPoints)
            {
                Debug.Log(
                    $"[PUZZLE SPAWNER] Rejected {point.name}. " +
                    $"Piece space blocked by: {overlap.name}"
                );
            }

            return false;
        }

        return true;
    }

    // =========================================================
    // PLAYER CLEARANCE
    // =========================================================

    private bool HasPlayerClearance(
        ItemSpawnPoint point,
        Vector3 floorPosition)
    {
        float radius =
            Mathf.Max(
                0.1f,
                playerClearanceRadius
            );

        float height =
            Mathf.Max(
                radius * 2f,
                playerClearanceHeight
            );

        float bottomGap =
            0.08f;

        Vector3 bottomSphere =
            floorPosition +
            Vector3.up *
            (
                bottomGap +
                radius
            );

        Vector3 topSphere =
            floorPosition +
            Vector3.up *
            (
                bottomGap +
                height -
                radius
            );

        Collider[] overlaps =
            Physics.OverlapCapsule(
                bottomSphere,
                topSphere,
                radius,
                obstacleMask,
                QueryTriggerInteraction.Ignore
            );

        if (overlaps == null ||
            overlaps.Length == 0)
        {
            return true;
        }

        foreach (Collider overlap in overlaps)
        {
            if (overlap == null)
                continue;

            if (ShouldIgnoreCollider(
                    point,
                    overlap,
                    floorPosition))
            {
                continue;
            }

            if (logRejectedSpawnPoints)
            {
                Debug.Log(
                    $"[PUZZLE SPAWNER] Rejected {point.name}. " +
                    $"Player clearance blocked by: " +
                    $"{overlap.name}"
                );
            }

            return false;
        }

        return true;
    }

    // =========================================================
    // COLLIDER FILTER
    // =========================================================

    private bool ShouldIgnoreCollider(
        ItemSpawnPoint point,
        Collider overlap,
        Vector3 floorPosition)
    {
        if (overlap == null)
            return true;

        // -----------------------------------------------------
        // IGNORE SPAWN MARKER ITSELF
        // -----------------------------------------------------

        if (point != null)
        {
            Transform overlapTransform =
                overlap.transform;

            if (overlapTransform ==
                point.transform)
            {
                return true;
            }

            if (overlapTransform.IsChildOf(
                    point.transform))
            {
                return true;
            }
        }

        // -----------------------------------------------------
        // IGNORE FLOOR
        //
        // Floor collider should end close to floorPosition.y.
        // Furniture/walls normally extend much higher.
        // -----------------------------------------------------

        if (overlap.bounds.max.y <=
            floorPosition.y + 0.12f)
        {
            return true;
        }

        return false;
    }

    // =========================================================
    // PIECE DISTANCE
    // =========================================================

    private bool IsFarEnoughFromOtherPieces(
        Vector3 candidatePosition)
    {
        Vector2 candidateXZ =
            new Vector2(
                candidatePosition.x,
                candidatePosition.z
            );

        foreach (Vector3 usedPosition
                 in usedPuzzlePositions)
        {
            Vector2 usedXZ =
                new Vector2(
                    usedPosition.x,
                    usedPosition.z
                );

            float distance =
                Vector2.Distance(
                    candidateXZ,
                    usedXZ
                );

            if (distance <
                minDistanceBetweenPieces)
            {
                return false;
            }
        }

        return true;
    }

    // =========================================================
    // DEBUG REJECTION
    // =========================================================

    private void RejectPoint(
        ItemSpawnPoint point,
        string reason)
    {
        if (!logRejectedSpawnPoints)
            return;

        string pointName =
            point != null
                ? point.name
                : "NULL";

        Debug.Log(
            $"[PUZZLE SPAWNER] Rejected " +
            $"{pointName}: {reason}."
        );
    }

    // =========================================================
    // SHUFFLE
    // =========================================================

    private void Shuffle<T>(
        List<T> list)
    {
        for (int i =
                 list.Count - 1;
             i > 0;
             i--)
        {
            int j =
                UnityEngine.Random.Range(
                    0,
                    i + 1
                );

            (list[i], list[j]) =
                (list[j], list[i]);
        }
    }
}