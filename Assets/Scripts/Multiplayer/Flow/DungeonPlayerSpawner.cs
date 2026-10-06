using Fusion;
using UnityEngine;

/// <summary>
/// Multiplayer spawner for the Dungeon scene.
///
/// The dungeon is procedurally generated (DungeonGenerator), so the
/// player cannot spawn until generation finishes. This component:
/// 1. Waits until DungeonGenerator.IsGenerated.
/// 2. Spawns the LOCAL player's PlayerRoot at the generated entrance.
/// 3. If the local player already has a PlayerRoot (e.g. the fallback
///    spawn on scene load by NetworkRunnerHandler), it teleports that
///    object to the dungeon entrance instead of double-spawning.
///
/// Mirrors Assets/Scripts/Multiplayer/PlayerSpawner.cs.
/// </summary>
public class DungeonPlayerSpawner :
    SimulationBehaviour,
    IPlayerJoined
{
    [Header("Player")]
    [SerializeField]
    private NetworkObject playerPrefab;

    [Header("Dungeon")]
    [SerializeField]
    private DungeonGenerator dungeon;

    [Header("Ground Check")]
    [SerializeField]
    private float groundCheckHeight = 10f;

    [SerializeField]
    private float groundCheckDistance = 50f;

    [SerializeField]
    private float playerHeightOffset = 1.1f;

    private bool hasSpawnedLocalPlayer;


    // =========================================================
    // UNITY
    // =========================================================

    // Handles the case where the player joined the session
    // before this scene finished loading.
    private void OnEnable()
    {
        TrySpawnLocalPlayer();
    }

    // Called by Fusion when a player joins.
    public void PlayerJoined(PlayerRef player)
    {
        if (player != Runner.LocalPlayer)
        {
            return;
        }

        TrySpawnLocalPlayer();
    }

    private void Update()
    {
        // Generation may finish after our first attempts;
        // keep polling until we succeed.
        TrySpawnLocalPlayer();
    }


    // =========================================================
    // SPAWN
    // =========================================================

    private void TrySpawnLocalPlayer()
    {
        if (hasSpawnedLocalPlayer)
        {
            return;
        }

        if (Runner == null)
        {
            return;
        }

        PlayerRef localPlayer =
            Runner.LocalPlayer;

        if (!localPlayer.IsValid)
        {
            return;
        }

        if (dungeon == null)
        {
            dungeon =
                FindAnyObjectByType<DungeonGenerator>();
        }

        if (dungeon == null ||
            !dungeon.IsGenerated)
        {
            Debug.Log(
                "[DUNGEON SPAWN] Waiting for dungeon generation..."
            );

            return;
        }

        if (dungeon.GetSpawnCount() == 0)
        {
            Debug.LogWarning(
                "[DUNGEON SPAWN] Dungeon has no spawn points yet."
            );

            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError(
                "[DUNGEON SPAWN] Player Prefab is not assigned!",
                this
            );

            return;
        }

        SpawnOrTeleportPlayer(localPlayer);

        hasSpawnedLocalPlayer = true;
    }

    private void SpawnOrTeleportPlayer(PlayerRef player)
    {
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (!TryGetDungeonSpawn(
            player,
            out spawnPosition,
            out spawnRotation))
        {
            return;
        }

        NetworkObject existing =
            Runner.GetPlayerObject(player);

        // ------------------------------------------------
        // Already spawned (fallback spawn on scene load):
        // just teleport into the dungeon.
        // ------------------------------------------------

        if (existing != null)
        {
            TeleportPlayer(
                existing,
                spawnPosition,
                spawnRotation
            );

            return;
        }

        // ------------------------------------------------
        // Fresh spawn.
        // ------------------------------------------------

        NetworkObject playerObject =
            Runner.Spawn(
                playerPrefab,
                spawnPosition,
                spawnRotation,
                player
            );

        if (playerObject == null)
        {
            Debug.LogError(
                "[DUNGEON SPAWN] Failed to spawn PlayerRoot."
            );

            return;
        }

        Runner.SetPlayerObject(
            player,
            playerObject
        );

        Debug.Log(
            $"[DUNGEON SPAWN] SPAWNED {player} at {spawnPosition}"
        );
    }


    // =========================================================
    // SPAWN POINT
    // =========================================================

    private bool TryGetDungeonSpawn(
        PlayerRef player,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (dungeon == null)
        {
            return false;
        }

        int index =
            (int) player.RawEncoded %
            dungeon.GetSpawnCount();

        dungeon.GetSpawn(
            index,
            out position,
            out rotation
        );

        position =
            TryGetGroundPosition(position);

        return true;
    }
    private Vector3 TryGetGroundPosition(
        Vector3 origin)
    {
        Vector3 rayOrigin =
            origin +
            Vector3.up * groundCheckHeight;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            return
                hit.point +
                Vector3.up * playerHeightOffset;
        }

        return
            origin +
            Vector3.up * playerHeightOffset;
    }


    // =========================================================
    // TELEPORT
    // =========================================================

    private void TeleportPlayer(
        NetworkObject playerObject,
        Vector3 position,
        Quaternion rotation)
    {
        PlayerController controller =
            playerObject.GetComponent<PlayerController>();

        if (controller != null)
        {
            controller.Teleport(
                position,
                rotation
            );

            Debug.Log(
                "[DUNGEON SPAWN] Teleported existing player " +
                "to dungeon entrance."
            );

            return;
        }

        playerObject.transform.SetPositionAndRotation(
            position,
            rotation
        );

        Debug.Log(
            "[DUNGEON SPAWN] Moved fallback player object " +
            "to dungeon entrance."
        );
    }
}
