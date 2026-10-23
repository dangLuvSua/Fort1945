using Fusion;
using UnityEngine;

public class PhaseManager : NetworkBehaviour
{
    public static PhaseManager Instance { get; private set; }

    [Header("Phase Settings")]
    [SerializeField, Min(1)]
    private int startingPhase = 1;

    [SerializeField, Min(1)]
    private int totalPhases = 3;

    [Networked]
    public int CurrentPhase { get; private set; }

    [Networked]
    public NetworkBool PhaseInitialized { get; private set; }

    [Networked]
    public int PhotoCapturesThisPhase { get; private set; }

    [Networked]
    public NetworkBool SequenceComplete { get; private set; }

    [Networked]
    public NetworkBool PuzzlePiecesCollected { get; private set; }

    [Networked]
    public int PhaseItemsRequired { get; private set; }

    [Networked]
    public int PhaseItemsCollected { get; private set; }

    public override void Spawned()
    {
        Instance = this;

        if (Object.HasStateAuthority && !PhaseInitialized)
        {
            CurrentPhase = Mathf.Max(1, startingPhase);
            PhaseInitialized = true;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetPhase(int phase)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            Debug.LogWarning(
                "[PHASE] Only the state authority can change phases."
            );
            return;
        }

        phase = Mathf.Max(1, phase);

        if (CurrentPhase == phase && PhaseInitialized)
            return;

        CurrentPhase = phase;
        PhotoCapturesThisPhase = 0;
        PuzzlePiecesCollected = false;
        PhaseItemsRequired = 0;
        PhaseItemsCollected = 0;
        PhaseInitialized = true;

        Debug.Log($"[PHASE] Changed to Phase {CurrentPhase}.");
    }

    public void AdvancePhase()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (SequenceComplete)
            return;

        if (CurrentPhase >= Mathf.Max(1, totalPhases))
        {
            SequenceComplete = true;
            Debug.Log("[PHASE] All mission phases completed.");
            RPC_ShowMission("ALL MISSIONS COMPLETE", "You completed every mission phase.");
            return;
        }

        SetPhase(CurrentPhase + 1);
    }

    /// <summary>Reports one successful camera capture to the state authority.</summary>
    public void ReportPhotoCaptured()
    {
        if (Object == null || !Object.IsValid)
            return;

        RPC_ReportPhotoCaptured();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportPhotoCaptured(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || SequenceComplete || CurrentPhase != 2)
            return;

        PhotoCapturesThisPhase++;
    }

    /// <summary>Completes a phase whose configured rule uses ExternalSignal.</summary>
    public void ReportExternalPhaseComplete()
    {
        if (Object == null || !Object.IsValid)
            return;

        RPC_ReportExternalPhaseComplete();
    }

    public void ReportPuzzlePiecesCollected()
    {
        if (Object == null || !Object.IsValid)
            return;

        RPC_ReportPuzzlePiecesCollected();
    }

    public void UpdateItemProgress(int required, int collected)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        PhaseItemsRequired = Mathf.Max(0, required);
        PhaseItemsCollected = Mathf.Clamp(collected, 0, PhaseItemsRequired);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportPuzzlePiecesCollected(RpcInfo info = default)
    {
        if (Object.HasStateAuthority && CurrentPhase == 2 && !SequenceComplete)
            PuzzlePiecesCollected = true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportExternalPhaseComplete(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || SequenceComplete ||
            CurrentPhase < Mathf.Max(1, totalPhases))
            return;

        AdvancePhase();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowMission(string title, string description)
    {
        MissionManager.ShowRaw(title, description, 0f);
    }
}
