using System.Collections;
using Fusion;
using UnityEngine;

public class PuzzleNetworkBootstrap : MonoBehaviour
{
    // =========================================================
    // NETWORK PUZZLE PREFAB
    // =========================================================

    [Header("Network Puzzle State")]

    [SerializeField]
    private NetworkObject puzzleNetworkStatePrefab;

    private bool spawnRequested;

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        StartCoroutine(
            WaitAndCreatePuzzleState()
        );
    }

    // =========================================================
    // WAIT FOR NETWORK + DUNGEON
    // =========================================================

    private IEnumerator WaitAndCreatePuzzleState()
    {
        // -----------------------------------------------------
        // WAIT FOR NETWORK RUNNER
        // -----------------------------------------------------

        while (NetworkRunnerHandler.Instance == null ||
               NetworkRunnerHandler.Instance.GameRunner == null ||
               !NetworkRunnerHandler.Instance.GameRunner.IsRunning)
        {
            yield return null;
        }

        NetworkRunner runner =
            NetworkRunnerHandler.Instance.GameRunner;

        // -----------------------------------------------------
        // WAIT FOR DUNGEON GENERATOR
        // -----------------------------------------------------

        DungeonGenerator generator = null;

        while (generator == null)
        {
            generator =
                FindFirstObjectByType<DungeonGenerator>();

            yield return null;
        }

        // -----------------------------------------------------
        // WAIT UNTIL PROCEDURAL DUNGEON IS READY
        // -----------------------------------------------------

        while (!generator.IsGenerated)
        {
            yield return null;
        }

        // Give Fusion one extra frame.
        yield return null;

        // -----------------------------------------------------
        // ALREADY EXISTS
        // -----------------------------------------------------

        if (PuzzleNetworkState.Instance != null)
        {
            Debug.Log(
                "[PUZZLE NETWORK] Puzzle state already exists."
            );

            yield break;
        }

        // -----------------------------------------------------
        // ONLY MASTER CLIENT SPAWNS IT
        // -----------------------------------------------------

        if (!runner.IsSharedModeMasterClient)
        {
            Debug.Log(
                "[PUZZLE NETWORK] Not Master Client. " +
                "Waiting for shared puzzle state."
            );

            yield break;
        }

        if (spawnRequested)
            yield break;

        spawnRequested = true;

        // -----------------------------------------------------
        // CHECK PREFAB
        // -----------------------------------------------------

        if (puzzleNetworkStatePrefab == null)
        {
            Debug.LogError(
                "[PUZZLE NETWORK] Puzzle Network State Prefab " +
                "is not assigned!"
            );

            yield break;
        }

        // -----------------------------------------------------
        // SPAWN NETWORK STATE
        // -----------------------------------------------------

        NetworkObject spawnedObject =
            runner.Spawn(
                puzzleNetworkStatePrefab,
                Vector3.zero,
                Quaternion.identity
            );

        if (spawnedObject == null)
        {
            Debug.LogError(
                "[PUZZLE NETWORK] Failed to spawn PuzzleNetworkState."
            );

            yield break;
        }

        Debug.Log(
            "[PUZZLE NETWORK] Master Client spawned " +
            "PuzzleNetworkState successfully."
        );
    }
}