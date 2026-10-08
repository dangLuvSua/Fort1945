using Fusion;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerInventoryUI : MonoBehaviour
{
    // =========================================================
    // INVENTORY
    // =========================================================

    [Header("Inventory")]
    [SerializeField] private PlayerInventory inventory;


    // =========================================================
    // QUICK BAR
    // =========================================================

    [Header("Quick Bar")]
    [SerializeField] private InventorySlotUI[] quickSlots;


    // =========================================================
    // DYNAMIC INVENTORY
    // =========================================================

    [Header("Dynamic Inventory")]
    [Tooltip("Your Inventory Slot prefab.")]
    [SerializeField] private InventorySlotUI slotPrefab;


    // =========================================================
    // CATEGORY CONTENT
    // =========================================================

    [Header("Category Content")]

    [Tooltip("Content object inside Item Scroll View.")]
    [SerializeField] private Transform itemContent;

    [Tooltip("Content object inside Gallery Scroll View.")]
    [SerializeField] private Transform galleryContent;

    [Tooltip("Content object inside Puzzle Scroll View.")]
    [SerializeField] private Transform puzzleContent;


    // =========================================================
    // DESCRIPTION PANEL
    // =========================================================

    [Header("Description Panel")]

    [SerializeField] private Image descriptionIcon;

    [SerializeField] private TMP_Text descriptionName;

    [SerializeField] private TMP_Text descriptionText;

    [SerializeField] private TMP_Text descriptionQuantity;


    // =========================================================
    // FULL INVENTORY PANEL
    // =========================================================

    [Header("Full Inventory Panel")]

    [Tooltip("The FullInventoryPanel GameObject.")]
    [SerializeField] private GameObject inventoryPanel;

    [Tooltip("Should the inventory be open when the scene starts?")]
    [SerializeField] private bool openOnStart = false;


    // =========================================================
    // DYNAMIC SLOT LISTS
    // =========================================================

    private readonly List<InventorySlotUI> itemSlots = new();

    private readonly List<InventorySlotUI> gallerySlots = new();

    private readonly List<InventorySlotUI> puzzleSlots = new();


    // =========================================================
    // SELECTED DESCRIPTION ITEM
    // =========================================================

    private ItemData selectedItem;


    // =========================================================
    // LOCAL INVENTORY CONNECTION
    // =========================================================

    private bool inventorySubscribed = false;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        // Start with the correct inventory panel state.
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(openOnStart);
        }

        // Clear description when scene starts.
        ClearDescription();
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


    private void Update()
    {
        // Sometimes the Canvas enables before
        // the player's NetworkObject has spawned.
        //
        // Keep trying until we find the local player.

        if (inventory == null)
        {
            TryFindLocalInventory();

            SubscribeToInventory();
        }
    }


    // =========================================================
    // FIND LOCAL PLAYER INVENTORY
    // =========================================================

    private void TryFindLocalInventory()
    {
        if (inventory != null)
            return;

        NetworkRunner runner =
            FindAnyObjectByType<NetworkRunner>();

        if (runner == null)
            return;

        if (!runner.IsRunning)
            return;

        if (!runner.TryGetPlayerObject(
                runner.LocalPlayer,
                out NetworkObject playerObject))
        {
            return;
        }

        inventory =
            playerObject.GetComponent<PlayerInventory>();

        if (inventory != null)
        {
            Debug.Log(
                "[INVENTORY UI] Connected to local PlayerInventory."
            );
        }
    }


    // =========================================================
    // INVENTORY EVENT
    // =========================================================

    private void SubscribeToInventory()
    {
        if (inventory == null)
            return;

        if (inventorySubscribed)
            return;

        inventory.InventoryChanged += Refresh;

        inventorySubscribed = true;

        SubscribeQuickSlots();

        Refresh();
    }


    private void UnsubscribeFromInventory()
    {
        if (inventory == null)
            return;

        if (!inventorySubscribed)
            return;

        inventory.InventoryChanged -= Refresh;

        inventorySubscribed = false;
    }


    // =========================================================
    // QUICK SLOT EVENTS
    // =========================================================

    private void SubscribeQuickSlots()
    {
        if (quickSlots == null)
            return;

        for (int i = 0; i < quickSlots.Length; i++)
        {
            if (quickSlots[i] == null)
                continue;

            quickSlots[i].OnClicked += ShowDescription;
        }
    }


    private void UnsubscribeQuickSlots()
    {
        if (quickSlots == null)
            return;

        for (int i = 0; i < quickSlots.Length; i++)
        {
            if (quickSlots[i] == null)
                continue;

            quickSlots[i].OnClicked -= ShowDescription;
        }
    }


    // =========================================================
    // REFRESH EVERYTHING
    // =========================================================

    public void Refresh()
    {
        if (inventory == null)
            return;

        RefreshQuickBar();

        RefreshCategory(
            ItemCategory.Item,
            itemContent,
            itemSlots
        );

        RefreshCategory(
            ItemCategory.Gallery,
            galleryContent,
            gallerySlots
        );

        RefreshCategory(
            ItemCategory.PuzzlePiece,
            puzzleContent,
            puzzleSlots
        );

        ValidateSelectedDescription();
    }


    // =========================================================
    // QUICK BAR
    // =========================================================

    private void RefreshQuickBar()
    {
        if (quickSlots == null)
            return;

        for (int i = 0; i < quickSlots.Length; i++)
        {
            if (quickSlots[i] == null)
                continue;

            // More UI slots than actual inventory capacity.
            if (i >= PlayerInventory.SlotCount)
            {
                quickSlots[i].Clear();
                continue;
            }

            ItemData item =
                inventory.GetItemAt(i);

            bool selected =
                inventory.SelectedSlot == i;

            quickSlots[i].SetItem(
                item,
                selected
            );
        }
    }


    // =========================================================
    // DYNAMIC CATEGORY
    // =========================================================

    private void RefreshCategory(
        ItemCategory category,
        Transform content,
        List<InventorySlotUI> slots)
    {
        if (content == null)
            return;

        if (slotPrefab == null)
        {
            Debug.LogWarning(
                "[INVENTORY UI] Slot Prefab is not assigned."
            );

            return;
        }

        // Remove previously generated slots.
        ClearSlots(
            content,
            slots
        );


        // =====================================================
        // SEARCH NETWORKED INVENTORY
        // =====================================================

        for (int i = 0;
             i < PlayerInventory.SlotCount;
             i++)
        {
            ItemData item =
                inventory.GetItemAt(i);

            // Empty inventory slot.
            if (item == null)
                continue;

            // Item belongs to another category.
            if (item.category != category)
                continue;


            // =================================================
            // CREATE SLOT
            // =================================================

            InventorySlotUI slot =
                Instantiate(
                    slotPrefab,
                    content
                );


            // Set item visual.
            slot.SetItem(
                item,
                false
            );


            // When player clicks this slot,
            // show its description.
            slot.OnClicked += ShowDescription;


            // Remember generated slot.
            slots.Add(slot);
        }
    }


    // =========================================================
    // CLEAR DYNAMIC SLOTS
    // =========================================================

    private void ClearSlots(
        Transform content,
        List<InventorySlotUI> slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                continue;

            // Important:
            // Remove event subscription before destroying.
            slots[i].OnClicked -= ShowDescription;

            Destroy(
                slots[i].gameObject
            );
        }

        slots.Clear();
    }


    private void ClearDynamicSlots()
    {
        if (itemContent != null)
        {
            ClearSlots(
                itemContent,
                itemSlots
            );
        }

        if (galleryContent != null)
        {
            ClearSlots(
                galleryContent,
                gallerySlots
            );
        }

        if (puzzleContent != null)
        {
            ClearSlots(
                puzzleContent,
                puzzleSlots
            );
        }
    }


    // =========================================================
    // DESCRIPTION PANEL
    // =========================================================

    public void ShowDescription(ItemData item)
    {
        if (item == null)
        {
            ClearDescription();
            return;
        }

        selectedItem = item;


        // =====================================================
        // ICON
        // =====================================================

        if (descriptionIcon != null)
        {
            descriptionIcon.sprite =
                item.icon;

            descriptionIcon.enabled =
                item.icon != null;
        }


        // =====================================================
        // NAME
        // =====================================================

        if (descriptionName != null)
        {
            descriptionName.text =
                item.itemName;
        }


        // =====================================================
        // DESCRIPTION
        // =====================================================

        if (descriptionText != null)
        {
            descriptionText.text =
                item.description;
        }


        // =====================================================
        // QUANTITY
        // =====================================================

        if (descriptionQuantity != null)
        {
            // Your current inventory does not stack items.
            descriptionQuantity.text = "";
        }
    }


    // =========================================================
    // CLEAR DESCRIPTION
    // =========================================================

    public void ClearDescription()
    {
        selectedItem = null;


        if (descriptionIcon != null)
        {
            descriptionIcon.sprite = null;
            descriptionIcon.enabled = false;
        }


        if (descriptionName != null)
        {
            descriptionName.text = "";
        }


        if (descriptionText != null)
        {
            descriptionText.text = "";
        }


        if (descriptionQuantity != null)
        {
            descriptionQuantity.text = "";
        }
    }


    // =========================================================
    // CHECK SELECTED ITEM
    // =========================================================

    private void ValidateSelectedDescription()
    {
        if (selectedItem == null)
            return;

        if (!inventory.Has(selectedItem))
        {
            ClearDescription();
        }
    }


    // =========================================================
    // OPEN INVENTORY
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


    // =========================================================
    // CLOSE INVENTORY
    // =========================================================

    public void CloseInventoryPanel()
    {
        if (inventoryPanel == null)
            return;

        inventoryPanel.SetActive(false);

        ClearDescription();
    }


    // =========================================================
    // TOGGLE INVENTORY
    // =========================================================

    public void ToggleInventoryPanel()
    {
        if (inventoryPanel == null)
        {
            Debug.LogWarning(
                "[INVENTORY UI] Inventory Panel is not assigned."
            );

            return;
        }

        bool isOpen =
            inventoryPanel.activeSelf;

        if (isOpen)
        {
            CloseInventoryPanel();
        }
        else
        {
            OpenInventoryPanel();
        }
    }


    // =========================================================
    // IS OPEN?
    // =========================================================

    public bool IsInventoryOpen()
    {
        if (inventoryPanel == null)
            return false;

        return inventoryPanel.activeSelf;
    }
}