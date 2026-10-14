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

        pending = false;

        // Host lang ang nag-i-spawn ng enemies
        if (!runner.IsServer && !runner.IsSharedModeMasterClient)
            return;

        SpawnEnemies();
    }

    private void FindRunner()
    {
        if (runner != null && runner.IsRunning)
            return;

        runner = null;

        foreach (NetworkRunner r in FindObjectsByType<NetworkRunner>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (r != null && r.IsRunning)
            {
                runner = r;
                return;
            }
        }
    }

    private void SpawnEnemies()
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
            if (count >= maxEnemies)
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

            runner.Spawn(
                prefab,
                hit.position,
                point.transform.rotation
            );

            count++;
        }

        spawned = true;

        Debug.Log($"[ENEMY SPAWN:{pointGroup}] Spawned {count} enemies from {points.Count} points.");
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