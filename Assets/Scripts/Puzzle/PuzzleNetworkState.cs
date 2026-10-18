using Fusion;
using UnityEngine;

public class PuzzleNetworkState : NetworkBehaviour
{
    public static PuzzleNetworkState Instance { get; private set; }

    // =========================================================
    // MAXIMUM SUPPORTED PIECES
    // =========================================================

    // int bitmask = maximum of 32 pieces.
    public const int MaxPieces = 32;

    // =========================================================
    // SHARED PUZZLE CONFIGURATION
    // =========================================================

    [Networked]
    public int ActivePieceCount { get; private set; }

    // 0 = Easy
    // 1 = Medium
    // 2 = Hard
    [Networked]
    public int PuzzleDifficultyId { get; private set; }

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

    // Bit = 1 means this piece already has a valid
    // authoritative board position and rotation.
    [Networked]
    public int PiecePoseValidMask { get; private set; }

    // =========================================================
    // LIVE PIECE TRANSFORMS
    // =========================================================

    [Networked, Capacity(32)]
    public NetworkArray<Vector3> PieceLocalPositions => default;

    [Networked, Capacity(32)]
    public NetworkArray<Quaternion> PieceLocalRotations => default;

    // -1 = nobody is holding the piece.
    // Otherwise stores PlayerRef.PlayerId.
    [Networked, Capacity(32)]
    public NetworkArray<int> PieceHolderIds => default;

    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        Instance = this;

