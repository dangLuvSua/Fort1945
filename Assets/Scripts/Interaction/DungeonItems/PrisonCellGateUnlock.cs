using System.Collections;
using Fusion;
using UnityEngine;

/// <summary>Hold-to-unlock interaction for one dungeon prison cell.</summary>
public sealed class PrisonCellGateUnlock : MonoBehaviour, IHoldInteractable, IQuickTapHoldInteractable
{
    [Header("Cell Key")]
    [SerializeField, Range(1, 2)] private int cellNumber = 1;
    [SerializeField, Min(1)] private int requiredKeyItemId = 102;
    [SerializeField] private string requiredKeyName = "PRISON KEY 1";
    [SerializeField] private string cellName = "CELL 1";

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float holdDuration = 2f;
    [SerializeField] private string unlockPrompt = "[E] HOLD TO UNLOCK";
    [SerializeField] private string closePrompt = "[E] CLOSE";
    [SerializeField] private string reopenPrompt = "[E] HOLD TO OPEN";

    [Header("Approach Side")]
    [Tooltip("Place this empty transform in the corridor/outside the cell. The gate can only be unlocked from this side.")]
    [SerializeField] private Transform outsideSidePoint;
    [SerializeField, Min(0.5f)] private float authorityInteractionDistance = 4f;

    [Header("Gate")]
    [Tooltip("Assign the gate leaf/hinge transform. If empty, the component's transform is used.")]
    [SerializeField] private Transform gateToRotate;
    [SerializeField] private Vector3 openRotationOffset = new Vector3(0f, 90f, 0f);
    [SerializeField, Min(0.05f)] private float openDuration = 1.25f;
    [Tooltip("Optional colliders to disable after unlocking, such as the bars' blocking collider.")]
    [SerializeField] private Collider[] blockingColliders;
    [SerializeField] private AudioSource unlockAudio;

    private PlayerInventory viewerInventory;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool unlocked;
    private bool unlockPending;
    private bool gateOpen;
    private bool gateTogglePending;
    private Coroutine gateMotionRoutine;
    private float requestPendingSince;

    public int CellNumber => cellNumber;

    public string Prompt
    {
        get
        {
            if (!IsViewerOnOutsideSide())
                return string.Empty;

            if (unlocked)
            {
                if (gateOpen)
                    return closePrompt;

                return HasRequiredKey()
                    ? reopenPrompt
                    : $"EQUIP {requiredKeyName}, THEN HOLD [E] TO OPEN";
            }

            return HasRequiredKey()
                ? unlockPrompt
                : $"EQUIP {requiredKeyName}, THEN HOLD [E] TO UNLOCK";
        }
    }

    // Don't expose this gate as an interaction target from inside the cell.
    // This also prevents PlayerInteractor from starting its hold-progress UI.
    public bool CanInteract => enabled && !unlockPending && !gateTogglePending && IsViewerOnOutsideSide();
    public bool CanTapInteract => unlocked && gateOpen && !gateTogglePending;
    public int Priority => 40;
    public float HoldDuration => holdDuration;

    private void Awake()
    {
        if (gateToRotate == null)
            gateToRotate = transform;

        closedRotation = gateToRotate.localRotation;
        openRotation = closedRotation * Quaternion.Euler(openRotationOffset);
    }

    private void Update()
    {
        if ((unlockPending || gateTogglePending) && Time.unscaledTime - requestPendingSince > 4f)
            ShowRequestRejectedMessage();

        DungeonNetworkGameManager manager = DungeonNetworkGameManager.ResolveInstance();
        if (manager == null || !manager.IsNetworkStateReady)
            return;

        bool networkUnlocked = cellNumber == 1
            ? manager.PrisonCell1Unlocked
            : manager.PrisonCell2Unlocked;

        // The manager is the source of truth for every client. Keep the
        // local interaction and visuals in sync even if this scene object
        // was enabled after the network state changed.
        unlocked = networkUnlocked;
        bool networkGateOpen = cellNumber == 1
            ? manager.PrisonCell1GateOpen
            : manager.PrisonCell2GateOpen;

        if (networkGateOpen != gateOpen)
            SetGateOpen(networkGateOpen);
    }

