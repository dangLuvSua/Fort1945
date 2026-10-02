using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkRunnerHandler Instance { get; private set; }

    [Header("Network")]
    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Game")]
    [SerializeField] private int maxPlayers = 4;

    // Build Index of the Lobby scene
    [SerializeField] private int gameSceneBuildIndex = 2;

    private NetworkRunner runner;

    private string currentSessionName;

    // Tracks whether the Lobby scene has finished loading



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

        // ---------------------------------------------------------
        // Create NetworkRunner
        // ---------------------------------------------------------

        runner = Instantiate(runnerPrefab);

        runner.name = "NetworkRunner";


        // ---------------------------------------------------------
        // Register callbacks
        // ---------------------------------------------------------

        runner.AddCallbacks(this);


        // ---------------------------------------------------------
        // Scene manager
        // ---------------------------------------------------------

        NetworkSceneManagerDefault sceneManager =
            runner.gameObject.AddComponent<
                NetworkSceneManagerDefault
            >();


        // ---------------------------------------------------------
        // Start Game Arguments
        // ---------------------------------------------------------

        StartGameArgs startGameArgs = new StartGameArgs
        {
            GameMode = gameMode,

            SessionName = sessionName,

            PlayerCount = maxPlayers,

            SceneManager = sceneManager
        };


        Debug.Log(
            $"Starting game: {sessionName}"
        );


        // ---------------------------------------------------------
        // Start Fusion
        // ---------------------------------------------------------

        StartGameResult result =
            await runner.StartGame(
                startGameArgs
            );


        // ---------------------------------------------------------
        // Start Failed
        // ---------------------------------------------------------

        if (!result.Ok)
        {
            Debug.LogError(
                $"Failed to start game: " +
                $"{result.ShutdownReason}"
            );

            Destroy(runner.gameObject);

            runner = null;

            return;
        }


        // ---------------------------------------------------------
        // Start Successful
        // ---------------------------------------------------------

        Debug.Log(
            $"Successfully joined session: " +
            $"{sessionName}"
        );


        // ---------------------------------------------------------
        // Load Lobby
        // ---------------------------------------------------------

        LoadLobbyScene();
    }


    // =========================================================
    // LOAD LOBBY SCENE
    // =========================================================

    private void LoadLobbyScene()
    {
        if (runner == null)
        {
            Debug.LogError(
                "Cannot load Lobby scene because Runner is null."
            );

            return;
        }


        // ---------------------------------------------------------
        // Only Scene Authority should load the scene
        // ---------------------------------------------------------

        if (!runner.IsSceneAuthority)
        {
            Debug.Log(
                "This client is not the Scene Authority. " +
                "Waiting for the Lobby scene to be loaded."
            );

            return;
        }


        Debug.Log(
            $"Loading Lobby scene. " +
            $"Build Index: {gameSceneBuildIndex}"
        );


        // ---------------------------------------------------------
        // Create Fusion SceneRef
        // ---------------------------------------------------------

        SceneRef lobbyScene =
            SceneRef.FromIndex(
                gameSceneBuildIndex
            );


        // ---------------------------------------------------------
        // Load Lobby through Fusion
        // ---------------------------------------------------------

        runner.LoadScene(
            lobbyScene,
            LoadSceneMode.Single
        );
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
    }


    // =========================================================
    // SPAWN PLAYER
    // =========================================================

    private void SpawnPlayer(PlayerRef player)
    {
        if (runner == null)
        {
            Debug.LogError(
                "Cannot spawn player because Runner is null."
            );

            return;
        }


        // ---------------------------------------------------------
        // Prevent duplicate player object
        // ---------------------------------------------------------

        if (runner.GetPlayerObject(player) != null)
        {
            Debug.LogWarning(
                $"Player object already exists for {player}."
            );

            return;
        }


        // ---------------------------------------------------------
        // Get spawn position
        // ---------------------------------------------------------

        Vector3 spawnPosition =
            GetSpawnPosition();


        // ---------------------------------------------------------
        // Spawn Network Player
        // ---------------------------------------------------------

        NetworkObject playerObject =
            runner.Spawn(
                playerPrefab,
                spawnPosition,
                Quaternion.identity,
                player
            );


        // ---------------------------------------------------------
        // Register player object
        // ---------------------------------------------------------

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


        Debug.Log(
            $"[SPAWN] Found {spawnPoints.Length} " +
            $"PlayerSpawn point(s)."
        );


        // ---------------------------------------------------------
        // No spawn points found
        // ---------------------------------------------------------

        if (spawnPoints.Length == 0)
        {
            Debug.LogError(
                "[SPAWN ERROR] No PlayerSpawn objects found!"
            );

            return new Vector3(
                0f,
                10f,
                0f
            );
        }


        // ---------------------------------------------------------
        // Choose a random spawn point
        // ---------------------------------------------------------

        int randomIndex =
            UnityEngine.Random.Range(
                0,
                spawnPoints.Length
            );


        Transform spawn =
            spawnPoints[randomIndex].transform;


        // ---------------------------------------------------------
        // Find actual ground below spawn point
        // ---------------------------------------------------------

        Vector3 rayOrigin =
            spawn.position +
            Vector3.up * 10f;


        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            50f,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            // Put PlayerRoot above actual ground
            Vector3 position =
                hit.point +
                Vector3.up * 1.1f;


            Debug.Log(
                $"[SPAWN] Using {spawn.name}\n" +
                $"Spawn Point: {spawn.position}\n" +
                $"Ground Hit: {hit.point}\n" +
                $"Final Player Position: {position}"
            );


            return position;
        }


        // ---------------------------------------------------------
        // Fallback
        // ---------------------------------------------------------

        Debug.LogWarning(
            $"[SPAWN WARNING] Could not find ground below " +
            $"{spawn.name}. Using spawn point position."
        );


        return spawn.position +
               Vector3.up * 2f;
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


        // ---------------------------------------------------------
        // Lobby is now ready
        // ---------------------------------------------------------


        // ---------------------------------------------------------
        // Spawn local player if it hasn't spawned yet
        // ---------------------------------------------------------

        if (runner.LocalPlayer != PlayerRef.None)
        {
            if (
                runner.GetPlayerObject(
                    runner.LocalPlayer
                ) == null
            )
            {
                SpawnPlayer(
                    runner.LocalPlayer
                );
            }
        }
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