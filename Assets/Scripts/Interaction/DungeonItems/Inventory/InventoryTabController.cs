using UnityEngine;
using UnityEngine.UI;

public class InventoryTabController : MonoBehaviour
{
    [Header("Tab Buttons")]
    [SerializeField] private Button itemButton;
    [SerializeField] private Button galleryButton;
    [SerializeField] private Button puzzleButton;

    [Header("Category Panels")]
    [SerializeField] private GameObject itemPanel;
    [SerializeField] private GameObject galleryPanel;
    [SerializeField] private GameObject puzzlePanel;

    [Header("Default Tab")]
    [SerializeField] private Tab defaultTab = Tab.Item;

    public enum Tab
    {
        Item,
        Gallery,
        Puzzle
    }

    private void Awake()
    {
        if (itemButton != null)
            itemButton.onClick.AddListener(OpenItemTab);

        if (galleryButton != null)
            galleryButton.onClick.AddListener(OpenGalleryTab);

        if (puzzleButton != null)
            puzzleButton.onClick.AddListener(OpenPuzzleTab);
    }

    private void Start()
    {
        OpenTab(defaultTab);
    }

    private void OnDestroy()
    {
        if (itemButton != null)
            itemButton.onClick.RemoveListener(OpenItemTab);

        if (galleryButton != null)
            galleryButton.onClick.RemoveListener(OpenGalleryTab);

        if (puzzleButton != null)
            puzzleButton.onClick.RemoveListener(OpenPuzzleTab);
    }

    // =========================================================
    // OPEN TABS
    // =========================================================

    public void OpenItemTab()
    {
        OpenTab(Tab.Item);
    }

    public void OpenGalleryTab()
    {
        OpenTab(Tab.Gallery);
    }

    public void OpenPuzzleTab()
    {
        OpenTab(Tab.Puzzle);
    }

    // =========================================================
    // TAB CONTROLLER
    // =========================================================

    public void OpenTab(Tab tab)
    {
        if (itemPanel != null)
            itemPanel.SetActive(tab == Tab.Item);

        if (galleryPanel != null)
            galleryPanel.SetActive(tab == Tab.Gallery);

        if (puzzlePanel != null)
            puzzlePanel.SetActive(tab == Tab.Puzzle);
    }
}