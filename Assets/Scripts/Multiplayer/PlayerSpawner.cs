using Fusion;
using System.Linq;
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
    private bool lobbyRecoveryComplete;


    // Important:
    // This handles the case where the player joined the session
    // BEFORE the Lobby scene finished loading.
    private void OnEnable()
    {
        TrySpawnLocalPlayer();
    }

    private void Update()
    {
        if (Runner == null)
            return;

        if (!hasCheckedInitialSpawn)
        {
            TrySpawnLocalPlayer();
            return;
        }

        if (!lobbyRecoveryComplete && Runner.IsSceneAuthority)
            TryReturnCapturedPlayersToLobby();
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
            if (Runner.IsSceneAuthority)
                TryReturnCapturedPlayersToLobby();
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

    private void TryReturnCapturedPlayersToLobby()
    {
        if (Runner == null || !Runner.IsSceneAuthority ||
            spawnPoints == null || spawnPoints.Length == 0)
            return;

        PlayerRef[] players = Runner.ActivePlayers
            .OrderBy(player => player.RawEncoded)
            .ToArray();
        if (players.Length == 0)
            return;

        foreach (PlayerRef player in players)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) ||
                playerObject == null ||
                !playerObject.TryGetComponent(out PlayerController controller) ||
                !controller.IsCapturedByKempeitai)
                return;
        }

        for (int index = 0; index < players.Length; index++)
        {
            if (!Runner.TryGetPlayerObject(players[index], out NetworkObject playerObject) ||
                playerObject == null ||
                !playerObject.TryGetComponent(out PlayerController controller))
                continue;

            Transform spawnPoint = spawnPoints[players[index].RawEncoded % spawnPoints.Length];
            if (spawnPoint == null)
                continue;

            Vector3 position = TryGetGroundPosition(spawnPoint, out Vector3 groundPosition)
                ? groundPosition
                : spawnPoint.position + Vector3.up * playerHeightOffset;
            controller.RpcReturnToLobby(position, spawnPoint.rotation);

            LobbyPlayerState playerState = playerObject.GetComponent<LobbyPlayerState>();
            if (playerState != null)
                playerState.RequestResetReady();
        }

        Debug.Log("[PLAYER SPAWNER] Returned all captured players to Fort2 lobby spawns; players must ready and start again.", this);
        lobbyRecoveryComplete = true;
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

    /// <summary>Places existing connected players at Fort2's ending spawn markers.</summary>
    public bool PlaceExistingPlayersAt(Transform spawnParent)
    {
        if (Runner == null || !Runner.IsSceneAuthority)
        {
            Debug.LogWarning("[PLAYER SPAWNER] Ending placement must be requested by Fusion scene authority.", this);
            return false;
        }

        if (spawnParent == null)
        {
            Debug.LogError("[PLAYER SPAWNER] Ending spawn parent is missing.", this);
            return false;
        }

        Transform[] endingPoints = Enumerable.Range(1, 4)
            .Select(index => spawnParent.Find($"ESP_{index:00}"))
            .ToArray();
        if (endingPoints.Any(point => point == null))
        {
            Debug.LogError("[PLAYER SPAWNER] EndingSpawnPoint must contain ESP_01 through ESP_04.", this);
            return false;
        }

        PlayerRef[] players = Runner.ActivePlayers
            .OrderBy(player => player.RawEncoded)
            .ToArray();

        for (int index = 0; index < players.Length && index < endingPoints.Length; index++)
        {
            NetworkObject playerObject = Runner.GetPlayerObject(players[index]);
            PlayerController controller = playerObject != null
                ? playerObject.GetComponent<PlayerController>()
                : null;
            if (controller == null)
            {
                Debug.LogWarning($"[PLAYER SPAWNER] No existing player object was found for {players[index]}.", this);
                continue;
            }

            Transform point = endingPoints[index];
            Vector3 position = TryGetGroundPosition(point, out Vector3 groundPosition)
                ? groundPosition
                : point.position + Vector3.up * playerHeightOffset;
            controller.RpcTeleportToEnding(position, point.rotation);
            Debug.Log($"[PLAYER SPAWNER] Returned {players[index]} to {point.name} at {position}.", this);
        }

        if (players.Length > endingPoints.Length)
            Debug.LogWarning($"[PLAYER SPAWNER] {players.Length} players connected; only the first four can use the ending spawn points.", this);

        return true;
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