    public void UpdateContext(PlayerInventory inventory) => viewerInventory = inventory;
    public void HoldStarted(PlayerInventory inventory) => viewerInventory = inventory;
    public void HoldCancelled() { }

    public void Interact(PlayerInventory inventory)
    {
        // Hold interactions are completed through IHoldInteractable.
        viewerInventory = inventory;
    }

    public void TapInteract(PlayerInventory inventory)
    {
        viewerInventory = inventory;
        if (CanTapInteract && IsPlayerOnOutsideSide(inventory))
            CloseGate();
    }

    public void HoldCompleted(PlayerInventory inventory)
    {
        viewerInventory = inventory;

        if (!IsPlayerOnOutsideSide(inventory))
        {
            MissionManager.ShowRaw(
                "LOCKED FROM INSIDE",
                $"{cellName} can only be operated from outside the cell.",
                3f);
            return;
        }

        if (unlocked)
        {
            if (!gateOpen && !HasRequiredKey())
            {
                ShowKeyError($"Equip {requiredKeyName} to reopen {cellName}.");
                return;
            }

            RequestGateOpenState(!gateOpen);
            return;
        }

        if (!HasRequiredKey())
        {
            ShowKeyError($"Find and equip {requiredKeyName} to unlock {cellName}.");
            return;
        }

        DungeonNetworkGameManager manager = DungeonNetworkGameManager.ResolveInstance();
        if (manager != null)
        {
            if (!manager.IsNetworkStateReady)
            {
                ShowNetworkSyncMessage();
                return;
            }

            unlockPending = true;
            requestPendingSince = Time.unscaledTime;
            manager.RequestPrisonCellUnlock(cellNumber, requiredKeyItemId);
            return;
        }

        if (IsNetworkSessionRunning())
        {
            ShowNetworkSyncMessage();
            return;
        }

        unlocked = true;
        SetGateOpen(true);
    }

    /// <summary>Closes an already unlocked cell gate.</summary>
    public void CloseGate()
    {
        if (!unlocked || !gateOpen)
            return;

        RequestGateOpenState(false);
    }

    private void RequestGateOpenState(bool open)
    {
        DungeonNetworkGameManager manager = DungeonNetworkGameManager.ResolveInstance();
        if (manager != null)
        {
            if (!manager.IsNetworkStateReady)
            {
                ShowNetworkSyncMessage();
                return;
            }

            gateTogglePending = true;
            requestPendingSince = Time.unscaledTime;
            manager.RequestPrisonCellGateState(cellNumber, open);
            return;
        }

        if (IsNetworkSessionRunning())
        {
            ShowNetworkSyncMessage();
            return;
        }

        SetGateOpen(open);
    }

    private static bool IsNetworkSessionRunning()
    {
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
        return runner != null && runner.IsRunning;
    }

    private void ShowNetworkSyncMessage()
    {
        unlockPending = false;
        gateTogglePending = false;
        MissionManager.ShowRaw(
            "GATE SYNCING",
            "The shared gate state is not ready yet. Try again in a moment.",
            2f);
    }

    private void ShowRequestRejectedMessage()
    {
        unlockPending = false;
        gateTogglePending = false;

        if (viewerInventory != null && !IsPlayerOnOutsideSide(viewerInventory))
        {
            MissionManager.ShowRaw(
                "LOCKED FROM INSIDE",
                $"{cellName} can only be operated from outside the cell.",
                3f);
            return;
        }

        MissionManager.ShowRaw(
            "GATE DID NOT OPEN",
            "The gate request was rejected. Move to the corridor side and try again.",
            3f);
    }

