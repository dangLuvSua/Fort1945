using System;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    public const int SlotCount = 8;

    [Header("Player Visual")]
    [SerializeField] private Transform holdPoint;

    [Header("Drop")]
    [SerializeField] private Transform dropOrigin;
    [SerializeField] private float throwForce = 4f;
    [SerializeField] private float throwUp = 1.5f;

    [Header("Item Database")]
    [Tooltip("Assign every ItemData used by this game.")]
    [SerializeField] private ItemData[] itemCatalog;

    // 0 = empty slot
    // > 0 = ItemData.itemId
    [Networked, Capacity(SlotCount)]
    private NetworkArray<int> NetworkSlots => default;

    [Networked]
    public int SelectedSlot { get; private set; }

    private GameObject heldObject;

    private int lastInventoryHash = int.MinValue;

    public event Action InventoryChanged;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            SelectedSlot = 0;
        }

        RefreshHeldVisual();
        lastInventoryHash = CalculateInventoryHash();

        InventoryChanged?.Invoke();
    }

    private void Update()
    {
        if (!Object.HasStateAuthority)
            return;

        if (Keyboard.current == null)
            return;

        // Select slots 1-8
        for (int i = 0; i < SlotCount; i++)
        {
            Key key = (Key)((int)Key.Digit1 + i);

            if (Keyboard.current[key].wasPressedThisFrame)
            {
                Select(i);
                break;
            }
        }

        // Drop selected item
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            RequestDrop();
        }
    }

    public override void Render()
    {
        int currentHash = CalculateInventoryHash();

        if (currentHash != lastInventoryHash)
        {
            lastInventoryHash = currentHash;

            RefreshHeldVisual();

            InventoryChanged?.Invoke();
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

        // -----------------------------------------------------
        // Spawn position / rotation
        // -----------------------------------------------------

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

        // -----------------------------------------------------
        // Spawn
        // -----------------------------------------------------

        NetworkObject droppedObject =
            Runner.Spawn(
                item.worldPrefab,
                spawnPosition,
                rotation
            );

        if (droppedObject == null)
        {
            Debug.LogError(
                "[INVENTORY] Runner.Spawn returned NULL for dropped item."
            );

            return;
        }

        // -----------------------------------------------------
        // Item identity
        // -----------------------------------------------------

        NetworkedDroppedItem droppedItem =
            droppedObject.GetComponent<NetworkedDroppedItem>();

        if (droppedItem != null)
        {
            droppedItem.Initialize(itemId);
        }

        // -----------------------------------------------------
        // Physics throw
        // -----------------------------------------------------

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

            // Para hindi tumama sa sarili mong collider
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
                "[INVENTORY] Dropped item has no DroppedItemPhysics. " +
                "It will stay in the air."
            );
        }

        // -----------------------------------------------------
        // Remove from inventory
        // -----------------------------------------------------

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

            if (item != null &&
                item.itemId == itemId)
            {
                return item;
            }
        }

        return null;
    }

    public ItemData GetItemAt(int slotIndex)
    {
        if (slotIndex < 0 ||
            slotIndex >= SlotCount)
        {
            return null;
        }

        int itemId =
            NetworkSlots.Get(slotIndex);

        return GetItemData(itemId);
    }

    public int GetItemIdAt(int slotIndex)
    {
        if (slotIndex < 0 ||
            slotIndex >= SlotCount)
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
    // HELD VISUAL
    // =========================================================

    private void RefreshHeldVisual()
    {
        if (heldObject != null)
        {
            Destroy(heldObject);
            heldObject = null;
        }

        if (holdPoint == null)
            return;

        ItemData item =
            GetItemAt(SelectedSlot);

        if (item == null)
            return;

        if (item.heldPrefab == null)
            return;

        heldObject =
            Instantiate(
                item.heldPrefab,
                holdPoint
            );

        heldObject.transform.localPosition =
            Vector3.zero;

        heldObject.transform.localRotation =
            Quaternion.identity;

        // Held visual only: walang physics, collider, o pickup.

        foreach (
            DroppedItemPhysics physics
            in heldObject.GetComponentsInChildren<DroppedItemPhysics>())
        {
            Destroy(physics);
        }

        foreach (
            Rigidbody body
            in heldObject.GetComponentsInChildren<Rigidbody>())
        {
            Destroy(body);
        }

        foreach (
            Collider collider
            in heldObject.GetComponentsInChildren<Collider>())
        {
            Destroy(collider);
        }

        foreach (
            KeyPickup pickup
            in heldObject.GetComponentsInChildren<KeyPickup>())
        {
            Destroy(pickup);
        }
    }

    // =========================================================
    // CHANGE DETECTION
    // =========================================================

    private int CalculateInventoryHash()
    {
        unchecked
        {
            int hash = SelectedSlot;

            for (int i = 0; i < SlotCount; i++)
            {
                hash =
                    hash * 31 +
                    NetworkSlots.Get(i);
            }

            return hash;
        }
    }
}