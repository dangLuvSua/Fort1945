using System.Collections;
using Fusion;
using UnityEngine;

/// <summary>Dungeon-only shared state for prison cell gates.</summary>
public sealed class DungeonNetworkGameManager : NetworkBehaviour
{
    [Header("All Players Captured")]
    [SerializeField] private LocalJumpscarePlayback allCapturedJumpscarePrefab;
    [SerializeField, Min(0f), Tooltip("Maximum seconds to show the game-over jumpscare before returning to Fort2.")]
    private float allCapturedReturnDelay = 3f;

    public static DungeonNetworkGameManager Instance { get; private set; }

    [Networked] public NetworkBool PrisonCell1Unlocked { get; private set; }
    [Networked] public NetworkBool PrisonCell2Unlocked { get; private set; }
    [Networked] public NetworkBool PrisonCell1GateOpen { get; private set; }
    [Networked] public NetworkBool PrisonCell2GateOpen { get; private set; }

    private bool networkStateReady;
    private bool returningCapturedPlayersToLobby;
    private Coroutine prisonCell1AutoCloseRoutine;
    private Coroutine prisonCell2AutoCloseRoutine;
    public bool IsNetworkStateReady => networkStateReady;

    public override void Spawned()
    {
        Instance = this;
        networkStateReady = true;

        if (Object.HasStateAuthority)
        {
            PrisonCell1Unlocked = false;
            PrisonCell2Unlocked = false;
            PrisonCell1GateOpen = false;
            PrisonCell2GateOpen = false;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        networkStateReady = false;
        if (Instance == this)
            Instance = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!networkStateReady || Runner == null || !Object.HasStateAuthority ||
            !Runner.IsSceneAuthority || returningCapturedPlayersToLobby)
            return;

        int activePlayerCount = 0;
        string capturedPlayerRoster = string.Empty;
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            activePlayerCount++;
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) ||
                playerObject == null ||
                !playerObject.TryGetComponent(out PlayerController controller) ||
                !controller.IsCapturedByKempeitai)
                return;

            if (capturedPlayerRoster.Length > 0)
                capturedPlayerRoster += ", ";
            capturedPlayerRoster += player.ToString();
        }

        if (activePlayerCount == 0)
            return;

        returningCapturedPlayersToLobby = true;
        Debug.Log(
            $"[PRISON] All {activePlayerCount} active players have been captured " +
            $"({capturedPlayerRoster}). Playing the group jumpscare before returning to Fort2.",
            this);

        RPC_PlayAllCapturedJumpscare();
        StartCoroutine(ReturnToLobbyAfterJumpscare());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAllCapturedJumpscare()
    {
        if (allCapturedJumpscarePrefab == null)
        {
            Debug.LogWarning("[PRISON] No all-captured jumpscare prefab is assigned.", this);
            return;
        }

        LocalJumpscarePlayback playback = Instantiate(allCapturedJumpscarePrefab);
        playback.Play();
    }

    private IEnumerator ReturnToLobbyAfterJumpscare()
    {
        if (allCapturedJumpscarePrefab != null)
        {
            float duration = Mathf.Min(
                allCapturedJumpscarePrefab.GetMaximumPlaybackDuration(),
                Mathf.Max(0f, allCapturedReturnDelay));
            if (duration > 0f)
                yield return new WaitForSecondsRealtime(duration);
        }

        RPC_CloseScreenForFort2Load();
        // Let each player's eyelids finish closing before the scene transition.
        yield return new WaitForSecondsRealtime(0.4f);

        NetworkRunnerHandler handler = NetworkRunnerHandler.Instance;
        if (handler == null || !handler.LoadFort2Scene())
        {
            returningCapturedPlayersToLobby = false;
            EyeBlinkTransition.Open();
            Debug.LogError("[PRISON] Could not return the captured group to Fort2.", this);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CloseScreenForFort2Load()
    {
        EyeBlinkTransition.CloseAndHold(Color.black, 0.35f);
        EyeBlinkTransition.OpenAfterSceneLoads("Fort2");
    }

    public static DungeonNetworkGameManager ResolveInstance()
    {
        if (Instance == null)
            Instance = FindFirstObjectByType<DungeonNetworkGameManager>();
        return Instance;
    }

    public void RequestPrisonCellUnlock(int cellNumber, int keyItemId)
    {
        if (Runner != null && Runner.IsRunning && Object != null && Object.IsValid)
            RPC_RequestPrisonCellUnlock(cellNumber, keyItemId);
    }

    public void RequestPrisonCellGateState(int cellNumber, bool open)
    {
        if (Runner != null && Runner.IsRunning && Object != null && Object.IsValid)
            RPC_RequestPrisonCellGateState(cellNumber, open);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPrisonCellUnlock(int cellNumber, int keyItemId, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || !TryGetRequestingPlayer(info.Source, out NetworkObject playerObject))
            return;

        int requiredKeyId = cellNumber == 1 ? 102 : cellNumber == 2 ? 104 : 0;
        if (requiredKeyId == 0 || keyItemId != requiredKeyId)
            return;

        PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
        PrisonCellGateUnlock gate = FindGate(cellNumber);
        if (!HasEquippedItem(inventory, requiredKeyId) || gate == null ||
            !gate.IsValidAuthorityRequest(playerObject.transform))
        {
            Debug.LogWarning($"[DUNGEON GATE] Rejected unlock request for cell {cellNumber} from {info.Source}.", this);
            return;
        }

        SetCellState(cellNumber, true, true);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPrisonCellGateState(int cellNumber, bool open, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || !TryGetRequestingPlayer(info.Source, out NetworkObject playerObject))
            return;

        bool unlocked = cellNumber == 1 ? PrisonCell1Unlocked :
                        cellNumber == 2 && PrisonCell2Unlocked;
        if (!unlocked)
            return;

        PrisonCellGateUnlock gate = FindGate(cellNumber);
        if (gate == null || !gate.IsValidAuthorityRequest(playerObject.transform, open))
            return;

        PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
        int keyId = cellNumber == 1 ? 102 : cellNumber == 2 ? 104 : 0;
        if (keyId == 0 || (open && !HasEquippedItem(inventory, keyId)))
            return;

        SetCellState(cellNumber, unlocked, open);
    }

    private bool TryGetRequestingPlayer(PlayerRef player, out NetworkObject playerObject)
    {
        playerObject = null;
        return player != PlayerRef.None && Runner != null &&
               Runner.TryGetPlayerObject(player, out playerObject) && playerObject != null;
    }

    private static bool HasEquippedItem(PlayerInventory inventory, int itemId)
    {
        if (inventory == null || !inventory.HasItemId(itemId))
            return false;

        int selectedSlot = inventory.SelectedSlot;
        return selectedSlot >= 0 && inventory.GetQuickBarItemIdAt(selectedSlot) == itemId;
    }

    private static PrisonCellGateUnlock FindGate(int cellNumber)
    {
        PrisonCellGateUnlock[] gates = FindObjectsByType<PrisonCellGateUnlock>(FindObjectsSortMode.None);
        foreach (PrisonCellGateUnlock gate in gates)
        {
            if (gate != null && gate.CellNumber == cellNumber)
                return gate;
        }
        return null;
    }

    private void SetCellState(int cellNumber, bool unlocked, bool open)
    {
        if (cellNumber == 1)
        {
            PrisonCell1Unlocked = unlocked;
            PrisonCell1GateOpen = open;
            RestartPrisonCellAutoClose(1, open);
        }
        else if (cellNumber == 2)
        {
            PrisonCell2Unlocked = unlocked;
            PrisonCell2GateOpen = open;
            RestartPrisonCellAutoClose(2, open);
        }
    }

    private void RestartPrisonCellAutoClose(int cellNumber, bool open)
    {
        Coroutine routine = cellNumber == 1
            ? prisonCell1AutoCloseRoutine
            : prisonCell2AutoCloseRoutine;
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        if (cellNumber == 1)
            prisonCell1AutoCloseRoutine = null;
        else
            prisonCell2AutoCloseRoutine = null;

        if (!open)
            return;

        PrisonCellGateUnlock gate = FindGate(cellNumber);
        float delay = gate != null ? gate.AutoCloseDelay : 5f;
        if (delay > 0f)
        {
            routine = StartCoroutine(AutoClosePrisonCell(cellNumber, delay));
            if (cellNumber == 1)
                prisonCell1AutoCloseRoutine = routine;
            else
                prisonCell2AutoCloseRoutine = routine;
        }
    }

    private IEnumerator AutoClosePrisonCell(int cellNumber, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        bool isStillOpen = cellNumber == 1
            ? PrisonCell1GateOpen
            : PrisonCell2GateOpen;
        bool isStillUnlocked = cellNumber == 1
            ? PrisonCell1Unlocked
            : PrisonCell2Unlocked;

        if (Object != null && Object.HasStateAuthority && isStillOpen && isStillUnlocked)
        {
            Debug.Log($"[PRISON GATE] Cell {cellNumber} auto-closed after {delay:0.##} seconds.", this);
            SetCellState(cellNumber, true, false);
        }

        if (cellNumber == 1)
            prisonCell1AutoCloseRoutine = null;
        else
            prisonCell2AutoCloseRoutine = null;
    }
}
