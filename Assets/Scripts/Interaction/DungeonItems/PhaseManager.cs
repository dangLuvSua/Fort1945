using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

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
    [SerializeField] private VideoClip endingVideo;
    [SerializeField] private TMP_FontAsset phaseCompletedFont;
    [SerializeField, Min(0.5f)] private float phaseCompletedDisplayDuration = 2.5f;
    private bool endingStarted;
    private float nextProximityVoiceCheck;

    [Header("Testing (Play Mode)")]
    [SerializeField, Tooltip("Turn this on during Play Mode to trigger the all-phases-complete ending. Resets itself after triggering.")]
    private bool testCompletePhases;

    [SerializeField, Tooltip("Turn this on during Play Mode to directly test the white fade, return to Fort2, and ESP spawn placement. Resets itself after triggering.")]
    private bool testReturnToFort2;

    [SerializeField, Tooltip("Turn this on during Play Mode to preview the phase-completed screen and mission panel without changing phase state.")]
    private bool testPhaseCompletionScreen;

    [Networked]
    public int CurrentPhase { get; private set; }

    [Networked]
    public NetworkBool PhaseInitialized { get; private set; }

    [Networked]
    public int PhotoCapturesThisPhase { get; private set; }
    [Networked, Capacity(32)]
    private NetworkArray<NetworkString<_128>> CapturedMissionPhotoIds => default;

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
        if (testPhaseCompletionScreen)
        {
            testPhaseCompletionScreen = false;
            EyeBlinkTransition.PlayPhaseCompletion(
                phaseCompletedFont,
                "PHASE COMPLETED",
                null,
                () => MissionManager.ShowRaw("PHASE COMPLETED", "The next mission will begin shortly.", 0f),
                phaseCompletedDisplayDuration);
        }

        if (IsNetworkReady && Object != null && Object.IsValid && Object.HasStateAuthority &&
            Runner != null && Runner.IsRunning && CurrentPhase == 2 && !SequenceComplete &&
            Time.unscaledTime >= nextProximityVoiceCheck)
        {
            nextProximityVoiceCheck = Time.unscaledTime + 0.2f;
            foreach (CameraPhotoPoint point in FindObjectsByType<CameraPhotoPoint>(FindObjectsSortMode.None))
            {
                if (point.ShouldPlayProximityVoice(Runner))
                    RPC_PlayCharacterVoice(point.Photo.name);
            }
        }

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
        for (int index = 0; index < CapturedMissionPhotoIds.Length; index++)
            CapturedMissionPhotoIds.Set(index, default);
        PuzzlePiecesCollected = false;
        PhaseItemsRequired = 0;
        PhaseItemsCollected = 0;
        PhaseInitialized = true;

        Debug.Log($"[PHASE] Changed to Phase {CurrentPhase}.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayCharacterVoice(string photoId)
    {
        foreach (CameraPhotoPoint point in FindObjectsByType<CameraPhotoPoint>(FindObjectsSortMode.None))
        {
            if (point.Photo != null && point.Photo.name == photoId)
            {
                point.PlayProximityVoice();
                break;
            }
        }
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
            RPC_ShowPhaseCompletion(CurrentPhase, true, true);
            return;
        }

        int completedPhase = CurrentPhase;
        SetPhase(CurrentPhase + 1);
        RPC_ShowPhaseCompletion(completedPhase, true, false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowPhaseCompletion(int completedPhase, bool teleportToStart, bool beginEnding)
    {
        EyeBlinkTransition.PlayPhaseCompletion(
            phaseCompletedFont,
            beginEnding ? "SUCCESSFULLY UNCOVERED THE HISTORY" : "PHASE COMPLETED",
            teleportToStart ? TeleportLocalPlayerToDungeonStart : null,
            () =>
            {
                MissionManager.ShowRaw(
                    beginEnding ? "SUCCESSFULLY UNCOVERED THE HISTORY" : $"PHASE {completedPhase} COMPLETE",
                    beginEnding ? "You completed every mission phase." : "Proceed to the next mission.",
                    0f);
                if (beginEnding && Object != null && Object.IsValid && Object.HasStateAuthority)
                    RPC_BeginEnding(endingFadeDuration, endingSpawnPointName);
            },
            phaseCompletedDisplayDuration);
    }

    private void TeleportLocalPlayerToDungeonStart()
    {
        if (Runner == null || !Runner.IsRunning)
            return;

        DungeonGenerator generator = FindFirstObjectByType<DungeonGenerator>();
        if (generator == null || !generator.IsGenerated)
        {
            Debug.LogWarning("[PHASE] Dungeon starting spawn is not ready; cannot reposition player.", this);
            return;
        }

        PlayerRef localPlayer = Runner.LocalPlayer;
        if (!localPlayer.IsValid || !Runner.TryGetPlayerObject(localPlayer, out NetworkObject playerObject) || playerObject == null)
            return;

        PlayerController controller = playerObject.GetComponent<PlayerController>();
        if (controller == null)
            return;

        // Keep the team distributed across the entrance spawn points, matching
        // the dungeon's initial per-player spawn assignment.
        generator.GetSpawn(localPlayer.RawEncoded, out Vector3 position, out Quaternion rotation);
        controller.TeleportDuringTransition(position, rotation);
    }

    /// <summary>Reports one successful camera capture to the state authority.</summary>
    public void ReportPhotoCaptured(string photoId)
    {
        if (Object == null || !Object.IsValid)
            return;

        if (!string.IsNullOrWhiteSpace(photoId))
            RPC_ReportPhotoCaptured(photoId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportPhotoCaptured(string photoId, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || SequenceComplete || CurrentPhase != 2)
            return;

        if (string.IsNullOrWhiteSpace(photoId) || photoId.Length > 128)
            return;

        // Validate against generated character photo points, then deduplicate
        // across the whole team. Ordinary snapshots never supply a target ID.
        bool knownTarget = false;
        foreach (CameraPhotoPoint point in FindObjectsByType<CameraPhotoPoint>(FindObjectsSortMode.None))
        {
            if (point.Photo != null && point.Photo.name == photoId)
            {
                knownTarget = true;
                break;
            }
        }
        if (!knownTarget)
            return;

        int emptyIndex = -1;
        for (int index = 0; index < CapturedMissionPhotoIds.Length; index++)
        {
            string existing = CapturedMissionPhotoIds.Get(index).ToString();
            if (existing == photoId)
                return;
            if (emptyIndex < 0 && string.IsNullOrEmpty(existing))
                emptyIndex = index;
        }
        if (emptyIndex < 0)
            return;
        CapturedMissionPhotoIds.Set(emptyIndex, photoId);
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
                : spawnPointName,
            endingVideo);
    }

}
