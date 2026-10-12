using System.Collections;
using UnityEngine;

/// <summary>
/// Hold-to-interact gate lock placed at the dungeon entrance
/// (the DungeonEntryZone object).
///
/// It is added at runtime by NetworkGameManager so no scene wiring is
/// required. The player must equip the mission KEY, then hold
/// [E] until the slider fills. On completion it asks NetworkGameManager
/// (state authority) to unlock the gate, which then loads the Dungeon scene.
///
/// A small solid collider is generated in front of the gate so the existing
/// PlayerInteractor raycast (layer 8) can target this component.
/// </summary>
public class DungeonGateUnlock : MonoBehaviour, IHoldInteractable
{
    // =========================================================
    // INTERACTION
    // =========================================================

    [Header("Interaction")]
    [Tooltip("Seconds the player must hold [E] to unlock the gate.")]
    [SerializeField, Min(0.1f)] private float holdDuration = 3f;

    [Tooltip("Prompt shown while the player has the key equipped.")]
    [SerializeField] private string unlockPrompt =
        "[E] HOLD TO UNLOCK THE GATE";

    [Tooltip("Prompt shown while the player does NOT have the key equipped.")]
    [SerializeField] private string missingKeyPrompt =
        "EQUIP THE DUNGEON KEY TO UNLOCK THE GATE";

    // =========================================================
    // KEY REQUIREMENT
    // =========================================================

    [Header("Key Requirement")]
    [Tooltip("ItemData asset of the key required to unlock the gate.")]
    [SerializeField] private ItemData keyItemData;

    [Tooltip("Fallback ItemData id used when the key asset is not assigned (103 = Dungeon Key).")]
    [SerializeField, Min(1)] private int fallbackKeyItemId = 103;

    // =========================================================
    // INTERACTION COLLIDER
    // =========================================================

    [Header("Interaction Collider")]
    [Tooltip("Unscaled offset from this object's position, in world units.")]
    [SerializeField] private Vector3 colliderLocalPosition =
        new Vector3(0f, 1.2f, 1.1f);

    [Tooltip("World-space size of the generated interaction box.")]
    [SerializeField] private Vector3 colliderSize =
        new Vector3(2.4f, 2.8f, 1.4f);

    [Tooltip("Layer of the generated interaction box. Must match the PlayerInteractor interaction layer (8).")]
    [SerializeField] private int interactionLayer = 8;

    // =========================================================
    // GATE FEEDBACK
    // =========================================================

    [Header("Gate Rotation")]
    [Tooltip("The door leaf or hinge pivot that should rotate open.")]
    [SerializeField] private Transform doorToRotate;

    [Tooltip("Local Euler rotation added to the door's starting rotation when it opens.")]
    [SerializeField] private Vector3 openRotationOffset = new Vector3(0f, 90f, 0f);

    [Tooltip("Seconds taken to rotate the door open.")]
    [SerializeField, Min(0.05f)] private float doorOpenDuration = 1.25f;

    [Tooltip("Optional sound played when the gate unlocks.")]
    [SerializeField] private AudioSource unlockAudio;

    // =========================================================
    // INTERNAL
    // =========================================================

    private PlayerInventory viewerInventory;

    private bool interactionZoneBuilt;
    private bool gateUnlocked;
    private Quaternion closedLocalRotation;
    private Quaternion openLocalRotation;
    private bool doorRotationInitialized;
    private Collider lockTriggerCollider;

    // =========================================================
    // IInteractable
    // =========================================================

    public string Prompt
    {
        get
        {
            if (gateUnlocked)
            {
                return string.Empty;
            }

            if (HasKeyEquipped())
            {
                return unlockPrompt;
            }

            ItemData key = GetResolvedKeyItem();

            return key != null
                ? $"EQUIP THE {key.itemName.ToUpperInvariant()} TO UNLOCK THE GATE"
                : missingKeyPrompt;
        }
    }

    public bool CanInteract => enabled && !gateUnlocked;

