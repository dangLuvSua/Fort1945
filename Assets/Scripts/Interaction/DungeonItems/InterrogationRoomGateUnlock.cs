using System.Collections;
using Fusion;
using UnityEngine;

/// <summary>Hold-to-unlock interaction for the interrogation room gate.</summary>
public sealed class InterrogationRoomGateUnlock : MonoBehaviour, IHoldInteractable, IQuickTapHoldInteractable
{
    [Header("Interrogation Key")]
    [SerializeField, Min(1)] private int requiredKeyItemId = 105;
    [SerializeField] private string requiredKeyName = "INTERROGATION KEY";
    [SerializeField] private string roomName = "INTERROGATION ROOM";

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float holdDuration = 2f;
    [SerializeField] private string unlockPrompt = "[E] HOLD TO UNLOCK";
    [SerializeField] private string closePrompt = "[E] CLOSE";
    [SerializeField] private string reopenPrompt = "[E] HOLD TO OPEN";
    [SerializeField] private string exitPrompt = "[E] HOLD TO EXIT";

    [Header("Approach Side")]
    [Tooltip("Place this empty transform on the side from which the gate should be operated.")]
    [SerializeField] private Transform outsideSidePoint;

    [Header("Gate")]
    [Tooltip("Assign the gate leaf/hinge transform. If empty, this component's transform is used.")]
    [SerializeField] private Transform gateToRotate;
    [SerializeField] private Vector3 openRotationOffset = new Vector3(0f, 90f, 0f);
    [SerializeField, Min(0.05f)] private float openDuration = 1.25f;
    [Tooltip("Optional colliders to disable while the gate is open.")]
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

    public string Prompt
    {
        get
        {
            if (gateOpen) return closePrompt;
            if (IsViewerOnInsideSide()) return exitPrompt;
            if (unlocked)
            {
                return HasRequiredKey()
                    ? reopenPrompt
                    : $"EQUIP {requiredKeyName}, THEN HOLD [E] TO OPEN";
            }
            return HasRequiredKey()
                ? unlockPrompt
                : $"EQUIP {requiredKeyName}, THEN HOLD [E] TO UNLOCK";
        }
    }

    public bool CanInteract => enabled && !unlockPending && !gateTogglePending;
    public bool CanTapInteract => gateOpen && !gateTogglePending;
    public int Priority => 40;
    public float HoldDuration => holdDuration;

    private void Awake()
    {
        if (gateToRotate == null) gateToRotate = transform;
        closedRotation = gateToRotate.localRotation;
        openRotation = closedRotation * Quaternion.Euler(openRotationOffset);
    }

    private void Update()
    {
        NetworkGameManager manager = NetworkGameManager.ResolveInstance();
        if (manager == null || !manager.IsNetworkStateReady) return;

        unlocked = manager.InterrogationRoomUnlocked;
        bool networkGateOpen = manager.InterrogationRoomGateOpen;
        if (networkGateOpen != gateOpen) SetGateOpen(networkGateOpen);
    }

    public void UpdateContext(PlayerInventory inventory) => viewerInventory = inventory;
    public void HoldStarted(PlayerInventory inventory) => viewerInventory = inventory;
    public void HoldCancelled() { }
    public void Interact(PlayerInventory inventory) => viewerInventory = inventory;

    public void TapInteract(PlayerInventory inventory)
    {
        viewerInventory = inventory;
        if (CanTapInteract) CloseGate();
    }

