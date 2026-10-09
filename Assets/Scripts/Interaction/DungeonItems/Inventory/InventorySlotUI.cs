
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

    public event Action<ItemData> OnClicked;
    public event Action<ItemData> OnRightClicked;

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

    public void SetItem(ItemData item, bool selected = false)
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

            Debug.Log(
                $"[SLOT UI] Slot='{gameObject.name}', " +
                $"Item='{item.itemName}', " +
                $"Image='{icon.name}', " +
                $"Sprite='{(icon.sprite != null ? icon.sprite.name : "NULL")}', " +
                $"Enabled={icon.enabled}, " +
                $"Active={icon.gameObject.activeInHierarchy}"
            );
        }
        else
        {
            Debug.LogError(
                $"[SLOT UI] Icon Image is not assigned on '{gameObject.name}'. " +
                $"Item='{item.itemName}'."
            );
        }

        if (quantityText != null)
            quantityText.text = "";

        if (itemNameText != null)
            itemNameText.text = item.itemName;

        SetSelected(selected);

        if (button != null)
            button.interactable = true;
    }

    // Left-click is handled by the Button's onClick event.
    private void HandleClick()
    {
        if (currentItem == null)
            return;

        OnClicked?.Invoke(currentItem);
    }

    // Right-click is used by the quickbar to unequip an item.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentItem == null || eventData == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            OnRightClicked?.Invoke(currentItem);
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