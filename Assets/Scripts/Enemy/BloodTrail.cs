using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class BloodTrail : MonoBehaviour
{
    private class Entry
    {
        public GameObject obj;
        public DecalProjector projector;
        public float spawnTime;
    }

    [Header("References")]
    [SerializeField] private PrisonerCrawler crawler;

    [Tooltip("Prefab na may DecalProjector (kahit anong decal material).")]
    [SerializeField] private GameObject decalPrefab;

    [Header("Trail")]
    [Tooltip("Gaano kalayo ang lalakarin bago maglagay ulit ng dugo.")]
    [SerializeField] private float spacing = 0.6f;

    [SerializeField] private Vector2 sizeRange = new Vector2(0.5f, 0.9f);

    [Tooltip("Random na usog pakaliwa o pakanan, para hindi tuwid na tuwid.")]
    [SerializeField] private float sideJitter = 0.15f;

    [Header("Look (walang PNG)")]
    [Tooltip("Checked = gagawa ng bilog na patak sa code. Unchecked = parisukat na kulay lang.")]
    [SerializeField] private bool useProceduralBlob = true;

    [Tooltip("RGB = kulay ng dugo. Alpha = kapal.")]
    [SerializeField] private Color bloodColor = new Color(0.35f, 0.03f, 0.03f, 0.9f);

    [Range(16, 256)]
    [SerializeField] private int blobResolution = 64;

    [Header("Lifetime")]
    [SerializeField] private int maxDecals = 60;
    [SerializeField] private float lifetime = 25f;
    [SerializeField] private float fadeTime = 6f;

    [Header("Floor")]
    [Tooltip("Mga layer ng sahig. Huwag isama ang Interactable, RoomBounds, at layer ng enemy.")]
    [SerializeField] private LayerMask floorLayers = ~0;

    private readonly Queue<Entry> active = new Queue<Entry>();
    private readonly Queue<Entry> pool = new Queue<Entry>();

    private Material blobMaterial;
    private Texture2D blobTexture;

    private Vector3 lastSpawnPosition;
    private bool initialized;

    // =========================================================
    // SETUP
    // =========================================================

    private void Awake()
    {
        if (crawler == null)
            crawler = GetComponent<PrisonerCrawler>();

        BuildBlobMaterial();
    }

    private void BuildBlobMaterial()
    {
        if (decalPrefab == null)
            return;

        DecalProjector prefabProjector = decalPrefab.GetComponent<DecalProjector>();

        if (prefabProjector == null || prefabProjector.material == null)
        {
            Debug.LogWarning("[BLOOD] Walang DecalProjector o material ang decal prefab.", this);
            return;
        }

        // Sariling kopya ng material, para hindi mabago ang asset
        blobMaterial = new Material(prefabProjector.material);

        int res = Mathf.Clamp(blobResolution, 16, 256);

        blobTexture = new Texture2D(res, res, TextureFormat.RGBA32, false);
        blobTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float alpha = 1f;

                if (useProceduralBlob)
                {
                    // -1 hanggang 1, nasa gitna ang (0,0)
                    Vector2 p = new Vector2(
                        (x + 0.5f) / res * 2f - 1f,
                        (y + 0.5f) / res * 2f - 1f
                    );

                    float dist = p.magnitude;

                    // Hindi perpektong bilog, medyo magulo ang gilid
                    float noise = Mathf.PerlinNoise(12.34f + p.x * 2.5f, 12.34f + p.y * 2.5f);
                    float edge = 0.75f + (noise - 0.5f) * 0.4f;

                    // Malambot na gilid
                    alpha = Mathf.Clamp01((edge - dist) / 0.25f);
                }

                // Nasa texture mismo ang kulay, kasi walang Base Color ang Decal shader
                blobTexture.SetPixel(
                    x,
                    y,
                    new Color(bloodColor.r, bloodColor.g, bloodColor.b, alpha * bloodColor.a)
                );
            }
        }

        blobTexture.Apply();

        string textureProp = blobMaterial.HasProperty("_BaseMap")
            ? "_BaseMap"
            : "_MainTex";

        blobMaterial.SetTexture(textureProp, blobTexture);

        Debug.Log($"[BLOOD] Blob texture assigned to {textureProp}.", this);
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        UpdateFade();

        if (decalPrefab == null || crawler == null)
            return;

        // Hindi pa tapos mag-spawn ang crawler, bawal pang basahin ang networked na value
        if (crawler.Object == null || !crawler.Object.IsValid)
            return;

        // Walang dugo habang nakahiga pa (hindi pa gising)
        if (!crawler.IsAwake)
        {
            initialized = false;
            return;
        }

        if (!initialized)
        {
            initialized = true;
            lastSpawnPosition = transform.position;
            return;
        }

        Vector3 delta = transform.position - lastSpawnPosition;
        delta.y = 0f;

        if (delta.magnitude >= spacing)
        {
            lastSpawnPosition = transform.position;
            SpawnDecal();
        }
    }

    // =========================================================
    // SPAWN
    // =========================================================

    private void SpawnDecal()
    {
        Vector3 origin =
            transform.position +
            Vector3.up * 0.5f +
            transform.right * Random.Range(-sideJitter, sideJitter);

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                2f,
                floorLayers,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Entry entry = GetEntry();

        // Ang decal ay nagpo-project sa +Z, kaya ituro ito pababa sa sahig
        Quaternion rotation =
            Quaternion.LookRotation(-hit.normal, Vector3.forward) *
            Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);

        entry.obj.transform.SetPositionAndRotation(
            hit.point + hit.normal * 0.1f,
            rotation
        );

        float size = Random.Range(sizeRange.x, sizeRange.y);

        if (entry.projector != null)
        {
            entry.projector.size = new Vector3(size, size, entry.projector.size.z);
            entry.projector.fadeFactor = 1f;
        }

        entry.spawnTime = Time.time;
        entry.obj.SetActive(true);

        active.Enqueue(entry);
    }

    private Entry GetEntry()
    {
        // Kung puno na, gamitin ulit ang pinakamatanda
        if (active.Count >= maxDecals)
            return active.Dequeue();

        if (pool.Count > 0)
            return pool.Dequeue();

        // Hindi child ng crawler, para hindi sumunod sa galaw niya
        GameObject obj = Instantiate(decalPrefab);

        DecalProjector projector = obj.GetComponent<DecalProjector>();

        if (projector != null && blobMaterial != null)
            projector.material = blobMaterial;

        return new Entry
        {
            obj = obj,
            projector = projector
        };
    }

    // =========================================================
    // FADE
    // =========================================================

    private void UpdateFade()
    {
        while (active.Count > 0)
        {
            Entry oldest = active.Peek();

            float age = Time.time - oldest.spawnTime;

            // Pagod na ang pinakamatanda, ibalik sa pool
            if (age >= lifetime)
            {
                active.Dequeue();
                oldest.obj.SetActive(false);
                pool.Enqueue(oldest);
                continue;
            }

            break;
        }

        // Unti-unting mawawala ang malapit nang mag-expire
        foreach (Entry e in active)
        {
            if (e.projector == null)
                continue;

            float remaining = lifetime - (Time.time - e.spawnTime);

            e.projector.fadeFactor =
                remaining >= fadeTime
                    ? 1f
                    : Mathf.Clamp01(remaining / fadeTime);
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        // Linisin ang mga decal kapag nawala ang crawler
        foreach (Entry e in active)
            if (e.obj != null) Destroy(e.obj);

        foreach (Entry e in pool)
            if (e.obj != null) Destroy(e.obj);

        if (blobTexture != null) Destroy(blobTexture);
        if (blobMaterial != null) Destroy(blobMaterial);
    }
}