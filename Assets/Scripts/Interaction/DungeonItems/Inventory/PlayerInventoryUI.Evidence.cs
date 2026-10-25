using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class PlayerInventoryUI
{
    [Header("Evidence Board Gallery Selection")]
    [SerializeField] private InventoryTabController evidenceTabs;
    [SerializeField] private Button selectPhotographButton;
    [SerializeField] private TMP_Text evidenceFeedback;
    private EvidenceBoardManager pendingBoard;
    private int pendingPolaroid = -1;
    private bool placementSubmitted;
    private float evidenceSubmitTime;
    private CursorLockMode previousEvidenceCursor;
    private bool previousEvidenceCursorVisible;
    public static bool EvidenceSelectionOpen { get; private set; }

    public void BeginEvidencePlacement(EvidenceBoardManager board, int slot)
    {
        if (!IsInventoryReady() || board == null || !board.Ready || slot < 0)
        {
            Debug.LogWarning($"[EVIDENCE] Cannot open Gallery: inventoryReady={IsInventoryReady()}, boardReady={board != null && board.Ready}, slot={slot}.", this);
            return;
        }
        if (board.IsPlaced(slot)) return;
        if (!EnsureEvidenceGalleryControls()) return;
        CancelEvidencePlacement();
        previousEvidenceCursor = Cursor.lockState;
        previousEvidenceCursorVisible = Cursor.visible;
        OpenInventoryPanel();
        evidenceTabs.OpenGalleryTab();
        // Keep the local selection context correct even if the controller's
        // initial lifecycle callback ran during this first panel activation.
        SetGalleryTabActive(true);
        ClearDescription();
        pendingBoard = board;
        pendingPolaroid = slot;
        EvidenceSelectionOpen = true;
        selectPhotographButton.onClick.RemoveListener(SelectEvidencePhotograph);
        selectPhotographButton.onClick.AddListener(SelectEvidencePhotograph);
        if (evidenceFeedback != null) evidenceFeedback.text = "";
        UpdateEvidenceSelection();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private bool EnsureEvidenceGalleryControls()
    {
        if (inventoryPanel == null)
        {
            Debug.LogWarning("[EVIDENCE] Assign Full Inventory Panel on PlayerInventoryUI.", this);
            return false;
        }
        if (evidenceTabs == null)
            evidenceTabs = inventoryPanel.GetComponentInChildren<InventoryTabController>(true);
        if (evidenceTabs == null || evidenceTabs.GalleryPanel == null)
        {
            Debug.LogWarning("[EVIDENCE] Assign Evidence Tabs and its Gallery Panel on the current scene's inventory.", this);
            return false;
        }

        GameObject gallery = evidenceTabs.GalleryPanel;
        if (selectPhotographButton == null)
            foreach (Button button in gallery.GetComponentsInChildren<Button>(true))
                if (button.name == "SelectPhotograph") { selectPhotographButton = button; break; }
        if (evidenceFeedback == null)
            foreach (TMP_Text label in gallery.GetComponentsInChildren<TMP_Text>(true))
                if (label.name == "EvidenceFeedback") { evidenceFeedback = label; break; }

        // Older scenes share the inventory design but do not yet contain the
        // matching controls. Add only these controls to their existing Gallery.
        if (selectPhotographButton == null)
        {
            GameObject root = new GameObject("SelectPhotograph", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            root.transform.SetParent(gallery.transform, false);
            root.GetComponent<LayoutElement>().ignoreLayout = true;
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 15f);
            rect.sizeDelta = new Vector2(180f, 44f);
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.22f, 0.15f, 0.09f, 1f);
            selectPhotographButton = root.GetComponent<Button>();
            selectPhotographButton.targetGraphic = background;
            TMP_Text label = CreateEvidenceLabel(root.transform, "Text (TMP)");
            label.text = "SELECT";
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            root.SetActive(false);
        }
        if (evidenceFeedback == null)
        {
            evidenceFeedback = CreateEvidenceLabel(gallery.transform, "EvidenceFeedback");
            RectTransform rect = evidenceFeedback.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(10f, 65f);
            rect.offsetMax = new Vector2(-10f, 110f);
        }
        return true;
    }

    private TMP_Text CreateEvidenceLabel(Transform parent, string objectName)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().ignoreLayout = true;
        TMP_Text label = root.GetComponent<TextMeshProUGUI>();
        if (descriptionText != null) label.font = descriptionText.font;
        label.fontSize = 20f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = "";
        return label;
    }

    public void CancelEvidencePlacement()
    {
        bool wasPending = pendingPolaroid >= 0;
        pendingBoard = null;
        pendingPolaroid = -1;
        placementSubmitted = false;
        if (selectPhotographButton != null)
        {
            selectPhotographButton.onClick.RemoveListener(SelectEvidencePhotograph);
            selectPhotographButton.gameObject.SetActive(false);
        }
        if (evidenceFeedback != null) evidenceFeedback.text = "";
        if (!wasPending) return;
        EvidenceSelectionOpen = false;
        Cursor.lockState = previousEvidenceCursor;
        Cursor.visible = previousEvidenceCursorVisible;
    }

    private void UpdateEvidenceSelection()
    {
        // Bind scene-authored controls before the first placement interaction,
        // so an initially active SELECT button is hidden during normal browsing.
        if (selectPhotographButton == null && inventoryPanel != null)
        {
            foreach (Button button in inventoryPanel.GetComponentsInChildren<Button>(true))
                if (button.name == "SelectPhotograph") { selectPhotographButton = button; break; }
        }
        if (selectPhotographButton == null) return;
        bool hasSelectedPhoto = pendingBoard != null && pendingBoard.Ready &&
            pendingPolaroid >= 0 && galleryTabActive && IsInventoryOpen() &&
            IsInventoryReady() && pendingBoard.ResolveCollectedPhoto(inventory, selectedItem) >= 0;
        selectPhotographButton.gameObject.SetActive(hasSelectedPhoto);
        selectPhotographButton.interactable = hasSelectedPhoto && !placementSubmitted;
    }

    private void SelectEvidencePhotograph()
    {
        if (pendingBoard == null || !IsInventoryReady() || placementSubmitted) return;
        int photo = pendingBoard.ResolveCollectedPhoto(inventory, selectedItem);
        if (photo < 0) return;
        if (!pendingBoard.Matches(pendingPolaroid, photo))
        {
            if (evidenceFeedback != null) evidenceFeedback.text = "Incorrect photograph. Try another.";
            return;
        }
        placementSubmitted = true;
        evidenceSubmitTime = Time.unscaledTime;
        pendingBoard.Submit(pendingPolaroid, photo);
        UpdateEvidenceSelection();
    }

    private void LateUpdate()
    {
        if (pendingPolaroid < 0) return;
        if (pendingBoard == null || !pendingBoard.Ready || !IsInventoryReady() ||
            !IsInventoryOpen() || !galleryTabActive)
        { CancelEvidencePlacement(); return; }
        if (pendingBoard.IsPlaced(pendingPolaroid))
        { CloseInventoryPanel(); return; }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        { CloseInventoryPanel(); return; }
        if (placementSubmitted && Time.unscaledTime - evidenceSubmitTime > 8f)
        {
            placementSubmitted = false;
            if (evidenceFeedback != null)
                evidenceFeedback.text = "Placement not confirmed. Please try again.";
        }
        UpdateEvidenceSelection();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
