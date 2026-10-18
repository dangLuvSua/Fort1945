
using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Attach to a scene NetworkObject, not a generated dungeon piece.
// Only State Authority chooses locations and spawns triggers.
[RequireComponent(typeof(NetworkObject))]
public sealed class DungeonJumpscareSpawner : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private DungeonGenerator dungeonGenerator;
    [SerializeField] private NetworkObject jumpscareTriggerPrefab;

    [Header("Counts")]
    [SerializeField, Min(0)] private int corridorTriggers = 3;
    [SerializeField, Min(0)] private int roomTriggers = 2;
    [SerializeField] private bool excludePlayerSpawnRoom = true;

    [Header("Spawn Point Settings")]
    [Tooltip("Name of the spawn point GameObject inside dungeon prefabs.")]
    [SerializeField] private string spawnPointName = "JumpscareSpawnPoint";

    [Tooltip("Draw spawn point positions in Scene view.")]
    [SerializeField] private bool showDebugLogs = true;

    private readonly List<NetworkObject> spawnedTriggers =
        new List<NetworkObject>();

    private bool spawnedForThisGeneration;

    private void OnEnable()
    {
        DungeonGenerator.OnDungeonGenerated += OnDungeonGenerated;
    }

    private void OnDisable()
    {
        DungeonGenerator.OnDungeonGenerated -= OnDungeonGenerated;
    }

    public override void Spawned()
    {
        if (dungeonGenerator == null)
            dungeonGenerator = FindFirstObjectByType<DungeonGenerator>();

        TrySpawnTriggers();
    }

    private void OnDungeonGenerated(DungeonGenerator generator)
    {
        if (generator != dungeonGenerator)
            return;

        TrySpawnTriggers();
    }

    private void TrySpawnTriggers()
    {
        if (spawnedForThisGeneration ||
            Object == null ||
            !Object.IsValid ||
            !Object.HasStateAuthority ||
            Runner == null ||
            !Runner.IsRunning ||
            dungeonGenerator == null ||
            !dungeonGenerator.IsGenerated ||
            jumpscareTriggerPrefab == null)
        {
            return;
        }

        spawnedForThisGeneration = true;

        List<GameObject> corridors = new List<GameObject>();
        List<GameObject> rooms = new List<GameObject>();

        foreach (GameObject piece in dungeonGenerator.GeneratedPieces)
        {
            if (piece == null)
                continue;

            if (excludePlayerSpawnRoom &&
                piece == dungeonGenerator.SpawnRoomObject)
                continue;

            // Only consider pieces containing a valid spawn point.
            Transform spawnPoint = FindJumpscareSpawnPoint(piece);

            if (spawnPoint == null)
            {
                if (showDebugLogs)
                    Debug.Log(
                        $"[Jumpscares] Skipping {piece.name}: " +
                        "No JumpscareSpawnPoint found."
                    );

                continue;
            }

            if (dungeonGenerator.IsCorridorPiece(piece))
            {
                corridors.Add(piece);
            }
            else if (
                piece.GetComponentInChildren<DungeonSpawnPoint>() != null ||
                HasSingleEntrance(piece))
            {
                rooms.Add(piece);
            }
        }

        SpawnFromCandidates(corridors, corridorTriggers, "Corridor");
        SpawnFromCandidates(rooms, roomTriggers, "Room");

        Debug.Log(
            $"[Jumpscares] Successfully spawned " +
            $"{spawnedTriggers.Count} networked jumpscare triggers."
        );
    }

    private static bool HasSingleEntrance(GameObject piece)
    {
        int entrances = 0;
        int exits = 0;

        foreach (DungeonConnector connector in
                 piece.GetComponentsInChildren<DungeonConnector>())
        {
            if (connector.connectorType ==
                DungeonConnector.ConnectorType.Entrance)
            {
                entrances++;
            }
            else if (connector.connectorType ==
                     DungeonConnector.ConnectorType.Exit)
            {
                exits++;
            }
        }

        return entrances == 1 && exits == 0;
    }

    private void SpawnFromCandidates(
        List<GameObject> candidates,
        int desiredCount,
        string category)
    {
        // Randomize eligible pieces on State Authority only.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            GameObject temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }

        int spawned = 0;

        foreach (GameObject piece in candidates)
        {
            if (spawned >= desiredCount)
                break;

            Transform spawnPoint = FindJumpscareSpawnPoint(piece);

            if (spawnPoint == null)
                continue;

            // Use the exact world-space position and rotation
            // of the manually placed spawn point.
            Vector3 position = spawnPoint.position;
            Quaternion rotation = spawnPoint.rotation;

            NetworkObject instance = Runner.Spawn(
                jumpscareTriggerPrefab,
                position,
                rotation
            );

            if (instance == null)
            {
                Debug.LogWarning(
                    $"[Jumpscares] Failed to spawn trigger at {piece.name}."
                );

                continue;
            }

            spawnedTriggers.Add(instance);
            spawned++;

            if (showDebugLogs)
            {
                Debug.Log(
                    $"[Jumpscares] {category} trigger spawned " +
                    $"inside {piece.name} at {position}."
                );
            }
        }

        if (spawned < desiredCount)
        {
            Debug.LogWarning(
                $"[Jumpscares] Requested {desiredCount} " +
                $"{category.ToLower()} triggers, but spawned {spawned}. " +
                "Check available generated pieces and spawn points."
            );
        }
    }

    private Transform FindJumpscareSpawnPoint(GameObject piece)
    {
        // Includes inactive children and nested spawn points.
        Transform[] children =
            piece.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == spawnPointName)
                return child;
        }

        return null;
    }
}