    public int Priority => 50;

    /// <summary>True when the local player's root is inside the Fort2 lock trigger.</summary>
    public bool IsPlayerInsideLockTrigger(Transform playerRoot)
    {
        if (playerRoot == null)
            return false;

        if (lockTriggerCollider == null)
        {
            GameObject lockTrigger = GameObject.Find("Locktrigger");
            if (lockTrigger != null)
                lockTriggerCollider = lockTrigger.GetComponent<Collider>();
        }

        if (lockTriggerCollider == null || !lockTriggerCollider.enabled)
            return false;

        Bounds triggerBounds = lockTriggerCollider.bounds;
        Collider[] playerColliders = playerRoot.GetComponentsInChildren<Collider>();
        foreach (Collider playerCollider in playerColliders)
        {
            if (playerCollider != null && playerCollider.enabled &&
                triggerBounds.Intersects(playerCollider.bounds))
                return true;
        }

        return triggerBounds.Contains(playerRoot.position);
    }

    public void Interact(PlayerInventory inv)
    {
        // Hold interactions are driven by PlayerInteractor via
        // IHoldInteractable. This method intentionally does nothing.
    }

    // =========================================================
    // IHoldInteractable
    // =========================================================

    public float HoldDuration => holdDuration;

    public void UpdateContext(PlayerInventory inv)
    {
        viewerInventory = inv;
    }

    public void HoldStarted(PlayerInventory inv)
    {
        viewerInventory = inv;

        if (gateUnlocked)
        {
            return;
        }

        if (!HasKeyEquipped())
        {
            Debug.Log(
                "[GATE] Player started holding but the key is not equipped."
            );
        }
    }

    public void HoldCancelled()
    {
    }

    public void HoldCompleted(PlayerInventory inv)
    {
        viewerInventory = inv;

        if (gateUnlocked)
        {
            return;
        }

        if (!HasKeyEquipped())
        {
            Debug.Log(
                "[GATE] Cannot unlock: the key is not equipped."
            );

            ItemData key = GetResolvedKeyItem();

            string keyName =
                key != null
                    ? key.itemName
                    : "KEY";

            MissionManager.ShowRaw(
                "KEY REQUIRED",
                $"Equip the {keyName} in your hotbar, then hold [E] at the " +
                "gate to unlock the dungeon.",
                4f
            );

            return;
        }

        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning(
                "[GATE] NetworkGameManager is missing."
            );

            return;
        }

        Debug.Log(
            "[GATE] Player held the key on the gate. " +
            "Requesting unlock."
        );

