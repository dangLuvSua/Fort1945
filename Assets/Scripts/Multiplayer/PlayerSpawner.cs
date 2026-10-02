using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [Header("Player")]
    [SerializeField]
    private NetworkObject playerPrefab;

    [Header("Spawn Points")]
    [SerializeField]
    private Transform[] spawnPoints;

    [Header("Ground Check")]
    [SerializeField]
    private float groundCheckHeight = 10f;

    [SerializeField]
    private float groundCheckDistance = 50f;

    [SerializeField]
    private float playerHeightOffset = 1.1f;

    private bool hasCheckedInitialSpawn;


    // Important:
    // This handles the case where the player joined the session
    // BEFORE the Lobby scene finished loading.
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

    private void TrySpawnLocalPlayer()
    {
        if (hasCheckedInitialSpawn)
        {
            return;
        }

        if (Runner == null)
        {
            return;
        }

        PlayerRef localPlayer =
            Runner.LocalPlayer;

        // Make sure the local player is actually connected.
        if (!localPlayer.IsValid)
        {
            return;
        }

        // Prevent duplicate spawning.
        if (Runner.GetPlayerObject(localPlayer) != null)
        {
            Debug.Log(
                $"[PLAYER SPAWNER] Player object already exists " +
                $"for {localPlayer}."
            );

            hasCheckedInitialSpawn = true;
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError(
                "[PLAYER SPAWNER] Player Prefab is not assigned!"
            );

            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError(
                "[PLAYER SPAWNER] No spawn points assigned!"
            );

            return;
        }

        SpawnLocalPlayer(localPlayer);
    }


    private void SpawnLocalPlayer(PlayerRef player)
    {
        // Use the PlayerRef to select a consistent spawn point.
        int spawnIndex =
            player.RawEncoded % spawnPoints.Length;

        Transform spawnPoint =
            spawnPoints[spawnIndex];

        if (spawnPoint == null)
        {
            Debug.LogError(
                $"[PLAYER SPAWNER] Spawn point " +
                $"{spawnIndex} is missing!"
            );

            return;
        }

        Vector3 spawnPosition;

        if (!TryGetGroundPosition(
                spawnPoint,
                out spawnPosition))
        {
            Debug.LogWarning(
                $"[PLAYER SPAWNER] Could not find ground " +
                $"below {spawnPoint.name}. " +
                $"Using spawn point position."
            );

            spawnPosition =
                spawnPoint.position +
                Vector3.up * playerHeightOffset;
        }

        Quaternion spawnRotation =
            spawnPoint.rotation;

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
                "[PLAYER SPAWNER] Failed to spawn PlayerRoot."
            );

            return;
        }

        Runner.SetPlayerObject(
            player,
            playerObject
        );

        hasCheckedInitialSpawn = true;

        Debug.Log(
            $"[PLAYER SPAWNER] SPAWNED {player}\n" +
            $"Spawn Point: {spawnPoint.name}\n" +
            $"Position: {spawnPosition}\n" +
            $"Rotation: {spawnRotation.eulerAngles}"
        );
    }


    private bool TryGetGroundPosition(
        Transform spawnPoint,
        out Vector3 groundPosition)
    {
        groundPosition = Vector3.zero;

        Vector3 rayOrigin =
            spawnPoint.position +
            Vector3.up * groundCheckHeight;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            groundPosition =
                hit.point +
                Vector3.up * playerHeightOffset;

            return true;
        }

        return false;
    }
}