using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkRunnerHandler :
    MonoBehaviour,
    INetworkRunnerCallbacks
{
    private const int CameraPhotoPacketMagic = 0x46313945;
    private const byte CameraPhotoUpload = 1;
    private const byte CameraPhotoRequest = 2;
    private const byte CameraPhotoTransfer = 3;

    private sealed class SharedCameraPhoto
    {
        public string ContentKey;
        public byte[] EncodedImage;
    }

    private static readonly Dictionary<int, List<SharedCameraPhoto>>
        SharedPhotosByCameraId = new();

    private static int nextCameraPhotoTransferId;

    public static NetworkRunnerHandler Instance { get; private set; }

    public bool IsSceneAuthority => runner != null && runner.IsSceneAuthority;


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

    [Tooltip("Build index of Fort2, used when returning from the Dungeon.")]
    [SerializeField]
    private int fort2SceneBuildIndex = 3;

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

    public NetworkRunner GameRunner => runner;

    // Separate runner used ONLY for browsing lobbies.
    private NetworkRunner lobbyRunner;

    // Scene manager attached to the main runner.
    private NetworkSceneManagerDefault networkSceneManager;

    private PlayerInventory.InventoryTransferSnapshot dungeonInventorySnapshot;

    private string currentSessionName;
    private Coroutine failureModalCoroutine;

    public void CaptureLocalInventoryForDungeon()
    {
        if (runner == null || !runner.IsRunning || !runner.LocalPlayer.IsValid ||
            !runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject playerObject) ||
            playerObject == null)
        {
            Debug.LogWarning(
                "[DUNGEON INVENTORY] Could not find the local Fort player before scene load.");
            return;
        }

        PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning(
                "[DUNGEON INVENTORY] Fort player has no PlayerInventory component.",
                playerObject);
            return;
        }

        dungeonInventorySnapshot = inventory.CaptureTransferSnapshot();
        Debug.Log(
            $"[DUNGEON INVENTORY] Saved local inventory before scene load: " +
            $"candle (203) x{inventory.GetQuantityForItemId(203)}, " +
            $"Dungeon Key (103) x{inventory.GetQuantityForItemId(103)}.",
            inventory);
    }

    public bool RestoreLocalInventoryForDungeon(PlayerInventory inventory)
    {
        if (dungeonInventorySnapshot == null)
            return true;

        if (inventory == null || !inventory.RestoreTransferSnapshot(dungeonInventorySnapshot))
            return false;

        Debug.Log(
            $"[DUNGEON INVENTORY] Restored saved local inventory: " +
            $"candle (203) x{inventory.GetQuantityForItemId(203)}, " +
            $"Dungeon Key (103) x{inventory.GetQuantityForItemId(103)}.",
            inventory);
        dungeonInventorySnapshot = null;
        return true;
    }


    // =========================================================
    // LOADING SCREEN INSTANCE
    // =========================================================

    private LoadingProgressUI loadingScreen;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        Debug.Log(
            $"[RUNNER HANDLER] Awake: {gameObject.name}",
            this
        );

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[RUNNER HANDLER] Duplicate NetworkRunnerHandler found. Destroying duplicate.",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        Debug.Log(
            "[RUNNER HANDLER] Instance assigned successfully.",
            this
        );
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;

            Debug.Log(
                "[RUNNER HANDLER] Instance restored in OnEnable.",
                this
            );
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }


    // =========================================================
    // DUNGEON SCENE CHECK
    // =========================================================

    private bool IsDungeonScene()
    {
        return SceneManager.GetActiveScene().name == "Dungeon";
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
            lobbyName,
            enableClientSessionCreation: true
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
        string lobbyName = null,
        bool enableClientSessionCreation = false)
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

                // Shared mode creates a session by default when the requested
                // name does not exist. Only the explicit Create flow may do so.
                EnableClientSessionCreation =
                    enableClientSessionCreation,

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


            LoadingFailed(enableClientSessionCreation
                ? "Could not create the lobby. Check your connection and try again."
                : "Lobby not found, full, or closed. Check the lobby code and try again.",
                showImmediately: !enableClientSessionCreation);


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

    public bool LoadFort2Scene()
    {
        if (runner == null)
        {
            Debug.LogError("[ENDING] Cannot load Fort2 because the NetworkRunner is missing.");
            return false;
        }

        if (!runner.IsSceneAuthority)
        {
            Debug.LogWarning("[ENDING] This client is not Fusion scene authority; waiting for the authority to load Fort2.");
            return true;
        }

        // Fort2 is build index 3 in EditorBuildSettings. Use its build index
        // directly, since this scene is not always registered in the current
        // network scene manager's path lookup after the Dungeon is loaded.
        SceneRef fort2 = SceneRef.FromIndex(fort2SceneBuildIndex);
        Debug.Log($"[ENDING] Loading Fort2 through Fusion (build index {fort2SceneBuildIndex}, SceneRef {fort2}).");

        runner.LoadScene(fort2, LoadSceneMode.Single);
        return true;
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

        // ---------------------------------------------------------
        // LOBBY BROWSER RUNNER
        // ---------------------------------------------------------

        if (callbackRunner == lobbyRunner)
        {
            lobbyRunner = null;
            return;
        }

        // ---------------------------------------------------------
        // MAIN GAME RUNNER
        // ---------------------------------------------------------

        if (callbackRunner == runner)
        {
            SharedPhotosByCameraId.Clear();
            runner = null;

            // The handler may already be in the process of being
            // destroyed when Fusion invokes this callback.
            if (this == null || gameObject == null)
            {
                return;
            }

            LoadingFailed(
                "NETWORK SHUTDOWN"
            );
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
        if (callbackRunner != runner ||
            !TryReadCameraPhotoPacket(data.ToArray(), out byte operation,
                out int cameraItemId, out byte[] photoBytes))
        {
            return;
        }

        if (operation == CameraPhotoUpload && callbackRunner.IsSharedModeMasterClient)
        {
            CacheCameraPhoto(cameraItemId, photoBytes);
        }
        else if (operation == CameraPhotoRequest &&
                 callbackRunner.IsSharedModeMasterClient)
        {
            SendCameraPhotoLibrary(callbackRunner, player, cameraItemId);
        }
        else if (operation == CameraPhotoTransfer)
        {
            StorePhotoOnLocalPlayer(callbackRunner, photoBytes);
        }
    }

    public static bool PublishCameraPhoto(
        NetworkRunner activeRunner,
        int cameraItemId,
        byte[] encodedImage)
    {
        if (activeRunner == null || !activeRunner.IsRunning || cameraItemId <= 0 ||
            encodedImage == null || encodedImage.Length == 0)
        {
            Debug.LogWarning(
                "[CAMERA PHOTOS] Cannot publish photo: " +
                $"runner={(activeRunner == null ? "null" : activeRunner.IsRunning ? "running" : "stopped")}, " +
                $"cameraItemId={cameraItemId}, " +
                $"bytes={(encodedImage == null ? 0 : encodedImage.Length)}.");
            return false;
        }

        if (activeRunner.IsSharedModeMasterClient)
        {
            CacheCameraPhoto(cameraItemId, encodedImage);
        }
        else
        {
            byte[] packet = MakeCameraPhotoPacket(
                CameraPhotoUpload, cameraItemId, encodedImage);
            activeRunner.SendReliableDataToServer(
                MakeCameraPhotoKey(activeRunner, cameraItemId, CameraPhotoUpload),
                packet);
        }

        return true;
    }

    public static void RequestCameraPhotoLibrary(
        NetworkRunner activeRunner,
        int cameraItemId)
    {
        if (activeRunner == null || !activeRunner.IsRunning || cameraItemId <= 0)
            return;

        if (activeRunner.IsSharedModeMasterClient)
        {
            SendCameraPhotoLibrary(
                activeRunner, activeRunner.LocalPlayer, cameraItemId);
            return;
        }

        activeRunner.SendReliableDataToServer(
            MakeCameraPhotoKey(activeRunner, cameraItemId, CameraPhotoRequest),
            MakeCameraPhotoPacket(CameraPhotoRequest, cameraItemId, null));
    }

    private static ReliableKey MakeCameraPhotoKey(
        NetworkRunner activeRunner,
        int cameraItemId,
        byte operation,
        PlayerRef target = default)
    {
        int playerId = target == default
            ? activeRunner.LocalPlayer.PlayerId
            : target.PlayerId;

        return ReliableKey.FromInts(
            cameraItemId,
            operation,
            ++nextCameraPhotoTransferId,
            playerId);
    }

    private static byte[] MakeCameraPhotoPacket(
        byte operation,
        int cameraItemId,
        byte[] photoBytes)
    {
        photoBytes ??= Array.Empty<byte>();
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream))
        {
            writer.Write(CameraPhotoPacketMagic);
            writer.Write(operation);
            writer.Write(cameraItemId);
            writer.Write(photoBytes.Length);
            writer.Write(photoBytes);
        }

        return stream.ToArray();
    }

    private static bool TryReadCameraPhotoPacket(
        byte[] packet,
        out byte operation,
        out int cameraItemId,
        out byte[] photoBytes)
    {
        operation = 0;
        cameraItemId = 0;
        photoBytes = null;

        try
        {
            using MemoryStream stream = new(packet, false);
            using BinaryReader reader = new(stream);
            if (reader.ReadInt32() != CameraPhotoPacketMagic)
                return false;

            operation = reader.ReadByte();
            cameraItemId = reader.ReadInt32();
            int payloadLength = reader.ReadInt32();
            if (payloadLength < 0 || payloadLength > stream.Length - stream.Position)
                return false;

            photoBytes = reader.ReadBytes(payloadLength);
            return photoBytes.Length == payloadLength;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[CAMERA PHOTOS] Invalid photo packet: {exception.Message}");
            return false;
        }
    }

    private static void CacheCameraPhoto(int cameraItemId, byte[] encodedImage)
    {
        if (encodedImage == null || encodedImage.Length == 0)
            return;

        if (!SharedPhotosByCameraId.TryGetValue(cameraItemId, out List<SharedCameraPhoto> photos))
        {
            photos = new List<SharedCameraPhoto>();
            SharedPhotosByCameraId.Add(cameraItemId, photos);
        }

        string contentKey = GetCameraPhotoContentKey(encodedImage);
        foreach (SharedCameraPhoto photo in photos)
        {
            if (photo.ContentKey == contentKey)
                return;
        }

        if (photos.Count >= PlayerInventory.SlotsPerCategory)
        {
            Debug.LogWarning(
                $"[CAMERA PHOTOS] Camera item {cameraItemId} reached its photo limit.");
            return;
        }

        photos.Add(new SharedCameraPhoto
        {
            ContentKey = contentKey,
            EncodedImage = (byte[])encodedImage.Clone()
        });

        Debug.Log($"[CAMERA PHOTOS] Stored photo {photos.Count} for camera item {cameraItemId}.");
    }

    private static void SendCameraPhotoLibrary(
        NetworkRunner activeRunner,
        PlayerRef target,
        int cameraItemId)
    {
        if (!SharedPhotosByCameraId.TryGetValue(cameraItemId, out List<SharedCameraPhoto> photos))
            return;

        foreach (SharedCameraPhoto photo in photos)
        {
            byte[] packet = MakeCameraPhotoPacket(
                CameraPhotoTransfer, cameraItemId, photo.EncodedImage);

            if (target == activeRunner.LocalPlayer)
            {
                StorePhotoOnLocalPlayer(activeRunner, photo.EncodedImage);
            }
            else
            {
                activeRunner.SendReliableDataToPlayer(
                    target,
                    MakeCameraPhotoKey(
                        activeRunner, cameraItemId, CameraPhotoTransfer, target),
                    packet);
            }
        }
    }

    private static void StorePhotoOnLocalPlayer(
        NetworkRunner activeRunner,
        byte[] encodedImage)
    {
        if (activeRunner.TryGetPlayerObject(
                activeRunner.LocalPlayer, out NetworkObject playerObject) &&
            playerObject != null)
        {
            PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
            if (inventory != null && !inventory.StoreSharedPhoto(encodedImage))
            {
                Debug.LogWarning(
                    "[CAMERA PHOTOS] Could not add a shared image to the " +
                    "local Gallery; it may be full.");
            }
        }
    }

    private static string GetCameraPhotoContentKey(byte[] bytes)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte value in bytes)
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }

            return hash.ToString("X16");
        }
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
        // DUNGEON SCENE
        // ---------------------------------------------------------
        //
        // IMPORTANT:
        // Do NOT use the normal SpawnPlayer() system here.
        //
        // The Dungeon scene is procedurally generated, so there
        // are no static PlayerSpawn objects to use.
        //
        // DungeonPlayerSpawner is responsible for:
        //
        // 1. Waiting for DungeonGenerator.IsGenerated.
        // 2. Getting the procedural dungeon spawn.
        // 3. Teleporting the existing PlayerRoot, OR
        // 4. Spawning a PlayerRoot if one does not exist.
        //
        // This prevents the old fallback:
        //
        //       (0, 10, 0)
        //
        // from being used in the Dungeon.
        // ---------------------------------------------------------

        if (IsDungeonScene())
        {
            Debug.Log(
                "[DUNGEON] Dungeon scene detected."
            );

            Debug.Log(
                "[DUNGEON] Skipping normal PlayerSpawn system. " +
                "DungeonPlayerSpawner will handle player placement."
            );

            StartCoroutine(
                WaitForDungeonPlayer()
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
    // WAIT FOR DUNGEON PLAYER
    // =========================================================
    //
    // DungeonPlayerSpawner is responsible for the actual
    // procedural spawn/teleport.
    //
    // This coroutine only waits for that process to finish
    // before removing the loading screen.
    // =========================================================

    private System.Collections.IEnumerator WaitForDungeonPlayer()
    {
        const float timeout = 30f;

        float elapsed = 0f;

        Debug.Log(
            "[DUNGEON] Waiting for DungeonPlayerSpawner " +
            "to spawn/teleport the local player..."
        );


        while (elapsed < timeout)
        {
            // -----------------------------------------------------
            // Make sure the runner still exists.
            // -----------------------------------------------------

            if (runner == null)
            {
                Debug.LogWarning(
                    "[DUNGEON] Runner became null while waiting " +
                    "for Dungeon player."
                );

                yield break;
            }


            // -----------------------------------------------------
            // Make sure we have a valid LocalPlayer.
            // -----------------------------------------------------

            if (runner.LocalPlayer != PlayerRef.None)
            {
                NetworkObject playerObject =
                    runner.GetPlayerObject(
                        runner.LocalPlayer
                    );


                // -------------------------------------------------
                // DungeonPlayerSpawner has successfully created
                // or assigned the player's NetworkObject.
                // -------------------------------------------------

                DungeonPlayerSpawner spawner =
                    FindFirstObjectByType<DungeonPlayerSpawner>();

                if (playerObject != null && spawner != null &&
                    spawner.IsLocalPlayerReady)
                {
                    Debug.Log(
                        "[DUNGEON] Local player confirmed after " +
                        "DungeonPlayerSpawner."
                    );

                    Debug.Log(
                        $"[DUNGEON] Player object: " +
                        $"{playerObject.name}"
                    );


                    DungeonTeleportLoadingOverlay.Hide();
                    CompleteLoading();

                    yield break;
                }
            }


            elapsed += Time.deltaTime;

            yield return null;
        }


        // ---------------------------------------------------------
        // TIMEOUT
        // ---------------------------------------------------------

        Debug.LogError(
            "[DUNGEON] Timed out waiting for " +
            "DungeonPlayerSpawner to create the local player."
        );

        Debug.LogError(
            "[DUNGEON] Check that DungeonPlayerSpawner exists " +
            "in the Dungeon scene and that its Player Prefab " +
            "is assigned."
        );
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
        string message,
        bool showImmediately = false)
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
        if (showImmediately)
        {
            if (failureModalCoroutine != null)
            {
                StopCoroutine(failureModalCoroutine);
                failureModalCoroutine = null;
            }

            RemoveLoadingScreen();
            ShowFailureModal(message);
            return;
        }

        if (failureModalCoroutine != null) StopCoroutine(failureModalCoroutine);
        failureModalCoroutine = StartCoroutine(RemoveLoadingScreenAfterFailure(message));
    }


    // =========================================================
    // REMOVE LOADING SCREEN AFTER FAILURE
    // =========================================================

    private System.Collections.IEnumerator
        RemoveLoadingScreenAfterFailure(string message)
    {
        yield return new WaitForSeconds(
            1.5f
        );


        RemoveLoadingScreen();
        ShowFailureModal(message);
        failureModalCoroutine = null;
    }

    private void RemoveLoadingScreen()
    {
        if (loadingScreen != null)
        {
            Destroy(loadingScreen.gameObject);
            loadingScreen = null;
        }
    }

    private void ShowFailureModal(string message)
    {
        if (CanvasManager.Instance != null)
        {
            CanvasManager.Instance.ShowInfo("LOBBY CONNECTION FAILED", message);
        }
    }
}
