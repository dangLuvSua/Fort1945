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

    // =========================================================
    // RUNNERS
    // =========================================================

    // Main runner used for Create Game / Join Game
    private NetworkRunner runner;

    // Separate runner used only for Find Lobby
    private NetworkRunner lobbyRunner;

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

    public async void CreateGame(
        string sessionName,
        string lobbyName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            sessionName = "MysteryRoom";
        }

        if (string.IsNullOrWhiteSpace(lobbyName))
        {
            lobbyName = sessionName;
        }

        currentSessionName = sessionName;

        await StartRunner(
            GameMode.Shared,
            sessionName,
            lobbyName
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
    // FIND LOBBIES
    // =========================================================

    public async void FindLobbies()
    {
        // ---------------------------------------------------------
        // Already browsing
        // ---------------------------------------------------------

        if (lobbyRunner != null)
        {
            Debug.Log(
                "[LOBBY] Lobby browser is already connected."
            );

            return;
        }

        // ---------------------------------------------------------
        // Create dedicated lobby browser runner
        // ---------------------------------------------------------

        Debug.Log(
            "[LOBBY] Creating dedicated lobby browser runner..."
        );

        lobbyRunner = Instantiate(runnerPrefab);

        lobbyRunner.name = "LobbyBrowserRunner";

        // ---------------------------------------------------------
        // Register callbacks
        // ---------------------------------------------------------

        lobbyRunner.AddCallbacks(this);

        Debug.Log(
            "[LOBBY] Joining Shared Session Lobby..."
        );

        // ---------------------------------------------------------
        // Join Shared Session Lobby
        // ---------------------------------------------------------

        StartGameResult result =
            await lobbyRunner.JoinSessionLobby(
                SessionLobby.Shared
            );

        // ---------------------------------------------------------
        // Failed
        // ---------------------------------------------------------

        if (!result.Ok)
        {
            Debug.LogError(
                $"[LOBBY] Failed to join Shared Session Lobby: " +
                $"{result.ShutdownReason}"
            );

            if (lobbyRunner != null)
            {
                Destroy(
                    lobbyRunner.gameObject
                );

                lobbyRunner = null;
            }

            return;
        }

        // ---------------------------------------------------------
        // Successful
        // ---------------------------------------------------------

        Debug.Log(
            "[LOBBY] Successfully joined Shared Session Lobby."
        );
    }


    // =========================================================
    // REFRESH LOBBIES
    // =========================================================

    public void RefreshLobbies()
    {
        // ---------------------------------------------------------
        // No lobby browser runner
        // ---------------------------------------------------------

        if (lobbyRunner == null)
        {
            Debug.Log(
                "[LOBBY] Lobby browser is not connected. " +
                "Connecting..."
            );

            FindLobbies();

            return;
        }

        // ---------------------------------------------------------
        // Already connected
        // ---------------------------------------------------------

        Debug.Log(
            "[LOBBY] Refresh requested. " +
            "Waiting for updated session list..."
        );

        // Fusion automatically sends session-list updates
        // through OnSessionListUpdated().
        //
        // We intentionally do NOT call JoinSessionLobby()
        // again here because the lobbyRunner is already inside
        // the Shared Session Lobby.
    }


    // =========================================================
    // START GAME RUNNER
    // =========================================================

    private async System.Threading.Tasks.Task StartRunner(
        GameMode gameMode,
        string sessionName,
        string lobbyName = null)
    {
        // ---------------------------------------------------------
        // Prevent duplicate main runner
        // ---------------------------------------------------------

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
        // Session Properties
        // ---------------------------------------------------------

        Dictionary<string, SessionProperty> sessionProperties =
            null;

        // Only the creator needs to send the LobbyName property.
        if (gameMode == GameMode.Shared &&
            !string.IsNullOrWhiteSpace(lobbyName))
        {
            sessionProperties =
                new Dictionary<string, SessionProperty>
                {
                    ["LobbyName"] = lobbyName
                };
        }

        // ---------------------------------------------------------
        // Start Game Arguments
        // ---------------------------------------------------------

        StartGameArgs startGameArgs =
            new StartGameArgs
            {
                GameMode = gameMode,

                SessionName = sessionName,

                PlayerCount = maxPlayers,

                SceneManager = sceneManager,

                SessionProperties = sessionProperties
            };

        Debug.Log(
            $"Starting game: {sessionName}"
        );

        if (!string.IsNullOrWhiteSpace(lobbyName))
        {
            Debug.Log(
                $"Lobby name: {lobbyName}"
            );
        }

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

            if (runner != null)
            {
                Destroy(
                    runner.gameObject
                );

                runner = null;
            }

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
        // Choose random spawn point
        // ---------------------------------------------------------

        int randomIndex =
            UnityEngine.Random.Range(
                0,
                spawnPoints.Length
            );

        Transform spawn =
            spawnPoints[randomIndex].transform;

        // ---------------------------------------------------------
        // Find actual ground below spawn
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
            $"[LOBBY] Sessions found: {sessionList.Count}"
        );

        foreach (SessionInfo session in sessionList)
        {
            Debug.Log(
                $"[LOBBY] {session.Name} " +
                $"Players: {session.PlayerCount}/{session.MaxPlayers}"
            );

            // Log lobby name if available.
            if (session.Properties.TryGetValue(
                "LobbyName",
                out SessionProperty lobbyNameProperty))
            {
                Debug.Log(
                    $"[LOBBY] Lobby Name: " +
                    $"{lobbyNameProperty.PropertyValue}"
                );
            }
        }

        if (LobbySessionList.Instance != null)
        {
            LobbySessionList.Instance.UpdateSessions(
                sessionList
            );
        }
        else
        {
            Debug.LogWarning(
                "[LOBBY] LobbySessionList instance was not found."
            );
        }
    }


    // =========================================================
    // DISCONNECT
    // =========================================================

    public void Disconnect()
    {
        // ---------------------------------------------------------
        // Shutdown main game runner
        // ---------------------------------------------------------

        if (runner != null)
        {
            runner.Shutdown();

            runner = null;
        }

        // ---------------------------------------------------------
        // Shutdown lobby browser runner
        // ---------------------------------------------------------

        if (lobbyRunner != null)
        {
            lobbyRunner.Shutdown();

            lobbyRunner = null;
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