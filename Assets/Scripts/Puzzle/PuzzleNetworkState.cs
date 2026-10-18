using Fusion;
using UnityEngine;

public class PuzzleNetworkState : NetworkBehaviour
{
    public static PuzzleNetworkState Instance { get; private set; }

    public const int MaxPieces = 8;

    // =========================================================
    // SHARED PUZZLE STATE
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

    // A bit is set when a piece has an authoritative board pose.
    [Networked]
    public int PiecePoseValidMask { get; private set; }

    // Board-local positions/rotations for live dragging.
    [Networked, Capacity(8)]
    public NetworkArray<Vector3> PieceLocalPositions => default;

    [Networked, Capacity(8)]
    public NetworkArray<Quaternion> PieceLocalRotations => default;

    // -1 = free. Otherwise stores PlayerRef.PlayerId.
    [Networked, Capacity(8)]
    public NetworkArray<int> PieceHolderIds => default;

    private const int AllPiecesMask = 0b11111111;

    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        Instance = this;

        if (Object.HasStateAuthority)
        {
            ResetLivePieceState();
        }

        Debug.Log(
            $"[PUZZLE NETWORK] State spawned. " +
            $"State Authority: {Object.HasStateAuthority}"
        );
    }

    // =========================================================
    // NETWORK TICK
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        ClearDisconnectedPieceHolders();

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
            PuzzleCompleted = false;

            // Failure always erases the entire attempt.
            SolvedMask = 0;

            PuzzleTimer = TickTimer.None;

            ResetLivePieceState();

            Debug.Log(
                "[PUZZLE NETWORK] Shared puzzle timer expired. " +
                "Progress and live piece state reset."
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

        // Do not restart a running attempt.
        if (PuzzleStarted &&
            !PuzzleFailed)
        {
            return;
        }

        SolvedMask = 0;

        PuzzleStarted = true;
        PuzzleCompleted = false;
        PuzzleFailed = false;

        ResetLivePieceState();

        PuzzleTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                Mathf.Max(0.1f, duration)
            );

        Debug.Log(
            $"[PUZZLE NETWORK] Shared puzzle started. " +
            $"Duration: {duration}s"
        );
    }

    // =========================================================
    // INITIAL PIECE POSE
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    public void RPC_InitializePiecePose(
        int pieceIndex,
        Vector3 localPosition,
        Quaternion localRotation)
    {
        if (!IsValidPieceIndex(pieceIndex) ||
            PuzzleCompleted ||
            PuzzleFailed ||
            IsPieceSolved(pieceIndex))
        {
            return;
        }

        // First client that publishes the piece wins.
        if (IsPiecePoseValid(pieceIndex))
            return;

        SetPiecePoseInternal(
            pieceIndex,
            localPosition,
            localRotation
        );
    }

    // =========================================================
    // REQUEST GRAB
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    public void RPC_RequestGrab(
        int pieceIndex,
        RpcInfo info = default)
    {
        if (!CanManipulatePiece(pieceIndex))
            return;

        int sourcePlayerId =
            info.Source.PlayerId;

        int currentHolder =
            PieceHolderIds[pieceIndex];

        if (currentHolder != -1 &&
            currentHolder != sourcePlayerId)
        {
            return;
        }

        PieceHolderIds.Set(
            pieceIndex,
            sourcePlayerId
        );
    }

    // =========================================================
    // LIVE DRAG UPDATE
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    public void RPC_UpdatePiecePose(
        int pieceIndex,
        Vector3 localPosition,
        Quaternion localRotation,
        RpcInfo info = default)
    {
        if (!CanManipulatePiece(pieceIndex))
            return;

        if (PieceHolderIds[pieceIndex] !=
            info.Source.PlayerId)
        {
            return;
        }

        SetPiecePoseInternal(
            pieceIndex,
            localPosition,
            localRotation
        );
    }

    // =========================================================
    // RELEASE
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    public void RPC_ReleasePiece(
        int pieceIndex,
        Vector3 localPosition,
        Quaternion localRotation,
        RpcInfo info = default)
    {
        if (!IsValidPieceIndex(pieceIndex))
            return;

        if (PieceHolderIds[pieceIndex] !=
            info.Source.PlayerId)
        {
            return;
        }

        // Preserve the final released pose unless it has already
        // become a solved piece.
        if (!IsPieceSolved(pieceIndex) &&
            !PuzzleCompleted &&
            !PuzzleFailed)
        {
            SetPiecePoseInternal(
                pieceIndex,
                localPosition,
                localRotation
            );
        }

        PieceHolderIds.Set(
            pieceIndex,
            -1
        );
    }

    // =========================================================
    // CANCEL GRAB
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    public void RPC_CancelGrab(
        int pieceIndex,
        RpcInfo info = default)
    {
        if (!IsValidPieceIndex(pieceIndex))
            return;

        if (PieceHolderIds[pieceIndex] ==
            info.Source.PlayerId)
        {
            PieceHolderIds.Set(
                pieceIndex,
                -1
            );
        }
    }

    // =========================================================
    // SOLVE PIECE
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    public void RPC_SolvePiece(
        int pieceIndex,
        RpcInfo info = default)
    {
        if (!CanManipulatePiece(pieceIndex))
            return;

        int holder =
            PieceHolderIds[pieceIndex];

        // If somebody owns this piece, only that player may solve it.
        if (holder != -1 &&
            holder != info.Source.PlayerId)
        {
            return;
        }

        int bit =
            1 << pieceIndex;

        if ((SolvedMask & bit) != 0)
            return;

        SolvedMask |= bit;

        // Solved pieces can no longer be held.
        PieceHolderIds.Set(
            pieceIndex,
            -1
        );

        Debug.Log(
            $"[PUZZLE NETWORK] Piece {pieceIndex + 1} solved. " +
            $"Mask: {SolvedMask}"
        );

        if ((SolvedMask & AllPiecesMask) ==
            AllPiecesMask)
        {
            PuzzleCompleted = true;
            PuzzleStarted = false;
            PuzzleFailed = false;

            PuzzleTimer = TickTimer.None;

            ReleaseAllPieceHolders();

            Debug.Log(
                "[PUZZLE NETWORK] SHARED PUZZLE COMPLETE!"
            );
        }
    }

    // =========================================================
    // RETRY
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

        ResetLivePieceState();

        PuzzleTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                Mathf.Max(0.1f, duration)
            );

        Debug.Log(
            "[PUZZLE NETWORK] Shared puzzle retry. " +
            "Progress reset to 0/8."
        );
    }

    // =========================================================
    // PUBLIC HELPERS
    // =========================================================

    public bool IsPieceSolved(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(pieceIndex))
            return false;

        return (SolvedMask &
                (1 << pieceIndex)) != 0;
    }

    public bool IsPiecePoseValid(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(pieceIndex))
            return false;

        return (PiecePoseValidMask &
                (1 << pieceIndex)) != 0;
    }

    public Vector3 GetPieceLocalPosition(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(pieceIndex))
            return Vector3.zero;

        return PieceLocalPositions[pieceIndex];
    }

    public Quaternion GetPieceLocalRotation(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(pieceIndex))
            return Quaternion.identity;

        return PieceLocalRotations[pieceIndex];
    }

    public int GetPieceHolderId(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(pieceIndex))
            return -1;

        return PieceHolderIds[pieceIndex];
    }

    public bool IsPieceHeldByLocalPlayer(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(pieceIndex) ||
            Runner == null ||
            !Runner.LocalPlayer.IsValid)
        {
            return false;
        }

        return PieceHolderIds[pieceIndex] ==
               Runner.LocalPlayer.PlayerId;
    }

    public bool IsPieceHeldByOtherPlayer(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(pieceIndex))
            return false;

        int holder =
            PieceHolderIds[pieceIndex];

        if (holder < 0)
            return false;

        if (Runner == null ||
            !Runner.LocalPlayer.IsValid)
        {
            return true;
        }

        return holder !=
               Runner.LocalPlayer.PlayerId;
    }

    public float GetRemainingTime()
    {
        if (!PuzzleStarted ||
            Runner == null)
        {
            return 0f;
        }

        return PuzzleTimer
            .RemainingTime(Runner)
            .GetValueOrDefault();
    }

    // =========================================================
    // INTERNAL HELPERS
    // =========================================================

    private bool CanManipulatePiece(
        int pieceIndex)
    {
        return IsValidPieceIndex(pieceIndex) &&
               PuzzleStarted &&
               !PuzzleCompleted &&
               !PuzzleFailed &&
               !IsPieceSolved(pieceIndex);
    }

    private bool IsValidPieceIndex(
        int pieceIndex)
    {
        return pieceIndex >= 0 &&
               pieceIndex < MaxPieces;
    }

    private void SetPiecePoseInternal(
        int pieceIndex,
        Vector3 localPosition,
        Quaternion localRotation)
    {
        PieceLocalPositions.Set(
            pieceIndex,
            localPosition
        );

        PieceLocalRotations.Set(
            pieceIndex,
            localRotation
        );

        PiecePoseValidMask |=
            1 << pieceIndex;
    }

    private void ResetLivePieceState()
    {
        PiecePoseValidMask = 0;

        for (int i = 0;
             i < MaxPieces;
             i++)
        {
            PieceHolderIds.Set(
                i,
                -1
            );

            PieceLocalPositions.Set(
                i,
                Vector3.zero
            );

            PieceLocalRotations.Set(
                i,
                Quaternion.identity
            );
        }
    }

    private void ReleaseAllPieceHolders()
    {
        for (int i = 0;
             i < MaxPieces;
             i++)
        {
            PieceHolderIds.Set(
                i,
                -1
            );
        }
    }

    private void ClearDisconnectedPieceHolders()
    {
        for (int pieceIndex = 0;
             pieceIndex < MaxPieces;
             pieceIndex++)
        {
            int holderId =
                PieceHolderIds[pieceIndex];

            if (holderId < 0)
                continue;

            bool stillConnected =
                false;

            foreach (PlayerRef player
                     in Runner.ActivePlayers)
            {
                if (player.PlayerId == holderId)
                {
                    stillConnected = true;
                    break;
                }
            }

            if (!stillConnected)
            {
                PieceHolderIds.Set(
                    pieceIndex,
                    -1
                );
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}