    private void SetGateOpen(bool open)
    {
        if (gateToRotate == null || (gateOpen == open && gateMotionRoutine == null))
            return;

        gateOpen = open;
        unlockPending = false;
        gateTogglePending = false;

        if (blockingColliders != null)
        {
            foreach (Collider blockingCollider in blockingColliders)
            {
                if (blockingCollider != null)
                    blockingCollider.enabled = !open;
            }
        }

        if (open && unlockAudio != null)
            unlockAudio.Play();

        if (gateMotionRoutine != null)
            StopCoroutine(gateMotionRoutine);
        gateMotionRoutine = StartCoroutine(AnimateGate(open));
    }

    private bool HasRequiredKey()
    {
        return GetEquippedItemId() == requiredKeyItemId &&
               viewerInventory != null && viewerInventory.HasItemId(requiredKeyItemId);
    }

    private int GetEquippedItemId()
    {
        if (viewerInventory == null)
            return 0;

        int selectedSlot = viewerInventory.SelectedSlot;
        return selectedSlot >= 0
            ? viewerInventory.GetQuickBarItemIdAt(selectedSlot)
            : 0;
    }

    private void ShowKeyError(string missingKeyMessage)
    {
        int equippedItemId = GetEquippedItemId();
        if (equippedItemId > 0 && equippedItemId != requiredKeyItemId)
        {
            ItemData equippedItem = viewerInventory.GetItemData(equippedItemId);
            string equippedName = equippedItem != null
                ? equippedItem.itemName
                : $"item {equippedItemId}";
            MissionManager.ShowRaw(
                "WRONG KEY EQUIPPED",
                $"{equippedName} cannot open {cellName}. Equip {requiredKeyName}.",
                3f);
            return;
        }

        MissionManager.ShowRaw("PRISON KEY REQUIRED", missingKeyMessage, 3f);
    }

    private bool IsViewerOnOutsideSide()
    {
        return IsPlayerOnOutsideSide(viewerInventory);
    }

    private bool IsPlayerOnOutsideSide(PlayerInventory inventory)
    {
        if (inventory == null || gateToRotate == null)
            return false;

        Vector3 outsideDirection = outsideSidePoint != null
            ? outsideSidePoint.position - gateToRotate.position
            : gateToRotate.forward;
        outsideDirection.y = 0f;
        if (outsideDirection.sqrMagnitude < 0.0001f)
            return false;
        outsideDirection.Normalize();

        Vector3 playerOffset = inventory.transform.root.position - gateToRotate.position;
        playerOffset.y = 0f;
        return Vector3.Dot(playerOffset, outsideDirection) > 0.05f;
    }

    /// <summary>Validates a remote gate request against the authoritative player position.</summary>
    public bool IsValidAuthorityRequest(Transform playerRoot)
    {
        if (playerRoot == null || gateToRotate == null || !enabled || !gameObject.activeInHierarchy)
            return false;

        Vector3 playerOffset = playerRoot.position - gateToRotate.position;
        if (playerOffset.sqrMagnitude > authorityInteractionDistance * authorityInteractionDistance)
            return false;

        Vector3 outsideDirection = outsideSidePoint != null
            ? outsideSidePoint.position - gateToRotate.position
            : gateToRotate.forward;
        outsideDirection.y = 0f;
        playerOffset.y = 0f;
        return outsideDirection.sqrMagnitude > 0.0001f &&
               Vector3.Dot(playerOffset, outsideDirection.normalized) > 0.05f;
    }

    private IEnumerator AnimateGate(bool open)
    {
        float duration = Mathf.Max(0.05f, openDuration);
        float elapsed = 0f;
        Quaternion targetRotation = open ? openRotation : closedRotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            gateToRotate.localRotation = Quaternion.Slerp(
                open ? closedRotation : openRotation,
                targetRotation,
                t);
            yield return null;
        }

        gateToRotate.localRotation = targetRotation;
        gateMotionRoutine = null;
    }
}
