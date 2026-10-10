using UnityEngine;

public class DungeonPropSpawner : MonoBehaviour
{
    public enum RotationMode
    {
        KeepSpawnerRotation, // sundin ang direksyon ng spawner
        RandomYaw,           // kahit anong anggulo
        Snap90               // 0, 90, 180, o 270 lang
    }

    [System.Serializable]
    public class PropEntry
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f; // mas mataas = mas madalas lumabas
    }

    [Header("Props")]
    [SerializeField] private PropEntry[] props;

    [Header("Spawn Settings")]
    [Range(0f, 1f)]
    [Tooltip("1 = laging may lalabas. 0.5 = 50% chance, minsan wala.")]
    [SerializeField] private float spawnChance = 1f;

    [SerializeField] private RotationMode rotationMode = RotationMode.KeepSpawnerRotation;

    [Tooltip("Random na usog sa floor (X at Z). 0 = eksaktong nasa spawner.")]
    [SerializeField] private float positionJitter = 0f;

    [Tooltip("Min at Max na scale. 1,1 = walang pagbabago.")]
    [SerializeField] private Vector2 scaleRange = Vector2.one;

    public GameObject SpawnedProp { get; private set; }

    // Tinatawag ng DungeonGenerator, huwag tawagin sa Start()
    public void Spawn()
    {
        if (SpawnedProp != null)
        {
            Destroy(SpawnedProp);
            SpawnedProp = null;
        }

        if (Random.value > spawnChance)
            return;

        GameObject prefab = PickPrefab();

        if (prefab == null)
            return;

        Vector3 position = transform.position;

        if (positionJitter > 0f)
        {
            Vector2 offset = Random.insideUnitCircle * positionJitter;
            position += new Vector3(offset.x, 0f, offset.y);
        }

        Quaternion rotation = transform.rotation;

        switch (rotationMode)
        {
            case RotationMode.RandomYaw:
                rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                break;

            case RotationMode.Snap90:
                rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f)
                           * transform.rotation;
                break;
        }

        SpawnedProp = Instantiate(prefab, position, rotation, transform);

        float minScale = Mathf.Min(scaleRange.x, scaleRange.y);
        float maxScale = Mathf.Max(scaleRange.x, scaleRange.y);

        if (!Mathf.Approximately(minScale, 1f) || !Mathf.Approximately(maxScale, 1f))
        {
            SpawnedProp.transform.localScale *= Random.Range(minScale, maxScale);
        }
    }

    private GameObject PickPrefab()
    {
        if (props == null || props.Length == 0)
            return null;

        float total = 0f;

        foreach (PropEntry entry in props)
        {
            if (entry != null && entry.prefab != null)
                total += entry.weight;
        }

        if (total <= 0f)
            return null;

        float roll = Random.value * total;
        GameObject last = null;

        foreach (PropEntry entry in props)
        {
            if (entry == null || entry.prefab == null)
                continue;

            last = entry.prefab;
            roll -= entry.weight;

            if (roll <= 0f)
                return entry.prefab;
        }

        return last;
    }

    // Para makita mo ang spawner sa Scene view
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.6f);

        if (positionJitter > 0f)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, positionJitter);
        }
    }
}