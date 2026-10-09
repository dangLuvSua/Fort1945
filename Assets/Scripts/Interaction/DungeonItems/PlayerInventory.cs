
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
    [SerializeField] private Transform holdPoint;
    [SerializeField] private string networkHoldPointName = "NetworkHoldPoint";

    private Transform networkHoldPoint;
    private Transform currentCharacterRoot;
    private GameObject heldObject;
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
    [SerializeField] private ItemData[] itemCatalog;

    // =========================================================
    // NETWORKED INVENTORY
    // =========================================================

    // 0 = empty; positive values = ItemData.itemId
    [Networked, Capacity(SlotCount)]
    private NetworkArray<int> NetworkSlots => default;

    [Networked]
    public int SelectedSlot { get; private set; }

    // Candle life is stored per inventory slot.
    [Networked, Capacity(SlotCount)]
    private NetworkArray<float> CandleLifeBySlot => default;

    [Networked, Capacity(SlotCount)]
    private NetworkArray<float> CandleMaxLifeBySlot => default;

    [Networked, Capacity(SlotCount)]
    private NetworkArray<NetworkBool> CandleInitializedBySlot => default;

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

        FindNetworkHoldPoint();
        RefreshHeldVisual();

        lastInventoryHash = CalculateInventoryHash();
        InventoryChanged?.Invoke();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (Keyboard.current == null)
            return;

        // Select inventory slots 1-8.
        for (int i = 0; i < SlotCount; i++)
        {
            Key key = (Key)((int)Key.Digit1 + i);

            if (Keyboard.current[key].wasPressedThisFrame)
            {
                Select(i);
                break;
            }
        }

        // Drop the selected item.
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
        if (Object == null)
            return;

        bool holdPointWasMissing = networkHoldPoint == null;

        if (!Object.HasInputAuthority)
        {
            FindNetworkHoldPoint();
        }

        bool networkHoldPointChanged =
            !Object.HasInputAuthority &&
            holdPointWasMissing &&
            networkHoldPoint != null;

        int currentHash = CalculateInventoryHash();

        bool inventoryChanged = currentHash != lastInventoryHash;

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
    // ADD ITEMS
    // =========================================================

    public bool TryAddNetworkedById(int itemId)
    {
        return TryAddNetworkedById(itemId, 0f, 0f);
    }

    // Preserves compatibility with existing pickup calls.
    public bool TryAddNetworkedById(
        int itemId,
        float candleRemaining,
        float candleMaxLife)
    {
        bool candleInitialized = candleMaxLife > 0f;

        return TryAddNetworkedById(
            itemId,
            candleRemaining,
            candleMaxLife,
            candleInitialized,
            out _
        );
    }

    // Use this overload when transferring a dropped candle.
    public bool TryAddNetworkedById(
        int itemId,
        float candleRemaining,
        float candleMaxLife,
        bool candleInitialized,
        out int addedSlot)
    {
        addedSlot = -1;

        if (Object == null || !Object.HasStateAuthority)
            return false;

        ItemData item = GetItemData(itemId);

        if (item == null)
        {
            Debug.LogError(
                $"[INVENTORY] Item ID {itemId} was not found."
            );
            return false;
        }

        bool isCandle = IsCandleItem(item);

        for (int i = 0; i < SlotCount; i++)
        {
            if (NetworkSlots.Get(i) != 0)
                continue;

            NetworkSlots.Set(i, itemId);

            // Never let an old slot's candle state leak into a new item.
            ClearCandleState(i);

            if (isCandle && candleInitialized && candleMaxLife > 0f)
            {
                float maxLife = Mathf.Max(0f, candleMaxLife);

                CandleMaxLifeBySlot.Set(i, maxLife);
                CandleLifeBySlot.Set(
                    i,
                    Mathf.Clamp(candleRemaining, 0f, maxLife)
                );
                CandleInitializedBySlot.Set(i, true);
            }

            addedSlot = i;

            Debug.Log(
                $"[INVENTORY] {item.itemName} added to slot {i + 1}."
            );

            return true;
        }

        Debug.Log("[INVENTORY] Inventory is full.");
        return false;
    }

    // =========================================================
    // CANDLE STATE
    // =========================================================

    private bool IsCandleItem(ItemData item)
    {
        return item != null &&
               item.heldPrefab != null &&
               item.heldPrefab.GetComponentInChildren<CandleVisual>(true)
                   != null;
    }

    public bool IsCandleSlot(int slotIndex)
    {
        return IsCandleItem(GetItemAt(slotIndex));
    }

    public bool IsCandleInitialized(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return false;

        return CandleInitializedBySlot.Get(slotIndex);
    }

    public float GetCandleRemaining(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return 0f;

        return CandleLifeBySlot.Get(slotIndex);
    }

    public float GetCandleMaxLife(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return 0f;

        return CandleMaxLifeBySlot.Get(slotIndex);
    }

    public void InitializeCandleSlot(int slotIndex, float maxLife)
    {
        if (Object == null ||
            !Object.HasStateAuthority ||
            slotIndex < 0 ||
            slotIndex >= SlotCount ||
            !IsCandleSlot(slotIndex) ||
            CandleInitializedBySlot.Get(slotIndex))
        {
            return;
        }

        maxLife = Mathf.Max(0f, maxLife);

        CandleMaxLifeBySlot.Set(slotIndex, maxLife);
        CandleLifeBySlot.Set(slotIndex, maxLife);
        CandleInitializedBySlot.Set(slotIndex, true);
    }

    public void SetCandleRemaining(int slotIndex, float remaining)
    {
        if (Object == null ||
            !Object.HasStateAuthority ||
            slotIndex < 0 ||
            slotIndex >= SlotCount ||
            !IsCandleInitialized(slotIndex))
        {
            return;
        }

        CandleLifeBySlot.Set(
            slotIndex,
            Mathf.Clamp(
                remaining,
                0f,
                CandleMaxLifeBySlot.Get(slotIndex)
            )
        );
    }

    // Used when dropping, restoring, or transferring a candle.
    public void SetCandleStateForSlot(
        int slotIndex,
        float remaining,
        float maxLife,
        bool initialized)
    {
        if (Object == null ||
            !Object.HasStateAuthority ||
            slotIndex < 0 ||
            slotIndex >= SlotCount)
        {
            return;
        }

        if (NetworkSlots.Get(slotIndex) == 0 ||
            !IsCandleSlot(slotIndex) ||
            !initialized ||
            maxLife <= 0f)
        {
            ClearCandleState(slotIndex);
            return;
        }

        maxLife = Mathf.Max(0f, maxLife);

        CandleMaxLifeBySlot.Set(slotIndex, maxLife);
        CandleLifeBySlot.Set(
            slotIndex,
            Mathf.Clamp(remaining, 0f, maxLife)
        );
        CandleInitializedBySlot.Set(slotIndex, true);
    }

    private void ClearCandleState(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return;

        CandleLifeBySlot.Set(slotIndex, 0f);
        CandleMaxLifeBySlot.Set(slotIndex, 0f);
        CandleInitializedBySlot.Set(slotIndex, false);
    }

    // =========================================================
    // HAS ITEM
    // =========================================================

    public bool Has(ItemData item)
    {
        return item != null && HasItemId(item.itemId);
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
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (index < 0 || index >= SlotCount)
            return;

        if (SelectedSlot == index)
            return;

        SelectedSlot = index;

        Debug.Log($"[INVENTORY] Selected slot {index + 1}");
    }

    // =========================================================
    // DROP
    // =========================================================

    private void RequestDrop()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        DropAuthoritative(SelectedSlot);
    }

    private void DropAuthoritative(int slotIndex)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (Runner == null)
        {
            Debug.LogError(
                "[INVENTORY] Cannot drop item because Runner is null."
            );
            return;
        }

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

        // Save candle state before clearing the inventory slot.
        bool isCandle = IsCandleSlot(slotIndex);

        float candleRemaining = 0f;
        float candleMaxLife = 0f;
        bool candleInitialized = false;

        if (isCandle)
        {
            candleRemaining = GetCandleRemaining(slotIndex);
            candleMaxLife = GetCandleMaxLife(slotIndex);
            candleInitialized = IsCandleInitialized(slotIndex);

            candleRemaining = Mathf.Max(0f, candleRemaining);
            candleMaxLife = Mathf.Max(0f, candleMaxLife);
        }

        Transform aim = dropOrigin != null ? dropOrigin : transform;

        Vector3 spawnPosition = aim.position + aim.forward * 0.8f;

        Quaternion rotation = Quaternion.Euler(
            0f,
            aim.eulerAngles.y,
            0f
        );

        NetworkObject droppedObject = Runner.Spawn(
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

        // Initialize the network item identity and preserve candle life.
        NetworkedDroppedItem droppedItem =
            droppedObject.GetComponent<NetworkedDroppedItem>();

        if (droppedItem != null)
        {
            if (isCandle)
            {
                droppedItem.Initialize(
                    itemId,
                    candleInitialized,
                    candleRemaining,
                    candleMaxLife,
                    false // Dropped candles are extinguished.
                );
            }
            else
            {
                droppedItem.Initialize(itemId);
            }
        }
        else
        {
            Debug.LogWarning(
                $"[INVENTORY] {item.itemName} world prefab has no " +
                "NetworkedDroppedItem component."
            );
        }

        ItemPickup itemPickup = droppedObject.GetComponent<ItemPickup>();

        if (itemPickup != null)
        {
            itemPickup.Initialize(itemId);
        }
        else
        {
            Debug.LogWarning(
                $"[INVENTORY] {item.itemName} world prefab has no " +
                "ItemPickup component."
            );
        }

        // Preserve the existing throw behavior.
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

            Collider[] ownColliders =
                GetComponentsInChildren<Collider>();

            Collider[] droppedColliders =
                droppedObject.GetComponentsInChildren<Collider>();

            foreach (Collider a in droppedColliders)
            {
                if (a == null)
                    continue;

                foreach (Collider b in ownColliders)
                {
                    if (b == null || a == b)
                        continue;

                    Physics.IgnoreCollision(a, b, true);
                }
            }
        }
        else
        {
            Debug.LogWarning(
                "[INVENTORY] Dropped item has no DroppedItemPhysics."
            );
        }

        NetworkSlots.Set(slotIndex, 0);
        ClearCandleState(slotIndex);

        Debug.Log(
            $"[INVENTORY] {item.itemName} dropped from slot {slotIndex + 1}."
        );

        if (isCandle)
        {
            Debug.Log(
                $"[CANDLE] Dropped with {candleRemaining:F1}s remaining " +
                $"out of {candleMaxLife:F1}s."
            );
        }
    }

    // =========================================================
    // ITEM LOOKUP
    // =========================================================

    public ItemData GetItemData(int itemId)
    {
        if (itemId <= 0 || itemCatalog == null)
            return null;

        foreach (ItemData item in itemCatalog)
        {
            if (item != null && item.itemId == itemId)
                return item;
        }

        return null;
    }

    public ItemData GetItemAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return null;

        return GetItemData(NetworkSlots.Get(slotIndex));
    }

    public int GetItemIdAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return 0;

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
        if (networkHoldPoint != null)
            return networkHoldPoint;

        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child == transform ||
                child.name != networkHoldPointName)
            {
                continue;
            }

            networkHoldPoint = child;
            currentCharacterRoot = GetCharacterRoot(networkHoldPoint);

            Debug.Log(
                "[INVENTORY] NetworkHoldPoint found: " +
                GetTransformPath(networkHoldPoint)
            );

            return networkHoldPoint;
        }

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

        while (current.parent != null && current.parent != transform)
        {
            current = current.parent;
        }

        return current.parent == transform ? current : hold;
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

        if (networkHeldObject != null)
        {
            Destroy(networkHeldObject);
            networkHeldObject = null;
        }

        ItemData item = GetItemAt(SelectedSlot);

        if (item == null || item.heldPrefab == null)
            return;

        if (Object.HasInputAuthority)
        {
            CreateFirstPersonHeldVisual(item);
        }
        else
        {
            CreateNetworkHeldVisual(item);
        }
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

        heldObject = Instantiate(item.heldPrefab, holdPoint);

        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;
        heldObject.transform.localScale = Vector3.one;

        CleanHeldVisual(heldObject);
    }

    // =========================================================
    // NETWORK / THIRD PERSON HELD VISUAL
    // =========================================================

    private void CreateNetworkHeldVisual(ItemData item)
    {
        if (networkHoldPoint == null)
            FindNetworkHoldPoint();

        if (networkHoldPoint == null)
        {
            Debug.LogWarning(
                $"[INVENTORY] Could not find NetworkHoldPoint for player {Object.Id}."
            );
            return;
        }

        if (item == null || item.heldPrefab == null)
            return;

        networkHeldObject = Instantiate(item.heldPrefab, networkHoldPoint);

        networkHeldObject.transform.localPosition = Vector3.zero;
        networkHeldObject.transform.localRotation = Quaternion.identity;
        networkHeldObject.transform.localScale = Vector3.one;

        CleanHeldVisual(networkHeldObject);
    }

    // =========================================================
    // CLEAN HELD VISUAL
    // =========================================================

    private void CleanHeldVisual(GameObject visual)
    {
        if (visual == null)
            return;

        foreach (DroppedItemPhysics component in
                 visual.GetComponentsInChildren<DroppedItemPhysics>(true))
        {
            Destroy(component);
        }

        foreach (Rigidbody component in
                 visual.GetComponentsInChildren<Rigidbody>(true))
        {
            Destroy(component);
        }

        foreach (Collider component in
                 visual.GetComponentsInChildren<Collider>(true))
        {
            Destroy(component);
        }

        foreach (KeyPickup component in
                 visual.GetComponentsInChildren<KeyPickup>(true))
        {
            Destroy(component);
        }

        foreach (ItemPickup component in
                 visual.GetComponentsInChildren<ItemPickup>(true))
        {
            Destroy(component);
        }

        foreach (NetworkedDroppedItem component in
                 visual.GetComponentsInChildren<NetworkedDroppedItem>(true))
        {
            Destroy(component);
        }
    }

    // =========================================================
    // INVENTORY HASH
    // =========================================================

    private int CalculateInventoryHash()
    {
        unchecked
        {
            int hash = SelectedSlot;

            for (int i = 0; i < SlotCount; i++)
            {
                hash = hash * 31 + NetworkSlots.Get(i);
            }

            return hash;
        }
    }

    // =========================================================
    // TRANSFORM PATH
    // =========================================================

    private string GetTransformPath(Transform target)
    {
        if (target == null)
            return "(null)";

        string path = target.name;
        Transform current = target.parent;

        while (current != null && current != transform)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}