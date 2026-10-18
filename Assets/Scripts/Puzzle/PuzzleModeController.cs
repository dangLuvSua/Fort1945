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
    // EVENTS
    // =========================================================

    public event Action PuzzleCompleted;
    public event Action PuzzleFailed;
    public event Action PuzzleRetried;

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

    private PlayerInventory currentInventory;

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

    private bool finalCountdownStarted;

    public bool IsCompleted => puzzleCompleted;

    // =========================================================
    // SHARED NETWORK STATE
    // =========================================================

    private int lastNetworkSolvedMask = -1;
    private bool lastNetworkStarted;
    private bool lastNetworkCompleted;
    private bool lastNetworkFailed;
    private bool networkSnapshotInitialized;
    private bool waitingForNetworkStart;
    private bool networkRetryPending;
    private bool completionEventRaised;
    private bool failureEventRaised;

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
        // In multiplayer the Fusion PuzzleNetworkState is the
        // authority for the solved pieces and countdown timer.
        if (HasSharedNetworkState())
        {
            SyncFromNetworkState();
        }
        else
        {
            // Offline / standalone fallback.
            UpdatePuzzleTimer();
        }

        if (!puzzleActive ||
            timerExpired ||
            puzzleCompleted ||
            puzzleCamera == null ||
            Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectPiece();
        }

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
        currentInventory = inventory;
        selectedPiece = null;

        progressText = playerProgressText;
        timerText = playerTimerText;

        CacheTimerVisuals();

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

        if (!testModeShowAllPieces && inventory == null)
        {
            Debug.LogError(
                "[PUZZLE TABLE] PlayerInventory is missing."
            );

            HidePuzzleHUD();
            return;
        }

        if (pieces == null || pieces.Length == 0)
        {
            Debug.LogError(
                "[PUZZLE TABLE] No puzzle pieces configured."
            );

            HidePuzzleHUD();
            return;
        }

        ResetGuidePieces();

        // =====================================================
        // SHARED MULTIPLAYER STATE
        // =====================================================

        PuzzleNetworkState sharedState =
            PuzzleNetworkState.Instance;

        if (sharedState != null)
        {
            ApplyNetworkSolvedMask(
                sharedState.SolvedMask,
                true
            );

            if (sharedState.PuzzleCompleted)
            {
                ApplyNetworkCompletion(true);
                return;
            }

            // If another player failed while this player was away,
            // entering the board should show TIME'S UP / RETRY.
            if (sharedState.PuzzleFailed &&
                !networkRetryPending)
            {
                ApplyNetworkFailure(true);
                return;
            }

            if (sharedState.PuzzleStarted)
            {
                waitingForNetworkStart = false;
                networkRetryPending = false;
                timerStarted = true;
                timerExpired = false;
                puzzleCompleted = false;
                remainingTime =
                    Mathf.Max(0f, sharedState.GetRemainingTime());
            }
            else
            {
                // First player to begin asks the State Authority
                // to create the shared Fusion TickTimer.
                if (!networkRetryPending)
                {
                    sharedState.RPC_StartPuzzle(
                        puzzleDuration
                    );
                }

                waitingForNetworkStart = true;
                timerStarted = true;
                timerExpired = false;
                puzzleCompleted = false;
                remainingTime =
                    Mathf.Max(0f, puzzleDuration);
            }
        }
        else
        {
            // =================================================
            // OFFLINE / STANDALONE FALLBACK
            // =================================================

            snappedCount = solvedItemIds.Count;
            ApplySolvedGuideState();

            if (!timerStarted &&
                !timerExpired &&
                !puzzleCompleted)
            {
                remainingTime =
                    Mathf.Max(0f, puzzleDuration);

                timerStarted = true;
                finalCountdownStarted = false;
            }
        }

        ShowPuzzleHUD();
        ShowWorldTimer();
        UpdateProgressUI();
        UpdateTimerAudio();
        UpdateTimerUI();

        int availableCount =
            SpawnAvailableRuntimePieces(
                currentInventory
            );

        puzzleActive = true;

        Debug.Log(
            $"[PUZZLE TABLE] Opened. " +
            $"Available {availableCount}/{pieces.Length}. " +
            $"Solved {snappedCount}/{pieces.Length}. " +
            $"Time: {remainingTime:F1}s. " +
            $"Shared={HasSharedNetworkState()}"
        );
    }

    // =========================================================
    // END PUZZLE
    // =========================================================

    public void EndPuzzle()
    {
        puzzleActive =
            false;

        selectedPiece =
            null;

        puzzleCamera =
            null;

        currentInventory =
            null;

        ClearRuntimePieces();

        // Do not stop timer here.
        // Leaving with ESC keeps timer running.
        HidePuzzleHUD();

        progressText =
            null;

        timerText =
            null;

        Debug.Log(
            $"[PUZZLE TABLE] Puzzle closed. " +
            $"Progress saved: " +
            $"{solvedItemIds.Count}/{pieces.Length}. " +
            $"Time remaining: {remainingTime:F1}s."
        );
    }

    // =========================================================
    // WORLD TIMER
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
            parent.gameObject.SetActive(
                true
            );
        }

        worldTimerText.gameObject.SetActive(
            true
        );
    }

    // =========================================================