        if (Object.HasStateAuthority)
        {
            SolvedMask = 0;

            PiecePoseValidMask = 0;

            PuzzleStarted = false;

            PuzzleCompleted = false;

            PuzzleFailed = false;

            PuzzleTimer = TickTimer.None;

            ActivePieceCount = 0;

            PuzzleDifficultyId = 0;

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
        {
            return;
        }

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

            SolvedMask = 0;

            PuzzleTimer = TickTimer.None;

            ResetLivePieceState();

            Debug.Log(
                $"[PUZZLE NETWORK] " +
                $"Shared puzzle timer expired. " +
                $"Progress reset to 0/{ActivePieceCount}."
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
        float duration,
        int pieceCount,
        int difficultyId)
    {
        // Completed puzzle cannot be restarted.
        if (PuzzleCompleted)
        {
            return;
        }

        // Another client already started this attempt.
        if (PuzzleStarted &&
            !PuzzleFailed)
        {
            return;
        }

        int safePieceCount =
            Mathf.Clamp(
                pieceCount,
                1,
                MaxPieces
            );

        ActivePieceCount =
            safePieceCount;

        PuzzleDifficultyId =
            Mathf.Clamp(
                difficultyId,
                0,
                2
            );

        SolvedMask = 0;

        PuzzleStarted = true;

        PuzzleCompleted = false;

        PuzzleFailed = false;

        ResetLivePieceState();

        PuzzleTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                Mathf.Max(
                    0.1f,
                    duration
                )
            );

        Debug.Log(
            $"[PUZZLE NETWORK] Shared puzzle started. " +
            $"Difficulty={GetDifficultyName()} | " +
            $"Pieces={ActivePieceCount} | " +
            $"Duration={duration:F1}s"
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
        if (!IsValidPieceIndex(
                pieceIndex) ||
            PuzzleCompleted ||
            PuzzleFailed ||
            IsPieceSolved(
                pieceIndex))
        {
            return;
        }

        // First valid pose becomes the authoritative
        // starting position for this attempt.
        if (IsPiecePoseValid(
                pieceIndex))
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
    // REQUEST PIECE GRAB
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    public void RPC_RequestGrab(
        int pieceIndex,
        RpcInfo info = default)
    {
        if (!CanManipulatePiece(
                pieceIndex))
        {
            return;
        }

        int sourcePlayerId =
            info.Source.PlayerId;

        int currentHolder =
            PieceHolderIds[pieceIndex];

        // Another player already owns this piece.
        if (currentHolder != -1 &&
            currentHolder != sourcePlayerId)
        {
            return;
        }

        PieceHolderIds.Set(
            pieceIndex,
            sourcePlayerId
        );

        Debug.Log(
            $"[PUZZLE NETWORK] Player " +
            $"{sourcePlayerId} grabbed piece " +
            $"{pieceIndex + 1}."
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
        if (!CanManipulatePiece(
                pieceIndex))
        {
            return;
        }

        // Only the player currently holding the piece
        // is allowed to change its transform.
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
    // RELEASE PIECE
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
        if (!IsValidPieceIndex(
                pieceIndex))
        {
            return;
        }

        if (PieceHolderIds[pieceIndex] !=
            info.Source.PlayerId)
        {
            return;
        }

        // Keep final released position for unsolved pieces.
        if (!IsPieceSolved(
                pieceIndex) &&
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
        if (!IsValidPieceIndex(
                pieceIndex))
        {
            return;
        }

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
        if (!CanManipulatePiece(
                pieceIndex))
        {
            return;
        }

        int holder =
            PieceHolderIds[pieceIndex];

        // If somebody currently owns the piece,
        // only that player may solve it.
        if (holder != -1 &&
            holder != info.Source.PlayerId)
        {
            return;
        }

        int bit =
            1 << pieceIndex;

        // Already solved.
        if ((SolvedMask & bit) != 0)
        {
            return;
        }

        SolvedMask |=
            bit;

        // Solved pieces are no longer held.
        PieceHolderIds.Set(
            pieceIndex,
            -1
        );

        int solvedCount =
            GetSolvedPieceCount();

        Debug.Log(
            $"[PUZZLE NETWORK] Piece " +
            $"{pieceIndex + 1} solved. " +
            $"Progress: {solvedCount}/" +
            $"{ActivePieceCount}"
        );

        // =====================================================
        // CHECK COMPLETION
        // =====================================================

        int requiredMask =
            GetRequiredSolvedMask();

        if ((SolvedMask & requiredMask) ==
            requiredMask)
        {
            PuzzleCompleted =
                true;

            PuzzleStarted =
                false;

            PuzzleFailed =
                false;

            PuzzleTimer =
                TickTimer.None;

            ReleaseAllPieceHolders();

            Debug.Log(
                $"[PUZZLE NETWORK] " +
                $"{GetDifficultyName()} " +
                $"PUZZLE COMPLETE! " +
                $"{ActivePieceCount}/" +
                $"{ActivePieceCount}"
            );
        }
    }

    // =========================================================
    // RETRY PUZZLE
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    public void RPC_RetryPuzzle(
        float duration,
        int pieceCount,
        int difficultyId)
    {
        // Successfully completed puzzles are permanently finished.
        if (PuzzleCompleted)
        {
            return;
        }

        ActivePieceCount =
            Mathf.Clamp(
                pieceCount,
                1,
                MaxPieces
            );

        PuzzleDifficultyId =
            Mathf.Clamp(
                difficultyId,
                0,
                2
            );

        SolvedMask =
            0;

        PuzzleFailed =
            false;

        PuzzleCompleted =
            false;

        PuzzleStarted =
            true;

        ResetLivePieceState();

        PuzzleTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                Mathf.Max(
                    0.1f,
                    duration
                )
            );

        Debug.Log(
            $"[PUZZLE NETWORK] Shared " +
            $"{GetDifficultyName()} puzzle retry. " +
            $"Progress reset to " +
            $"0/{ActivePieceCount}."
        );
    }

    // =========================================================
    // PUBLIC HELPERS
    // =========================================================

    public bool IsPieceSolved(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(
                pieceIndex))
        {
            return false;
        }

        return
            (SolvedMask &
             (1 << pieceIndex)) != 0;
    }

    public bool IsPiecePoseValid(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(
                pieceIndex))
        {
            return false;
        }

        return
            (PiecePoseValidMask &
             (1 << pieceIndex)) != 0;
    }

    public Vector3 GetPieceLocalPosition(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(
                pieceIndex))
        {
            return Vector3.zero;
        }

        return
            PieceLocalPositions[pieceIndex];
    }

    public Quaternion GetPieceLocalRotation(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(
                pieceIndex))
        {
            return Quaternion.identity;
        }

