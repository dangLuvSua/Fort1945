using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class DungeonEnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DungeonGenerator dungeon;

    [Header("Enemies")]
    [SerializeField] private NetworkObject[] enemyPrefabs;

    [Header("Points")]
    [Tooltip("Gagamitin lang ang DungeonEnemyPoint na may parehong Group. " +
             "Blangko = mga point na walang group.")]
    [SerializeField] private string pointGroup = "";

    [Header("Rules")]
    [Tooltip("Enemy limit for other groups. The Soldier group uses the selected difficulty's soldier spawn count.")]
    [SerializeField] private int maxEnemies = 6;

    [Range(0f, 1f)]
    [Tooltip("1 = laging may enemy sa bawat point. 0.5 = kalahati lang ng points.")]
    [SerializeField] private float spawnChancePerPoint = 1f;

    [Tooltip("Huwag mag-spawn ng enemy sa room kung saan nagsisimula ang players.")]
    [SerializeField] private bool avoidPlayerSpawnRoom = true;

    [Tooltip("Gaano kalayo hahanapin ang pinakamalapit na NavMesh.")]
    [SerializeField] private float navMeshSnapDistance = 2f;

    private NetworkRunner runner;
    private bool pending;
    private bool spawned;

    private void OnEnable()
    {
        DungeonGenerator.OnNavMeshBuilt += HandleNavMeshBuilt;
    }

    private void OnDisable()
    {
        DungeonGenerator.OnNavMeshBuilt -= HandleNavMeshBuilt;
    }

    private void Start()
    {
        if (dungeon == null)
            dungeon = FindFirstObjectByType<DungeonGenerator>();

        // kung na-bake na bago pa tayo nag-start
        if (dungeon != null && dungeon.NavMeshReady)
            pending = true;
    }

    private void HandleNavMeshBuilt(DungeonGenerator built)
    {
        if (dungeon == null)
            dungeon = built;

        if (built != dungeon)
            return;

        spawned = false;
        pending = true;
    }

    private void Update()
    {
        if (!pending || spawned)
            return;

        FindRunner();

        if (runner == null)
            return;

        // Spawn through the active game session, once, from its state authority.
        if (!runner.IsSharedModeMasterClient && !runner.IsServer)
        {
            pending = false;
            return;
        }

        int spawnLimit = maxEnemies;
        GameDifficulty? difficulty = null;
        if (pointGroup == "Soldier")
        {
            NetworkGameManager manager = NetworkGameManager.Instance;
            // Keep pending until the active session has its initialized difficulty.
            // Do not fall back to Normal while network state is still arriving.
            if (manager == null || manager.Object == null || !manager.Object.IsValid ||
                manager.Runner != runner || !manager.IsDifficultyReady)
                return;

            difficulty = manager.Difficulty;
            spawnLimit = DifficultyDatabase.Get(difficulty.Value).soldierSpawnCount;
        }

        pending = false;
        SpawnEnemies(spawnLimit, difficulty);
    }

    private void FindRunner()
    {
        if (runner != null && runner.IsRunning)
            return;

        runner = null;

        NetworkRunnerHandler handler = NetworkRunnerHandler.Instance;
        if (handler != null && handler.GameRunner != null && handler.GameRunner.IsRunning)
        {
            runner = handler.GameRunner;
            return;
        }

        NetworkRunner[] runners = FindObjectsByType<NetworkRunner>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None);
        foreach (NetworkRunner r in runners)
        {
            if (r != null && r.IsRunning && (r.IsSharedModeMasterClient || r.IsServer))
            {
                runner = r;
                return;
            }
        }
    }

    private void SpawnEnemies(int spawnLimit, GameDifficulty? difficulty)
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning($"[ENEMY SPAWN:{pointGroup}] Walang enemy prefab na naka-assign.");
            return;
        }

        List<DungeonEnemyPoint> points = new List<DungeonEnemyPoint>();

        foreach (DungeonEnemyPoint p in FindObjectsByType<DungeonEnemyPoint>(FindObjectsSortMode.None))
        {
            if (p.group == pointGroup)
                points.Add(p);
        }

        GameObject spawnRoom = dungeon != null ? dungeon.SpawnRoomObject : null;

        if (avoidPlayerSpawnRoom && spawnRoom != null)
        {
            points.RemoveAll(p => p.transform.IsChildOf(spawnRoom.transform));
        }

        Shuffle(points);

        int count = 0;

        foreach (DungeonEnemyPoint point in points)
        {
            if (count >= spawnLimit)
                break;

            if (Random.value > spawnChancePerPoint)
                continue;

            NetworkObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];

            if (prefab == null)
                continue;

            if (!NavMesh.SamplePosition(
                    point.transform.position,
                    out NavMeshHit hit,
                    navMeshSnapDistance,
                    NavMesh.AllAreas))
            {
                Debug.LogWarning(
                    $"[ENEMY SPAWN:{pointGroup}] {point.name} is not near the NavMesh. Skipped."
                );
                continue;
            }

            NetworkObject enemy = runner.Spawn(
                prefab,
                hit.position,
                point.transform.rotation
            );

            if (enemy != null)
                count++;
        }

        spawned = true;

        string difficultyInfo = difficulty.HasValue
            ? $" Difficulty: {difficulty.Value}, limit: {spawnLimit}."
            : "";
        Debug.Log($"[ENEMY SPAWN:{pointGroup}] Spawned {count} enemies from {points.Count} points.{difficultyInfo}");
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
