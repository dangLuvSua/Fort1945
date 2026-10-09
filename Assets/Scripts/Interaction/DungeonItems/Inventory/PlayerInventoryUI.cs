
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
        // Retry when Fusion has not registered the local player yet.
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
        // Keep a valid reference only if it belongs to the local player.
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

            // Avoid duplicate event subscriptions.
            slot.OnClicked -= ShowDescription;
            slot.OnClicked += ShowDescription;

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

            slot.OnClicked -= ShowDescription;
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

        RefreshCategory(ItemCategory.Item, itemContent, itemSlots);
        RefreshCategory(ItemCategory.PuzzlePiece, puzzleContent, puzzleSlots);
        RefreshCategory(ItemCategory.Gallery, galleryContent, gallerySlots);

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
            slot.SetItem(item, inventory.SelectedSlot == i);

            Debug.Log(
                $"[INVENTORY UI] Quick slot {i + 1}: " +
                $"Item={(item != null ? item.itemName : "EMPTY")}, " +
                $"ItemID={(item != null ? item.itemId : 0)}"
            );
        }
    }

    private void HandleQuickSlotRightClick(ItemData item)
    {
        if (!IsInventoryReady() || item == null)
            return;

        // Unequip only; the stored item remains in inventory.
        bool removed = inventory.RemoveItemFromQuickBar(item.itemId);

        if (!removed)
        {
            Debug.LogWarning(
                $"[INVENTORY UI] Could not unequip '{item.itemName}'."
            );
            return;
        }

        if (selectedItem != null && selectedItem.itemId == item.itemId)
            ClearDescription();

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

            slot.SetItem(item, false);
            slot.OnClicked += ShowDescription;

            slots.Add(slot);
        }
    }

    private void ClearSlots(
        Transform content,
        List<InventorySlotUI> slots)
    {
        foreach (InventorySlotUI slot in slots)
        {
            if (slot == null)
                continue;

            slot.OnClicked -= ShowDescription;
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


    public void ShowDescription(ItemData item)
    {
        if (item == null)
            return;

        selectedItem = item;

        // Update quickbar selection highlights.
        if (quickSlots != null)
        {
            foreach (InventorySlotUI slot in quickSlots)
            {
                if (slot == null)
                    continue;

                ItemData slotItem = slot.GetItem();

                slot.SetSelected(
                    slotItem != null &&
                    slotItem.itemId == item.itemId
                );
            }
        }

        // Update full inventory selection highlights.
        UpdateCategorySelection(itemSlots);
        UpdateCategorySelection(puzzleSlots);
        UpdateCategorySelection(gallerySlots);

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
            descriptionQuantity.text = "";

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

        UpdateActionButtons();
    }

    private void UpdateCategorySelection(
        List<InventorySlotUI> slots)
    {
        foreach (InventorySlotUI slot in slots)
        {
            if (slot == null)
                continue;

            ItemData slotItem = slot.GetItem();

            bool isSelected =
                selectedItem != null &&
                slotItem != null &&
                slotItem.itemId == selectedItem.itemId;

            slot.SetSelected(isSelected);
        }
    }
    public void ClearDescription()
    {
        selectedItem = null;

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
            Refresh();
        else
            Debug.LogWarning(
                "[INVENTORY UI] This item is not in the quickbar."
            );
    }

    public void DeleteSelectedItemCompletely()
    {
        if (!IsInventoryReady() || selectedItem == null)
            return;

        int itemId = selectedItem.itemId;

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
        bool hasSelection = ready && selectedItem != null;

        bool isGalleryPhoto =
            hasSelection &&
            selectedItem.category == ItemCategory.Gallery;

        if (equipButton != null)
        {
            equipButton.interactable =
                hasSelection &&
                !isGalleryPhoto &&
                inventory.Has(selectedItem) &&
                !inventory.IsItemInQuickBar(selectedItem.itemId);
        }

        if (removeButton != null)
        {
            removeButton.interactable =
                hasSelection &&
                inventory.IsItemInQuickBar(selectedItem.itemId);
        }

        if (deleteInventoryButton != null)
        {
            deleteInventoryButton.interactable =
                hasSelection &&
                inventory.Has(selectedItem);
        }
    }

    private void ValidateSelectedDescription()
    {
        if (selectedItem == null || !IsInventoryReady())
            return;

        if (!inventory.Has(selectedItem))
            ClearDescription();
        else
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