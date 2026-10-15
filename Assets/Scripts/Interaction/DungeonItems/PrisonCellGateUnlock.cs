using System.Collections;
using UnityEngine;

/// <summary>Hold-to-unlock interaction for one dungeon prison cell.</summary>
public sealed class PrisonCellGateUnlock : MonoBehaviour, IHoldInteractable
{
    [Header("Cell Key")]
    [SerializeField, Range(1, 2)] private int cellNumber = 1;
    [SerializeField, Min(1)] private int requiredKeyItemId = 102;
    [SerializeField] private string requiredKeyName = "PRISON KEY 1";
    [SerializeField] private string cellName = "CELL 1";

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float holdDuration = 2f;
    [SerializeField] private string unlockPrompt = "HOLD [E] TO UNLOCK";

    [Header("Approach Side")]
    [Tooltip("Place this empty transform in the corridor/outside the cell. The gate can only be unlocked from this side.")]
    [SerializeField] private Transform outsideSidePoint;

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
    private Coroutine openingRoutine;

    public string Prompt
    {
        get
        {
            if (unlocked || !IsViewerOnOutsideSide())
                return string.Empty;

            return HasRequiredKey()
                ? $"{unlockPrompt} WITH {requiredKeyName} ({cellName})"
                : $"EQUIP {requiredKeyName}, THEN HOLD [E] ({cellName})";
        }
    }

    // Don't expose this gate as an interaction target from inside the cell.
    // This also prevents PlayerInteractor from starting its hold-progress UI.
    public bool CanInteract => enabled && !unlocked && !unlockPending && IsViewerOnOutsideSide();
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
        if (unlocked)
            return;

        NetworkGameManager manager = NetworkGameManager.Instance;
        if (manager == null || !manager.IsNetworkStateReady)
            return;

        bool networkUnlocked = cellNumber == 1
            ? manager.PrisonCell1Unlocked
            : manager.PrisonCell2Unlocked;

        if (networkUnlocked)
            UnlockGate();
    }

    public void UpdateContext(PlayerInventory inventory) => viewerInventory = inventory;
    public void HoldStarted(PlayerInventory inventory) => viewerInventory = inventory;
    public void HoldCancelled() { }

    public void Interact(PlayerInventory inventory)
    {
        // Hold interactions are completed through IHoldInteractable.
        viewerInventory = inventory;
    }

    public void HoldCompleted(PlayerInventory inventory)
    {
        viewerInventory = inventory;

        if (unlocked)
            return;

        if (!IsPlayerOnOutsideSide(inventory))
        {
            MissionManager.ShowRaw(
                "LOCKED FROM INSIDE",
                $"{cellName} can only be unlocked from outside the cell.",
                3f);
            return;
        }

        if (!HasRequiredKey())
        {
            MissionManager.ShowRaw(
                "PRISON KEY REQUIRED",
                $"Find and equip {requiredKeyName} to unlock {cellName}.",
                3f);
            return;
        }

        NetworkGameManager manager = NetworkGameManager.Instance;
        if (manager != null && manager.IsNetworkStateReady)
        {
            unlockPending = true;
            manager.RequestPrisonCellUnlock(cellNumber, requiredKeyItemId);
            return;
        }

        UnlockGate();
    }

    private void UnlockGate()
    {
        if (unlocked)
            return;

        unlockPending = false;
        unlocked = true;

        if (blockingColliders != null)
        {
            foreach (Collider blockingCollider in blockingColliders)
            {
                if (blockingCollider != null)
                    blockingCollider.enabled = false;
            }
        }

        if (unlockAudio != null)
            unlockAudio.Play();

        if (openingRoutine != null)
            StopCoroutine(openingRoutine);
        openingRoutine = StartCoroutine(OpenGate());
    }

    private bool HasRequiredKey()
    {
        if (viewerInventory == null || !viewerInventory.HasItemId(requiredKeyItemId))
            return false;

        int selectedSlot = viewerInventory.SelectedSlot;
        return selectedSlot >= 0 &&
               viewerInventory.GetQuickBarItemIdAt(selectedSlot) == requiredKeyItemId;
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

    private IEnumerator OpenGate()
    {
        float duration = Mathf.Max(0.05f, openDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            gateToRotate.localRotation = Quaternion.Slerp(closedRotation, openRotation, t);
            yield return null;
        }

        gateToRotate.localRotation = openRotation;
        openingRoutine = null;
    }
}
