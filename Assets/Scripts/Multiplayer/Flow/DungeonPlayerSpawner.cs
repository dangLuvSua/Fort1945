using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DungeonPlayerSpawner : MonoBehaviour
{
    [Header("Testing")]
    [Tooltip(
    "Kapag naka-check at walang active na NetworkRunner (hindi galing sa MainMenu), " +
    "gagawa ng sariling runner ang spawner para ma-Play mo agad ang scene na ito."
)]
    [SerializeField]
    private bool testingMode = false;

    [SerializeField]
    private GameMode testGameMode = GameMode.Single;

    private bool testRunnerStarting;

    [Header("Player")]
    [SerializeField]
    private NetworkObject playerPrefab;

    [Header("Dungeon")]
    [SerializeField]
    private DungeonGenerator dungeon;

    [Header("Ground Check")]
    [Tooltip(
        "Only objects on this layer will be considered valid dungeon ground."
    )]
    [SerializeField]
    private LayerMask groundLayer;

    [Tooltip(
        "How far above the generated spawn point the ground raycast begins."
    )]
    [SerializeField]
    private float groundCheckHeight = 10f;

    [Tooltip(
        "Maximum distance of the downward ground raycast."
    )]
    [SerializeField]
    private float groundCheckDistance = 50f;

    private NetworkRunner runner;

    private bool hasSpawnedLocalPlayer;
    private float debugTimer;

    private void Awake()
    {
        Debug.Log(
            "[DUNGEON SPAWN] DungeonPlayerSpawner Awake.",
            this
        );

        Debug.Log(
            $"[DUNGEON SPAWN] GameObject: {gameObject.name}",
            this
        );

        Debug.Log(
            $"[DUNGEON SPAWN] Enabled: {enabled}",
            this
        );

        Debug.Log(
            $"[DUNGEON SPAWN] Active In Hierarchy: {gameObject.activeInHierarchy}",
            this
        );

        Debug.Log(
            $"[DUNGEON SPAWN] Player Prefab: " +
            $"{(playerPrefab != null ? playerPrefab.name : "NULL")}",
            this
        );

        Debug.Log(
            $"[DUNGEON SPAWN] Dungeon Reference: " +
            $"{(dungeon != null ? dungeon.name : "NULL")}",
            this
        );

        Debug.Log(
            $"[DUNGEON SPAWN] Ground Layer Mask: {groundLayer.value}",
            this
        );

        FindRunner();
    }

    private void Start()
    {
        Debug.Log(
            "[DUNGEON SPAWN] DungeonPlayerSpawner Start.",
            this
        );

        FindRunner();
        TrySpawnLocalPlayer();
    }

    private void Update()
    {
        if (hasSpawnedLocalPlayer)
            return;

        FindRunner();

        // Testing: walang runner galing MainMenu, kaya gagawa tayo ng sarili
        if (runner == null && testingMode && !testRunnerStarting)
        {
            StartTestRunner();
        }

        debugTimer += Time.deltaTime;

        TrySpawnLocalPlayer();

        if (debugTimer >= 2f)
        {
            debugTimer = 0f;
            PrintStatus();
        }
    }

    private void FindRunner()
    {
        if (runner != null)
            return;

        NetworkRunner[] runners =
            FindObjectsByType<NetworkRunner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (NetworkRunner foundRunner in runners)
        {
            if (foundRunner == null)
                continue;

            if (!foundRunner.IsRunning)
                continue;

            runner = foundRunner;

            Debug.Log(
                $"[DUNGEON SPAWN] Found active NetworkRunner: {runner.name}",
                this
            );

            return;
        }
    }

    private async void StartTestRunner()
    {
        testRunnerStarting = true;

        Debug.Log(
            "[DUNGEON SPAWN] Testing mode: no runner found, starting a test runner.",
            this
        );

        GameObject runnerObject = new GameObject("TestNetworkRunner");

        NetworkRunner testRunner =
            runnerObject.AddComponent<NetworkRunner>();

        testRunner.ProvideInput = true;

        NetworkSceneManagerDefault sceneManager =
            runnerObject.AddComponent<NetworkSceneManagerDefault>();

        StartGameResult result =
            await testRunner.StartGame(new StartGameArgs
            {
                GameMode = testGameMode,
                SessionName = "TestSession",
                Scene = SceneRef.FromIndex(
                    SceneManager.GetActiveScene().buildIndex
                ),
                SceneManager = sceneManager
            });

        if (result.Ok)
        {
            runner = testRunner;

            Debug.Log(
                "[DUNGEON SPAWN] Test runner started.",
                this
            );
        }
        else
        {
            Debug.LogError(
                $"[DUNGEON SPAWN] Test runner failed: {result.ShutdownReason}",
                this
            );

            Destroy(runnerObject);
        }
    }

    private void PrintStatus()
    {
        Debug.Log(
            "[DUNGEON SPAWN STATUS]\n" +
            $"Runner: {(runner != null ? runner.name : "NULL")}\n" +
            $"Runner Running: {(runner != null && runner.IsRunning)}\n" +
            $"LocalPlayer: {(runner != null ? runner.LocalPlayer.ToString() : "N/A")}\n" +
            $"Dungeon: {(dungeon != null ? dungeon.name : "NULL")}\n" +
            $"Dungeon Generated: {(dungeon != null && dungeon.IsGenerated)}\n" +
            $"Player Prefab: {(playerPrefab != null ? playerPrefab.name : "NULL")}\n" +
            $"Has Spawned: {hasSpawnedLocalPlayer}"
        );
    }

    private void TrySpawnLocalPlayer()
    {
        if (hasSpawnedLocalPlayer)
            return;

        if (runner == null)
            return;

        if (!runner.IsRunning)
            return;

        PlayerRef localPlayer =
            runner.LocalPlayer;

        if (!localPlayer.IsValid)
        {
            Debug.LogWarning(
                "[DUNGEON SPAWN] LocalPlayer is not valid yet."
            );

            return;
        }

        if (dungeon == null)
        {
            dungeon =
                FindFirstObjectByType<DungeonGenerator>();

            if (dungeon == null)
                return;

            Debug.Log(
                $"[DUNGEON SPAWN] Found DungeonGenerator automatically: {dungeon.name}"
            );
        }

        if (!dungeon.IsGenerated)
            return;

        Debug.Log(
            "[DUNGEON SPAWN] Dungeon generation confirmed."
        );

        int spawnCount =
            dungeon.GetSpawnCount();

        if (spawnCount <= 0)
        {
            Debug.LogWarning(
                "[DUNGEON SPAWN] Dungeon has no spawn points."
            );

            return;
        }

        Debug.Log(
            $"[DUNGEON SPAWN] Dungeon spawn count: {spawnCount}"
        );

        NetworkObject existingPlayer =
            runner.GetPlayerObject(localPlayer);

        if (existingPlayer != null)
        {
            Debug.Log(
                $"[DUNGEON SPAWN] Existing PlayerRoot found: " +
                $"{existingPlayer.name}"
            );
        }
        else
        {
            Debug.Log(
                "[DUNGEON SPAWN] No existing PlayerRoot found."
            );
        }

        if (existingPlayer == null &&
            playerPrefab == null)
        {
            Debug.LogError(
                "[DUNGEON SPAWN] Player Prefab is NULL. " +
                "Assign the same PlayerRoot NetworkObject prefab " +
                "used by NetworkRunnerHandler."
            );

            return;
        }

        bool success =
            SpawnOrTeleportPlayer(
                localPlayer,
                existingPlayer
            );

        if (!success)
        {
            Debug.LogWarning(
                "[DUNGEON SPAWN] Spawn/teleport attempt failed."
            );

            return;
        }

        hasSpawnedLocalPlayer = true;

        Debug.Log(
            "[DUNGEON SPAWN] " +
            "Local player dungeon placement complete."
        );
    }

    private bool SpawnOrTeleportPlayer(
        PlayerRef player,
        NetworkObject existingPlayer)
    {
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (!TryGetDungeonSpawn(
            player,
            existingPlayer,
            out spawnPosition,
            out spawnRotation))
        {
            Debug.LogWarning(
                "[DUNGEON SPAWN] " +
                "Failed to calculate dungeon spawn."
            );

            return false;
        }

        if (existingPlayer != null)
        {
            TeleportPlayer(
                existingPlayer,
                spawnPosition,
                spawnRotation
            );

            Debug.Log(
                $"[DUNGEON SPAWN] " +
                $"Teleported existing player {player} " +
                $"to {spawnPosition}"
            );

            return true;
        }

        Debug.Log(
            $"[DUNGEON SPAWN] " +
            $"Spawning new PlayerRoot at {spawnPosition}"
        );

        NetworkObject playerObject =
            runner.Spawn(
                playerPrefab,
                spawnPosition,
                spawnRotation,
                player
            );

        if (playerObject == null)
        {
            Debug.LogError(
                "[DUNGEON SPAWN] " +
                "Runner.Spawn returned NULL."
            );

            return false;
        }

        runner.SetPlayerObject(
            player,
            playerObject
        );

        Debug.Log(
            $"[DUNGEON SPAWN] " +
            $"Spawned PlayerRoot for {player}."
        );

        return true;
    }

    private bool TryGetDungeonSpawn(
        PlayerRef player,
        NetworkObject playerObject,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (dungeon == null)
        {
            Debug.LogWarning(
                "[DUNGEON SPAWN] Dungeon reference is NULL."
            );

            return false;
        }

        int spawnCount =
            dungeon.GetSpawnCount();

        if (spawnCount <= 0)
        {
            Debug.LogWarning(
                "[DUNGEON SPAWN] GetSpawnCount returned 0."
            );

            return false;
        }

        int index =
            (int)player.RawEncoded % spawnCount;

        dungeon.GetSpawn(
            index,
            out position,
            out rotation
        );

        Debug.Log(
            $"[DUNGEON SPAWN] " +
            $"Raw dungeon spawn position: {position}"
        );

        position =
            GetCorrectGroundPosition(
                position,
                playerObject
            );

        Debug.Log(
            $"[DUNGEON SPAWN] " +
            $"Final dungeon player position: {position}"
        );

        return true;
    }

    private Vector3 GetCorrectGroundPosition(
        Vector3 origin,
        NetworkObject playerObject)
    {
        Vector3 rayOrigin =
            origin +
            Vector3.up *
            groundCheckHeight;

        Debug.DrawRay(
            rayOrigin,
            Vector3.down *
            groundCheckDistance,
            Color.red,
            10f
        );

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            Debug.Log(
                $"[DUNGEON SPAWN] " +
                $"Ground detected at {hit.point} " +
                $"on {hit.collider.name}"
            );

            CharacterController cc =
                null;

            if (playerObject != null)
            {
                cc =
                    playerObject.GetComponent<CharacterController>();
            }

            if (cc != null)
            {
                float bottomOffset =
                    cc.height * 0.5f -
                    cc.center.y;

                Vector3 finalPosition =
                    hit.point +
                    Vector3.up *
                    bottomOffset;

                Debug.Log(
                    $"[DUNGEON SPAWN] " +
                    $"CharacterController Height: {cc.height}"
                );

                Debug.Log(
                    $"[DUNGEON SPAWN] " +
                    $"CharacterController Center: {cc.center}"
                );

                Debug.Log(
                    $"[DUNGEON SPAWN] " +
                    $"Bottom Offset: {bottomOffset}"
                );

                return finalPosition;
            }

            return hit.point + Vector3.up;
        }

        Debug.LogWarning(
            "[DUNGEON SPAWN] " +
            $"No ground detected below {origin} " +
            $"using Ground Layer mask {groundLayer.value}."
        );

        return origin;
    }

    private void TeleportPlayer(
        NetworkObject playerObject,
        Vector3 position,
        Quaternion rotation)
    {
        if (playerObject == null)
        {
            Debug.LogWarning(
                "[DUNGEON SPAWN] " +
                "Cannot teleport. PlayerObject is NULL."
            );

            return;
        }

        PlayerController controller =
            playerObject.GetComponent<PlayerController>();

        if (controller != null)
        {
            controller.Teleport(
                position,
                rotation
            );

            Debug.Log(
                "[DUNGEON SPAWN] " +
                "Existing PlayerController teleported."
            );

            return;
        }

        playerObject.transform.SetPositionAndRotation(
            position,
            rotation
        );

        Debug.Log(
            "[DUNGEON SPAWN] " +
            "PlayerController not found. " +
            "Moved PlayerRoot directly."
        );
    }
}