        manager.RequestDungeonGateUnlock();
    }

    // =========================================================
    // CONFIGURATION
    // =========================================================

    /// <summary>
    /// Keeps the required key in sync with NetworkGameManager.
    /// </summary>
    public void SetKeyItemData(ItemData keyData)
    {
        keyItemData = keyData;
    }

    private ItemData GetResolvedKeyItem()
    {
        if (keyItemData != null)
        {
            return keyItemData;
        }

        if (viewerInventory != null)
        {
            return viewerInventory.GetItemData(fallbackKeyItemId);
        }

        return null;
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        BuildInteractionZone();
        InitializeDoorRotation();
        GameObject lockTrigger = GameObject.Find("Locktrigger");
        if (lockTrigger != null)
            lockTriggerCollider = lockTrigger.GetComponent<Collider>();
    }

    private void Update()
    {
        if (gateUnlocked)
        {
            return;
        }

        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager == null ||
            !manager.IsNetworkStateReady)
        {
            return;
        }

        if (manager.DungeonGateUnlocked)
        {
            OnGateUnlocked();
        }
    }

    // =========================================================
    // GATE UNLOCKED FEEDBACK
    // =========================================================

    private void OnGateUnlocked()
    {
        gateUnlocked = true;

        Debug.Log(
            "[GATE] Gate unlocked locally. Playing feedback."
        );

        if (!doorRotationInitialized)
            InitializeDoorRotation();

        if (doorToRotate != null)
            StartCoroutine(RotateDoorOpen());
        else
            Debug.LogWarning("[GATE] Door To Rotate is not assigned; gate unlocked without door movement.", this);

        if (unlockAudio != null)
        {
            unlockAudio.Play();
        }
    }

    private void InitializeDoorRotation()
    {
        if (doorToRotate == null)
            return;

        closedLocalRotation = doorToRotate.localRotation;
        openLocalRotation = closedLocalRotation * Quaternion.Euler(openRotationOffset);
        doorRotationInitialized = true;
    }

    private IEnumerator RotateDoorOpen()
    {
        Quaternion start = doorToRotate.localRotation;
        float duration = Mathf.Max(0.05f, doorOpenDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            // Smooth ease-in/ease-out motion.
            progress = progress * progress * (3f - 2f * progress);
            doorToRotate.localRotation = Quaternion.Slerp(start, openLocalRotation, progress);
            yield return null;
        }

        doorToRotate.localRotation = openLocalRotation;
    }

    // =========================================================
    // INTERACTION COLLIDER
    // =========================================================

    private void BuildInteractionZone()
    {
        if (interactionZoneBuilt)
        {
            return;
        }

        interactionZoneBuilt = true;

        // If this object (or a child) already has a SOLID collider,
        // the raycast can already hit it - nothing else to do.
        Collider existing =
            GetComponentInChildren<Collider>();

        if (existing != null &&
            !existing.isTrigger)
        {
            return;
        }

        GameObject lockTrigger = GameObject.Find("Locktrigger");
        Collider lockTriggerCollider = lockTrigger != null
            ? lockTrigger.GetComponent<Collider>()
            : null;

        GameObject zone = new GameObject("DungeonGateUnlockZone");

        // Keep the collider beneath this interactable so GetComponentInParent
        // can resolve DungeonGateUnlock, while cancelling the large scale on
        // DungeonEntryZone so its offset and size remain in world units.
        zone.transform.SetParent(transform, false);
        Vector3 inheritedScale = transform.lossyScale;
        Vector3 worldCenter = lockTriggerCollider != null
            ? lockTriggerCollider.bounds.center
            : transform.position + transform.rotation * colliderLocalPosition;
        Vector3 worldSize = lockTriggerCollider != null
            ? lockTriggerCollider.bounds.size
            : colliderSize;

        zone.transform.localPosition = transform.InverseTransformPoint(worldCenter);
        zone.transform.localRotation = Quaternion.Inverse(transform.rotation);
        zone.transform.localScale = DivideByScale(Vector3.one, inheritedScale);
        zone.layer = interactionLayer;

        BoxCollider box =
            zone.AddComponent<BoxCollider>();

        box.isTrigger = false;
        box.size = worldSize;

        Debug.Log(
            lockTriggerCollider != null
                ? "[GATE] Created a solid interaction target over Locktrigger."
                : "[GATE] Generated a solid interaction target in front of the gate."
        );
    }

    private static Vector3 DivideByScale(Vector3 value, Vector3 scale)
    {
        return new Vector3(
            Mathf.Abs(scale.x) > 0.0001f ? value.x / scale.x : value.x,
            Mathf.Abs(scale.y) > 0.0001f ? value.y / scale.y : value.y,
            Mathf.Abs(scale.z) > 0.0001f ? value.z / scale.z : value.z
        );
    }

    // =========================================================
    // KEY CHECK
    // =========================================================

    private bool HasKeyEquipped()
    {
        if (viewerInventory == null)
        {
            return false;
        }

        ItemData key = GetResolvedKeyItem();

        if (key == null || !viewerInventory.HasItemId(key.itemId))
            return false;

        int selectedSlot = viewerInventory.SelectedSlot;
        return selectedSlot >= 0 &&
               viewerInventory.GetQuickBarItemIdAt(selectedSlot) == key.itemId;
    }
}