    public void HoldCompleted(PlayerInventory inventory)
    {
        viewerInventory = inventory;
        if (gateOpen)
        {
            CloseGate();
            return;
        }

        bool playerInside = inventory != null && IsPlayerOnInsideSide(inventory.transform.root);
        if (playerInside)
        {
            RequestGateOpenState(true);
            return;
        }

        // From the outside, the key is required every time the gate is opened.
        if (!HasRequiredKey())
        {
            MissionManager.ShowRaw("INTERROGATION KEY REQUIRED", $"Equip {requiredKeyName} to open the gate from outside.", 3f);
            return;
        }

        if (unlocked)
        {
            RequestGateOpenState(true);
            return;
        }

        NetworkGameManager manager = NetworkGameManager.ResolveInstance();
        if (manager != null)
        {
            if (!manager.IsNetworkStateReady)
            {
                ShowNetworkSyncMessage();
                return;
            }

            unlockPending = true;
            manager.RequestInterrogationRoomUnlock(requiredKeyItemId);
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

    public void CloseGate()
    {
        if (gateOpen) RequestGateOpenState(false);
    }

    private void RequestGateOpenState(bool open)
    {
        NetworkGameManager manager = NetworkGameManager.ResolveInstance();
        if (manager != null)
        {
            if (!manager.IsNetworkStateReady)
            {
                ShowNetworkSyncMessage();
                return;
            }

            gateTogglePending = true;
            manager.RequestInterrogationRoomGateState(open);
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

    private void SetGateOpen(bool open)
    {
        if (gateToRotate == null || (gateOpen == open && gateMotionRoutine == null)) return;
        gateOpen = open;
        unlockPending = false;
        gateTogglePending = false;

        if (blockingColliders != null)
        {
            foreach (Collider blockingCollider in blockingColliders)
                if (blockingCollider != null) blockingCollider.enabled = !open;
        }
        if (open && unlockAudio != null) unlockAudio.Play();
        if (gateMotionRoutine != null) StopCoroutine(gateMotionRoutine);
        gateMotionRoutine = StartCoroutine(AnimateGate(open));
    }

    private bool HasRequiredKey()
    {
        if (viewerInventory == null || !viewerInventory.HasItemId(requiredKeyItemId)) return false;
        int selectedSlot = viewerInventory.SelectedSlot;
        return selectedSlot >= 0 && viewerInventory.GetQuickBarItemIdAt(selectedSlot) == requiredKeyItemId;
    }

    private bool IsViewerOnOutsideSide() => IsPlayerOnOutsideSide(viewerInventory);
    private bool IsViewerOnInsideSide() => viewerInventory != null && IsPlayerOnInsideSide(viewerInventory.transform.root);

    /// <summary>Used by NetworkGameManager to validate a no-key exit request.</summary>
    public bool IsPlayerOnInsideSide(Transform playerRoot)
    {
        if (playerRoot == null || gateToRotate == null)
            return false;

        Vector3 sideDirection = outsideSidePoint != null
            ? outsideSidePoint.position - gateToRotate.position
            : gateToRotate.forward;
        sideDirection.y = 0f;
        if (sideDirection.sqrMagnitude < 0.0001f)
            return false;
        sideDirection.Normalize();

        Vector3 playerOffset = playerRoot.position - gateToRotate.position;
        playerOffset.y = 0f;
        return Vector3.Dot(playerOffset, sideDirection) < -0.05f;
    }

    private bool IsPlayerOnOutsideSide(PlayerInventory inventory)
    {
        if (inventory == null || gateToRotate == null) return false;
        Vector3 sideDirection = outsideSidePoint != null
            ? outsideSidePoint.position - gateToRotate.position
            : gateToRotate.forward;
        sideDirection.y = 0f;
        if (sideDirection.sqrMagnitude < 0.0001f) return false;
        sideDirection.Normalize();
        Vector3 playerOffset = inventory.transform.root.position - gateToRotate.position;
        playerOffset.y = 0f;
        return Vector3.Dot(playerOffset, sideDirection) > 0.05f;
    }

    private IEnumerator AnimateGate(bool open)
    {
        float duration = Mathf.Max(0.05f, openDuration);
        float elapsed = 0f;
        Quaternion targetRotation = open ? openRotation : closedRotation;
        Quaternion startRotation = gateToRotate.localRotation;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            gateToRotate.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }
        gateToRotate.localRotation = targetRotation;
        gateMotionRoutine = null;
    }
}
