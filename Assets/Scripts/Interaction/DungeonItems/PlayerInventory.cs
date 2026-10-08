using System;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    public const int SlotCount = 8;

    // =========================================================
    // PLAYER VISUAL
    // =========================================================

    [Header("Player Visual")]

    [Tooltip(
        "First-person hold point under the local player's camera. " +
        "Used only by the local player."
    )]
    [SerializeField] private Transform holdPoint;

    [Tooltip(
        "Name of the hold point inside the character visual prefab. " +
        "Example: NetworkHoldPoint"
    )]
    [SerializeField] private string networkHoldPointName = "NetworkHoldPoint";

    // Automatically found inside the dynamically spawned character.
    private Transform networkHoldPoint;

    // The character visual currently containing NetworkHoldPoint.
    private Transform currentCharacterRoot;

    // Local first-person held object.
    private GameObject heldObject;

    // Third-person/network held object.
    private GameObject networkHeldObject;


    // =========================================================
    // DROP
    // =========================================================

    [Header("Drop")]

    [SerializeField] private Transform dropOrigin;

    [SerializeField] private float throwForce = 4f;

    [SerializeField] private float throwUp = 1.5f;


    // =========================================================
    // ITEM DATABASE
    // =========================================================

    [Header("Item Database")]

    [Tooltip("Assign every ItemData used by this game.")]
    [SerializeField] private ItemData[] itemCatalog;


    // =========================================================
    // NETWORKED INVENTORY
    // =========================================================

    // 0 = empty slot
    // > 0 = ItemData.itemId
    [Networked, Capacity(SlotCount)]
    private NetworkArray<int> NetworkSlots => default;

    [Networked]
    public int SelectedSlot { get; private set; }


    // =========================================================
    // CHANGE DETECTION
    // =========================================================

    private int lastInventoryHash = int.MinValue;

    public event Action InventoryChanged;


    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            SelectedSlot = 0;
        }

        // Try to find the character's NetworkHoldPoint.
        FindNetworkHoldPoint();

        // Create the appropriate held visual.
        RefreshHeldVisual();

        // Store current inventory state.
        lastInventoryHash = CalculateInventoryHash();

        InventoryChanged?.Invoke();
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!Object.HasStateAuthority)
            return;

        if (Keyboard.current == null)
            return;

        // -----------------------------------------------------
        // Select slots 1-8
        // -----------------------------------------------------

        for (int i = 0; i < SlotCount; i++)
        {
            Key key = (Key)((int)Key.Digit1 + i);

            if (Keyboard.current[key].wasPressedThisFrame)
            {
                Select(i);
                break;
            }
        }

        // -----------------------------------------------------
        // Drop selected item
        // -----------------------------------------------------

        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            RequestDrop();
        }
    }


    // =========================================================
    // RENDER
    // =========================================================

    public override void Render()
    {
        // -----------------------------------------------------
        // For remote players, make sure the dynamically spawned
        // character has been found.
        // -----------------------------------------------------

        bool networkHoldPointChanged = false;

        if (!Object.HasInputAuthority)
        {
            Transform foundHoldPoint = FindNetworkHoldPoint();

            if (foundHoldPoint != networkHoldPoint)
            {
                networkHoldPointChanged = true;
            }
        }

        // -----------------------------------------------------
        // Check inventory state.
        // -----------------------------------------------------

        int currentHash = CalculateInventoryHash();

        bool inventoryChanged =
            currentHash != lastInventoryHash;

        // -----------------------------------------------------
        // Refresh held visual when:
        //
        // 1. Inventory changes
        // 2. Selected slot changes
        // 3. NetworkHoldPoint becomes available
        // 4. Character visual changes
        // -----------------------------------------------------

        if (inventoryChanged || networkHoldPointChanged)
        {
            lastInventoryHash = currentHash;

            RefreshHeldVisual();

            if (inventoryChanged)
            {
                InventoryChanged?.Invoke();
            }
        }
    }


    // =========================================================
    // NETWORKED INVENTORY
    // =========================================================

    public bool TryAddNetworkedById(int itemId)
    {
        if (!Object.HasStateAuthority)
            return false;

        ItemData item = GetItemData(itemId);

        if (item == null)
        {
            Debug.LogError(
                $"[INVENTORY] Item ID {itemId} was not found."
            );

            return false;
        }

        // -----------------------------------------------------
        // Find first empty slot.
        // -----------------------------------------------------

        for (int i = 0; i < SlotCount; i++)
        {
            if (NetworkSlots.Get(i) == 0)
            {
                NetworkSlots.Set(i, itemId);

                Debug.Log(
                    $"[INVENTORY] " +
                    $"{item.itemName} added to slot {i + 1}."
                );

                return true;
            }
        }

        Debug.Log(
            "[INVENTORY] Inventory is full."
        );

        return false;
    }


    // =========================================================
    // HAS ITEM
    // =========================================================

    public bool Has(ItemData item)
    {
        if (item == null)
            return false;

        return HasItemId(item.itemId);
    }


    public bool HasItemId(int itemId)
    {
        if (itemId <= 0)
            return false;

        for (int i = 0; i < SlotCount; i++)
        {
            if (NetworkSlots.Get(i) == itemId)
                return true;
        }

        return false;
    }


    // =========================================================
    // SELECT
    // =========================================================

    private void Select(int index)
    {
        if (!Object.HasStateAuthority)
            return;

        if (index < 0 || index >= SlotCount)
            return;

        if (SelectedSlot == index)
            return;

        SelectedSlot = index;

        Debug.Log(
            $"[INVENTORY] Selected slot {index + 1}"
        );
    }


    // =========================================================
    // DROP
    // =========================================================

    private void RequestDrop()
    {
        if (!Object.HasStateAuthority)
            return;

        DropAuthoritative(SelectedSlot);
    }


    private void DropAuthoritative(int slotIndex)
    {
        if (!Object.HasStateAuthority)
            return;

        if (slotIndex < 0 || slotIndex >= SlotCount)
            return;

        int itemId = NetworkSlots.Get(slotIndex);

        if (itemId == 0)
            return;

        ItemData item = GetItemData(itemId);

        if (item == null)
        {
            Debug.LogError(
                $"[INVENTORY] Cannot drop unknown item ID {itemId}."
            );

            return;
        }

        if (item.worldPrefab == null)
        {
            Debug.LogError(
                $"[INVENTORY] {item.itemName} " +
                "does not have a NetworkObject worldPrefab."
            );

            return;
        }


        // =====================================================
        // SPAWN POSITION / ROTATION
        // =====================================================

        Transform aim =
            dropOrigin != null
                ? dropOrigin
                : transform;

        Vector3 spawnPosition =
            aim.position +
            aim.forward * 0.8f;

        Quaternion rotation =
            Quaternion.Euler(
                0f,
                aim.eulerAngles.y,
                0f
            );


        // =====================================================
        // SPAWN
        // =====================================================

        NetworkObject droppedObject =
            Runner.Spawn(
                item.worldPrefab,
                spawnPosition,
                rotation
            );

        if (droppedObject == null)
        {
            Debug.LogError(
                "[INVENTORY] Runner.Spawn returned NULL " +
                "for dropped item."
            );

            return;
        }


        // =====================================================
        // ITEM IDENTITY
        // =====================================================

        NetworkedDroppedItem droppedItem =
            droppedObject.GetComponent<NetworkedDroppedItem>();

        if (droppedItem != null)
        {
            droppedItem.Initialize(itemId);
        }


        // =====================================================
        // PHYSICS THROW
        // =====================================================

        DroppedItemPhysics dropPhysics =
            droppedObject.GetComponent<DroppedItemPhysics>();

        if (dropPhysics != null)
        {
            Vector3 velocity =
                aim.forward * throwForce +
                Vector3.up * throwUp;

            dropPhysics.Launch(
                velocity,
                UnityEngine.Random.insideUnitSphere * 3f
            );


            // -------------------------------------------------
            // Ignore collision with player's own colliders.
            // -------------------------------------------------

            Collider[] ownColliders =
                GetComponentsInChildren<Collider>();

            foreach (
                Collider a
                in droppedObject.GetComponentsInChildren<Collider>())
            {
                foreach (Collider b in ownColliders)
                {
                    Physics.IgnoreCollision(a, b);
                }
            }
        }
        else
        {
            Debug.LogWarning(
                "[INVENTORY] Dropped item has no " +
                "DroppedItemPhysics. It will stay in the air."
            );
        }


        // =====================================================
        // REMOVE FROM INVENTORY
        // =====================================================

        NetworkSlots.Set(slotIndex, 0);

        Debug.Log(
            $"[INVENTORY] " +
            $"{item.itemName} dropped from slot {slotIndex + 1}."
        );
    }


    // =========================================================
    // ITEM LOOKUP
    // =========================================================

    public ItemData GetItemData(int itemId)
    {
        if (itemId <= 0)
            return null;

        if (itemCatalog == null)
            return null;

        for (int i = 0; i < itemCatalog.Length; i++)
        {
            ItemData item = itemCatalog[i];

            if (
                item != null &&
                item.itemId == itemId
            )
            {
                return item;
            }
        }

        return null;
    }


    public ItemData GetItemAt(int slotIndex)
    {
        if (
            slotIndex < 0 ||
            slotIndex >= SlotCount
        )
        {
            return null;
        }

        int itemId =
            NetworkSlots.Get(slotIndex);

        return GetItemData(itemId);
    }


    public int GetItemIdAt(int slotIndex)
    {
        if (
            slotIndex < 0 ||
            slotIndex >= SlotCount
        )
        {
            return 0;
        }

        return NetworkSlots.Get(slotIndex);
    }


    public int GetSlotCount()
    {
        return SlotCount;
    }


    // =========================================================
    // FIND NETWORK HOLD POINT
    // =========================================================

    private Transform FindNetworkHoldPoint()
    {
        // -----------------------------------------------------
        // If our cached point is still valid, use it.
        // -----------------------------------------------------

        if (networkHoldPoint != null)
            return networkHoldPoint;


        // -----------------------------------------------------
        // Search the dynamically spawned character.
        //
        // PlayerNetwork creates the character visual under
        // PlayerRoot, so this search works with:
        //
        // Boy1
        // Boy2
        // Girl1
        // Girl2
        //
        // as long as they contain:
        //
        // RightHand
        //     └── NetworkHoldPoint
        // -----------------------------------------------------

        Transform[] children =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child == transform)
                continue;

            if (child.name == networkHoldPointName)
            {
                networkHoldPoint = child;

                // The root of the currently spawned character
                // is the direct child of PlayerRoot containing
                // the NetworkHoldPoint.
                currentCharacterRoot =
                    GetCharacterRoot(networkHoldPoint);

                Debug.Log(
                    $"[INVENTORY] NetworkHoldPoint found: " +
                    $"{GetTransformPath(networkHoldPoint)}"
                );

                return networkHoldPoint;
            }
        }


        // -----------------------------------------------------
        // It is normal for this to happen briefly if
        // PlayerNetwork has not instantiated the character yet.
        // -----------------------------------------------------

        return null;
    }


    // =========================================================
    // GET CHARACTER ROOT
    // =========================================================

    private Transform GetCharacterRoot(Transform hold)
    {
        if (hold == null)
            return null;

        Transform current = hold;

        Transform playerRoot =
            transform;

        Transform previous = current;

        while (
            current != null &&
            current.parent != null &&
            current.parent != playerRoot
        )
        {
            previous = current;
            current = current.parent;
        }

        if (
            current != null &&
            current.parent == playerRoot
        )
        {
            return current;
        }

        return previous;
    }


    // =========================================================
    // HELD VISUAL
    // =========================================================

    private void RefreshHeldVisual()
    {
        // -----------------------------------------------------
        // Remove old local held visual.
        // -----------------------------------------------------

        if (heldObject != null)
        {
            Destroy(heldObject);
            heldObject = null;
        }


        // -----------------------------------------------------
        // Remove old network/third-person held visual.
        // -----------------------------------------------------

        if (networkHeldObject != null)
        {
            Destroy(networkHeldObject);
            networkHeldObject = null;
        }


        // -----------------------------------------------------
        // Get selected item.
        // -----------------------------------------------------

        ItemData item =
            GetItemAt(SelectedSlot);

        if (item == null)
            return;

        if (item.heldPrefab == null)
            return;


        // =====================================================
        // LOCAL PLAYER
        // =====================================================

        if (Object.HasInputAuthority)
        {
            CreateFirstPersonHeldVisual(item);

            return;
        }


        // =====================================================
        // REMOTE PLAYER
        // =====================================================

        CreateNetworkHeldVisual(item);
    }


    // =========================================================
    // FIRST PERSON HELD VISUAL
    // =========================================================

    private void CreateFirstPersonHeldVisual(ItemData item)
    {
        if (holdPoint == null)
        {
            Debug.LogWarning(
                "[INVENTORY] First-person holdPoint is not assigned."
            );

            return;
        }

        if (item == null || item.heldPrefab == null)
            return;


        // -----------------------------------------------------
        // Create item under camera hold point.
        // -----------------------------------------------------

        heldObject =
            Instantiate(
                item.heldPrefab,
                holdPoint
            );

        heldObject.transform.localPosition =
            Vector3.zero;

        heldObject.transform.localRotation =
            Quaternion.identity;

        heldObject.transform.localScale =
            Vector3.one;


        // -----------------------------------------------------
        // Remove gameplay components from held visual.
        // -----------------------------------------------------

        CleanHeldVisual(heldObject);
    }


    // =========================================================
    // NETWORK / THIRD PERSON HELD VISUAL
    // =========================================================

    private void CreateNetworkHeldVisual(ItemData item)
    {
        // -----------------------------------------------------
        // Find the NetworkHoldPoint if necessary.
        // -----------------------------------------------------

        if (networkHoldPoint == null)
        {
            FindNetworkHoldPoint();
        }

        if (networkHoldPoint == null)
        {
            Debug.LogWarning(
                "[INVENTORY] Could not find NetworkHoldPoint " +
                $"for player {Object.Id}."
            );

            return;
        }

        if (item == null || item.heldPrefab == null)
            return;


        // -----------------------------------------------------
        // Create item under character's hand.
        // -----------------------------------------------------

        networkHeldObject =
            Instantiate(
                item.heldPrefab,
                networkHoldPoint
            );

        networkHeldObject.transform.localPosition =
            Vector3.zero;

        networkHeldObject.transform.localRotation =
            Quaternion.identity;

        networkHeldObject.transform.localScale =
            Vector3.one;


        // -----------------------------------------------------
        // Remove gameplay components.
        // -----------------------------------------------------

        CleanHeldVisual(networkHeldObject);
    }


    // =========================================================
    // CLEAN HELD VISUAL
    // =========================================================

    private void CleanHeldVisual(GameObject visual)
    {
        if (visual == null)
            return;


        // -----------------------------------------------------
        // Remove dropped-item physics.
        // -----------------------------------------------------

        foreach (
            DroppedItemPhysics physics
            in visual.GetComponentsInChildren<DroppedItemPhysics>(true)
        )
        {
            Destroy(physics);
        }


        // -----------------------------------------------------
        // Remove rigidbodies.
        // -----------------------------------------------------

        foreach (
            Rigidbody body
            in visual.GetComponentsInChildren<Rigidbody>(true)
        )
        {
            Destroy(body);
        }


        // -----------------------------------------------------
        // Remove colliders.
        // -----------------------------------------------------

        foreach (
            Collider collider
            in visual.GetComponentsInChildren<Collider>(true)
        )
        {
            Destroy(collider);
        }


        // -----------------------------------------------------
        // Remove pickup interaction.
        // -----------------------------------------------------

        foreach (
            KeyPickup pickup
            in visual.GetComponentsInChildren<KeyPickup>(true)
        )
        {
            Destroy(pickup);
        }
    }


    // =========================================================
    // INVENTORY HASH
    // =========================================================

    private int CalculateInventoryHash()
    {
        unchecked
        {
            int hash =
                SelectedSlot;

            for (int i = 0; i < SlotCount; i++)
            {
                hash =
                    hash * 31 +
                    NetworkSlots.Get(i);
            }

            return hash;
        }
    }


    // =========================================================
    // DEBUG / TRANSFORM PATH
    // =========================================================

    private string GetTransformPath(Transform target)
    {
        if (target == null)
            return "(null)";

        string path =
            target.name;

        Transform current =
            target.parent;

        while (
            current != null &&
            current != transform
        )
        {
            path =
                current.name +
                "/" +
                path;

            current =
                current.parent;
        }

        return path;
    }
}