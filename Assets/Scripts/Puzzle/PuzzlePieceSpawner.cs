using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PuzzlePieceSpawner : NetworkBehaviour
{
    [Serializable]
    public class PuzzleDifficultySet
    {
        public GameDifficulty difficulty;

        [Tooltip("Every puzzle piece that must spawn for this difficulty.")]
        public ItemData[] pieces;
    }

    [Header("Puzzle Sets")]
    [SerializeField]
    private PuzzleDifficultySet[] puzzleSets;

    [Header("References")]
    [SerializeField]
    private DungeonGenerator dungeonGenerator;

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

    [Tooltip("How many times to search for a sufficiently distant spawn point.")]
    [SerializeField]
    private int maxSpawnPointAttempts = 30;

    [Header("Standalone Testing")]
    [SerializeField]
    private bool allowStandaloneTesting = true;

    [SerializeField]
    private GameDifficulty standaloneDifficulty =
        GameDifficulty.Normal;

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
            HandleDungeonGenerated(dungeonGenerator);
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

        dungeonGenerator = generator;

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

            availablePoints.Add(point);
        }

        Shuffle(availablePoints);

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

        GameDifficulty difficulty;

        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager != null &&
            manager.IsNetworkStateReady)
        {
            difficulty = manager.Difficulty;
        }
        else if (allowStandaloneTesting)
        {
            difficulty = standaloneDifficulty;
        }
        else
        {
            return;
        }

        PuzzleDifficultySet set =
            GetSet(difficulty);

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
            SpawnPuzzleSet(set);
    }

    // =========================================================
    // GET DIFFICULTY SET
    // =========================================================

    private PuzzleDifficultySet GetSet(
        GameDifficulty difficulty)
    {
        if (puzzleSets == null)
            return null;

        foreach (PuzzleDifficultySet set in puzzleSets)
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

        int successful = 0;

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

            if (!spawnedIds.Add(piece.itemId))
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

            if (!SpawnPiece(piece))
                return false;

            successful++;
        }

        Debug.Log(
            $"[PUZZLE SPAWNER] SUCCESS: " +
            $"{successful}/{set.pieces.Length} pieces spawned."
        );

        return successful == set.pieces.Length;
    }

    // =========================================================
    // SPAWN SINGLE PIECE
    // =========================================================

    private bool SpawnPiece(ItemData piece)
    {
        if (availablePoints.Count == 0)
            return false;

        ItemSpawnPoint point = null;
        int selectedIndex = -1;

        // =====================================================
        // FIND A SPAWN POINT FAR FROM PREVIOUS PIECES
        // =====================================================

        int attempts =
            Mathf.Max(1, maxSpawnPointAttempts);

        for (int attempt = 0;
             attempt < attempts;
             attempt++)
        {
            if (availablePoints.Count == 0)
                break;

            int randomIndex =
                UnityEngine.Random.Range(
                    0,
                    availablePoints.Count
                );

            ItemSpawnPoint candidate =
                availablePoints[randomIndex];

            if (candidate == null)
                continue;

            bool farEnough = true;

            foreach (Vector3 usedPosition
                     in usedPuzzlePositions)
            {
                float distance =
                    Vector3.Distance(
                        candidate.Position,
                        usedPosition
                    );

                if (distance <
                    minDistanceBetweenPieces)
                {
                    farEnough = false;
                    break;
                }
            }

            if (farEnough)
            {
                point = candidate;
                selectedIndex = randomIndex;
                break;
            }
        }

        // Fallback:
        // if no sufficiently distant point exists,
        // use any remaining point.
        if (point == null)
        {
            selectedIndex =
                UnityEngine.Random.Range(
                    0,
                    availablePoints.Count
                );

            point =
                availablePoints[selectedIndex];

            Debug.LogWarning(
                $"[PUZZLE SPAWNER] Could not find a point " +
                $"at least {minDistanceBetweenPieces} units away. " +
                $"Using fallback point for {piece.itemName}."
            );
        }

        if (point == null)
            return false;

        availablePoints.RemoveAt(selectedIndex);

        // =====================================================
        // FIND PHYSICAL FLOOR
        // =====================================================

        Vector3 rayStart =
            point.Position +
            Vector3.up * floorRayStartHeight;

        float floorY =
            point.Position.y;

        bool foundFloor =
            Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit floorHit,
                floorRayDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );

        if (foundFloor)
        {
            floorY =
                floorHit.point.y;

            Debug.DrawLine(
                rayStart,
                floorHit.point,
                Color.green,
                5f
            );
        }
        else
        {
            Debug.LogWarning(
                $"[PUZZLE SPAWNER] No floor detected under " +
                $"{point.name}. Using original spawn Y."
            );

            Debug.DrawRay(
                rayStart,
                Vector3.down * floorRayDistance,
                Color.red,
                5f
            );
        }

        Vector3 spawnPosition =
            new Vector3(
                point.Position.x,
                floorY,
                point.Position.z
            );

        // Random horizontal rotation.
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
        // SPAWN THROUGH FUSION
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

                    foreach (Renderer renderer in renderers)
                    {
                        if (renderer == null)
                            continue;

                        lowestY =
                            Mathf.Min(
                                lowestY,
                                renderer.bounds.min.y
                            );
                    }

                    if (float.IsInfinity(lowestY))
                        return;

                    float correction =
                        floorY -
                        lowestY +
                        verticalOffset;

                    obj.transform.position +=
                        Vector3.up * correction;
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

        // Store final position so following pieces
        // must try to spawn farther away.
        usedPuzzlePositions.Add(
            spawned.transform.position
        );

        // =====================================================
        // INITIALIZE ITEM DATA
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
    // SHUFFLE
    // =========================================================

    private void Shuffle<T>(
        List<T> list)
    {
        for (int i = list.Count - 1;
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