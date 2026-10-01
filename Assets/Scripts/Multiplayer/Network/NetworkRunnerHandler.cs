using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkRunnerHandler Instance { get; private set; }

    [Header("Network")]
    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Game")]
    [SerializeField] private int maxPlayers = 5;
    [SerializeField] private int gameSceneBuildIndex = 0;

    private NetworkRunner runner;

    private string currentSessionName;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    // =========================================================
    // CREATE GAME
    // =========================================================

    public async void CreateGame(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            sessionName = "MysteryRoom";
        }

        currentSessionName = sessionName;

        await StartRunner(
            GameMode.Shared,
            sessionName
        );
    }

    // =========================================================
    // JOIN GAME
    // =========================================================

    public async void JoinGame(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            Debug.LogWarning(
                "Session name is empty."
            );

            return;
        }

        currentSessionName = sessionName;

        await StartRunner(
            GameMode.Shared,
            sessionName
        );
    }

    // =========================================================
    // START RUNNER
    // =========================================================

    private async System.Threading.Tasks.Task StartRunner(
        GameMode gameMode,
        string sessionName)
    {
        if (runner != null)
        {
            Debug.LogWarning(
                "Runner is already running."
            );

            return;
        }

        // Create NetworkRunner
        runner = Instantiate(runnerPrefab);

        runner.name = "NetworkRunner";

        // Register callbacks
        runner.AddCallbacks(this);

        // Scene manager
        NetworkSceneManagerDefault sceneManager =
            runner.gameObject.AddComponent<
                NetworkSceneManagerDefault>();

        StartGameArgs startGameArgs =
            new StartGameArgs
            {
                GameMode = gameMode,

                SessionName = sessionName,

                PlayerCount = maxPlayers,

                Scene = SceneRef.FromIndex(
                    gameSceneBuildIndex
                ),

                SceneManager = sceneManager
            };

        Debug.Log(
            $"Starting game: {sessionName}"
        );

        StartGameResult result =
            await runner.StartGame(
                startGameArgs
            );

        if (!result.Ok)
        {
            Debug.LogError(
                $"Failed to start game: " +
                $"{result.ShutdownReason}"
            );

            Destroy(runner.gameObject);

            runner = null;
        }
        else
        {
            Debug.Log(
                $"Successfully joined session: " +
                $"{sessionName}"
            );
        }
    }

    // =========================================================
    // PLAYER JOINED
    // =========================================================

    public void OnPlayerJoined(
        NetworkRunner runner,
        PlayerRef player)
    {
        Debug.Log(
            $"Player joined: {player}"
        );

        // In Shared Mode, the local player
        // spawns its own player object.
        if (player != runner.LocalPlayer)
        {
            return;
        }

        SpawnPlayer(player);
    }

    // =========================================================
    // SPAWN PLAYER
    // =========================================================

    private void SpawnPlayer(PlayerRef player)
    {
        Vector3 spawnPosition =
            GetSpawnPosition();

        NetworkObject playerObject =
            runner.Spawn(
                playerPrefab,
                spawnPosition,
                Quaternion.identity,
                player
            );

        runner.SetPlayerObject(
            player,
            playerObject
        );

        Debug.Log(
            $"Spawned player for {player}"
        );
    }

    // =========================================================
    // SPAWN POSITION
    // =========================================================

    private Vector3 GetSpawnPosition()
    {
        GameObject[] spawnPoints =
            GameObject.FindGameObjectsWithTag(
                "PlayerSpawn"
            );

        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning(
                "No PlayerSpawn objects found. " +
                "Using Vector3.zero."
            );

            return Vector3.zero;
        }

        int randomIndex =
            UnityEngine.Random.Range(
                0,
                spawnPoints.Length
            );

        return spawnPoints[
            randomIndex
        ].transform.position;
    }

    // =========================================================
    // SESSION LIST
    // =========================================================

    public void OnSessionListUpdated(
        NetworkRunner runner,
        List<SessionInfo> sessionList)
    {
        Debug.Log(
            $"Available sessions: " +
            $"{sessionList.Count}"
        );

        foreach (
            SessionInfo session
            in sessionList)
        {
            Debug.Log(
                $"Session: {session.Name} | " +
                $"Players: {session.PlayerCount}"
            );
        }
    }

    // =========================================================
    // DISCONNECT
    // =========================================================

    public void Disconnect()
    {
        if (runner != null)
        {
            runner.Shutdown();

            runner = null;
        }
    }

    // =========================================================
    // CONNECTION CALLBACKS
    // =========================================================

    public void OnConnectedToServer(
        NetworkRunner runner)
    {
        Debug.Log(
            "Connected to server."
        );
    }

    public void OnDisconnectedFromServer(
        NetworkRunner runner,
        NetDisconnectReason reason)
    {
        Debug.Log(
            $"Disconnected: {reason}"
        );
    }

    public void OnConnectFailed(
        NetworkRunner runner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason)
    {
        Debug.LogError(
            $"Connection failed: {reason}"
        );
    }

    // =========================================================
    // PLAYER CALLBACKS
    // =========================================================

    public void OnPlayerLeft(
        NetworkRunner runner,
        PlayerRef player)
    {
        Debug.Log(
            $"Player left: {player}"
        );
    }

    // =========================================================
    // INPUT
    // =========================================================

    public void OnInput(
        NetworkRunner runner,
        NetworkInput input)
    {
        // We will implement player input
        // in the next networking stage.
    }

    public void OnInputMissing(
        NetworkRunner runner,
        PlayerRef player,
        NetworkInput input)
    {
    }

    // =========================================================
    // SHUTDOWN
    // =========================================================

    public void OnShutdown(
        NetworkRunner runner,
        ShutdownReason shutdownReason)
    {
        Debug.Log(
            $"Runner shutdown: " +
            $"{shutdownReason}"
        );
    }

    // =========================================================
    // CONNECTION REQUEST
    // =========================================================

    public void OnConnectRequest(
        NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
    }

    // =========================================================
    // AUTHENTICATION
    // =========================================================

    public void OnCustomAuthenticationResponse(
        NetworkRunner runner,
        Dictionary<string, object> data)
    {
    }

    // =========================================================
    // HOST MIGRATION
    // =========================================================

    public void OnHostMigration(
        NetworkRunner runner,
        HostMigrationToken hostMigrationToken)
    {
    }

    // =========================================================
    // RELIABLE DATA
    // =========================================================

    public void OnReliableDataReceived(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        ReadOnlySpan<byte> data)
    {
    }

    public void OnReliableDataProgress(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        float progress)
    {
    }

    // =========================================================
    // SCENE
    // =========================================================

    public void OnSceneLoadDone(
        NetworkRunner runner)
    {
        Debug.Log(
            "Scene loading completed."
        );
    }

    public void OnSceneLoadStart(
        NetworkRunner runner)
    {
        Debug.Log(
            "Scene loading started."
        );
    }

    // =========================================================
    // AREA OF INTEREST
    // =========================================================

    public void OnObjectEnterAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    public void OnObjectExitAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }
}