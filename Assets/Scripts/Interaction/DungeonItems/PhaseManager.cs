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

    [Header("Ending")]
    [SerializeField, Min(0.1f)] private float endingFadeDuration = 1.5f;
    [SerializeField] private string endingSpawnPointName = "EndingSpawnPoint";
    private bool endingStarted;

    [Header("Testing (Play Mode)")]
    [SerializeField, Tooltip("Turn this on during Play Mode to trigger the all-phases-complete ending. Resets itself after triggering.")]
    private bool testCompletePhases;

    [SerializeField, Tooltip("Turn this on during Play Mode to directly test the white fade, return to Fort2, and ESP spawn placement. Resets itself after triggering.")]
    private bool testReturnToFort2;

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

    public bool IsNetworkReady { get; private set; }

    public override void Spawned()
    {
        Instance = this;
        IsNetworkReady = true;

        if (Object.HasStateAuthority && !PhaseInitialized)
        {
            CurrentPhase = Mathf.Max(1, startingPhase);
            PhaseInitialized = true;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        IsNetworkReady = false;
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (testReturnToFort2)
        {
            testReturnToFort2 = false;
            if (Object == null || !Object.HasStateAuthority)
            {
                Debug.LogWarning("[PHASE TEST] Enable this toggle on the state-authority PhaseManager instance.", this);
                return;
            }

            SequenceComplete = true;
            RPC_BeginEnding(endingFadeDuration, endingSpawnPointName);
            return;
        }

        if (!testCompletePhases)
            return;

        testCompletePhases = false;
        if (Object == null || !Object.HasStateAuthority)
        {
            Debug.LogWarning("[PHASE TEST] Enable this toggle on the state-authority PhaseManager instance.", this);
            return;
        }

        if (SequenceComplete)
            return;

        CurrentPhase = Mathf.Max(1, totalPhases);
        PhaseInitialized = true;
        AdvancePhase();
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
            RPC_BeginEnding(endingFadeDuration, endingSpawnPointName);
            return;
        }

        SetPhase(CurrentPhase + 1);
        RPC_TeleportPlayersToPhaseSpawn();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_TeleportPlayersToPhaseSpawn()
    {
        if (Runner == null || !Runner.IsRunning)
            return;

        DungeonGenerator generator = FindFirstObjectByType<DungeonGenerator>();
        if (generator == null || !generator.IsGenerated)
        {
            Debug.LogWarning("[PHASE] Dungeon spawn points are not ready; cannot reposition players for the next phase.", this);
            return;
        }

        PlayerRef localPlayer = Runner.LocalPlayer;
        if (!localPlayer.IsValid ||
            !Runner.TryGetPlayerObject(localPlayer, out NetworkObject playerObject) ||
            playerObject == null)
            return;

        PlayerController controller = playerObject.GetComponent<PlayerController>();
        if (controller == null)
            return;

        generator.GetSpawn(localPlayer.RawEncoded, out Vector3 position, out Quaternion rotation);
        controller.Teleport(position, rotation);
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BeginEnding(float fadeDuration, string spawnPointName)
    {
        if (endingStarted)
            return;

        endingStarted = true;
        EyeBlinkTransition.CloseAndHold(Color.black, fadeDuration);
        EndingSceneTransition.Begin(
            string.IsNullOrWhiteSpace(spawnPointName)
                ? endingSpawnPointName
                : spawnPointName);
    }

}
