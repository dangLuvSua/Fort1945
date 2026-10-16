using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

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
    // TIMER
    // =========================================================

    [Header("Puzzle Timer")]

    [Tooltip("Puzzle duration in seconds. 180 = 3 minutes.")]
    [SerializeField]
    private float puzzleDuration = 180f;

    // =========================================================
    // TIMER AUDIO
    // =========================================================

    [Header("Puzzle Timer Audio")]

    [Tooltip("Looping timer sound used before the final 10 seconds.")]
    [SerializeField]
    private AudioSource normalTimerAudioSource;

    [Tooltip("Countdown sound played once when the timer reaches 10.")]
    [SerializeField]
    private AudioSource finalCountdownAudioSource;

    // =========================================================
    // WORLD TIMER
    // =========================================================

    [Header("World Timer UI")]

    [Tooltip("Timer text attached to the World Space Canvas above the table.")]
    [SerializeField]
    private TMP_Text worldTimerText;

    // =========================================================
    // RUNTIME PIECES
    // =========================================================

    private readonly List<GameObject> runtimePieces =
        new List<GameObject>();

    private readonly HashSet<int> solvedItemIds =
        new HashSet<int>();

    private Camera puzzleCamera;

    private PuzzleRuntimePiece selectedPiece;

    private bool puzzleActive;

    private int snappedCount;

    // =========================================================
    // HUD UI
    // =========================================================

    private TMP_Text progressText;

    private TMP_Text timerText;

    // =========================================================
    // TIMER STATE
    // =========================================================

    private float remainingTime;

    private bool timerStarted;

    private bool timerExpired;

    private bool puzzleCompleted;

    // Prevents final countdown SFX from playing repeatedly.
    private bool finalCountdownStarted;

    // =========================================================
    // HUD TIMER VISUAL STATE
    // =========================================================

    private Color timerNormalColor =
        Color.white;

    private Vector3 timerNormalScale =
        Vector3.one;

    private bool timerVisualCached;

    // =========================================================
    // WORLD TIMER VISUAL STATE
    // =========================================================

    private Color worldTimerNormalColor =
        Color.white;

    private Vector3 worldTimerNormalScale =
        Vector3.one;

    private bool worldTimerVisualCached;

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        // Timer and timer audio continue even
        // after the player presses Escape.
        UpdatePuzzleTimer();

        if (!puzzleActive ||
            timerExpired ||
            puzzleCompleted ||
            puzzleCamera == null ||
            Mouse.current == null)
        {
            return;
        }

        // -----------------------------------------------------
        // SELECT PIECE
        // -----------------------------------------------------

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectPiece();
        }

        // -----------------------------------------------------
        // DRAG + ROTATE
        // -----------------------------------------------------

        if (selectedPiece != null &&
            Mouse.current.leftButton.isPressed)
        {
            DragSelectedPiece();

            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.wasPressedThisFrame)
                {
                    RotateSelectedPiece(
                        -rotationStep
                    );
                }

                if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    RotateSelectedPiece(
                        rotationStep
                    );
                }
            }
        }

        // -----------------------------------------------------
        // RELEASE + SNAP
        // -----------------------------------------------------

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
        Camera camera,
        TMP_Text playerProgressText,
        TMP_Text playerTimerText)
    {
        ClearRuntimePieces();

        puzzleCamera = camera;

        selectedPiece = null;

        snappedCount =
            solvedItemIds.Count;

        progressText =
            playerProgressText;

        timerText =
            playerTimerText;

        // -----------------------------------------------------
        // CACHE HUD TIMER VISUALS
        // -----------------------------------------------------

        if (timerText != null &&
            !timerVisualCached)
        {
            timerNormalColor =
                timerText.color;

            timerNormalScale =
                timerText.rectTransform.localScale;

            timerVisualCached = true;
        }

        // -----------------------------------------------------
        // CACHE WORLD TIMER VISUALS
        // -----------------------------------------------------

        if (worldTimerText != null &&
            !worldTimerVisualCached)
        {
            worldTimerNormalColor =
                worldTimerText.color;

            worldTimerNormalScale =
                worldTimerText.rectTransform.localScale;

            worldTimerVisualCached = true;
        }

        ResetGuidePieces();
        ApplySolvedGuideState();

        // -----------------------------------------------------
        // VALIDATION
        // -----------------------------------------------------

        if (puzzleCamera == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] Puzzle camera is missing."
            );

            HidePuzzleHUD();
            return;
        }

        if (scatterCenter == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] Scatter Center is missing."
            );

            HidePuzzleHUD();
            return;
        }

        if (puzzleDragArea == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] Puzzle Drag Area is missing."
            );

            HidePuzzleHUD();
            return;
        }

        if (!testModeShowAllPieces &&
            inventory == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] PlayerInventory is missing."
            );

            HidePuzzleHUD();
            return;
        }

        if (pieces == null ||
            pieces.Length == 0)
        {
            Debug.LogError(
                "[PUZZLE TABLE] No puzzle pieces configured."
            );

            HidePuzzleHUD();
            return;
        }

        // -----------------------------------------------------
        // START TIMER
        // -----------------------------------------------------

        // Timer starts only once.
        // Pressing Esc will NOT restart it.
        if (!timerStarted &&
            !timerExpired &&
            !puzzleCompleted)
        {
            remainingTime =
                Mathf.Max(
                    0f,
                    puzzleDuration
                );

            timerStarted = true;

            finalCountdownStarted = false;

            // Start the appropriate timer sound immediately.
            UpdateTimerAudio();
        }
        else
        {
            // Re-entering puzzle mode does not restart
            // the countdown sound.
            UpdateTimerAudio();
        }

        // -----------------------------------------------------
        // SHOW HUD
        // -----------------------------------------------------

        if (progressText != null)
        {
            progressText.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "[PUZZLE TABLE] Puzzle Progress Text was not assigned."
            );
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "[PUZZLE TABLE] Puzzle Timer Text was not assigned."
            );
        }

        // -----------------------------------------------------
        // SHOW WORLD TIMER
        // -----------------------------------------------------

        ShowWorldTimer();

        UpdateProgressUI();
        UpdateTimerUI();

        // -----------------------------------------------------
        // SPAWN PIECES
        // -----------------------------------------------------

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

            int itemId =
                entry.item.itemId;

            // Already solved.
            if (solvedItemIds.Contains(itemId))
            {
                SpawnSolvedRuntimePiece(entry);

                availableCount++;

                continue;
            }

            // Normal gameplay:
            // only show pieces the player collected.
            if (!testModeShowAllPieces)
            {
                if (!inventory.HasItemId(itemId))
                {
                    continue;
                }
            }

            SpawnRuntimePiece(entry);

            availableCount++;
        }

        puzzleActive = true;

        // -----------------------------------------------------
        // DEBUG
        // -----------------------------------------------------

        if (timerExpired)
        {
            Debug.Log(
                "[PUZZLE TABLE] Puzzle opened but time already expired."
            );
        }
        else if (puzzleCompleted)
        {
            Debug.Log(
                "[PUZZLE TABLE] Puzzle already completed."
            );
        }
        else if (testModeShowAllPieces)
        {
            Debug.Log(
                $"[PUZZLE TABLE] TEST MODE: " +
                $"Available {availableCount}/{pieces.Length}. " +
                $"Solved {snappedCount}/{pieces.Length}. " +
                $"Time: {remainingTime:F1}s"
            );
        }
        else
        {
            Debug.Log(
                $"[PUZZLE TABLE] Available " +
                $"{availableCount}/{pieces.Length}. " +
                $"Solved {snappedCount}/{pieces.Length}. " +
                $"Time: {remainingTime:F1}s"
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

        // Hide only the player's screen HUD.
        //
        // DO NOT stop the timer.
        // DO NOT stop the timer audio.
        // DO NOT hide the world timer.
        HidePuzzleHUD();

        progressText = null;
        timerText = null;

        Debug.Log(
            $"[PUZZLE TABLE] Puzzle closed. " +
            $"Progress saved: " +
            $"{solvedItemIds.Count}/{pieces.Length}. " +
            $"Time remaining: {remainingTime:F1}s."
        );
    }

    // =========================================================
    // WORLD TIMER VISIBILITY
    // =========================================================

    private void ShowWorldTimer()
    {
        if (worldTimerText == null)
        {
            Debug.LogWarning(
                "[PUZZLE TABLE] World Timer Text was not assigned."
            );

            return;
        }

        Transform parent =
            worldTimerText.transform.parent;

        if (parent != null)
        {
            parent.gameObject.SetActive(true);
        }

        worldTimerText.gameObject.SetActive(true);
    }

    // =========================================================
    // TIMER
    // =========================================================

    private void UpdatePuzzleTimer()
    {
        if (!timerStarted ||
            timerExpired ||
            puzzleCompleted)
        {
            return;
        }

        remainingTime -=
            Time.deltaTime;

        // -----------------------------------------------------
        // TIME UP
        // -----------------------------------------------------

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;

            timerStarted = false;

            timerExpired = true;

            selectedPiece = null;

            StopTimerAudio();

            UpdateTimerUI();

            Debug.Log(
                "[PUZZLE] TIME UP!"
            );

            return;
        }

        // Switch between normal ticking
        // and final countdown SFX.
        UpdateTimerAudio();

        // Update both timers.
        UpdateTimerUI();
    }

    // =========================================================
    // TIMER AUDIO
    // =========================================================

    private void UpdateTimerAudio()
    {
        if (!timerStarted ||
            timerExpired ||
            puzzleCompleted)
        {
            StopTimerAudio();
            return;
        }

        int totalSeconds =
            Mathf.CeilToInt(
                Mathf.Max(
                    0f,
                    remainingTime
                )
            );

        // -----------------------------------------------------
        // FINAL 10 SECONDS
        // -----------------------------------------------------

        if (totalSeconds <= 10 &&
            totalSeconds > 0)
        {
            // Stop normal ticking.
            if (normalTimerAudioSource != null &&
                normalTimerAudioSource.isPlaying)
            {
                normalTimerAudioSource.Stop();
            }

            // Play the final countdown ONLY ONCE.
            if (!finalCountdownStarted)
            {
                finalCountdownStarted = true;

                if (finalCountdownAudioSource != null)
                {
                    finalCountdownAudioSource.Play();
                }

                Debug.Log(
                    "[PUZZLE AUDIO] Final 10-second countdown started."
                );
            }

            return;
        }

        // -----------------------------------------------------
        // NORMAL TIMER SOUND
        // -----------------------------------------------------

        if (normalTimerAudioSource != null &&
            !normalTimerAudioSource.isPlaying)
        {
            normalTimerAudioSource.Play();

            Debug.Log(
                "[PUZZLE AUDIO] Normal timer sound started."
            );
        }
    }

    // =========================================================
    // STOP TIMER AUDIO
    // =========================================================

    private void StopTimerAudio()
    {
        if (normalTimerAudioSource != null &&
            normalTimerAudioSource.isPlaying)
        {
            normalTimerAudioSource.Stop();
        }

        if (finalCountdownAudioSource != null &&
            finalCountdownAudioSource.isPlaying)
        {
            finalCountdownAudioSource.Stop();
        }
    }

    // =========================================================
    // TIMER UI
    // =========================================================

    private void UpdateTimerUI()
    {
        int totalSeconds =
            Mathf.CeilToInt(
                Mathf.Max(
                    0f,
                    remainingTime
                )
            );

        // Player HUD timer.
        UpdateSingleTimerText(
            timerText,
            timerNormalColor,
            timerNormalScale,
            totalSeconds
        );

        // World-space timer.
        UpdateSingleTimerText(
            worldTimerText,
            worldTimerNormalColor,
            worldTimerNormalScale,
            totalSeconds
        );
    }

    // =========================================================
    // UPDATE ONE TIMER
    // =========================================================

    private void UpdateSingleTimerText(
        TMP_Text targetText,
        Color normalColor,
        Vector3 normalScale,
        int totalSeconds)
    {
        if (targetText == null)
            return;

        // -----------------------------------------------------
        // TIME UP
        // -----------------------------------------------------

        if (timerExpired)
        {
            targetText.text =
                "TIME UP!";

            targetText.color =
                Color.red;

            targetText.rectTransform.localScale =
                normalScale;

            return;
        }

        // -----------------------------------------------------
        // PUZZLE COMPLETE
        // -----------------------------------------------------

        if (puzzleCompleted)
        {
            int completedMinutes =
                totalSeconds / 60;

            int completedSeconds =
                totalSeconds % 60;

            targetText.text =
                $"{completedMinutes:00}:{completedSeconds:00}";

            targetText.color =
                normalColor;

            targetText.rectTransform.localScale =
                normalScale;

            return;
        }

        // -----------------------------------------------------
        // FINAL 10 SECONDS
        // -----------------------------------------------------

        if (totalSeconds <= 10 &&
            totalSeconds > 0)
        {
            // Displays:
            //
            // 10
            // 9
            // 8
            // ...
            // 1

            targetText.text =
                totalSeconds.ToString();

            targetText.color =
                Color.red;

            UpdateTimerHeartbeat(
                targetText,
                normalScale
            );

            return;
        }

        // -----------------------------------------------------
        // NORMAL TIMER
        // -----------------------------------------------------

        int minutes =
            totalSeconds / 60;

        int seconds =
            totalSeconds % 60;

        targetText.text =
            $"{minutes:00}:{seconds:00}";

        targetText.color =
            normalColor;

        targetText.rectTransform.localScale =
            normalScale;
    }

    // =========================================================
    // HEARTBEAT ANIMATION
    // =========================================================

    private void UpdateTimerHeartbeat(
        TMP_Text targetText,
        Vector3 normalScale)
    {
        if (targetText == null)
            return;

        float phase =
            Mathf.Repeat(
                Time.unscaledTime,
                1f
            );

        // Strong first beat.
        float beat1 =
            Mathf.Clamp01(
                1f -
                Mathf.Abs(
                    phase - 0.12f
                ) / 0.10f
            );

        // Smaller second beat.
        float beat2 =
            Mathf.Clamp01(
                1f -
                Mathf.Abs(
                    phase - 0.32f
                ) / 0.08f
            );

        beat1 =
            Mathf.SmoothStep(
                0f,
                1f,
                beat1
            );

        beat2 =
            Mathf.SmoothStep(
                0f,
                1f,
                beat2
            );

        // THUMP - thump
        float pulse =
            (beat1 * 0.18f) +
            (beat2 * 0.10f);

        targetText.rectTransform.localScale =
            normalScale *
            (1f + pulse);
    }

    // =========================================================
    // PROGRESS UI
    // =========================================================

    private void UpdateProgressUI()
    {
        if (progressText == null)
            return;

        int totalPieces =
            pieces != null
                ? pieces.Length
                : 0;

        progressText.text =
            $"Pieces: {snappedCount} / {totalPieces}";
    }

    // =========================================================
    // HIDE PLAYER HUD
    // =========================================================

    private void HidePuzzleHUD()
    {
        if (progressText != null)
        {
            progressText.gameObject.SetActive(false);
        }

        if (timerText != null)
        {
            timerText.rectTransform.localScale =
                timerNormalScale;

            timerText.gameObject.SetActive(false);
        }

        // World timer intentionally stays visible.
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

        PuzzleRuntimePiece bestPiece =
            null;

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

            if (hit.distance <
                bestDistance)
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
    // SPAWN UNSOLVED PIECE
    // =========================================================

    private void SpawnRuntimePiece(
        PuzzlePieceEntry entry)
    {
        GameObject pieceObject =
            CreateRuntimePieceVisual(
                entry
            );

        if (pieceObject == null)
            return;

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

        EnsurePieceCollider(
            pieceObject
        );

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
    // SPAWN SOLVED PIECE
    // =========================================================

    private void SpawnSolvedRuntimePiece(
        PuzzlePieceEntry entry)
    {
        GameObject pieceObject =
            CreateRuntimePieceVisual(
                entry
            );

        if (pieceObject == null)
            return;

        pieceObject.name =
            $"Solved_{entry.item.itemName}";

        PuzzleRuntimePiece runtimePiece =
            pieceObject.AddComponent<PuzzleRuntimePiece>();

        runtimePiece.Initialize(
            entry.item,
            entry.solvedTarget,
            this
        );

        runtimePiece.LockToSolvedTarget();

        runtimePieces.Add(
            pieceObject
        );
    }

    // =========================================================
    // CREATE RUNTIME VISUAL
    // =========================================================

    private GameObject CreateRuntimePieceVisual(
        PuzzlePieceEntry entry)
    {
        if (entry == null ||
            entry.item == null ||
            entry.solvedTarget == null)
        {
            return null;
        }

        GameObject pieceObject =
            Instantiate(
                entry.solvedTarget.gameObject,
                entry.solvedTarget.parent
            );

        pieceObject.name =
            $"Runtime_{entry.item.itemName}";

        PuzzleTarget[] targetComponents =
            pieceObject.GetComponentsInChildren<PuzzleTarget>(
                true
            );

        foreach (PuzzleTarget target
                 in targetComponents)
        {
            if (target != null)
            {
                Destroy(target);
            }
        }

        Renderer[] renderers =
            pieceObject.GetComponentsInChildren<Renderer>(
                true
            );

        foreach (Renderer renderer
                 in renderers)
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

        if (runtimePiecesRoot != null)
        {
            pieceObject.transform.SetParent(
                runtimePiecesRoot,
                true
            );
        }

        return pieceObject;
    }

    // =========================================================
    // COLLIDER
    // =========================================================

    private void EnsurePieceCollider(
        GameObject pieceObject)
    {
        if (pieceObject == null)
            return;

        Collider[] colliders =
            pieceObject.GetComponentsInChildren<Collider>(
                true
            );

        if (colliders.Length > 0)
        {
            foreach (Collider col
                     in colliders)
            {
                if (col != null)
                {
                    col.enabled = true;
                }
            }

            return;
        }

        MeshFilter meshFilter =
            pieceObject.GetComponentInChildren<MeshFilter>(
                true
            );

        if (meshFilter == null ||
            meshFilter.sharedMesh == null)
        {
            Debug.LogWarning(
                $"[PUZZLE] No collider or mesh found on " +
                $"{pieceObject.name}."
            );

            return;
        }

        MeshCollider meshCollider =
            meshFilter.gameObject
                .AddComponent<MeshCollider>();

        meshCollider.sharedMesh =
            meshFilter.sharedMesh;
    }

    // =========================================================
    // PIECE SNAPPED
    // =========================================================

    public void NotifyPieceSnapped(
        PuzzleRuntimePiece piece)
    {
        if (piece == null ||
            piece.Item == null ||
            timerExpired ||
            puzzleCompleted)
        {
            return;
        }

        int itemId =
            piece.Item.itemId;

        if (!solvedItemIds.Add(itemId))
        {
            Debug.LogWarning(
                $"[PUZZLE] {piece.Item.itemName} " +
                $"was already solved."
            );

            return;
        }

        snappedCount =
            solvedItemIds.Count;

        UpdateProgressUI();

        foreach (PuzzlePieceEntry entry
                 in pieces)
        {
            if (entry == null ||
                entry.item == null)
            {
                continue;
            }

            if (entry.item == piece.Item ||
                entry.item.itemId == itemId)
            {
                if (entry.guideRenderer != null)
                {
                    entry.guideRenderer.enabled =
                        false;
                }

                break;
            }
        }

        Debug.Log(
            $"[PUZZLE] Correct! " +
            $"{piece.Item.itemName} snapped. " +
            $"Progress: {snappedCount}/{pieces.Length}"
        );

        // -----------------------------------------------------
        // PUZZLE COMPLETE
        // -----------------------------------------------------

        if (snappedCount >= pieces.Length)
        {
            puzzleCompleted = true;

            timerStarted = false;

            // Stop both timer sounds immediately.
            StopTimerAudio();

            UpdateTimerUI();

            Debug.Log(
                $"[PUZZLE] PUZZLE COMPLETE! " +
                $"Time remaining: " +
                $"{remainingTime:F1} seconds."
            );
        }
    }

    // =========================================================
    // RESET GUIDE PIECES
    // =========================================================

    private void ResetGuidePieces()
    {
        if (pieces == null)
            return;

        foreach (PuzzlePieceEntry entry
                 in pieces)
        {
            if (entry == null)
                continue;

            if (entry.guideRenderer != null)
            {
                entry.guideRenderer.enabled =
                    true;
            }
        }
    }

    // =========================================================
    // APPLY SAVED GUIDE STATE
    // =========================================================

    private void ApplySolvedGuideState()
    {
        if (pieces == null)
            return;

        foreach (PuzzlePieceEntry entry
                 in pieces)
        {
            if (entry == null ||
                entry.item == null ||
                entry.guideRenderer == null)
            {
                continue;
            }

            bool isSolved =
                solvedItemIds.Contains(
                    entry.item.itemId
                );

            entry.guideRenderer.enabled =
                !isSolved;
        }
    }

    // =========================================================
    // CLEAR RUNTIME PIECES
    // =========================================================

    private void ClearRuntimePieces()
    {
        foreach (GameObject piece
                 in runtimePieces)
        {
            if (piece != null)
            {
                Destroy(piece);
            }
        }

        runtimePieces.Clear();
    }
}