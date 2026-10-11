using UnityEngine;

/// <summary>
/// Hold-to-interact gate lock placed at the dungeon entrance
/// (the DungeonEntryZone object).
///
/// It is added at runtime by NetworkGameManager so no scene wiring is
/// required. The player must have the mission KEY equipped, then hold
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
    [Tooltip("Local position of the generated interaction box relative to this object.")]
    [SerializeField] private Vector3 colliderLocalPosition =
        new Vector3(0f, 1.2f, 1.1f);

    [Tooltip("Size of the generated interaction box (local units).")]
    [SerializeField] private Vector3 colliderSize =
        new Vector3(2.4f, 2.8f, 1.4f);

    [Tooltip("Layer of the generated interaction box. Must match the PlayerInteractor interaction layer (8).")]
    [SerializeField] private int interactionLayer = 8;

    // =========================================================
    // GATE FEEDBACK
    // =========================================================

    [Header("Gate Feedback")]
    [Tooltip("Optional animator played when the gate unlocks.")]
    [SerializeField] private Animator gateAnimator;

    [Tooltip("Optional Animator trigger name for the unlock animation.")]
    [SerializeField] private string openTriggerName = "Open";

    [Tooltip("Optional sound played when the gate unlocks.")]
    [SerializeField] private AudioSource unlockAudio;

    // =========================================================
    // INTERNAL
    // =========================================================

    private PlayerInventory viewerInventory;

    private bool interactionZoneBuilt;
    private bool gateUnlocked;

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

    public bool CanInteract => !gateUnlocked;

    public int Priority => 50;

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
                $"Equip the {keyName} " +
                "(select it in your hotbar), then hold [E] at the " +
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

    public void Configure(
        Animator animator,
        string openTrigger,
        AudioSource audio)
    {
        gateAnimator = animator;
        openTriggerName = openTrigger;
        unlockAudio = audio;
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        BuildInteractionZone();
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

        if (gateAnimator != null)
        {
            gateAnimator.SetTrigger(openTriggerName);
        }

        if (unlockAudio != null)
        {
            unlockAudio.Play();
        }
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

        GameObject zone =
            new GameObject("DungeonGateUnlockZone");

        zone.transform.SetParent(transform, false);
        zone.transform.localPosition = colliderLocalPosition;
        zone.transform.localRotation = Quaternion.identity;
        zone.transform.localScale = Vector3.one;
        zone.layer = interactionLayer;

        BoxCollider box =
            zone.AddComponent<BoxCollider>();

        box.isTrigger = false;
        box.size = colliderSize;

        Debug.Log(
            "[GATE] Generated an interaction collider " +
            "in front of the dungeon gate."
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

        if (key == null ||
            !viewerInventory.HasItemId(key.itemId))
        {
            return false;
        }

        int selectedSlot =
            viewerInventory.SelectedSlot;

        return selectedSlot >= 0 &&
               viewerInventory.GetQuickBarItemIdAt(selectedSlot) ==
                   key.itemId;
    }
}