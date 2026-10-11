using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class MissionItemSpawnArea : MonoBehaviour
{
    [Header("Spawn Area")]
    [SerializeField] private BoxCollider areaCollider;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField, Min(1f)] private float raycastDistance = 20f;
    [SerializeField, Min(0f)] private float verticalOffset = 0.15f;

    private void Reset()
    {
        areaCollider = GetComponent<BoxCollider>();
        if (areaCollider != null)
            areaCollider.isTrigger = true;
    }

    private void Awake()
    {
        if (areaCollider == null)
            areaCollider = GetComponent<BoxCollider>();
    }

    public bool TryGetRandomSpawnPosition(out Vector3 position)
    {
        position = Vector3.zero;

        if (areaCollider == null)
            areaCollider = GetComponent<BoxCollider>();

        if (areaCollider == null || !areaCollider.enabled || !gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[SPAWN AREA] Missing, disabled, or inactive collider: {name}", this);
            return false;
        }

        // Prefer explicitly named child markers. Ignore unrelated child transforms.
        List<Transform> childPoints = new List<Transform>();
        foreach (Transform child in areaCollider.transform)
        {
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            bool isMarker =
                child.name.IndexOf("ItemSpawn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("SpawnPoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.Equals("Spawn", StringComparison.OrdinalIgnoreCase) ||
                child.name.Equals("Point", StringComparison.OrdinalIgnoreCase);

            if (isMarker)
                childPoints.Add(child);
        }

        if (childPoints.Count > 0)
        {
            Transform picked = childPoints[UnityEngine.Random.Range(0, childPoints.Count)];
            position = picked.position + Vector3.up * verticalOffset;
            return true;
        }

        Bounds bounds = areaCollider.bounds;
        float searchDistance = Mathf.Max(raycastDistance, bounds.size.y + 3f);

        for (int attempt = 0; attempt < 30; attempt++)
        {
            float x = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
            float z = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);
            Vector3 origin = new Vector3(x, bounds.max.y + 0.5f, z);

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                    searchDistance, groundLayers, QueryTriggerInteraction.Ignore))
                continue;

            if (hit.point.y < bounds.min.y - 2f || hit.point.y > bounds.max.y + 0.5f)
                continue;

            position = hit.point + Vector3.up * verticalOffset;
            Debug.DrawLine(origin, hit.point, Color.green, 5f);
            Debug.Log($"[SPAWN AREA] Valid position in {name}: {position}", this);
            return true;
        }

        // The area can be placed directly on the surface as a fallback.
        float fallbackX = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
        float fallbackZ = UnityEngine.Random.Range(bounds.min.z, bounds.max.z);
        position = new Vector3(fallbackX, bounds.max.y + verticalOffset, fallbackZ);
        Debug.LogWarning($"[SPAWN AREA] Ground raycast failed for {name}; using collider top at {position}.", this);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider box = areaCollider != null ? areaCollider : GetComponent<BoxCollider>();
        if (box == null)
            return;

        Gizmos.color = Color.cyan;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = box.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = oldMatrix;
    }
}
