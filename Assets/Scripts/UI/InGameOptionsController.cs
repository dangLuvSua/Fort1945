using UnityEngine;
using UnityEngine.InputSystem;

public class InGameOptionsController : MonoBehaviour
{
    public static InGameOptionsController Instance { get; private set; }

    [Header("Main Containers")]
    [SerializeField] private GameObject inGameSettingsRoot;

    [Header("Sub-Panels")]
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject graphicsPanel;
    [SerializeField] private GameObject leaveModalPanel;

    public bool IsAnyPanelOpen
    {
        get
        {
            return (audioPanel != null && audioPanel.activeSelf) ||
                   (controlsPanel != null && controlsPanel.activeSelf) ||
                   (graphicsPanel != null && graphicsPanel.activeSelf) ||
                   (leaveModalPanel != null && leaveModalPanel.activeSelf);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        CloseAllPanels();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsAnyPanelOpen)
            {
                CloseAllPanels();
            }
            else
            {
                // By default, pressing Esc toggles the main in-game settings bar/panel
                ToggleSettingsRoot();
            }
        }
    }

    public void ToggleSettingsRoot()
    {
        if (inGameSettingsRoot != null)
        {
            bool willOpen = !inGameSettingsRoot.activeSelf;
            inGameSettingsRoot.SetActive(willOpen);

            if (willOpen)
            {
                UnlockCursor();
            }
            else
            {
                CloseAllPanels();
                LockCursor();
            }
        }
    }

    // ==========================================
    // INDIVIDUAL BUTTON TRIGGERS
    // ==========================================

    public void OpenAudioPanel()
    {
        gameObject.SetActive(true);
        HideSubPanels();
        if (audioPanel != null) audioPanel.SetActive(true);
        UnlockCursor();
    }

    public void OpenControlsPanel()
    {
        gameObject.SetActive(true);
        HideSubPanels();
        if (controlsPanel != null) controlsPanel.SetActive(true);
        UnlockCursor();
    }

    public void OpenGraphicsPanel()
    {
        gameObject.SetActive(true);
        HideSubPanels();
        if (graphicsPanel != null) graphicsPanel.SetActive(true);
        UnlockCursor();
    }

    public void OpenLeaveModal()
    {
        gameObject.SetActive(true);
        HideSubPanels();
        if (leaveModalPanel != null) leaveModalPanel.SetActive(true);
        UnlockCursor();
    }

    // ==========================================
    // CLOSING & CURSOR
    // ==========================================

    public void CloseAllPanels()
    {
        HideSubPanels();
        
        // If settings bar is not open either, lock cursor
        if (inGameSettingsRoot == null || !inGameSettingsRoot.activeSelf)
        {
            LockCursor();
        }
    }

    private void HideSubPanels()
    {
        if (audioPanel != null) audioPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (graphicsPanel != null) graphicsPanel.SetActive(false);
        if (leaveModalPanel != null) leaveModalPanel.SetActive(false);
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