        return
            PieceLocalRotations[pieceIndex];
    }

    public int GetPieceHolderId(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(
                pieceIndex))
        {
            return -1;
        }

        return
            PieceHolderIds[pieceIndex];
    }

    public bool IsPieceHeldByLocalPlayer(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(
                pieceIndex) ||
            Runner == null ||
            !Runner.LocalPlayer.IsValid)
        {
            return false;
        }

        return
            PieceHolderIds[pieceIndex] ==
            Runner.LocalPlayer.PlayerId;
    }

    public bool IsPieceHeldByOtherPlayer(
        int pieceIndex)
    {
        if (!IsValidPieceIndex(
                pieceIndex))
        {
            return false;
        }

        int holder =
            PieceHolderIds[pieceIndex];

        if (holder < 0)
        {
            return false;
        }

        if (Runner == null ||
            !Runner.LocalPlayer.IsValid)
        {
            return true;
        }

        return
            holder !=
            Runner.LocalPlayer.PlayerId;
    }

    public float GetRemainingTime()
    {
        if (!PuzzleStarted ||
            Runner == null)
        {
            return 0f;
        }

        return
            PuzzleTimer
                .RemainingTime(
                    Runner
                )
                .GetValueOrDefault();
    }

    public int GetSolvedPieceCount()
    {
        int count =
            0;

        int pieceCount =
            Mathf.Clamp(
                ActivePieceCount,
                0,
                MaxPieces
            );

        for (int i = 0;
             i < pieceCount;
             i++)
        {
            if ((SolvedMask &
                 (1 << i)) != 0)
            {
                count++;
            }
        }

        return count;
    }

    public string GetDifficultyName()
    {
        switch (PuzzleDifficultyId)
        {
            case 1:
                return "Medium";

            case 2:
                return "Hard";

            default:
                return "Easy";
        }
    }

    // =========================================================
    // VALIDATE MANIPULATION
    // =========================================================

    private bool CanManipulatePiece(
        int pieceIndex)
    {
        return
            IsValidPieceIndex(
                pieceIndex
            ) &&
            PuzzleStarted &&
            !PuzzleCompleted &&
            !PuzzleFailed &&
            !IsPieceSolved(
                pieceIndex
            );
    }

    // =========================================================
    // VALIDATE INDEX
    // =========================================================

    private bool IsValidPieceIndex(
        int pieceIndex)
    {
        if (pieceIndex < 0 ||
            pieceIndex >= MaxPieces)
        {
            return false;
        }

        // Before puzzle configuration exists,
        // only the maximum array bounds matter.
        if (ActivePieceCount <= 0)
        {
            return true;
        }

        return
            pieceIndex <
            ActivePieceCount;
    }

    // =========================================================
    // SET PIECE POSE
    // =========================================================

    private void SetPiecePoseInternal(
        int pieceIndex,
        Vector3 localPosition,
        Quaternion localRotation)
    {
        if (pieceIndex < 0 ||
            pieceIndex >= MaxPieces)
        {
            return;
        }

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

    // =========================================================
    // BUILD REQUIRED SOLVED MASK
    // =========================================================

    private int GetRequiredSolvedMask()
    {
        int count =
            Mathf.Clamp(
                ActivePieceCount,
                1,
                MaxPieces
            );

        // All 32 bits.
        if (count >= 32)
        {
            return -1;
        }

        return
            (1 << count) - 1;
    }

    // =========================================================
    // RESET LIVE PIECES
    // =========================================================

    private void ResetLivePieceState()
    {
        PiecePoseValidMask =
            0;

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

    // =========================================================
    // RELEASE ALL PIECES
    // =========================================================

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

    // =========================================================
    // DISCONNECTED PLAYERS
    // =========================================================

    private void ClearDisconnectedPieceHolders()
    {
        if (Runner == null)
        {
            return;
        }

        for (int pieceIndex = 0;
             pieceIndex < MaxPieces;
             pieceIndex++)
        {
            int holderId =
                PieceHolderIds[pieceIndex];

            if (holderId < 0)
            {
                continue;
            }

            bool stillConnected =
                false;

            foreach (PlayerRef player
                     in Runner.ActivePlayers)
            {
                if (player.PlayerId ==
                    holderId)
                {
                    stillConnected =
                        true;

                    break;
                }
            }

            if (!stillConnected)
            {
                PieceHolderIds.Set(
                    pieceIndex,
                    -1
                );

                Debug.Log(
                    $"[PUZZLE NETWORK] Released piece " +
                    $"{pieceIndex + 1} because player " +
                    $"{holderId} disconnected."
                );
            }
        }
    }

    // =========================================================
    // DESTROY
    // =========================================================

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance =
                null;
        }
    }
}