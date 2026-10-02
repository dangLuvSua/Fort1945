using Fusion;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Networked]
    public NetworkBool GameStarted { get; private set; }

    [Networked]
    public NetworkBool LobbyLocked { get; private set; }

    public bool IsHost
    {
        get
        {
            return Runner != null &&
                   Runner.IsSharedModeMasterClient;
        }
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[GAME MANAGER] Duplicate NetworkGameManager found. Destroying duplicate.",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;

        Debug.Log(
            "[GAME MANAGER] Instance assigned."
        );
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    // =========================================================
    // FUSION
    // =========================================================

    public override void Spawned()
    {
        Debug.Log(
            "[GAME MANAGER] Spawned."
        );

        Debug.Log(
            $"[GAME MANAGER] State Authority: {Object.HasStateAuthority}"
        );

        Debug.Log(
            $"[GAME MANAGER] Is Shared Master: {IsHost}"
        );

        // Only the State Authority is allowed to initialize
        // Networked properties.
        if (Object.HasStateAuthority)
        {
            GameStarted = false;
            LobbyLocked = false;

            Debug.Log(
                "[GAME MANAGER] Network state initialized."
            );
        }
    }


    // =========================================================
    // HOST CHECK
    // =========================================================

    public bool IsPlayerHost(PlayerRef player)
    {
        if (Runner == null)
            return false;

        if (!Runner.IsSharedModeMasterClient)
            return false;

        return player == Runner.LocalPlayer;
    }


    // =========================================================
    // START GAME
    // =========================================================

    public void RequestStartGame()
    {
        if (Runner == null)
        {
            Debug.LogWarning(
                "[GAME MANAGER] Cannot start game. Runner is null."
            );

            return;
        }

        if (!IsHost)
        {
            Debug.LogWarning(
                "[GAME MANAGER] Only the host can start the game."
            );

            return;
        }

        if (GameStarted)
        {
            Debug.LogWarning(
                "[GAME MANAGER] Game is already started."
            );

            return;
        }

        Debug.Log(
            "[GAME MANAGER] Host requested game start."
        );

        RPC_RequestStartGame();
    }


    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    private void RPC_RequestStartGame(
        RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning(
                "[GAME MANAGER] RPC reached non-StateAuthority object."
            );

            return;
        }

        if (GameStarted)
        {
            return;
        }

        Debug.Log(
            "[GAME MANAGER] Checking whether all players are ready..."
        );

        if (!AreAllPlayersReady())
        {
            Debug.LogWarning(
                "[GAME MANAGER] Cannot start. Not all players are ready."
            );

            return;
        }

        GameStarted = true;
        LobbyLocked = true;

        Debug.Log(
            "[GAME MANAGER] GAME STARTED."
        );

        Debug.Log(
            "[GAME MANAGER] Lobby locked."
        );
    }


    // =========================================================
    // READY CHECK
    // =========================================================

    public bool AreAllPlayersReady()
    {
        if (Runner == null)
            return false;

        int playerCount = 0;
        int readyCount = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            playerCount++;

            NetworkObject playerObject =
                Runner.GetPlayerObject(player);

            if (playerObject == null)
            {
                Debug.LogWarning(
                    $"[GAME MANAGER] No player object found for {player}."
                );

                return false;
            }

            LobbyPlayerState playerState =
                playerObject.GetComponent<LobbyPlayerState>();

            if (playerState == null)
            {
                Debug.LogWarning(
                    $"[GAME MANAGER] LobbyPlayerState missing on {player}."
                );

                return false;
            }

            if (playerState.IsReady)
            {
                readyCount++;
            }
        }

        if (playerCount == 0)
        {
            Debug.LogWarning(
                "[GAME MANAGER] No active players."
            );

            return false;
        }

        Debug.Log(
            $"[GAME MANAGER] Ready check: {readyCount}/{playerCount}"
        );

        return readyCount == playerCount;
    }


    // =========================================================
    // PLAYER COUNT
    // =========================================================

    public int GetPlayerCount()
    {
        if (Runner == null)
            return 0;

        int count = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            count++;
        }

        return count;
    }


    // =========================================================
    // READY COUNT
    // =========================================================

    public int GetReadyPlayerCount()
    {
        if (Runner == null)
            return 0;

        int readyCount = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            NetworkObject playerObject =
                Runner.GetPlayerObject(player);

            if (playerObject == null)
                continue;

            LobbyPlayerState playerState =
                playerObject.GetComponent<LobbyPlayerState>();

            if (playerState != null &&
                playerState.IsReady)
            {
                readyCount++;
            }
        }

        return readyCount;
    }
}