using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image icon;

    [SerializeField] private TMP_Text quantityText;

    [SerializeField] private TMP_Text itemNameText;

    [Tooltip("GameObject used for the selected/highlighted state.")]
    [SerializeField] private GameObject selectedObject;


    // =========================================================
    // DATA
    // =========================================================

    private ItemData currentItem;

    private Button button;


    // =========================================================
    // EVENTS
    // =========================================================

    /// <summary>
    /// Fired when the player clicks this inventory slot.
    /// The selected ItemData is passed to the inventory UI.
    /// </summary>
    public event Action<ItemData> OnClicked;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }

        Clear();
    }


    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }


    // =========================================================
    // SET ITEM
    // =========================================================

    /// <summary>
    /// Assigns an ItemData to this UI slot.
    /// </summary>
    public void SetItem(
        ItemData item,
        bool selected = false)
    {
        currentItem = item;

        // -----------------------------------------------------
        // EMPTY SLOT
        // -----------------------------------------------------

        if (item == null)
        {
            Clear();
            return;
        }


        // -----------------------------------------------------
        // ICON
        // -----------------------------------------------------

        if (icon != null)
        {
            icon.sprite = item.icon;

            // Hide the Image completely if there is no icon.
            icon.enabled = item.icon != null;

            // Make sure the icon does not retain an old color.
            icon.color = Color.white;
        }


        // -----------------------------------------------------
        // QUANTITY
        // -----------------------------------------------------

        if (quantityText != null)
        {
            // Currently items are not stackable.
            quantityText.text = "";
        }


        // -----------------------------------------------------
        // ITEM NAME
        // -----------------------------------------------------

        if (itemNameText != null)
        {
            itemNameText.text = item.itemName;
        }


        // -----------------------------------------------------
        // SELECTED STATE
        // -----------------------------------------------------

        SetSelected(selected);


        // -----------------------------------------------------
        // BUTTON
        // -----------------------------------------------------

        if (button != null)
        {
            button.interactable = true;
        }
    }


    // =========================================================
    // SELECTED STATE
    // =========================================================

    /// <summary>
    /// Changes only the selected/highlight state.
    /// Does not change the item.
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectedObject != null)
        {
            selectedObject.SetActive(
                selected && currentItem != null
            );
        }
    }


    // =========================================================
    // CLEAR
    // =========================================================

    /// <summary>
    /// Completely clears this slot.
    /// </summary>
    public void Clear()
    {
        currentItem = null;


        // -----------------------------------------------------
        // ICON
        // -----------------------------------------------------

        if (icon != null)
        {
            icon.sprite = null;

            // Completely hide the icon when empty.
            icon.enabled = false;
        }


        // -----------------------------------------------------
        // QUANTITY
        // -----------------------------------------------------

        if (quantityText != null)
        {
            quantityText.text = "";
        }


        // -----------------------------------------------------
        // ITEM NAME
        // -----------------------------------------------------

        if (itemNameText != null)
        {
            itemNameText.text = "";
        }


        // -----------------------------------------------------
        // SELECTED
        // -----------------------------------------------------

        if (selectedObject != null)
        {
            selectedObject.SetActive(false);
        }


        // -----------------------------------------------------
        // BUTTON
        // -----------------------------------------------------

        if (button != null)
        {
            button.interactable = false;
        }
    }


    // =========================================================
    // CLICK
    // =========================================================

    private void HandleClick()
    {
        // Do nothing if this slot is empty.
        if (currentItem == null)
            return;

        // Tell PlayerInventoryUI which item was clicked.
        OnClicked?.Invoke(currentItem);
    }


    // =========================================================
    // GET CURRENT ITEM
    // =========================================================

    /// <summary>
    /// Returns the ItemData currently displayed by this slot.
    /// </summary>
    public ItemData GetItem()
    {
        return currentItem;
    }


    // =========================================================
    // OPTIONAL
    // =========================================================

    /// <summary>
    /// Allows PlayerInventoryUI to enable/disable clicking.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable =
                interactable && currentItem != null;
        }
    }
}