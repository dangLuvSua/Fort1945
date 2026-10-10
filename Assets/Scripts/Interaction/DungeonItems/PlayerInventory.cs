using System;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    // Keep the old constant available for existing scripts.
    public const int SlotCount = 8;

    public const int QuickBarSlotCount = 8;
    public const int SlotsPerCategory = 24;
    public const int StorageSlotCount = SlotsPerCategory * 3;

    // Storage ranges:
    // Items:  0-23
    // Puzzle: 24-47
    // Gallery: 48-71

    [Header("Player Visual")]
    [SerializeField] private Transform holdPoint;
    [SerializeField]
    private string networkHoldPointName =
        "NetworkHoldPoint";

    private Transform networkHoldPoint;
    private Transform currentCharacterRoot;
    private GameObject heldObject;
    private GameObject networkHeldObject;

    [Header("Drop")]
    [SerializeField] private Transform dropOrigin;
    [SerializeField] private float throwForce = 4f;
    [SerializeField] private float throwUp = 1.5f;

    [Header("Item Database")]
    [SerializeField] private ItemData[] itemCatalog;

    // NetworkSlots contains the actual stored items.
    // 0 = empty; positive values = ItemData.itemId.
    [Networked, Capacity(StorageSlotCount)]
    private NetworkArray<int> NetworkSlots => default;

    // Quick-bar entries encode storage index + 1.
    // 0 = empty; 1 = storage index 0; 72 = storage index 71.
    [Networked, Capacity(QuickBarSlotCount)]
    private NetworkArray<int> NetworkQuickBar => default;

    // SelectedSlot is a QUICK-BAR index, or -1 for no selection.
    [Networked]
    public int SelectedSlot { get; private set; }

    // Candle data is associated with STORAGE slots.
    [Networked, Capacity(StorageSlotCount)]
    private NetworkArray<float> CandleLifeBySlot => default;

    [Networked, Capacity(StorageSlotCount)]
    private NetworkArray<float> CandleMaxLifeBySlot => default;

    [Networked, Capacity(StorageSlotCount)]
    private NetworkArray<NetworkBool> CandleInitializedBySlot => default;
    // Quantity stored in each inventory slot.
    // Empty slots have quantity 0.
    [Networked, Capacity(StorageSlotCount)]
    private NetworkArray<int> NetworkQuantities => default;

    private int lastInventoryHash = int.MinValue;

    public event Action InventoryChanged;

    public bool IsInventorySpawned { get; private set; }
    public override void Spawned()
    {
        // Mark the inventory ready only after Fusion calls Spawned().
        IsInventorySpawned = true;

        if (Object.HasStateAuthority)
        {
            SelectedSlot = -1;

            for (int i = 0; i < QuickBarSlotCount; i++)
                NetworkQuickBar.Set(i, 0);
        }

        FindNetworkHoldPoint();
        RefreshHeldVisual();

        lastInventoryHash = CalculateInventoryHash();
        InventoryChanged?.Invoke();

        Debug.Log(
            $"[INVENTORY] Spawned and ready. " +
            $"StateAuthority={Object.HasStateAuthority}"
        );
    }

    private void Update()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (Keyboard.current == null)
            return;

        // Number keys 1-8 select QUICK-BAR positions.
        for (int i = 0; i < QuickBarSlotCount; i++)
        {
            Key key = (Key)((int)Key.Digit1 + i);

            if (Keyboard.current[key].wasPressedThisFrame)
            {
                Select(i);
                break;
            }
        }

        if (Keyboard.current.gKey.wasPressedThisFrame)
            RequestDrop();
    }

    public override void Render()
    {
        if (Object == null)
            return;

        bool holdPointWasMissing = networkHoldPoint == null;

        if (!Object.HasInputAuthority)
            FindNetworkHoldPoint();

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
                InventoryChanged?.Invoke();
        }
    }

    // =========================================================
    // STORAGE CATEGORY RANGES
    // =========================================================

    private bool TryGetCategoryRange(
        ItemCategory category,
        out int start,
        out int end)
    {
        start = 0;
        end = 0;

        switch (category)
        {
            case ItemCategory.Item:
                start = 0;
                end = SlotsPerCategory;
                return true;

            case ItemCategory.PuzzlePiece:
                start = SlotsPerCategory;
                end = SlotsPerCategory * 2;
                return true;

            case ItemCategory.Gallery:
                start = SlotsPerCategory * 2;
                end = StorageSlotCount;
                return true;

            default:
                Debug.LogWarning(
                    $"[INVENTORY] Unsupported item category: {category}"
                );
                return false;
        }
    }

    // =========================================================
    // ADD ITEMS
    // =========================================================

    public bool TryAddNetworkedById(int itemId)
    {
        return TryAddNetworkedById(itemId, 0f, 0f);
    }

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

        if (!TryGetCategoryRange(
                item.category,
                out int start,
                out int end))
        {
            return false;
        }

        // -----------------------------------------------------
        // 1. Try adding to an existing compatible stack.
        // -----------------------------------------------------

        int maxStack = item.EffectiveMaxStackSize;

        if (item.isStackable && maxStack > 1)
        {
            for (int i = start; i < end; i++)
            {
                if (NetworkSlots.Get(i) != itemId)
                    continue;

                int currentQuantity = NetworkQuantities.Get(i);

                if (currentQuantity <= 0 ||
                    currentQuantity >= maxStack)
                {
                    continue;
                }

                // Do not combine candles with incompatible burn states.
                if (IsCandleItem(item) &&
                    !CanStackCandle(
                        i,
                        candleInitialized,
                        candleRemaining,
                        candleMaxLife))
                {
                    continue;
                }

                NetworkQuantities.Set(i, currentQuantity + 1);
                addedSlot = i;

                Debug.Log(
                    $"[INVENTORY] Added {item.itemName} to stack " +
                    $"in slot {i + 1}. " +
                    $"Quantity: {currentQuantity + 1}/{maxStack}."
                );

                return true;
            }
        }

        // -----------------------------------------------------
        // 2. Otherwise, find an empty slot in this category.
        // -----------------------------------------------------

        for (int i = start; i < end; i++)
        {
            if (NetworkSlots.Get(i) != 0)
                continue;

            NetworkSlots.Set(i, itemId);
            NetworkQuantities.Set(i, 1);
            ClearCandleState(i);

            if (IsCandleItem(item) &&
                candleInitialized &&
                candleMaxLife > 0f)
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

            if (item.category != ItemCategory.Gallery)
                TryEquipItemToQuickBar(itemId);

            Debug.Log(
                $"[INVENTORY] {item.itemName} added to storage " +
                $"slot {i + 1}. Quantity: 1."
            );

            return true;
        }

        Debug.Log(
            $"[INVENTORY] The {item.category} storage tab is full."
        );

        return false;
    }

    private bool CanStackCandle(
        int storageIndex,
        bool incomingInitialized,
        float incomingRemaining,
        float incomingMaxLife)
    {
        bool existingInitialized =
            CandleInitializedBySlot.Get(storageIndex);

        // Initialized and uninitialized candles are not interchangeable.
        if (existingInitialized != incomingInitialized)
            return false;

        // Both candles have no initialized burn state.
        if (!existingInitialized)
            return true;

        float existingRemaining =
            CandleLifeBySlot.Get(storageIndex);

        float existingMaxLife =
            CandleMaxLifeBySlot.Get(storageIndex);

        // Only combine candles with compatible remaining and maximum life.
        const float tolerance = 0.1f;

        return Mathf.Abs(existingRemaining - incomingRemaining)
                   <= tolerance
            && Mathf.Abs(existingMaxLife - incomingMaxLife)
                   <= tolerance;
    }

    public int GetStorageQuantityAt(int storageIndex)
    {
        if (!IsValidStorageIndex(storageIndex))
            return 0;

        if (NetworkSlots.Get(storageIndex) == 0)
            return 0;

        return NetworkQuantities.Get(storageIndex);
    }

    public int GetQuickBarQuantityAt(int quickBarIndex)
    {
        int storageIndex =
            GetQuickBarStorageIndex(quickBarIndex);

        return storageIndex < 0
            ? 0
            : GetStorageQuantityAt(storageIndex);
    }

    public int GetQuantityForItemId(int itemId)
    {
        int storageIndex = FindStorageIndex(itemId);

        return storageIndex < 0
            ? 0
            : GetStorageQuantityAt(storageIndex);
    }

    // =========================================================
    // QUICK BAR EQUIP / REMOVE
    // =========================================================

    public bool TryEquipItemToQuickBar(int itemId)
    {
        if (Object == null || !Object.HasStateAuthority)
            return false;

        ItemData item = GetItemData(itemId);

        if (item == null || item.category == ItemCategory.Gallery)
            return false;

        int storageIndex = FindStorageIndex(itemId);

        if (storageIndex < 0)
        {
            Debug.LogWarning(
                "[INVENTORY] Cannot equip an item that is not stored."
            );
            return false;
        }

        // Do not assign the same stored item more than once.
        for (int i = 0; i < QuickBarSlotCount; i++)
        {
            if (GetQuickBarStorageIndex(i) == storageIndex)
                return true;
        }

        // Assign to the first empty quick-bar slot.
        for (int i = 0; i < QuickBarSlotCount; i++)
        {
            if (NetworkQuickBar.Get(i) != 0)
                continue;

            NetworkQuickBar.Set(i, storageIndex + 1);

            // Automatically select the newly collected item.
            SelectedSlot = i;

            Debug.Log(
                $"[INVENTORY] {item.itemName} equipped in quick slot {i + 1}."
            );

            return true;
        }

        Debug.Log("[INVENTORY] Quick bar is full.");
        return false;
    }

    public bool RemoveItemFromQuickBar(int itemId)
    {
        if (Object == null || !Object.HasStateAuthority)
            return false;

        int storageIndex = FindStorageIndex(itemId);

        if (storageIndex < 0)
            return false;

        for (int i = 0; i < QuickBarSlotCount; i++)
        {
            if (GetQuickBarStorageIndex(i) != storageIndex)
                continue;

            NetworkQuickBar.Set(i, 0);

            if (SelectedSlot == i)
                SelectedSlot = -1;

            Debug.Log(
                $"[INVENTORY] Removed item ID {itemId} from the quick bar. " +
                "The stored item was kept."
            );

            return true;
        }

        return false;
    }


    public bool RemoveItemCompletely(int itemId)
    {
        if (Object == null || !Object.HasStateAuthority)
            return false;

        if (itemId <= 0)
            return false;

        int storageIndex = FindStorageIndex(itemId);

        if (storageIndex < 0)
            return false;

        // Remove all quickbar references to this stored item.
        for (int i = 0; i < QuickBarSlotCount; i++)
        {
            if (GetQuickBarStorageIndex(i) != storageIndex)
                continue;

            NetworkQuickBar.Set(i, 0);

            if (SelectedSlot == i)
                SelectedSlot = -1;
        }

        // Permanently remove the item from storage.
        NetworkSlots.Set(storageIndex, 0);
        NetworkQuantities.Set(storageIndex, 0);

        // Clear associated candle data if applicable.
        ClearCandleState(storageIndex);

        Debug.Log(
            $"[INVENTORY] Permanently removed item ID {itemId} " +
            $"from storage slot {storageIndex + 1}."
        );

        return true;
    }
    public bool IsItemInQuickBar(int itemId)
    {
        int storageIndex = FindStorageIndex(itemId);

        if (storageIndex < 0)
            return false;

        for (int i = 0; i < QuickBarSlotCount; i++)
        {
            if (GetQuickBarStorageIndex(i) == storageIndex)
                return true;
        }

        return false;
    }

    public int GetQuickBarStorageIndex(int quickBarIndex)
    {
        if (quickBarIndex < 0 ||
            quickBarIndex >= QuickBarSlotCount)
        {
            return -1;
        }

        int encoded = NetworkQuickBar.Get(quickBarIndex);

        return encoded == 0 ? -1 : encoded - 1;
    }

    public ItemData GetQuickBarItemAt(int quickBarIndex)
    {
        int storageIndex = GetQuickBarStorageIndex(quickBarIndex);

        return storageIndex < 0
            ? null
            : GetStorageItemAt(storageIndex);
    }

    public int GetQuickBarItemIdAt(int quickBarIndex)
    {
        int storageIndex = GetQuickBarStorageIndex(quickBarIndex);

        return storageIndex < 0
            ? 0
            : GetStorageItemIdAt(storageIndex);
    }

    // =========================================================
    // CANDLE STATE
    // Candle APIs use STORAGE slot indices.
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
        return IsCandleItem(GetStorageItemAt(slotIndex));
    }

    public bool IsCandleInitialized(int slotIndex)
    {
        if (!IsValidStorageIndex(slotIndex))
            return false;

        return CandleInitializedBySlot.Get(slotIndex);
    }

    public float GetCandleRemaining(int slotIndex)
    {
        if (!IsValidStorageIndex(slotIndex))
            return 0f;

        return CandleLifeBySlot.Get(slotIndex);
    }

    public float GetCandleMaxLife(int slotIndex)
    {
        if (!IsValidStorageIndex(slotIndex))
            return 0f;

        return CandleMaxLifeBySlot.Get(slotIndex);
    }

    public void InitializeCandleSlot(int slotIndex, float maxLife)
    {
        if (Object == null ||
            !Object.HasStateAuthority ||
            !IsValidStorageIndex(slotIndex) ||
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
            !IsValidStorageIndex(slotIndex) ||
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

    public void SetCandleStateForSlot(
        int slotIndex,
        float remaining,
        float maxLife,
        bool initialized)
    {
        if (Object == null ||
            !Object.HasStateAuthority ||
            !IsValidStorageIndex(slotIndex))
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
        if (!IsValidStorageIndex(slotIndex))
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

        for (int i = 0; i < StorageSlotCount; i++)
        {
            if (NetworkSlots.Get(i) == itemId &&
                NetworkQuantities.Get(i) > 0)
            {
                return true;
            }
        }

        return false;
    }
    private int FindStorageIndex(int itemId)
    {
        if (itemId <= 0)
            return -1;

        for (int i = 0; i < StorageSlotCount; i++)
        {
            if (NetworkSlots.Get(i) == itemId &&
                NetworkQuantities.Get(i) > 0)
            {
                return i;
            }
        }

        return -1;
    }

    // =========================================================
    // SELECT QUICK-BAR SLOT
    // =========================================================

    private void Select(int index)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (index < 0 || index >= QuickBarSlotCount)
            return;

        // Pressing the selected number again puts the item away.
        if (SelectedSlot == index)
        {
            DeselectItem();
            return;
        }

        SelectedSlot = index;

        Debug.Log(
            $"[INVENTORY] Selected quick-bar slot {index + 1}."
        );
    }

    public void SelectQuickBarSlot(int index)
    {
        Select(index);
    }

    public void DeselectItem()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        SelectedSlot = -1;

        Debug.Log("[INVENTORY] Item deselected. Hand is empty.");
    }

    // =========================================================
    // DROP SELECTED QUICK-BAR ITEM
    // =========================================================

    private void RequestDrop()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        DropAuthoritative(SelectedSlot);
    }

    private void DropAuthoritative(int quickBarIndex)
    {
        if (Object == null ||
            !Object.HasStateAuthority ||
            Runner == null)
        {
            return;
        }

        if (quickBarIndex < 0 ||
            quickBarIndex >= QuickBarSlotCount)
        {
            return;
        }

        int storageIndex = GetQuickBarStorageIndex(quickBarIndex);

        if (storageIndex < 0)
            return;

        int itemId = NetworkSlots.Get(storageIndex);

        if (itemId == 0)
            return;

        ItemData item = GetItemData(itemId);

        if (item == null || item.worldPrefab == null)
        {
            Debug.LogError(
                $"[INVENTORY] Cannot drop item ID {itemId}. " +
                "Check ItemData and its worldPrefab."
            );
            return;
        }

        bool isCandle = IsCandleSlot(storageIndex);

        float candleRemaining = isCandle
            ? GetCandleRemaining(storageIndex)
            : 0f;

        float candleMaxLife = isCandle
            ? GetCandleMaxLife(storageIndex)
            : 0f;

        bool candleInitialized = isCandle &&
            IsCandleInitialized(storageIndex);

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
                    false
                );
            }
            else
            {
                droppedItem.Initialize(itemId);
            }
        }

        ItemPickup itemPickup = droppedObject.GetComponent<ItemPickup>();

        if (itemPickup != null)
            itemPickup.Initialize(itemId);

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
                    if (b != null && a != b)
                        Physics.IgnoreCollision(a, b, true);
                }
            }
        }

        // Remove all quick-bar references to this stored item.
        for (int i = 0; i < QuickBarSlotCount; i++)
        {
            if (GetQuickBarStorageIndex(i) == storageIndex)
                NetworkQuickBar.Set(i, 0);
        }

        int quantity = NetworkQuantities.Get(storageIndex);

        if (quantity > 1)
        {
            // Remove only one item from the stack.
            NetworkQuantities.Set(storageIndex, quantity - 1);

            Debug.Log(
                $"[INVENTORY] Dropped one {item.itemName}. " +
                $"Remaining quantity: {quantity - 1}."
            );
        }
        else
        {
            // Last item in the stack: clear the storage slot.
            NetworkSlots.Set(storageIndex, 0);
            NetworkQuantities.Set(storageIndex, 0);

            ClearCandleState(storageIndex);

            // Remove quick-bar references to the now-empty slot.
            for (int i = 0; i < QuickBarSlotCount; i++)
            {
                if (GetQuickBarStorageIndex(i) == storageIndex)
                    NetworkQuickBar.Set(i, 0);
            }

            Debug.Log($"[INVENTORY] {item.itemName} dropped.");
        }

        SelectedSlot = -1;
        Debug.Log($"[INVENTORY] {item.itemName} dropped.");
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

    // Compatibility method: gets the item assigned to a quick-bar slot.
    public ItemData GetItemAt(int slotIndex)
    {
        return GetQuickBarItemAt(slotIndex);
    }

    public int GetItemIdAt(int slotIndex)
    {
        return GetQuickBarItemIdAt(slotIndex);
    }

    // Use these methods for full-inventory storage slots.
    public ItemData GetStorageItemAt(int storageIndex)
    {
        if (!IsValidStorageIndex(storageIndex))
            return null;

        return GetItemData(NetworkSlots.Get(storageIndex));
    }

    public int GetStorageItemIdAt(int storageIndex)
    {
        if (!IsValidStorageIndex(storageIndex))
            return 0;

        return NetworkSlots.Get(storageIndex);
    }

    public int GetSlotCount()
    {
        return QuickBarSlotCount;
    }

    public int GetStorageSlotCount()
    {
        return StorageSlotCount;
    }

    public int GetSelectedStorageSlot()
    {
        return GetQuickBarStorageIndex(SelectedSlot);
    }

    private bool IsValidStorageIndex(int index)
    {
        return index >= 0 && index < StorageSlotCount;
    }

    // =========================================================
    // HELD VISUALS
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

        ItemData item = GetQuickBarItemAt(SelectedSlot);

        if (item == null || item.heldPrefab == null)
            return;

        if (Object.HasInputAuthority)
            CreateFirstPersonHeldVisual(item);
        else
            CreateNetworkHeldVisual(item);
    }

    private void CreateFirstPersonHeldVisual(ItemData item)
    {
        if (holdPoint == null)
        {
            Debug.LogWarning(
                "[INVENTORY] holdPoint is not assigned. " +
                "Assign the player's first-person hand transform."
            );

            return;
        }

        if (item == null || item.heldPrefab == null)
        {
            Debug.LogWarning(
                "[INVENTORY] ItemData or heldPrefab is missing."
            );

            return;
        }

        heldObject = Instantiate(item.heldPrefab, holdPoint);

        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;
        heldObject.transform.localScale = Vector3.one;

        CleanHeldVisual(heldObject);

        Debug.Log(
            $"[INVENTORY] Holding '{item.itemName}' " +
            $"under '{holdPoint.name}'."
        );
    }

    private void CreateNetworkHeldVisual(ItemData item)
    {
        if (networkHoldPoint == null)
            FindNetworkHoldPoint();

        if (networkHoldPoint == null || item == null ||
            item.heldPrefab == null)
        {
            return;
        }

        networkHeldObject = Instantiate(item.heldPrefab, networkHoldPoint);
        networkHeldObject.transform.localPosition = Vector3.zero;
        networkHeldObject.transform.localRotation = Quaternion.identity;
        networkHeldObject.transform.localScale = Vector3.one;

        CleanHeldVisual(networkHeldObject);
    }

    private void CleanHeldVisual(GameObject visual)
    {
        if (visual == null)
            return;

        foreach (DroppedItemPhysics c in
                 visual.GetComponentsInChildren<DroppedItemPhysics>(true))
            Destroy(c);

        foreach (Rigidbody c in
                 visual.GetComponentsInChildren<Rigidbody>(true))
            Destroy(c);

        foreach (Collider c in
                 visual.GetComponentsInChildren<Collider>(true))
            Destroy(c);

        foreach (KeyPickup c in
                 visual.GetComponentsInChildren<KeyPickup>(true))
            Destroy(c);

        foreach (ItemPickup c in
                 visual.GetComponentsInChildren<ItemPickup>(true))
            Destroy(c);

        foreach (NetworkedDroppedItem c in
                 visual.GetComponentsInChildren<NetworkedDroppedItem>(true))
            Destroy(c);
    }

    // =========================================================
    // NETWORK HOLD POINT
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
                continue;

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

    private Transform GetCharacterRoot(Transform hold)
    {
        if (hold == null)
            return null;

        Transform current = hold;

        while (current.parent != null && current.parent != transform)
            current = current.parent;

        return current.parent == transform ? current : hold;
    }

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

    // =========================================================
    // CHANGE DETECTION
    // =========================================================

    private int CalculateInventoryHash()
    {
        unchecked
        {
            int hash = SelectedSlot;

            // Include stored item IDs and quantities.
            for (int i = 0; i < StorageSlotCount; i++)
            {
                hash = hash * 31 + NetworkSlots.Get(i);
                hash = hash * 31 + NetworkQuantities.Get(i);
            }

            // Include quick-bar storage references.
            for (int i = 0; i < QuickBarSlotCount; i++)
            {
                hash = hash * 31 + NetworkQuickBar.Get(i);
            }

            return hash;
        }
    }
}