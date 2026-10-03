using UnityEngine;

public class ChestLoot : MonoBehaviour
{
    [Header("Loot")]
    [Tooltip("Possible items. One random item will spawn inside the chest.")]
    public GameObject[] itemPrefabs;

    [Tooltip("Where the item appears. Put an empty child inside the 'bottom' part.")]
    public Transform itemSpawnPoint;

    [Range(0f, 1f)]
    [Tooltip("1 = always has an item. 0.5 = 50% chance.")]
    public float spawnChance = 1f;

    [Header("Debug")]
    public bool showGizmo = true;

    private GameObject spawnedItem;

    private void Start()
    {
        SpawnItem();
    }

    private void SpawnItem()
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0)
        {
            return;
        }

        if (itemSpawnPoint == null)
        {
            Debug.LogError("ChestLoot: itemSpawnPoint is not assigned.", this);
            return;
        }

        // Chance kung may laman ang chest o wala
        if (Random.value > spawnChance)
        {
            return;
        }

        GameObject prefab = itemPrefabs[Random.Range(0, itemPrefabs.Length)];

        if (prefab == null)
        {
            return;
        }

        // Naka-parent sa chest, kaya sumasama sa chest at nawawala pag nawala ang chest
        spawnedItem = Instantiate(
            prefab,
            itemSpawnPoint.position,
            itemSpawnPoint.rotation,
            itemSpawnPoint
        );
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo || itemSpawnPoint == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(itemSpawnPoint.position, 0.1f);
        Gizmos.DrawRay(itemSpawnPoint.position, itemSpawnPoint.up * 0.3f);
    }
}