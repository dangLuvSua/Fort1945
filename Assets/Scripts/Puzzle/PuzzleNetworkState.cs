using Fusion;
using UnityEngine;

public class PuzzleNetworkState : NetworkBehaviour
{
    public static PuzzleNetworkState Instance { get; private set; }

    // =========================================================
    // NETWORKED PUZZLE STATE
    // =========================================================

    [Networked]
    public int SolvedMask { get; private set; }

    [Networked]
    public NetworkBool PuzzleStarted { get; private set; }

    [Networked]
    public NetworkBool PuzzleCompleted { get; private set; }

    [Networked]
    public NetworkBool PuzzleFailed { get; private set; }

    [Networked]
    public TickTimer PuzzleTimer { get; private set; }

    // Easy puzzle = 8 pieces.
    private const int AllPiecesMask = 0b11111111;

    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        Instance = this;

        Debug.Log(
            $"[PUZZLE NETWORK] State spawned. " +
            $"State Authority: {Object.HasStateAuthority}"
        );
    }

    // =========================================================
    // NETWORK UPDATE
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (!PuzzleStarted ||
            PuzzleCompleted ||
            PuzzleFailed)
        {
            return;
        }

        if (PuzzleTimer.Expired(Runner))
        {
            PuzzleStarted = false;
            PuzzleFailed = true;

            Debug.Log(
                "[PUZZLE NETWORK] Shared puzzle timer expired."
            );
        }
    }

    // =========================================================
    // START PUZZLE
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    public void RPC_StartPuzzle(
        float duration)
    {
        if (PuzzleCompleted)
            return;

        // Don't restart an already running timer.
        if (PuzzleStarted &&
            !PuzzleFailed)
        {
            return;
        }

        PuzzleStarted = true;
        PuzzleCompleted = false;
        PuzzleFailed = false;

        SolvedMask = 0;

        PuzzleTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                duration
            );

        Debug.Log(
            $"[PUZZLE NETWORK] Shared puzzle started. " +
            $"Duration: {duration}s"
        );
    }

    // =========================================================
    // SOLVE PIECE
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    public void RPC_SolvePiece(
        int pieceIndex)
    {
        if (!PuzzleStarted ||
            PuzzleCompleted ||
            PuzzleFailed)
        {
            return;
        }

        if (pieceIndex < 0 ||
            pieceIndex >= 8)
        {
            Debug.LogWarning(
                $"[PUZZLE NETWORK] Invalid piece index: " +
                $"{pieceIndex}"
            );

            return;
        }

        int bit =
            1 << pieceIndex;

        // Already solved.
        if ((SolvedMask & bit) != 0)
            return;

        SolvedMask |= bit;

        Debug.Log(
            $"[PUZZLE NETWORK] Piece {pieceIndex + 1} solved. " +
            $"Mask: {SolvedMask}"
        );

        // All 8 pieces solved.
        if ((SolvedMask & AllPiecesMask) ==
            AllPiecesMask)
        {
            PuzzleCompleted = true;
            PuzzleStarted = false;
            PuzzleFailed = false;

            PuzzleTimer = TickTimer.None;

            Debug.Log(
                "[PUZZLE NETWORK] SHARED PUZZLE COMPLETE!"
            );
        }
    }

    // =========================================================
    // RETRY AFTER FAILURE
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    public void RPC_RetryPuzzle(
        float duration)
    {
        if (PuzzleCompleted)
            return;

        SolvedMask = 0;

        PuzzleFailed = false;
        PuzzleCompleted = false;
        PuzzleStarted = true;

        PuzzleTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                duration
            );

        Debug.Log(
            "[PUZZLE NETWORK] Shared puzzle retry. " +
            "Progress reset to 0/8."
        );
    }

    // =========================================================
    // HELPERS
    // =========================================================

    public bool IsPieceSolved(
        int pieceIndex)
    {
        if (pieceIndex < 0 ||
            pieceIndex >= 8)
        {
            return false;
        }

        int bit =
            1 << pieceIndex;

        return (SolvedMask & bit) != 0;
    }

    public float GetRemainingTime()
    {
        if (!PuzzleStarted)
            return 0f;

        return PuzzleTimer
            .RemainingTime(Runner)
            .GetValueOrDefault();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}