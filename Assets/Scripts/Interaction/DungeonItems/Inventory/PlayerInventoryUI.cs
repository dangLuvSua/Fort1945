
using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInventoryUI : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Quick Bar")]
    [SerializeField] private InventorySlotUI[] quickSlots;

    [Header("Dynamic Inventory")]
    [Tooltip("Prefab used to generate occupied inventory slots.")]
    [SerializeField] private InventorySlotUI slotPrefab;

    [Header("Category Content")]
    [SerializeField] private Transform itemContent;
    [SerializeField] private Transform galleryContent;
    [SerializeField] private Transform puzzleContent;

    [Header("Description Panel")]
    [SerializeField] private Image descriptionIcon;
    [SerializeField] private TMP_Text descriptionName;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text descriptionQuantity;

    [Header("Item Actions")]
    [Tooltip("Equips the selected stored item to the quickbar.")]
    [SerializeField] private Button equipButton;

    [Tooltip("Removes the selected item from the quickbar only.")]
    [SerializeField] private Button removeButton;

    [Tooltip("Permanently deletes the selected item from storage.")]
    [SerializeField] private Button deleteInventoryButton;

    [Header("Gallery Photograph Preview")]
    [SerializeField] private GameObject galleryPreviewPanel;
    [SerializeField] private Image galleryPreviewImage;

    [Header("Full Inventory Panel")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private bool openOnStart = false;

    private readonly List<InventorySlotUI> itemSlots = new();
    private readonly List<InventorySlotUI> gallerySlots = new();
    private readonly List<InventorySlotUI> puzzleSlots = new();

    private ItemData selectedItem;

    // The selected item is identified by its actual storage position.
    // This prevents two separate stacks of the same item type
    // from being highlighted or treated as the same selection.
    private int selectedStorageIndex = -1;

    // Tracks which quickbar slot was selected, if applicable.
    private int selectedQuickBarIndex = -1;

    private bool inventorySubscribed;

    private void Awake()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(openOnStart);

        if (galleryPreviewPanel != null)
            galleryPreviewPanel.SetActive(false);

        ClearDescription();

        if (equipButton != null)
            equipButton.onClick.AddListener(EquipSelectedItem);

        if (removeButton != null)
            removeButton.onClick.AddListener(RemoveSelectedItemFromQuickBar);

        if (deleteInventoryButton != null)
            deleteInventoryButton.onClick.AddListener(DeleteSelectedItemCompletely);
    }

    private void OnEnable()
    {
        TryFindLocalInventory();
        SubscribeToInventory();
    }

    private void OnDisable()
    {
        UnsubscribeFromInventory();
        UnsubscribeQuickSlots();
        ClearDynamicSlots();
    }

    private void OnDestroy()
    {
        if (equipButton != null)
            equipButton.onClick.RemoveListener(EquipSelectedItem);

        if (removeButton != null)
            removeButton.onClick.RemoveListener(RemoveSelectedItemFromQuickBar);

        if (deleteInventoryButton != null)
            deleteInventoryButton.onClick.RemoveListener(DeleteSelectedItemCompletely);
    }

    private bool IsInventoryReady()
    {
        return inventory != null
            && inventory.IsInventorySpawned
            && inventory.Object != null
            && inventory.Object.IsValid
            && inventory.Object.IsInSimulation;
    }

    private void Update()
    {
        // Retry while Fusion is registering the local player.
        if (!IsInventoryReady())
        {
            if (inventorySubscribed)
                UnsubscribeFromInventory();

            TryFindLocalInventory();
            SubscribeToInventory();
            return;
        }

        if (!inventorySubscribed)
            SubscribeToInventory();
    }

    // =========================================================
    // FIND LOCAL PLAYER INVENTORY
    // =========================================================

    private void TryFindLocalInventory()
    {
        if (inventory != null
            && inventory.Object != null
            && inventory.Object.IsValid
            && inventory.Object.HasInputAuthority)
        {
            return;
        }

        if (inventorySubscribed)
            UnsubscribeFromInventory();

        inventory = null;

        NetworkRunner[] runners =
            FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None);

        foreach (NetworkRunner runner in runners)
        {
            if (runner == null || !runner.IsRunning)
                continue;

            if (!runner.TryGetPlayerObject(
                    runner.LocalPlayer,
                    out NetworkObject playerObject))
            {
                continue;
            }

            if (playerObject == null || !playerObject.IsValid)
                continue;

            PlayerInventory foundInventory =
                playerObject.GetComponent<PlayerInventory>();

            if (foundInventory == null)
            {
                Debug.LogError(
                    "[INVENTORY UI] Local PlayerObject has no PlayerInventory."
                );
                continue;
            }

            inventory = foundInventory;

            Debug.Log(
                $"[INVENTORY UI] Found local inventory on " +
                $"'{playerObject.name}'. " +
                $"Ready={inventory.IsInventorySpawned}"
            );

            return;
        }
    }

    // =========================================================
    // INVENTORY EVENTS
    // =========================================================

    private void SubscribeToInventory()
    {
        if (inventorySubscribed || !IsInventoryReady())
            return;

        inventory.InventoryChanged -= Refresh;
        inventory.InventoryChanged += Refresh;

        inventorySubscribed = true;

        SubscribeQuickSlots();
        Refresh();

        Debug.Log(
            $"[INVENTORY UI] Subscribed to local inventory on " +
            $"'{inventory.gameObject.name}'."
        );
    }

    private void UnsubscribeFromInventory()
    {
        if (inventory != null && inventorySubscribed)
            inventory.InventoryChanged -= Refresh;

        inventorySubscribed = false;
    }

    private void SubscribeQuickSlots()
    {
        if (quickSlots == null)
            return;

        foreach (InventorySlotUI slot in quickSlots)
        {
            if (slot == null)
                continue;

            // Subscribe to the new slot-aware events.
            slot.OnSlotClicked -= HandleQuickSlotClicked;
            slot.OnSlotClicked += HandleQuickSlotClicked;

            slot.OnRightClicked -= HandleQuickSlotRightClick;
            slot.OnRightClicked += HandleQuickSlotRightClick;
        }
    }

    private void UnsubscribeQuickSlots()
    {
        if (quickSlots == null)
            return;

        foreach (InventorySlotUI slot in quickSlots)
        {
            if (slot == null)
                continue;

            slot.OnSlotClicked -= HandleQuickSlotClicked;
            slot.OnRightClicked -= HandleQuickSlotRightClick;
        }
    }

    // =========================================================
    // REFRESH ALL INVENTORY UI
    // =========================================================

    public void Refresh()
    {
        if (!IsInventoryReady())
            return;

        RefreshQuickBar();

        RefreshCategory(
            ItemCategory.Item,
            itemContent,
            itemSlots
        );

        RefreshCategory(
            ItemCategory.PuzzlePiece,
            puzzleContent,
            puzzleSlots
        );

        RefreshCategory(
            ItemCategory.Gallery,
            galleryContent,
            gallerySlots
        );

        ValidateSelectedDescription();
        UpdateActionButtons();
    }

    // =========================================================
    // QUICK BAR
    // =========================================================

    private void RefreshQuickBar()
    {
        if (!IsInventoryReady())
            return;

        if (quickSlots == null)
        {
            Debug.LogError(
                "[INVENTORY UI] Quick Slots array is not assigned."
            );
            return;
        }

        for (int i = 0; i < quickSlots.Length; i++)
        {
            InventorySlotUI slot = quickSlots[i];

            if (slot == null)
            {
                Debug.LogError(
                    $"[INVENTORY UI] Quick slot {i + 1} is not assigned."
                );
                continue;
            }

            if (i >= PlayerInventory.QuickBarSlotCount)
            {
                slot.Clear();
                continue;
            }

            ItemData item = inventory.GetQuickBarItemAt(i);
            int quantity = inventory.GetQuickBarQuantityAt(i);
            int storageIndex = inventory.GetQuickBarStorageIndex(i);

            bool selected = inventory.SelectedSlot == i;

            slot.SetQuickBarItem(
                item,
                quantity,
                i,
                storageIndex,
                selected
            );
        }
    }

    private void HandleQuickSlotClicked(InventorySlotUI slot)
    {
        if (!IsInventoryReady() || slot == null)
            return;

        ItemData item = slot.GetItem();

        if (item == null)
            return;

        selectedItem = item;
        selectedQuickBarIndex = slot.QuickBarIndex;
        selectedStorageIndex = slot.StorageIndex;

        UpdateDescription(item);
        UpdateAllSelectionHighlights();
        UpdateActionButtons();
    }

    private void HandleQuickSlotRightClick(ItemData item)
    {
        if (!IsInventoryReady() || item == null)
            return;

        // Preserve the existing behavior: right-click unequips an item
        // without deleting it from storage.
        bool removed = inventory.RemoveItemFromQuickBar(item.itemId);

        if (!removed)
        {
            Debug.LogWarning(
                $"[INVENTORY UI] Could not unequip '{item.itemName}'."
            );
            return;
        }

        // Clear the selection only if it refers to the same item type.
        // Refresh will validate whether the selected storage slot remains.
        if (selectedItem != null
            && selectedItem.itemId == item.itemId)
        {
            ClearDescription();
        }

        Refresh();
    }

    // =========================================================
    // DYNAMIC CATEGORY SLOTS
    // =========================================================

    private void RefreshCategory(
        ItemCategory category,
        Transform content,
        List<InventorySlotUI> slots)
    {
        if (!IsInventoryReady() || content == null)
            return;

        if (slotPrefab == null)
        {
            Debug.LogWarning(
                "[INVENTORY UI] Dynamic Slot Prefab is not assigned."
            );
            return;
        }

        ClearSlots(content, slots);

        int count = inventory.GetStorageSlotCount();

        for (int i = 0; i < count; i++)
        {
            ItemData item = inventory.GetStorageItemAt(i);

            if (item == null || item.category != category)
                continue;

            InventorySlotUI slot = Instantiate(slotPrefab, content);

            int quantity = inventory.GetStorageQuantityAt(i);

            // Store the actual storage index in the UI slot.
            bool selected = i == selectedStorageIndex;

            slot.SetStorageItem(
                item,
                quantity,
                i,
                selected
            );

            slot.OnSlotClicked -= HandleInventorySlotClicked;
            slot.OnSlotClicked += HandleInventorySlotClicked;

            slots.Add(slot);
        }
    }

    private void HandleInventorySlotClicked(InventorySlotUI slot)
    {
        if (!IsInventoryReady() || slot == null)
            return;

        if (!slot.HasStorageIndex)
            return;

        ItemData item = slot.GetItem();

        if (item == null)
            return;

        // Select the exact stored item, not every item with the same ID.
        selectedItem = item;
        selectedStorageIndex = slot.StorageIndex;
        selectedQuickBarIndex = -1;

        UpdateDescription(item);
        UpdateAllSelectionHighlights();
        UpdateActionButtons();
    }

    private void ClearSlots(
        Transform content,
        List<InventorySlotUI> slots)
    {
        foreach (InventorySlotUI slot in slots)
        {
            if (slot == null)
                continue;


            slot.OnSlotClicked -= HandleInventorySlotClicked;

            Destroy(slot.gameObject);
        }

        slots.Clear();
    }

    private void ClearDynamicSlots()
    {
        if (itemContent != null)
            ClearSlots(itemContent, itemSlots);

        if (galleryContent != null)
            ClearSlots(galleryContent, gallerySlots);

        if (puzzleContent != null)
            ClearSlots(puzzleContent, puzzleSlots);
    }

    // =========================================================
    // SELECT AN ITEM / DISPLAY DESCRIPTION
    // =========================================================

    // Compatibility method for existing UI buttons or scripts
    // that call ShowDescription(ItemData).
    public void ShowDescription(ItemData item)
    {
        if (item == null)
            return;

        int foundIndex = -1;

        // If invoked without a specific slot, prefer the current selection
        // if it still contains this item type.
        if (IsInventoryReady()
            && selectedStorageIndex >= 0
            && selectedStorageIndex < inventory.GetStorageSlotCount())
        {
            ItemData existing =
                inventory.GetStorageItemAt(selectedStorageIndex);

            if (existing != null && existing.itemId == item.itemId)
                foundIndex = selectedStorageIndex;
        }

        // Otherwise, select the first matching storage entry.
        // Slot clicks use HandleInventorySlotClicked instead and are exact.
        if (foundIndex < 0 && IsInventoryReady())
        {
            for (int i = 0; i < inventory.GetStorageSlotCount(); i++)
            {
                ItemData stored = inventory.GetStorageItemAt(i);

                if (stored != null && stored.itemId == item.itemId)
                {
                    foundIndex = i;
                    break;
                }
            }
        }

        selectedItem = item;
        selectedStorageIndex = foundIndex;
        selectedQuickBarIndex = -1;

        UpdateDescription(item);
        UpdateAllSelectionHighlights();
        UpdateActionButtons();
    }

    private void UpdateDescription(ItemData item)
    {
        if (item == null)
            return;

        if (descriptionIcon != null)
        {
            descriptionIcon.sprite = item.icon;
            descriptionIcon.enabled = item.icon != null;
            descriptionIcon.preserveAspect = true;
        }

        if (descriptionName != null)
            descriptionName.text = item.itemName;

        if (descriptionText != null)
            descriptionText.text = item.description;

        if (descriptionQuantity != null)
        {
            int quantity = 1;

            if (IsInventoryReady() && selectedStorageIndex >= 0)
                quantity = inventory.GetStorageQuantityAt(selectedStorageIndex);

            descriptionQuantity.text = quantity > 1
                ? $"Quantity: {quantity}"
                : "Quantity: 1";
        }

        if (galleryPreviewPanel != null)
        {
            bool isPhoto = item.category == ItemCategory.Gallery;
            galleryPreviewPanel.SetActive(isPhoto);

            if (isPhoto && galleryPreviewImage != null)
            {
                galleryPreviewImage.sprite = item.icon;
                galleryPreviewImage.enabled = item.icon != null;
                galleryPreviewImage.preserveAspect = true;
            }
        }
    }

    private void UpdateAllSelectionHighlights()
    {
        UpdateCategorySelection(itemSlots);
        UpdateCategorySelection(puzzleSlots);
        UpdateCategorySelection(gallerySlots);

        if (quickSlots == null)
            return;

        foreach (InventorySlotUI slot in quickSlots)
        {
            if (slot == null)
                continue;

            bool selected = false;

            // If selected from a quickbar slot, highlight that exact slot.
            if (selectedQuickBarIndex >= 0)
            {
                selected = slot.QuickBarIndex == selectedQuickBarIndex;
            }
            // Otherwise highlight a quickbar entry pointing to the selected
            // storage index, not every entry sharing the same item ID.
            else if (selectedStorageIndex >= 0)
            {
                selected = slot.StorageIndex == selectedStorageIndex;
            }

            slot.SetSelected(selected);
        }
    }

    private void UpdateCategorySelection(
        List<InventorySlotUI> slots)
    {
        foreach (InventorySlotUI slot in slots)
        {
            if (slot == null)
                continue;

            bool isSelected =
                selectedStorageIndex >= 0
                && slot.StorageIndex == selectedStorageIndex;

            slot.SetSelected(isSelected);
        }
    }

    public void ClearDescription()
    {
        selectedItem = null;
        selectedStorageIndex = -1;
        selectedQuickBarIndex = -1;

        if (descriptionIcon != null)
        {
            descriptionIcon.sprite = null;
            descriptionIcon.enabled = false;
        }

        if (descriptionName != null)
            descriptionName.text = "";

        if (descriptionText != null)
            descriptionText.text = "";

        if (descriptionQuantity != null)
            descriptionQuantity.text = "";

        if (galleryPreviewPanel != null)
            galleryPreviewPanel.SetActive(false);

        if (galleryPreviewImage != null)
        {
            galleryPreviewImage.sprite = null;
            galleryPreviewImage.enabled = false;
        }

        UpdateAllSelectionHighlights();
        UpdateActionButtons();
    }

    // =========================================================
    // EQUIP / UNEQUIP / DELETE
    // =========================================================

    public void EquipSelectedItem()
    {
        if (!IsInventoryReady() || selectedItem == null)
            return;

        if (selectedItem.category == ItemCategory.Gallery)
        {
            Debug.Log(
                "[INVENTORY UI] Photographs cannot be equipped."
            );
            return;
        }

        // Existing PlayerInventory API accepts itemId, so this retains
        // the original behavior. See the note below about duplicate IDs.
        bool equipped =
            inventory.TryEquipItemToQuickBar(selectedItem.itemId);

        if (equipped)
            Refresh();
        else
            Debug.LogWarning(
                "[INVENTORY UI] Could not equip item. " +
                "The quickbar may be full or the item may not be stored."
            );
    }

    public void RemoveSelectedItemFromQuickBar()
    {
        if (!IsInventoryReady() || selectedItem == null)
            return;

        bool removed =
            inventory.RemoveItemFromQuickBar(selectedItem.itemId);

        if (removed)
        {
            selectedQuickBarIndex = -1;
            Refresh();
        }
        else
        {
            Debug.LogWarning(
                "[INVENTORY UI] This item is not in the quickbar."
            );
        }
    }

    public void DeleteSelectedItemCompletely()
    {
        if (!IsInventoryReady() || selectedItem == null)
            return;

        int itemId = selectedItem.itemId;

        // Existing PlayerInventory API deletes by item ID.
        // This preserves the existing API and behavior.
        bool deleted = inventory.RemoveItemCompletely(itemId);

        if (deleted)
        {
            ClearDescription();
            Refresh();
        }
        else
        {
            Debug.LogWarning(
                $"[INVENTORY UI] Could not delete item ID {itemId}."
            );
        }
    }

    private void UpdateActionButtons()
    {
        bool ready = IsInventoryReady();
        bool hasSelection =
            ready && selectedItem != null && selectedStorageIndex >= 0;

        bool isGalleryPhoto =
            hasSelection &&
            selectedItem.category == ItemCategory.Gallery;

        bool itemExists = false;

        if (hasSelection
            && selectedStorageIndex < inventory.GetStorageSlotCount())
        {
            ItemData stored =
                inventory.GetStorageItemAt(selectedStorageIndex);

            itemExists =
                stored != null &&
                stored.itemId == selectedItem.itemId;
        }

        if (equipButton != null)
        {
            equipButton.interactable =
                hasSelection &&
                itemExists &&
                !isGalleryPhoto &&
                inventory.Has(selectedItem) &&
                !inventory.IsItemInQuickBar(selectedItem.itemId);
        }

        if (removeButton != null)
        {
            removeButton.interactable =
                hasSelection &&
                itemExists &&
                inventory.IsItemInQuickBar(selectedItem.itemId);
        }

        if (deleteInventoryButton != null)
        {
            deleteInventoryButton.interactable =
                hasSelection &&
                itemExists &&
                inventory.Has(selectedItem);
        }
    }

    private void ValidateSelectedDescription()
    {
        if (selectedItem == null || !IsInventoryReady())
            return;

        bool validIndex =
            selectedStorageIndex >= 0 &&
            selectedStorageIndex < inventory.GetStorageSlotCount();

        if (!validIndex)
        {
            ClearDescription();
            return;
        }

        ItemData stored =
            inventory.GetStorageItemAt(selectedStorageIndex);

        // Validate the exact storage slot, not merely whether the
        // same ItemData asset exists somewhere else in the inventory.
        if (stored == null || stored.itemId != selectedItem.itemId)
        {
            ClearDescription();
            return;
        }

        UpdateDescription(stored);
        UpdateActionButtons();
    }

    // =========================================================
    // OPEN / CLOSE / TOGGLE INVENTORY
    // =========================================================

    public void OpenInventoryPanel()
    {
        if (inventoryPanel == null)
        {
            Debug.LogWarning(
                "[INVENTORY UI] Inventory Panel is not assigned."
            );
            return;
        }

        inventoryPanel.SetActive(true);
        Refresh();
    }

    public void CloseInventoryPanel()
    {
        if (inventoryPanel == null)
            return;

        inventoryPanel.SetActive(false);
        ClearDescription();
    }

    public void ToggleInventoryPanel()
    {
        if (inventoryPanel == null)
        {
            Debug.LogWarning(
                "[INVENTORY UI] Inventory Panel is not assigned."
            );
            return;
        }

        if (inventoryPanel.activeSelf)
            CloseInventoryPanel();
        else
            OpenInventoryPanel();
    }

    public bool IsInventoryOpen()
    {
        return inventoryPanel != null && inventoryPanel.activeSelf;
    }
}

