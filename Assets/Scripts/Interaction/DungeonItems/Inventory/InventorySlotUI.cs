
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text itemNameText;

    [Tooltip("GameObject used for the selected/highlighted state.")]
    [SerializeField] private GameObject selectedObject;

    private ItemData currentItem;
    private Button button;

    // Identifies the exact inventory location occupied by this item.
    // -1 means the slot has not been assigned a storage index.
    private int storageIndex = -1;

    // Identifies the quickbar location when this UI represents a quickbar slot.
    private int quickBarIndex = -1;

    public event Action<ItemData> OnClicked;
    public event Action<ItemData> OnRightClicked;

    // New events provide the exact clicked UI slot.
    // Existing event subscribers can continue using the original events.
    public event Action<InventorySlotUI> OnSlotClicked;
    public event Action<InventorySlotUI> OnSlotRightClicked;

    public int StorageIndex => storageIndex;
    public int QuickBarIndex => quickBarIndex;

    public bool HasStorageIndex => storageIndex >= 0;
    public bool IsQuickBarSlot => quickBarIndex >= 0;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(HandleClick);

        Clear();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    // Compatibility overload for existing calls:
    // slot.SetItem(item, selected)
    public void SetItem(ItemData item, bool selected = false)
    {
        SetItem(item, 1, selected);
    }

    // Compatibility overload for existing quantity-based calls:
    // slot.SetItem(item, quantity, selected)
    public void SetItem(
        ItemData item,
        int quantity,
        bool selected = false)
    {
        SetItemInternal(item, quantity, selected);
    }

    // Use for a full-inventory slot.
    // storageIndex must be the actual index in PlayerInventory.
    public void SetStorageItem(
        ItemData item,
        int quantity,
        int storageSlotIndex,
        bool selected = false)
    {
        storageIndex = storageSlotIndex;
        quickBarIndex = -1;

        SetItemInternal(item, quantity, selected);
    }

    // Use for a quickbar slot.
    public void SetQuickBarItem(
        ItemData item,
        int quantity,
        int quickbarSlotIndex,
        int correspondingStorageIndex,
        bool selected = false)
    {
        storageIndex = correspondingStorageIndex;
        quickBarIndex = quickbarSlotIndex;

        SetItemInternal(item, quantity, selected);
    }

    private void SetItemInternal(
        ItemData item,
        int quantity,
        bool selected)
    {
        currentItem = item;

        if (item == null)
        {
            Clear();
            return;
        }

        if (icon != null)
        {
            icon.sprite = item.icon;
            icon.enabled = item.icon != null;
            icon.color = Color.white;
            icon.preserveAspect = true;
        }
        else
        {
            Debug.LogError(
                $"[SLOT UI] Icon Image is not assigned on " +
                $"'{gameObject.name}'. Item='{item.itemName}'."
            );
        }

        if (quantityText != null)
        {
            quantityText.text = quantity > 1
                ? $"x{quantity}"
                : "";
        }

        if (itemNameText != null)
            itemNameText.text = item.itemName;

        SetSelected(selected);

        if (button != null)
            button.interactable = true;
    }

    private void HandleClick()
    {
        if (currentItem == null)
            return;

        // Preserve existing event behavior.
        OnClicked?.Invoke(currentItem);

        // Also report the exact UI slot that was clicked.
        OnSlotClicked?.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentItem == null || eventData == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Preserve existing event behavior.
            OnRightClicked?.Invoke(currentItem);

            // Also report the exact UI slot.
            OnSlotRightClicked?.Invoke(this);
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedObject != null)
            selectedObject.SetActive(selected && currentItem != null);
    }

    public void Clear()
    {
        currentItem = null;

        // Reset slot identity so a reused UI slot cannot retain stale data.
        storageIndex = -1;
        quickBarIndex = -1;

        if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }

        if (quantityText != null)
            quantityText.text = "";

        if (itemNameText != null)
            itemNameText.text = "";

        if (selectedObject != null)
            selectedObject.SetActive(false);

        if (button != null)
            button.interactable = false;
    }

    public ItemData GetItem()
    {
        return currentItem;
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable && currentItem != null;
    }
}

