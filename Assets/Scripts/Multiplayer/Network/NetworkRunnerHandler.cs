using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkRunnerHandler :
    MonoBehaviour,
    INetworkRunnerCallbacks
{
    public static NetworkRunnerHandler Instance { get; private set; }


    // =========================================================
    // NETWORK
    // =========================================================

    [Header("Network")]

    [SerializeField]
    private NetworkRunner runnerPrefab;

    [SerializeField]
    private NetworkObject playerPrefab;


    // =========================================================
    // GAME
    // =========================================================

    [Header("Game")]

    [SerializeField]
    private int maxPlayers = 4;


    // =========================================================
    // SCENES
    // =========================================================

    [Header("Scenes")]

    [SerializeField]
    private int gameSceneBuildIndex = 2;

    [Tooltip(
        "Build index of the Dungeon scene used when the squad enters the dungeon."
    )]
    [SerializeField]
    private int dungeonSceneBuildIndex = 3;


    // =========================================================
    // INTRO FLOW ASSETS
    // =========================================================

    [Header("Intro Flow Assets")]

    [Tooltip(
        "World-space text tag prefab used by the intro flow " +
        "(gate / soldier / dungeon zone labels)."
    )]
    [SerializeField]
    public GameObject worldTagPrefab;

    [Tooltip(
        "Salt used to derive the deterministic shared dungeon seed " +
        "from the session name."
    )]
    [SerializeField]
    private float dungeonSeedSalt = 19452214f;


    // =========================================================
    // LOADING SCREEN
    // =========================================================

    [Header("Loading Screen")]

    [Tooltip(
        "Optional direct reference to the LoadingProgressUI prefab. " +
        "The LobbyUI normally creates the active instance."
    )]
    [SerializeField]
    private LoadingProgressUI loadingScreenPrefab;


    // =========================================================
    // RUNNERS
    // =========================================================

    // Main game runner.
    private NetworkRunner runner;

    // Separate runner used ONLY for browsing lobbies.
    private NetworkRunner lobbyRunner;

    // Scene manager attached to the main runner.
    private NetworkSceneManagerDefault networkSceneManager;

    private string currentSessionName;


    // =========================================================
    // LOADING SCREEN INSTANCE
    // =========================================================

    private LoadingProgressUI loadingScreen;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Debug.LogWarning(
                "[RUNNER HANDLER] Duplicate NetworkRunnerHandler found. " +
                "Destroying duplicate.",
                this
            );

            Destroy(gameObject);

            return;
        }


        Instance = this;


        DontDestroyOnLoad(
            gameObject
        );


        Debug.Log(
            "[RUNNER HANDLER] Initialized."
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
    // LOADING SCREEN - FIND
    // =========================================================

    private LoadingProgressUI GetLoadingScreen()
    {
        if (loadingScreen != null)
            return loadingScreen;


        loadingScreen =
            FindFirstObjectByType<LoadingProgressUI>(
                FindObjectsInactive.Include
            );


        return loadingScreen;
    }


    // =========================================================
    // LOADING SCREEN - BEGIN
    // =========================================================
    private void BeginLoading(string message)
    {
        LoadingProgressUI ui = GetLoadingScreen();

        if (ui == null)
        {
            Debug.LogWarning(
                "[LOADING] LoadingProgressUI instance was not found."
            );

            return;
        }

        ui.BeginLoading(message);

        Debug.Log($"[LOADING] {message}");
    }

    // =========================================================
    // LOADING SCREEN - CONNECTING
    // =========================================================

    private void SetLoadingConnecting()
    {
        LoadingProgressUI ui = GetLoadingScreen();

        if (ui == null)
            return;

        ui.SetStatus("CONNECTING...");

        Debug.Log("[LOADING] Fusion connection started.");
    }


    // =========================================================
    // LOADING SCREEN - CONNECTED
    // =========================================================

    private void SetLoadingConnected()
    {
        LoadingProgressUI ui = GetLoadingScreen();

        if (ui == null)
            return;

        ui.SetStatus("CONNECTED...");

        Debug.Log("[LOADING] Fusion connection established.");
    }

    // =========================================================
    // LOADING SCREEN - SCENE START
    // =========================================================

    private void SetLoadingSceneStart()
    {
        LoadingProgressUI ui = GetLoadingScreen();

        if (ui == null)
            return;

        ui.SetStatus("LOADING GAME...");

        Debug.Log("[LOADING] Fusion scene loading started.");
    }


    // =========================================================
    // LOADING SCREEN - COMPLETE
    // =========================================================

    private void CompleteLoading()
    {
        LoadingProgressUI ui = GetLoadingScreen();

        if (ui == null)
        {
            Debug.Log(
                "[LOADING] Loading screen was already removed."
            );

            return;
        }

        // Show READY very briefly in the same frame.
        ui.CompleteLoading();

        Debug.Log(
            "[LOADING] Game scene loaded and player spawned. " +
            "Removing loading screen."
        );

        // Hide immediately.
        ui.gameObject.SetActive(false);

        // Destroy the loading screen.
        Destroy(ui.gameObject);

        loadingScreen = null;
    }

    // =========================================================
    // HIDE LOADING SCREEN
    // =========================================================



    // =========================================================
    // CREATE GAME
    // =========================================================

    public async void CreateGame(
        string sessionName,
        string lobbyName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            sessionName =
                "MysteryRoom";
        }


        if (string.IsNullOrWhiteSpace(lobbyName))
        {
            lobbyName =
                sessionName;
        }


        currentSessionName =
            sessionName;


        Debug.Log(
            $"[GAME] Creating session: {sessionName}"
        );


        Debug.Log(
            $"[GAME] Lobby: {lobbyName}"
        );


        BeginLoading(
            "CREATING LOBBY..."
        );


        await StartRunner(
            GameMode.Shared,
            sessionName,
            lobbyName
        );
    }


    // =========================================================
    // JOIN GAME
    // =========================================================

    public async void JoinGame(
        string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            Debug.LogWarning(
                "[GAME] Session name is empty."
            );

            LoadingFailed(
                "Lobby code is empty."
            );

            return;
        }


        currentSessionName =
            sessionName;


        Debug.Log(
            $"[GAME] Joining session: {sessionName}"
        );


        BeginLoading(
            "JOINING LOBBY..."
        );


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
        if (lobbyRunner != null)
        {
            Debug.Log(
                "[LOBBY] Lobby browser is already connected."
            );

            return;
        }


        if (runner != null)
        {
            Debug.Log(
                "[LOBBY] Main game runner is already running."
            );
        }


        Debug.Log(
            "[LOBBY] Creating dedicated lobby browser runner..."
        );


        lobbyRunner =
            Instantiate(
                runnerPrefab
            );


        lobbyRunner.name =
            "LobbyBrowserRunner";


        lobbyRunner.AddCallbacks(
            this
        );


        Debug.Log(
            "[LOBBY] Joining Shared Session Lobby..."
        );


        StartGameResult result =
            await lobbyRunner.JoinSessionLobby(
                SessionLobby.Shared
            );


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


        Debug.Log(
            "[LOBBY] Successfully joined Shared Session Lobby."
        );
    }


    // =========================================================
    // REFRESH LOBBIES
    // =========================================================

    public void RefreshLobbies()
    {
        if (lobbyRunner == null)
        {
            Debug.Log(
                "[LOBBY] Lobby browser is not connected. Connecting..."
            );


            FindLobbies();


            return;
        }


        Debug.Log(
            "[LOBBY] Refresh requested. " +
            "Waiting for updated session list..."
        );
    }


    // =========================================================
    // START GAME RUNNER
    // =========================================================

    private async Task StartRunner(
        GameMode gameMode,
        string sessionName,
        string lobbyName = null)
    {
        if (runner != null)
        {
            Debug.LogWarning(
                "[GAME] Main runner is already running."
            );

            return;
        }


        Debug.Log(
            "[GAME] Creating main NetworkRunner..."
        );


        runner =
            Instantiate(
                runnerPrefab
            );


        runner.name =
            "NetworkRunner";


        runner.AddCallbacks(
            this
        );


        NetworkSceneManagerDefault sceneManager =
            runner.gameObject.AddComponent<
                NetworkSceneManagerDefault
            >();


        networkSceneManager =
            sceneManager;


        Dictionary<string, SessionProperty>
            sessionProperties = null;


        if (gameMode == GameMode.Shared &&
            !string.IsNullOrWhiteSpace(lobbyName))
        {
            sessionProperties =
                new Dictionary<
                    string,
                    SessionProperty
                >
                {
                    ["LobbyName"] =
                        lobbyName
                };
        }


        StartGameArgs startGameArgs =
            new StartGameArgs
            {
                GameMode =
                    gameMode,

                SessionName =
                    sessionName,

                PlayerCount =
                    maxPlayers,

                SceneManager =
                    sceneManager,

                SessionProperties =
                    sessionProperties
            };


        Debug.Log(
            $"[GAME] Starting game: {sessionName}"
        );


        if (!string.IsNullOrWhiteSpace(lobbyName))
        {
            Debug.Log(
                $"[GAME] Lobby name: {lobbyName}"
            );
        }


        // -----------------------------------------------------
        // REAL FUSION CONNECTION STAGE
        // -----------------------------------------------------

        SetLoadingConnecting();


        StartGameResult result =
            await runner.StartGame(
                startGameArgs
            );


        // -----------------------------------------------------
        // CONNECTION FAILED
        // -----------------------------------------------------

        if (!result.Ok)
        {
            Debug.LogError(
                $"[GAME] Failed to start game: " +
                $"{result.ShutdownReason}"
            );


            LoadingFailed(
                "FAILED TO CONNECT"
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


        // -----------------------------------------------------
        // CONNECTION SUCCEEDED
        // -----------------------------------------------------

        Debug.Log(
            $"[GAME] Successfully joined session: " +
            $"{sessionName}"
        );


        SetLoadingConnected();


        // -----------------------------------------------------
        // LOAD LOBBY SCENE
        // -----------------------------------------------------

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
                "[GAME] Cannot load Lobby scene because Runner is null."
            );


            LoadingFailed(
                "NETWORK RUNNER ERROR"
            );


            return;
        }


        if (!runner.IsSceneAuthority)
        {
            Debug.Log(
                "[GAME] This client is not Scene Authority. " +
                "Waiting for scene load."
            );


            SetLoadingSceneStart();


            return;
        }


        Debug.Log(
            $"[GAME] Loading Lobby scene. " +
            $"Build Index: {gameSceneBuildIndex}"
        );


        SetLoadingSceneStart();


        SceneRef lobbyScene =
            SceneRef.FromIndex(
                gameSceneBuildIndex
            );


        runner.LoadScene(
            lobbyScene,
            LoadSceneMode.Single
        );
    }


    // =========================================================
    // LOAD DUNGEON SCENE
    // =========================================================

    public void LoadDungeonScene()
    {
        if (runner == null)
        {
            Debug.LogError(
                "[GAME] Cannot load Dungeon scene because Runner is null."
            );

            return;
        }


        if (!runner.IsSceneAuthority)
        {
            Debug.Log(
                "[GAME] This client is not Scene Authority. " +
                "Waiting for scene load."
            );

            return;
        }


        if (networkSceneManager == null)
        {
            Debug.LogError(
                "[GAME] Network scene manager is missing.",
                runner.gameObject
            );

            return;
        }


        SceneRef dungeonScene =
            networkSceneManager.GetSceneRef(
                "Assets/Scenes/Dungeon.unity"
            );


        if (dungeonScene == SceneRef.None)
        {
            Debug.LogWarning(
                "[GAME] Dungeon scene not found by path. " +
                "Falling back to build index."
            );


            dungeonScene =
                SceneRef.FromIndex(
                    dungeonSceneBuildIndex
                );
        }


        Debug.Log(
            $"[GAME] Loading Dungeon scene. " +
            $"SceneRef: {dungeonScene}"
        );


        runner.LoadScene(
            dungeonScene,
            LoadSceneMode.Single
        );
    }


    // =========================================================
    // SESSION SEED
    // =========================================================

    public int GetSessionSeed()
    {
        string input =
            string.IsNullOrWhiteSpace(
                currentSessionName
            )
                ? "Fort1945Session"
                : currentSessionName;


        // FNV-1a 32-bit hash.
        uint hash =
            2166136261;


        for (int i = 0;
             i < input.Length;
             i++)
        {
            hash ^=
                (uint)input[i];

            hash *=
                16777619;
        }


        int seed =
            (int)hash ^
            (int)dungeonSeedSalt;


        if (seed == 0)
        {
            seed =
                19451945;
        }


        return Mathf.Abs(
            seed
        );
    }


    // =========================================================
    // PLAYER JOINED
    // =========================================================

    public void OnPlayerJoined(
        NetworkRunner callbackRunner,
        PlayerRef player)
    {
        Debug.Log(
            $"[GAME] Player joined: {player}"
        );

        // Player spawning is handled after the network
        // scene has finished loading.
    }


    // =========================================================
    // SPAWN PLAYER
    // =========================================================

    private void SpawnPlayer(
        NetworkRunner callbackRunner,
        PlayerRef player)
    {
        if (callbackRunner == null)
        {
            Debug.LogError(
                "[SPAWN ERROR] Callback runner is null."
            );

            return;
        }


        if (playerPrefab == null)
        {
            Debug.LogError(
                "[SPAWN ERROR] Player Prefab is not assigned!"
            );

            return;
        }


        if (callbackRunner.GetPlayerObject(player) != null)
        {
            Debug.LogWarning(
                $"[SPAWN] Player object already exists for {player}."
            );

            return;
        }


        Vector3 spawnPosition =
            GetSpawnPosition();


        Debug.Log(
            $"[SPAWN] Spawning player {player} " +
            $"at {spawnPosition}"
        );


        NetworkObject playerObject =
            callbackRunner.Spawn(
                playerPrefab,
                spawnPosition,
                Quaternion.identity,
                player
            );


        callbackRunner.SetPlayerObject(
            player,
            playerObject
        );


        Debug.Log(
            $"[SPAWN] Spawned player for {player}"
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


        int randomIndex =
            UnityEngine.Random.Range(
                0,
                spawnPoints.Length
            );


        Transform spawn =
            spawnPoints[
                randomIndex
            ].transform;


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
        NetworkRunner callbackRunner,
        List<SessionInfo> sessionList)
    {
        if (callbackRunner != lobbyRunner)
        {
            return;
        }


        Debug.Log(
            $"[LOBBY] Sessions found: {sessionList.Count}"
        );


        foreach (SessionInfo session in sessionList)
        {
            Debug.Log(
                $"[LOBBY] {session.Name} " +
                $"Players: {session.PlayerCount}/" +
                $"{session.MaxPlayers}"
            );


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
        Debug.Log(
            "[NETWORK] Disconnect requested."
        );


        if (runner != null)
        {
            runner.Shutdown();

            runner = null;
        }


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
        NetworkRunner callbackRunner)
    {
        Debug.Log(
            $"[NETWORK] Connected to server. " +
            $"Runner = {callbackRunner.name}"
        );


        // Only the actual game runner controls
        // the Create/Join loading screen.
        if (callbackRunner == runner)
        {
            SetLoadingConnected();
        }
    }


    public void OnDisconnectedFromServer(
        NetworkRunner callbackRunner,
        NetDisconnectReason reason)
    {
        Debug.Log(
            $"[NETWORK] Disconnected: {reason}"
        );


        if (callbackRunner == lobbyRunner)
        {
            lobbyRunner = null;

            return;
        }


        if (callbackRunner == runner)
        {
            LoadingFailed(
                "DISCONNECTED"
            );
        }
    }


    public void OnConnectFailed(
        NetworkRunner callbackRunner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason)
    {
        Debug.LogError(
            $"[NETWORK] Connection failed: {reason}"
        );


        if (callbackRunner == runner)
        {
            LoadingFailed(
                "CONNECTION FAILED"
            );
        }
    }


    // =========================================================
    // PLAYER CALLBACKS
    // =========================================================

    public void OnPlayerLeft(
        NetworkRunner callbackRunner,
        PlayerRef player)
    {
        Debug.Log(
            $"[GAME] Player left: {player}"
        );
    }


    // =========================================================
    // INPUT
    // =========================================================

    public void OnInput(
        NetworkRunner callbackRunner,
        NetworkInput input)
    {
        /*
         * Your current PlayerController reads the Unity
         * Input System directly, so this remains empty.
         */
    }


    public void OnInputMissing(
        NetworkRunner callbackRunner,
        PlayerRef player,
        NetworkInput input)
    {
    }


    // =========================================================
    // SHUTDOWN
    // =========================================================

    public void OnShutdown(
        NetworkRunner callbackRunner,
        ShutdownReason shutdownReason)
    {
        Debug.Log(
            $"[NETWORK] Runner shutdown: " +
            $"{shutdownReason}"
        );


        if (callbackRunner == lobbyRunner)
        {
            lobbyRunner = null;
        }


        if (callbackRunner == runner)
        {
            LoadingFailed(
                "NETWORK SHUTDOWN"
            );


            runner = null;
        }
    }


    // =========================================================
    // CONNECTION REQUEST
    // =========================================================

    public void OnConnectRequest(
        NetworkRunner callbackRunner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
    }


    // =========================================================
    // AUTHENTICATION
    // =========================================================

    public void OnCustomAuthenticationResponse(
        NetworkRunner callbackRunner,
        Dictionary<string, object> data)
    {
    }


    // =========================================================
    // HOST MIGRATION
    // =========================================================

    public void OnHostMigration(
        NetworkRunner callbackRunner,
        HostMigrationToken hostMigrationToken)
    {
    }


    // =========================================================
    // RELIABLE DATA
    // =========================================================

    public void OnReliableDataReceived(
        NetworkRunner callbackRunner,
        PlayerRef player,
        ReliableKey key,
        ReadOnlySpan<byte> data)
    {
    }


    public void OnReliableDataProgress(
        NetworkRunner callbackRunner,
        PlayerRef player,
        ReliableKey key,
        float progress)
    {
    }


    // =========================================================
    // SCENE LOAD START
    // =========================================================

    public void OnSceneLoadStart(
        NetworkRunner callbackRunner)
    {
        Debug.Log(
            $"[SCENE] Scene loading started. " +
            $"Runner = {callbackRunner.name}"
        );


        // Ignore the lobby browser runner.
        if (callbackRunner != runner)
        {
            Debug.Log(
                "[SCENE] Ignoring scene callback " +
                "from lobby browser runner."
            );

            return;
        }


        SetLoadingSceneStart();
    }


    // =========================================================
    // SCENE LOAD DONE
    // =========================================================

    // =========================================================
    // SCENE LOAD DONE
    // =========================================================

    public void OnSceneLoadDone(
    NetworkRunner callbackRunner)
    {
        Debug.Log(
            $"[SCENE] Scene loading completed. " +
            $"Runner = {callbackRunner.name}"
        );

        // ---------------------------------------------------------
        // IGNORE OTHER RUNNERS
        // ---------------------------------------------------------

        if (callbackRunner != runner)
        {
            Debug.Log(
                "[SCENE] Ignoring scene callback " +
                "from lobby browser runner."
            );

            return;
        }

        // ---------------------------------------------------------
        // CHECK LOCAL PLAYER
        // ---------------------------------------------------------

        if (callbackRunner.LocalPlayer == PlayerRef.None)
        {
            Debug.LogWarning(
                "[SCENE] LocalPlayer is None. " +
                "Loading screen will remain visible."
            );

            return;
        }

        // ---------------------------------------------------------
        // CHECK IF PLAYER ALREADY EXISTS
        // ---------------------------------------------------------

        NetworkObject localPlayer =
            callbackRunner.GetPlayerObject(
                callbackRunner.LocalPlayer
            );

        // ---------------------------------------------------------
        // SPAWN PLAYER IF NECESSARY
        // ---------------------------------------------------------

        if (localPlayer == null)
        {
            Debug.Log(
                "[SPAWN] Local player does not exist. " +
                "Spawning..."
            );

            SpawnPlayer(
                callbackRunner,
                callbackRunner.LocalPlayer
            );

            localPlayer =
                callbackRunner.GetPlayerObject(
                    callbackRunner.LocalPlayer
                );
        }

        // ---------------------------------------------------------
        // VERIFY PLAYER SPAWN
        // ---------------------------------------------------------

        if (localPlayer == null)
        {
            Debug.LogError(
                "[SPAWN ERROR] Local player could not be found " +
                "after SpawnPlayer(). Loading screen will remain."
            );

            return;
        }

        Debug.Log(
            $"[SPAWN] Local player confirmed: {localPlayer.name}"
        );

        // ---------------------------------------------------------
        // PLAYER IS NOW IN THE GAME
        // ---------------------------------------------------------

        CompleteLoading();
    }
    // =========================================================
    // AREA OF INTEREST
    // =========================================================

    public void OnObjectEnterAOI(
        NetworkRunner callbackRunner,
        NetworkObject obj,
        PlayerRef player)
    {
    }


    public void OnObjectExitAOI(
        NetworkRunner callbackRunner,
        NetworkObject obj,
        PlayerRef player)
    {
    }


    // =========================================================
    // LOADING FAILURE
    // =========================================================

    private void LoadingFailed(
        string message)
    {
        Debug.LogError(
            $"[LOADING] {message}"
        );


        LoadingProgressUI ui =
            GetLoadingScreen();


        if (ui != null)
        {
            ui.SetStatus(
                message
            );
        }


        // Give the player a brief moment to see the
        // failure message before removing the loading UI.
        StartCoroutine(
            RemoveLoadingScreenAfterFailure()
        );
    }


    // =========================================================
    // REMOVE LOADING SCREEN AFTER FAILURE
    // =========================================================

    private System.Collections.IEnumerator
        RemoveLoadingScreenAfterFailure()
    {
        yield return new WaitForSeconds(
            1.5f
        );


        if (loadingScreen != null)
        {
            Destroy(
                loadingScreen.gameObject
            );

            loadingScreen = null;
        }
    }
}