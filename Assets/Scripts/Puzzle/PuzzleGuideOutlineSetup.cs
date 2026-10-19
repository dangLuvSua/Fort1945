using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class PuzzleGuideOutlineSetup : MonoBehaviour
{
    public enum SurfaceSide
    {
        Max,
        Min
    }

    // =========================================================
    // MATERIALS
    // =========================================================

    [Header("Materials")]

    [Tooltip("Normal black material used by the puzzle guide pieces.")]
    [SerializeField]
    private Material guideMaterial;

    [Tooltip("URP Unlit material used for the visible outline.")]
    [SerializeField]
    private Material outlineMaterial;

    // =========================================================
    // OUTLINE SETTINGS
    // =========================================================

    [Header("Outline Settings")]

    [Tooltip("Width of the outline in world units.")]
    [SerializeField]
    [Min(0.001f)]
    private float lineWidth = 0.012f;

    [Tooltip(
        "Very small world-space lift above the puzzle surface " +
        "to prevent z-fighting."
    )]
    [SerializeField]
    [Min(0f)]
    private float worldSurfaceOffset = 0.001f;

    [Tooltip(
        "Which flat side of the mesh receives the outline. " +
        "If the outline does not appear, switch Max to Min."
    )]
    [SerializeField]
    private SurfaceSide surfaceSide =
        SurfaceSide.Max;

    [Tooltip("Only objects beginning with this name are processed.")]
    [SerializeField]
    private string pieceNamePrefix =
        "JIGSAW.";

    // =========================================================
    // INTERNAL EDGE
    // =========================================================

    private struct Edge : IEquatable<Edge>
    {
        public int a;
        public int b;

        public Edge(int first, int second)
        {
            if (first < second)
            {
                a = first;
                b = second;
            }
            else
            {
                a = second;
                b = first;
            }
        }

        public bool Equals(Edge other)
        {
            return a == other.a &&
                   b == other.b;
        }

        public override bool Equals(object obj)
        {
            return obj is Edge other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (a * 397) ^ b;
            }
        }
    }

    // =========================================================
    // BUILD
    // =========================================================

    [ContextMenu("Build Puzzle Guide Outlines")]
    public void BuildPuzzleGuideOutlines()
    {
        if (guideMaterial == null)
        {
            Debug.LogError(
                "[PUZZLE GUIDE OUTLINE] Guide Material is missing."
            );

            return;
        }

        if (outlineMaterial == null)
        {
            Debug.LogError(
                "[PUZZLE GUIDE OUTLINE] Outline Material is missing."
            );

            return;
        }

        Transform[] allTransforms =
            GetComponentsInChildren<Transform>(
                true
            );

        List<Transform> puzzlePieces =
            new List<Transform>();

        foreach (Transform candidate in allTransforms)
        {
            if (candidate == null ||
                candidate == transform)
            {
                continue;
            }

            if (!candidate.name.StartsWith(
                    pieceNamePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            MeshFilter filter =
                candidate.GetComponent<MeshFilter>();

            MeshRenderer renderer =
                candidate.GetComponent<MeshRenderer>();

            if (filter == null ||
                filter.sharedMesh == null ||
                renderer == null)
            {
                continue;
            }

            puzzlePieces.Add(
                candidate
            );
        }

        int configured =
            0;

        foreach (Transform piece in puzzlePieces)
        {
            if (BuildOutlineForPiece(
                    piece))
            {
                configured++;
            }
        }

        Debug.Log(
            $"[PUZZLE GUIDE OUTLINE] COMPLETE. " +
            $"{configured}/{puzzlePieces.Count} pieces configured " +
            $"under {name}."
        );
    }

    // =========================================================
    // SINGLE PIECE
    // =========================================================

    private bool BuildOutlineForPiece(
        Transform piece)
    {
        MeshFilter sourceFilter =
            piece.GetComponent<MeshFilter>();

        MeshRenderer sourceRenderer =
            piece.GetComponent<MeshRenderer>();

        if (sourceFilter == null ||
            sourceFilter.sharedMesh == null ||
            sourceRenderer == null)
        {
            return false;
        }

        sourceRenderer.sharedMaterial =
            guideMaterial;

        RemoveGeneratedOutline(
            piece
        );

        Mesh mesh =
            sourceFilter.sharedMesh;

        Vector3[] vertices;
        int[] triangles;

        try
        {
            vertices =
                mesh.vertices;

            triangles =
                mesh.triangles;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[PUZZLE GUIDE OUTLINE] Could not read " +
                $"{mesh.name}: {exception.Message}"
            );

            return false;
        }

        if (vertices == null ||
            vertices.Length == 0 ||
            triangles == null ||
            triangles.Length < 3)
        {
            return false;
        }

        Bounds bounds =
            mesh.bounds;

        int thinAxis =
            GetSmallestBoundsAxis(
                bounds
            );

        float maximumDimension =
            Mathf.Max(
                bounds.size.x,
                bounds.size.y,
                bounds.size.z
            );

        float surfaceTolerance =
            Mathf.Max(
                0.00001f,
                maximumDimension * 0.001f
            );

        float weldTolerance =
            Mathf.Max(
                0.000001f,
                maximumDimension * 0.00001f
            );

        float surfaceCoordinate =
            surfaceSide == SurfaceSide.Max
                ? GetAxis(
                    bounds.max,
                    thinAxis
                )
                : GetAxis(
                    bounds.min,
                    thinAxis
                );

        Dictionary<Vector3Int, int> weldedLookup =
            new Dictionary<Vector3Int, int>();

        List<Vector3> weldedPositions =
            new List<Vector3>();

        Dictionary<int, int> originalToWelded =
            new Dictionary<int, int>();

        Dictionary<Edge, int> edgeCounts =
            new Dictionary<Edge, int>();

        int selectedTriangleCount =
            0;

        for (int i = 0;
             i + 2 < triangles.Length;
             i += 3)
        {
            int ia =
                triangles[i];

            int ib =
                triangles[i + 1];

            int ic =
                triangles[i + 2];

            Vector3 a =
                vertices[ia];

            Vector3 b =
                vertices[ib];

            Vector3 c =
                vertices[ic];

            if (!IsOnSurface(
                    a,
                    thinAxis,
                    surfaceCoordinate,
                    surfaceTolerance) ||
                !IsOnSurface(
                    b,
                    thinAxis,
                    surfaceCoordinate,
                    surfaceTolerance) ||
                !IsOnSurface(
                    c,
                    thinAxis,
                    surfaceCoordinate,
                    surfaceTolerance))
            {
                continue;
            }

            selectedTriangleCount++;

            int wa =
                GetWeldedVertex(
                    ia,
                    vertices,
                    weldTolerance,
                    originalToWelded,
                    weldedLookup,
                    weldedPositions
                );

            int wb =
                GetWeldedVertex(
                    ib,
                    vertices,
                    weldTolerance,
                    originalToWelded,
                    weldedLookup,
                    weldedPositions
                );

            int wc =
                GetWeldedVertex(
                    ic,
                    vertices,
                    weldTolerance,
                    originalToWelded,
                    weldedLookup,
                    weldedPositions
                );

            CountEdge(
                edgeCounts,
                new Edge(
                    wa,
                    wb
                )
            );

            CountEdge(
                edgeCounts,
                new Edge(
                    wb,
                    wc
                )
            );

            CountEdge(
                edgeCounts,
                new Edge(
                    wc,
                    wa
                )
            );
        }

        if (selectedTriangleCount == 0)
        {
            Debug.LogWarning(
                $"[PUZZLE GUIDE OUTLINE] No surface triangles " +
                $"were found for {piece.name}. " +
                $"Try switching Surface Side."
            );

            return false;
        }

        List<Edge> boundaryEdges =
            new List<Edge>();

        foreach (KeyValuePair<Edge, int> pair
                 in edgeCounts)
        {
            if (pair.Value == 1)
            {
                boundaryEdges.Add(
                    pair.Key
                );
            }
        }

        if (boundaryEdges.Count == 0)
        {
            Debug.LogWarning(
                $"[PUZZLE GUIDE OUTLINE] No boundary was found " +
                $"for {piece.name}."
            );

            return false;
        }

        List<List<int>> loops =
            BuildBoundaryLoops(
                boundaryEdges
            );

        if (loops.Count == 0)
        {
            Debug.LogWarning(
                $"[PUZZLE GUIDE OUTLINE] Could not construct " +
                $"an outline loop for {piece.name}."
            );

            return false;
        }

        GameObject outlineRoot =
            new GameObject(
                "Outline"
            );

        outlineRoot.transform.SetParent(
            piece,
            false
        );

        outlineRoot.transform.localPosition =
            Vector3.zero;

        outlineRoot.transform.localRotation =
            Quaternion.identity;

        outlineRoot.transform.localScale =
            Vector3.one;

        float localSurfaceOffset =
            ConvertWorldOffsetToLocal(
                piece,
                thinAxis,
                worldSurfaceOffset
            );

        float offsetDirection =
            surfaceSide == SurfaceSide.Max
                ? 1f
                : -1f;

        for (int loopIndex = 0;
             loopIndex < loops.Count;
             loopIndex++)
        {
            List<int> loop =
                loops[loopIndex];

            if (loop.Count < 3)
            {
                continue;
            }

            GameObject lineObject =
                new GameObject(
                    $"Loop_{loopIndex:00}"
                );

            lineObject.transform.SetParent(
                outlineRoot.transform,
                false
            );

            LineRenderer line =
                lineObject.AddComponent<LineRenderer>();

            line.useWorldSpace =
                false;

            line.loop =
                true;

            line.material =
                outlineMaterial;

            line.widthMultiplier =
                lineWidth;

            line.numCornerVertices =
                5;

            line.numCapVertices =
                2;

            line.alignment =
                LineAlignment.View;

            line.textureMode =
                LineTextureMode.Stretch;

            line.shadowCastingMode =
                ShadowCastingMode.Off;

            line.receiveShadows =
                false;

            line.lightProbeUsage =
                LightProbeUsage.Off;

            line.reflectionProbeUsage =
                ReflectionProbeUsage.Off;

            line.positionCount =
                loop.Count;

            for (int pointIndex = 0;
                 pointIndex < loop.Count;
                 pointIndex++)
            {
                Vector3 point =
                    weldedPositions[
                        loop[pointIndex]
                    ];

                SetAxis(
                    ref point,
                    thinAxis,
                    surfaceCoordinate +
                    localSurfaceOffset *
                    offsetDirection
                );

                line.SetPosition(
                    pointIndex,
                    point
                );
            }
        }

        return true;
    }

    // =========================================================
    // BOUNDARY LOOPS
    // =========================================================

    private List<List<int>> BuildBoundaryLoops(
        List<Edge> boundaryEdges)
    {
        Dictionary<int, List<int>> adjacency =
            new Dictionary<int, List<int>>();

        HashSet<Edge> unusedEdges =
            new HashSet<Edge>();

        foreach (Edge edge in boundaryEdges)
        {
            AddNeighbour(
                adjacency,
                edge.a,
                edge.b
            );

            AddNeighbour(
                adjacency,
                edge.b,
                edge.a
            );

            unusedEdges.Add(
                edge
            );
        }

        List<List<int>> result =
            new List<List<int>>();

        while (unusedEdges.Count > 0)
        {
            Edge startingEdge =
                default;

            foreach (Edge edge in unusedEdges)
            {
                startingEdge =
                    edge;

                break;
            }

            unusedEdges.Remove(
                startingEdge
            );

            List<int> loop =
                new List<int>();

            loop.Add(
                startingEdge.a
            );

            loop.Add(
                startingEdge.b
            );

            int start =
                startingEdge.a;

            int current =
                startingEdge.b;

            bool closed =
                false;

            int guard =
                0;

            while (guard <
                   boundaryEdges.Count + 10)
            {
                guard++;

                if (!adjacency.TryGetValue(
                        current,
                        out List<int> neighbours))
                {
                    break;
                }

                int next =
                    -1;

                foreach (int candidate
                         in neighbours)
                {
                    Edge candidateEdge =
                        new Edge(
                            current,
                            candidate
                        );

                    if (!unusedEdges.Contains(
                            candidateEdge))
                    {
                        continue;
                    }

                    next =
                        candidate;

                    unusedEdges.Remove(
                        candidateEdge
                    );

                    break;
                }

                if (next < 0)
                {
                    break;
                }

                if (next == start)
                {
                    closed =
                        true;

                    break;
                }

                loop.Add(
                    next
                );

                current =
                    next;
            }

            if (closed &&
                loop.Count >= 3)
            {
                result.Add(
                    loop
                );
            }
        }

        return result;
    }

    // =========================================================
    // WELDING
    // =========================================================

    private int GetWeldedVertex(
        int originalIndex,
        Vector3[] vertices,
        float tolerance,
        Dictionary<int, int> originalToWelded,
        Dictionary<Vector3Int, int> weldedLookup,
        List<Vector3> weldedPositions)
    {
        if (originalToWelded.TryGetValue(
                originalIndex,
                out int existing))
        {
            return existing;
        }

        Vector3 position =
            vertices[originalIndex];

        Vector3Int key =
            new Vector3Int(
                Mathf.RoundToInt(
                    position.x /
                    tolerance
                ),
                Mathf.RoundToInt(
                    position.y /
                    tolerance
                ),
                Mathf.RoundToInt(
                    position.z /
                    tolerance
                )
            );

        if (!weldedLookup.TryGetValue(
                key,
                out int weldedIndex))
        {
            weldedIndex =
                weldedPositions.Count;

            weldedLookup.Add(
                key,
                weldedIndex
            );

            weldedPositions.Add(
                position
            );
        }

        originalToWelded.Add(
            originalIndex,
            weldedIndex
        );

        return weldedIndex;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private void CountEdge(
        Dictionary<Edge, int> edgeCounts,
        Edge edge)
    {
        if (edge.a == edge.b)
        {
            return;
        }

        if (edgeCounts.TryGetValue(
                edge,
                out int count))
        {
            edgeCounts[edge] =
                count + 1;
        }
        else
        {
            edgeCounts.Add(
                edge,
                1
            );
        }
    }

    private void AddNeighbour(
        Dictionary<int, List<int>> adjacency,
        int from,
        int to)
    {
        if (!adjacency.TryGetValue(
                from,
                out List<int> neighbours))
        {
            neighbours =
                new List<int>();

            adjacency.Add(
                from,
                neighbours
            );
        }

        if (!neighbours.Contains(
                to))
        {
            neighbours.Add(
                to
            );
        }
    }

    private bool IsOnSurface(
        Vector3 point,
        int axis,
        float surfaceCoordinate,
        float tolerance)
    {
        return Mathf.Abs(
                   GetAxis(
                       point,
                       axis
                   ) -
                   surfaceCoordinate
               ) <= tolerance;
    }

    private int GetSmallestBoundsAxis(
        Bounds bounds)
    {
        Vector3 size =
            bounds.size;

        if (size.x <= size.y &&
            size.x <= size.z)
        {
            return 0;
        }

        if (size.y <= size.x &&
            size.y <= size.z)
        {
            return 1;
        }

        return 2;
    }

    private float GetAxis(
        Vector3 value,
        int axis)
    {
        switch (axis)
        {
            case 0:
                return value.x;

            case 1:
                return value.y;

            default:
                return value.z;
        }
    }

    private void SetAxis(
        ref Vector3 value,
        int axis,
        float coordinate)
    {
        switch (axis)
        {
            case 0:

                value.x =
                    coordinate;

                break;

            case 1:

                value.y =
                    coordinate;

                break;

            default:

                value.z =
                    coordinate;

                break;
        }
    }

    private float ConvertWorldOffsetToLocal(
        Transform piece,
        int axis,
        float worldOffset)
    {
        if (worldOffset <= 0f)
        {
            return 0f;
        }

        Vector3 scale =
            piece.lossyScale;

        float axisScale;

        switch (axis)
        {
            case 0:

                axisScale =
                    Mathf.Abs(
                        scale.x
                    );

                break;

            case 1:

                axisScale =
                    Mathf.Abs(
                        scale.y
                    );

                break;

            default:

                axisScale =
                    Mathf.Abs(
                        scale.z
                    );

                break;
        }

        if (axisScale <=
            0.000001f)
        {
            return 0f;
        }

        return worldOffset /
               axisScale;
    }

    // =========================================================
    // REMOVE
    // =========================================================

    [ContextMenu("Remove Puzzle Guide Outlines")]
    public void RemovePuzzleGuideOutlines()
    {
        Transform[] allTransforms =
            GetComponentsInChildren<Transform>(
                true
            );

        List<GameObject> objectsToDelete =
            new List<GameObject>();

        foreach (Transform child
                 in allTransforms)
        {
            if (child == null ||
                child == transform)
            {
                continue;
            }

            if (child.name ==
                "Outline")
            {
                objectsToDelete.Add(
                    child.gameObject
                );
            }
        }

        foreach (GameObject target
                 in objectsToDelete)
        {
            if (target == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(
                    target
                );
            }
            else
            {
                DestroyImmediate(
                    target
                );
            }
        }

        Debug.Log(
            $"[PUZZLE GUIDE OUTLINE] Removed " +
            $"{objectsToDelete.Count} generated outlines."
        );
    }

    private void RemoveGeneratedOutline(
        Transform piece)
    {
        Transform existing =
            piece.Find(
                "Outline"
            );

        if (existing == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(
                existing.gameObject
            );
        }
        else
        {
            DestroyImmediate(
                existing.gameObject
            );
        }
    }
}