// HIDE WORLD TIMER
// =========================================================

private void HideWorldTimer()
{
    if (worldTimerText == null)
        return;

    worldTimerText.gameObject.SetActive(false);

    Transform parent =
        worldTimerText.transform.parent;

    if (parent != null)
    {
        parent.gameObject.SetActive(false);
    }
}

    // =========================================================
    // TIMER
    // =========================================================

    private bool HasSharedNetworkState()
    {
        return PuzzleNetworkState.Instance != null &&
               PuzzleNetworkState.Instance.Object != null &&
               PuzzleNetworkState.Instance.Runner != null;
    }

    // =========================================================
    // CACHE TIMER VISUALS
    // =========================================================

    private void CacheTimerVisuals()
    {
        if (timerText != null && !timerVisualCached)
        {
            timerNormalColor = timerText.color;
            timerNormalScale = timerText.rectTransform.localScale;
            timerVisualCached = true;
        }

        if (worldTimerText != null && !worldTimerVisualCached)
        {
            worldTimerNormalColor = worldTimerText.color;
            worldTimerNormalScale = worldTimerText.rectTransform.localScale;
            worldTimerVisualCached = true;
        }
    }

    // =========================================================
    // SHARED NETWORK SYNCHRONIZATION
    // =========================================================

    private void SyncFromNetworkState()
    {
        PuzzleNetworkState state =
            PuzzleNetworkState.Instance;

        if (state == null)
            return;

        int solvedMask = state.SolvedMask;
        bool started = state.PuzzleStarted;
        bool completed = state.PuzzleCompleted;
        bool failed = state.PuzzleFailed;

        if (!networkSnapshotInitialized)
        {
            networkSnapshotInitialized = true;
            lastNetworkSolvedMask = solvedMask;
            lastNetworkStarted = started;
            lastNetworkCompleted = completed;
            lastNetworkFailed = failed;

            ApplyNetworkSolvedMask(
                solvedMask,
                true
            );
        }
        else if (solvedMask != lastNetworkSolvedMask)
        {
            ApplyNetworkSolvedMask(
                solvedMask,
                false
            );
        }

        // Authority-confirmed completion always wins.
        if (completed)
        {
            ApplyNetworkCompletion(false);
            RememberNetworkSnapshot(
                solvedMask,
                started,
                completed,
                failed
            );
            return;
        }

        // While a RETRY RPC is travelling to State Authority,
        // ignore the old failed snapshot for a moment.
        if (failed && !networkRetryPending)
        {
            ApplyNetworkFailure(false);
            RememberNetworkSnapshot(
                solvedMask,
                started,
                completed,
                failed
            );
            return;
        }

        if (started)
        {
            bool retryJustStarted =
                lastNetworkFailed ||
                timerExpired ||
                networkRetryPending;

            waitingForNetworkStart = false;
            networkRetryPending = false;

            timerStarted = true;
            timerExpired = false;
            puzzleCompleted = false;

            remainingTime =
                Mathf.Max(
                    0f,
                    state.GetRemainingTime()
                );

            if (retryJustStarted)
            {
                failureEventRaised = false;
                completionEventRaised = false;
                finalCountdownStarted = false;

                if (puzzleCamera != null)
                {
                    ShowPuzzleHUD();
                    RebuildRuntimePiecesForCurrentPlayer();
                    puzzleActive = true;
                }

                PuzzleRetried?.Invoke();
            }

            ShowWorldTimer();
            UpdateTimerAudio();
            UpdateTimerUI();
        }
        else if (waitingForNetworkStart)
        {
            // Keep the locally opened board responsive while waiting
            // for State Authority to acknowledge START/RETRY.
            timerStarted = true;
            timerExpired = false;
            puzzleCompleted = false;

            ShowWorldTimer();
            UpdateTimerAudio();
            UpdateTimerUI();
        }
        else
        {
            timerStarted = false;
            StopTimerAudio();
        }

        RememberNetworkSnapshot(
            solvedMask,
            started,
            completed,
            failed
        );
    }

    private void RememberNetworkSnapshot(
        int solvedMask,
        bool started,
        bool completed,
        bool failed)
    {
        lastNetworkSolvedMask = solvedMask;
        lastNetworkStarted = started;
        lastNetworkCompleted = completed;
        lastNetworkFailed = failed;
    }

    // =========================================================
    // APPLY SHARED SOLVED MASK
    // =========================================================

    private void ApplyNetworkSolvedMask(
        int solvedMask,
        bool forceRebuild)
    {
        bool removedPieces =
            lastNetworkSolvedMask >= 0 &&
            (lastNetworkSolvedMask & ~solvedMask) != 0;

        if (forceRebuild || removedPieces)
        {
            solvedItemIds.Clear();
            ResetGuidePieces();

            for (int i = 0; i < pieces.Length; i++)
            {
                if ((solvedMask & (1 << i)) != 0)
                {
                    AddSolvedEntryLocal(
                        i,
                        false
                    );
                }
            }

            snappedCount = solvedItemIds.Count;
            ApplySolvedGuideState();
            UpdateProgressUI();

            if (puzzleActive && puzzleCamera != null)
            {
                RebuildRuntimePiecesForCurrentPlayer();
            }

            lastNetworkSolvedMask = solvedMask;
            return;
        }

        for (int i = 0; i < pieces.Length; i++)
        {
            if ((solvedMask & (1 << i)) == 0)
                continue;

            PuzzlePieceEntry entry = pieces[i];

            if (entry == null || entry.item == null)
                continue;

            if (!solvedItemIds.Contains(entry.item.itemId))
            {
                AddSolvedEntryLocal(
                    i,
                    true
                );
            }
        }

        snappedCount = solvedItemIds.Count;
        UpdateProgressUI();
        lastNetworkSolvedMask = solvedMask;
    }

    private void AddSolvedEntryLocal(
        int pieceIndex,
        bool updateRuntimePiece)
    {
        if (pieces == null ||
            pieceIndex < 0 ||
            pieceIndex >= pieces.Length)
        {
            return;
        }

        PuzzlePieceEntry entry =
            pieces[pieceIndex];

        if (entry == null || entry.item == null)
            return;

        solvedItemIds.Add(
            entry.item.itemId
        );

        if (entry.guideRenderer != null)
        {
            entry.guideRenderer.enabled = false;
        }

        if (!updateRuntimePiece || !puzzleActive)
            return;

        PuzzleRuntimePiece runtimePiece =
            FindRuntimePieceByItemId(
                entry.item.itemId
            );

        if (runtimePiece != null)
        {
            runtimePiece.LockToSolvedTarget();
        }
        else
        {
            // A remote player may solve a piece that this local
            // inventory did not originally spawn. Show it solved.
            SpawnSolvedRuntimePiece(entry);
        }
    }

    private PuzzleRuntimePiece FindRuntimePieceByItemId(
        int itemId)
    {
        foreach (GameObject pieceObject in runtimePieces)
        {
            if (pieceObject == null)
                continue;

            PuzzleRuntimePiece runtimePiece =
                pieceObject.GetComponent<PuzzleRuntimePiece>();

            if (runtimePiece == null ||
                runtimePiece.Item == null)
            {
                continue;
            }

            if (runtimePiece.Item.itemId == itemId)
            {
                return runtimePiece;
            }
        }

        return null;
    }

    private int FindPieceIndexByItemId(
        int itemId)
    {
        if (pieces == null)
            return -1;

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] != null &&
                pieces[i].item != null &&
                pieces[i].item.itemId == itemId)
            {
                return i;
            }
        }

        return -1;
    }

    // =========================================================
    // NETWORK RESULT STATES
    // =========================================================

    private void ApplyNetworkCompletion(
        bool forceEvent)
    {
        puzzleCompleted = true;
        timerStarted = false;
        timerExpired = false;
        puzzleActive = false;
        selectedPiece = null;
        waitingForNetworkStart = false;
        networkRetryPending = false;

        StopTimerAudio();
        UpdateTimerUI();
        HideWorldTimer();

        if (forceEvent || !completionEventRaised)
        {
            completionEventRaised = true;
            failureEventRaised = false;

            Debug.Log(
                "[PUZZLE NETWORK] Shared puzzle completed locally."
            );

            PuzzleCompleted?.Invoke();
        }
    }

    private void ApplyNetworkFailure(
        bool forceEvent)
    {
        timerStarted = false;
        timerExpired = true;
        puzzleCompleted = false;
        puzzleActive = false;
        selectedPiece = null;
        waitingForNetworkStart = false;

        remainingTime = 0f;

        StopTimerAudio();
        UpdateTimerUI();
        HideWorldTimer();

        solvedItemIds.Clear();
        snappedCount = 0;
        ClearRuntimePieces();
        ResetGuidePieces();
        UpdateProgressUI();

        if (forceEvent || !failureEventRaised)
        {
            failureEventRaised = true;
            completionEventRaised = false;

            Debug.Log(
                "[PUZZLE NETWORK] Shared puzzle failed. " +
                "Local board reset to 0/8."
            );

            PuzzleFailed?.Invoke();
        }
    }

    // =========================================================
    // RUNTIME BOARD HELPERS
    // =========================================================

    private void ShowPuzzleHUD()
    {
        if (progressText != null)
        {
            progressText.gameObject.SetActive(true);
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
        }
    }

    private int SpawnAvailableRuntimePieces(
        PlayerInventory inventory)
    {
        int availableCount = 0;

        foreach (PuzzlePieceEntry entry in pieces)
        {
            if (entry == null ||
                entry.item == null ||
                entry.solvedTarget == null)
            {
                continue;
            }

            int itemId = entry.item.itemId;

            if (solvedItemIds.Contains(itemId))
            {
                SpawnSolvedRuntimePiece(entry);
                availableCount++;
                continue;
            }

            if (!testModeShowAllPieces)
            {
                if (inventory == null ||
                    !inventory.HasItemId(itemId))
                {
                    continue;
                }
            }

            SpawnRuntimePiece(entry);
            availableCount++;
        }

        return availableCount;
    }

    private void RebuildRuntimePiecesForCurrentPlayer()
    {
        ClearRuntimePieces();
        ResetGuidePieces();
        ApplySolvedGuideState();

        if (puzzleCamera == null)
            return;

        SpawnAvailableRuntimePieces(
            currentInventory
        );

        snappedCount = solvedItemIds.Count;
        UpdateProgressUI();
    }

    // =========================================================
    // LOCAL/OFFLINE TIMER FALLBACK
    // =========================================================

    private void UpdatePuzzleTimer()
    {
        if (!timerStarted ||
            timerExpired ||
            puzzleCompleted)
        {
            return;
        }

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            timerStarted = false;
            timerExpired = true;
            puzzleActive = false;
            selectedPiece = null;

            StopTimerAudio();
            UpdateTimerUI();
            HideWorldTimer();

            solvedItemIds.Clear();
            snappedCount = 0;
            ClearRuntimePieces();
            ResetGuidePieces();
            UpdateProgressUI();

            Debug.Log(
                "[PUZZLE] TIME'S UP! Attempt reset to 0."
            );

            PuzzleFailed?.Invoke();
            return;
        }

        UpdateTimerAudio();
        UpdateTimerUI();
    }

    // =========================================================
    // RESET FAILED ATTEMPT
    // =========================================================

    public void ResetFailedAttempt()
    {
        StopTimerAudio();

        puzzleActive = false;
        selectedPiece = null;

        solvedItemIds.Clear();
        snappedCount = 0;

        timerStarted = false;
        timerExpired = false;
        puzzleCompleted = false;
        finalCountdownStarted = false;
        completionEventRaised = false;
        failureEventRaised = false;

        remainingTime =
            Mathf.Max(0f, puzzleDuration);

        ClearRuntimePieces();
        ResetGuidePieces();
        UpdateProgressUI();
        UpdateTimerUI();

        if (HasSharedNetworkState())
        {
            networkRetryPending = true;
            waitingForNetworkStart = true;

            PuzzleNetworkState.Instance
                .RPC_RetryPuzzle(
                    puzzleDuration
                );

            Debug.Log(
                "[PUZZLE NETWORK] RETRY requested. " +
                "Waiting for shared state authority."
            );
        }
        else
        {
            Debug.Log(
                "[PUZZLE] Offline retry prepared from 0/8."
            );
        }
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
            if (normalTimerAudioSource != null &&
                normalTimerAudioSource.isPlaying)
            {
                normalTimerAudioSource.Stop();
            }

            if (!finalCountdownStarted)
            {
                finalCountdownStarted =
                    true;

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
        // NORMAL TIMER
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

        UpdateSingleTimerText(
            timerText,
            timerNormalColor,
            timerNormalScale,
            totalSeconds
        );

        UpdateSingleTimerText(
            worldTimerText,
            worldTimerNormalColor,
            worldTimerNormalScale,
            totalSeconds
        );
    }

    // =========================================================
    // SINGLE TIMER
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
        // COMPLETED
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
        // FINAL 10
        // -----------------------------------------------------

        if (totalSeconds <= 10 &&
            totalSeconds > 0)
        {
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
        // NORMAL
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
    // HEARTBEAT
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

        float beat1 =
            Mathf.Clamp01(
                1f -
                Mathf.Abs(
                    phase - 0.12f
                ) / 0.10f
            );

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
            progressText.gameObject.SetActive(
                false
            );
        }

        if (timerText != null)
        {
            timerText.rectTransform.localScale =
                timerNormalScale;

            timerText.gameObject.SetActive(
                false
            );
        }
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
    // DRAG
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
            ray.GetPoint(
                enter
            );

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
    // ROTATE
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
    // SPAWN UNSOLVED
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
    // SPAWN SOLVED
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

            renderer.enabled =
                true;

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
            foreach (Collider col in colliders)
            {
                if (col != null)
                {
                    col.enabled =
                        true;
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

        // =====================================================
        // SHARED MULTIPLAYER SOLVE
        // =====================================================

        if (HasSharedNetworkState())
        {
            int pieceIndex =
                FindPieceIndexByItemId(
                    itemId
                );

            if (pieceIndex < 0)
            {
                Debug.LogError(
                    $"[PUZZLE NETWORK] Could not map item " +
                    $"{itemId} to a puzzle index."
                );

                return;
            }

            PuzzleNetworkState state =
                PuzzleNetworkState.Instance;

            if (state.PuzzleCompleted ||
                state.PuzzleFailed)
            {
                return;
            }

            // Optimistic local update for instant feedback.
            AddSolvedEntryLocal(
                pieceIndex,
                true
            );

            snappedCount =
                solvedItemIds.Count;

            UpdateProgressUI();

            state.RPC_SolvePiece(
                pieceIndex
            );

            Debug.Log(
                $"[PUZZLE NETWORK] Requested solve for " +
                $"piece {pieceIndex + 1}. " +
                $"Local progress: {snappedCount}/{pieces.Length}"
            );

            // Completion is NOT decided locally.
            // PuzzleNetworkState is authoritative and will
            // replicate PuzzleCompleted to every player.
            return;
        }

        // =====================================================
        // OFFLINE / STANDALONE SOLVE
        // =====================================================

        if (!solvedItemIds.Add(itemId))
        {
            return;
        }

        snappedCount =
            solvedItemIds.Count;

        UpdateProgressUI();

        foreach (PuzzlePieceEntry entry in pieces)
        {
            if (entry == null || entry.item == null)
                continue;

            if (entry.item == piece.Item ||
                entry.item.itemId == itemId)
            {
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
            puzzleCompleted = true;
            timerStarted = false;
            puzzleActive = false;
            selectedPiece = null;

            StopTimerAudio();
            UpdateTimerUI();
            HideWorldTimer();

            PuzzleCompleted?.Invoke();
        }
    }

    // =========================================================
    // RESET GUIDES
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
                entry.guideRenderer.enabled =
                    true;
            }
        }
    }

    // =========================================================
    // APPLY SAVED GUIDES
    // =========================================================

    private void ApplySolvedGuideState()
    {
        if (pieces == null)
            return;

        foreach (PuzzlePieceEntry entry in pieces)
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