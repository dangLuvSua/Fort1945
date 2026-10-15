using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PuzzleTableController : MonoBehaviour
{
    [Serializable]
    public class PuzzlePieceEntry
    {
        [Header("Piece")]
        public ItemData item;

        [Tooltip("Invisible correct position from PuzzleSolvedTargets_Easy.")]
        public Transform solvedTarget;

        [Tooltip("Visible black guide piece from PuzzleGuide_Easy.")]
        public Renderer guideRenderer;
    }

    // =========================================================
    // TESTING
    // =========================================================

    [Header("Testing")]
    [Tooltip("Show all puzzle pieces without collecting them.")]
    [SerializeField]
    private bool testModeShowAllPieces = false;

    // =========================================================
    // VISUAL
    // =========================================================

    [Header("Puzzle Piece Visual")]
    [SerializeField]
    private Material puzzlePieceMaterial;

    // =========================================================
    // PUZZLE PIECES
    // =========================================================

    [Header("Easy Puzzle Pieces")]
    [SerializeField]
    private PuzzlePieceEntry[] pieces;

    // =========================================================
    // RUNTIME PIECE AREA
    // =========================================================

    [Header("Runtime Piece Area")]

    [SerializeField]
    private Transform scatterCenter;

    [SerializeField]
    private Vector2 scatterSize =
        new Vector2(1.2f, 1.2f);

    [SerializeField]
    private float surfaceOffset = 0.02f;

    [SerializeField]
    private Transform runtimePiecesRoot;

    [Tooltip("Box Collider covering the black puzzle board.")]
    [SerializeField]
    private BoxCollider puzzleDragArea;

    // =========================================================
    // CONTROLS
    // =========================================================

    [Header("Piece Controls")]

    [SerializeField]
    private float snapDistance = 0.18f;

    [SerializeField]
    private float snapRotationTolerance = 20f;

    [SerializeField]
    private float rotationStep = 90f;

    // =========================================================
    // RUNTIME
    // =========================================================

    private readonly List<GameObject> runtimePieces =
        new List<GameObject>();

    private Camera puzzleCamera;

    private PuzzleRuntimePiece selectedPiece;

    private bool puzzleActive;

    private int snappedCount;

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!puzzleActive ||
            puzzleCamera == null ||
            Mouse.current == null)
        {
            return;
        }

        // Select a puzzle piece.
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectPiece();
        }

        // Drag and rotate.
        if (selectedPiece != null &&
            Mouse.current.leftButton.isPressed)
        {
            DragSelectedPiece();

            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.wasPressedThisFrame)
                {
                    RotateSelectedPiece(-rotationStep);
                }

                if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    RotateSelectedPiece(rotationStep);
                }
            }
        }

        // Release and attempt snap.
        if (selectedPiece != null &&
            Mouse.current.leftButton.wasReleasedThisFrame)
        {
            selectedPiece.TrySnap(
                snapDistance,
                snapRotationTolerance
            );

            selectedPiece = null;
        }
    }

    // =========================================================
    // BEGIN PUZZLE
    // =========================================================

    public void BeginPuzzle(
        PlayerInventory inventory,
        Camera camera)
    {
        ClearRuntimePieces();

        puzzleCamera = camera;

        selectedPiece = null;

        snappedCount = 0;

        // Reset all black guide pieces.
        ResetGuidePieces();

        if (puzzleCamera == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] Puzzle camera is missing."
            );

            return;
        }

        if (scatterCenter == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] Scatter Center is missing."
            );

            return;
        }

        if (puzzleDragArea == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] Puzzle Drag Area is missing."
            );

            return;
        }

        if (!testModeShowAllPieces &&
            inventory == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] PlayerInventory is missing."
            );

            return;
        }

        if (pieces == null ||
            pieces.Length == 0)
        {
            Debug.LogError(
                "[PUZZLE TABLE] No puzzle pieces configured."
            );

            return;
        }

        int availableCount = 0;

        foreach (PuzzlePieceEntry entry in pieces)
        {
            if (entry == null ||
                entry.item == null ||
                entry.solvedTarget == null)
            {
                Debug.LogWarning(
                    "[PUZZLE TABLE] Incomplete puzzle entry."
                );

                continue;
            }

            // Normal gameplay:
            // Only show collected puzzle pieces.
            if (!testModeShowAllPieces)
            {
                if (!inventory.HasItemId(
                        entry.item.itemId))
                {
                    continue;
                }
            }

            SpawnRuntimePiece(entry);

            availableCount++;
        }

        puzzleActive = true;

        if (testModeShowAllPieces)
        {
            Debug.Log(
                $"[PUZZLE TABLE] TEST MODE: " +
                $"Spawned {availableCount}/{pieces.Length} pieces."
            );
        }
        else
        {
            Debug.Log(
                $"[PUZZLE TABLE] Player has " +
                $"{availableCount}/{pieces.Length} puzzle pieces."
            );
        }
    }

    // =========================================================
    // END PUZZLE
    // =========================================================

    public void EndPuzzle()
    {
        puzzleActive = false;

        selectedPiece = null;

        puzzleCamera = null;

        ClearRuntimePieces();

        // TEMPORARY:
        // Reset puzzle when leaving puzzle mode.
        ResetGuidePieces();
    }

    // =========================================================
    // SELECT PIECE
    // =========================================================

    private void TrySelectPiece()
    {
        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        Ray ray =
            puzzleCamera.ScreenPointToRay(
                mousePosition
            );

        RaycastHit[] hits =
            Physics.RaycastAll(
                ray,
                100f,
                ~0,
                QueryTriggerInteraction.Ignore
            );

        PuzzleRuntimePiece bestPiece = null;

        float bestDistance =
            float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            PuzzleRuntimePiece piece =
                hit.collider
                    .GetComponentInParent<PuzzleRuntimePiece>();

            if (piece == null ||
                piece.IsLocked)
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance =
                    hit.distance;

                bestPiece =
                    piece;
            }
        }

        selectedPiece =
            bestPiece;

        if (selectedPiece != null)
        {
            Debug.Log(
                $"[PUZZLE] Selected " +
                $"{selectedPiece.Item.itemName}"
            );
        }
    }

    // =========================================================
    // DRAG PIECE
    // =========================================================

    private void DragSelectedPiece()
    {
        if (selectedPiece == null ||
            puzzleCamera == null ||
            puzzleDragArea == null ||
            Mouse.current == null)
        {
            return;
        }

        Transform areaTransform =
            puzzleDragArea.transform;

        Vector3 boardNormal =
            areaTransform.up;

        Plane boardPlane =
            new Plane(
                boardNormal,
                areaTransform.position +
                boardNormal * surfaceOffset
            );

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        Ray ray =
            puzzleCamera.ScreenPointToRay(
                mousePosition
            );

        if (!boardPlane.Raycast(
                ray,
                out float enter))
        {
            return;
        }

        Vector3 worldHit =
            ray.GetPoint(enter);

        Vector3 localHit =
            areaTransform.InverseTransformPoint(
                worldHit
            );

        Vector3 center =
            puzzleDragArea.center;

        Vector3 size =
            puzzleDragArea.size;

        float minX =
            center.x -
            size.x * 0.5f;

        float maxX =
            center.x +
            size.x * 0.5f;

        float minZ =
            center.z -
            size.z * 0.5f;

        float maxZ =
            center.z +
            size.z * 0.5f;

        localHit.x =
            Mathf.Clamp(
                localHit.x,
                minX,
                maxX
            );

        localHit.z =
            Mathf.Clamp(
                localHit.z,
                minZ,
                maxZ
            );

        localHit.y =
            center.y +
            surfaceOffset;

        selectedPiece.transform.position =
            areaTransform.TransformPoint(
                localHit
            );
    }

    // =========================================================
    // ROTATE PIECE
    // =========================================================

    private void RotateSelectedPiece(
        float angle)
    {
        if (selectedPiece == null)
            return;

        Transform rotationReference =
            puzzleDragArea != null
                ? puzzleDragArea.transform
                : scatterCenter;

        selectedPiece.transform.rotation =
            Quaternion.AngleAxis(
                angle,
                rotationReference.up
            ) *
            selectedPiece.transform.rotation;
    }

    // =========================================================
    // SPAWN PIECE
    // =========================================================

    private void SpawnRuntimePiece(
        PuzzlePieceEntry entry)
    {
        GameObject pieceObject =
            Instantiate(
                entry.solvedTarget.gameObject,
                entry.solvedTarget.parent
            );

        pieceObject.name =
            $"Runtime_{entry.item.itemName}";

        // Remove target component from runtime copy.
        PuzzleTarget[] targetComponents =
            pieceObject.GetComponentsInChildren<PuzzleTarget>(
                true
            );

        foreach (PuzzleTarget target in targetComponents)
        {
            if (target != null)
            {
                Destroy(target);
            }
        }

        // Enable puzzle-piece visual.
        Renderer[] renderers =
            pieceObject.GetComponentsInChildren<Renderer>(
                true
            );

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            renderer.enabled = true;

            if (puzzlePieceMaterial != null)
            {
                renderer.sharedMaterial =
                    puzzlePieceMaterial;
            }
        }

        // Parent to runtime container.
        if (runtimePiecesRoot != null)
        {
            pieceObject.transform.SetParent(
                runtimePiecesRoot,
                true
            );
        }

        // Random start position.
        float randomX =
            UnityEngine.Random.Range(
                -scatterSize.x * 0.5f,
                scatterSize.x * 0.5f
            );

        float randomZ =
            UnityEngine.Random.Range(
                -scatterSize.y * 0.5f,
                scatterSize.y * 0.5f
            );

        pieceObject.transform.position =
            scatterCenter.position +
            scatterCenter.right * randomX +
            scatterCenter.forward * randomZ +
            scatterCenter.up * surfaceOffset;

        // Random 90-degree rotation.
        float randomRotation =
            UnityEngine.Random.Range(
                0,
                4
            ) * 90f;

        pieceObject.transform.rotation =
            Quaternion.AngleAxis(
                randomRotation,
                scatterCenter.up
            ) *
            entry.solvedTarget.rotation;

        // Make sure colliders are enabled.
        Collider[] existingColliders =
            pieceObject.GetComponentsInChildren<Collider>(
                true
            );

        foreach (Collider col in existingColliders)
        {
            if (col != null)
            {
                col.enabled = true;
            }
        }

        // Add MeshCollider if no collider exists.
        if (existingColliders.Length == 0)
        {
            MeshFilter meshFilter =
                pieceObject.GetComponentInChildren<MeshFilter>(
                    true
                );

            if (meshFilter != null &&
                meshFilter.sharedMesh != null)
            {
                MeshCollider meshCollider =
                    meshFilter.gameObject
                        .AddComponent<MeshCollider>();

                meshCollider.sharedMesh =
                    meshFilter.sharedMesh;
            }
        }

        PuzzleRuntimePiece runtimePiece =
            pieceObject.AddComponent<PuzzleRuntimePiece>();

        runtimePiece.Initialize(
            entry.item,
            entry.solvedTarget,
            this
        );

        runtimePieces.Add(
            pieceObject
        );
    }

    // =========================================================
    // PIECE SNAPPED
    // =========================================================

    public void NotifyPieceSnapped(
        PuzzleRuntimePiece piece)
    {
        if (piece == null ||
            piece.Item == null)
        {
            return;
        }

        snappedCount++;

        // Find matching entry.
        foreach (PuzzlePieceEntry entry in pieces)
        {
            if (entry == null ||
                entry.item == null)
            {
                continue;
            }

            if (entry.item == piece.Item ||
                entry.item.itemId == piece.Item.itemId)
            {
                // Hide the black guide piece.
                if (entry.guideRenderer != null)
                {
                    entry.guideRenderer.enabled = false;
                }

                break;
            }
        }

        Debug.Log(
            $"[PUZZLE] Correct! " +
            $"{piece.Item.itemName} snapped. " +
            $"Progress: {snappedCount}/{pieces.Length}"
        );

        if (snappedCount >= pieces.Length)
        {
            Debug.Log(
                "[PUZZLE] PUZZLE COMPLETE!"
            );
        }
    }

    // =========================================================
    // RESET BLACK GUIDE
    // =========================================================

    private void ResetGuidePieces()
    {
        if (pieces == null)
            return;

        foreach (PuzzlePieceEntry entry in pieces)
        {
            if (entry == null)
                continue;

            if (entry.guideRenderer != null)
            {
                entry.guideRenderer.enabled = true;
            }
        }
    }

    // =========================================================
    // CLEAR RUNTIME PIECES
    // =========================================================

    private void ClearRuntimePieces()
    {
        foreach (GameObject piece in runtimePieces)
        {
            if (piece != null)
            {
                Destroy(piece);
            }
        }

        runtimePieces.Clear();
    }
}