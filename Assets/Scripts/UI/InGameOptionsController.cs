using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

    [Header("Menu Backdrop")]
    [SerializeField, Range(0f, 1f)] private float menuDimOpacity = 0.55f;
    [SerializeField] private VolumeProfile menuBlurProfile;

    private Canvas optionsCanvas;
    private GraphicRaycaster optionsRaycaster;
    private Image menuBackdrop;
    private Volume menuBlurVolume;
    private UniversalAdditionalCameraData blurredCamera;
    private bool previousPostProcessing;
    private LayerMask previousVolumeMask;

    public bool IsAnyPanelOpen
    {
        get
        {
            return (audioPanel != null && audioPanel.activeInHierarchy) ||
                   (controlsPanel != null && controlsPanel.activeInHierarchy) ||
                   (graphicsPanel != null && graphicsPanel.activeInHierarchy) ||
                   (leaveModalPanel != null && leaveModalPanel.activeInHierarchy);
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
        optionsCanvas = GetComponent<Canvas>();
        optionsRaycaster = GetComponent<GraphicRaycaster>();
        InitializeLeaveModal();
        InitializeMenuBackdrop();
        // Bind Back to this live canvas instance, rather than an inherited prefab target.
        Button backToGame = transform.Find("BackTxt")?.GetComponent<Button>();
        if (backToGame != null)
        {
            backToGame.onClick = new Button.ButtonClickedEvent();
            backToGame.onClick.AddListener(CloseAllPanels);
        }
        HideSubPanels();
        SetCanvasVisible(false);
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
            // The HUD bar starts visible; cursor ownership determines whether it is open.
            bool willOpen = Cursor.lockState == CursorLockMode.Locked;
            inGameSettingsRoot.SetActive(willOpen);

            if (willOpen)
            {
                UnlockCursor();
            }
            else
            {
                CloseAllPanels();
            }
        }
    }

    // ==========================================
    // INDIVIDUAL BUTTON TRIGGERS
    // ==========================================

    public void OpenAudioPanel()
    {
        OpenPanel(audioPanel);
    }

    public void OpenControlsPanel()
    {
        OpenPanel(controlsPanel);
    }

    public void OpenGraphicsPanel()
    {
        OpenPanel(graphicsPanel);
        if (graphicsPanel == null) return;
        TMP_InputField playerName = graphicsPanel.GetComponentInChildren<TMP_InputField>(true);
        if (playerName != null) playerName.text = PlayerProfile.PlayerName;
    }

    public void OpenLeaveModal()
    {
        OpenPanel(leaveModalPanel);
    }

    private void OpenPanel(GameObject panel)
    {
        if (panel == null) return;
        HideSubPanels();
        SetCanvasVisible(true);
        panel.SetActive(true);
        UnlockCursor();
    }

    public void SavePlayerName()
    {
        TMP_InputField playerName = graphicsPanel != null
            ? graphicsPanel.GetComponentInChildren<TMP_InputField>(true) : null;
        if (playerName != null)
        {
            string value = playerName.text.Trim();
            PlayerProfile.SetPlayerName(string.IsNullOrEmpty(value) ? "Player" : value);
        }
        CloseAllPanels();
    }

    private void InitializeLeaveModal()
    {
        if (leaveModalPanel == null) return;
        // The prefab supplies a template; SetActive on the asset cannot display UI.
        if (!leaveModalPanel.scene.IsValid())
            leaveModalPanel = Instantiate(leaveModalPanel, transform, false);

        Transform dialog = leaveModalPanel.transform.Find("DialogPanel");
        if (dialog == null) return;
        TMP_Text title = dialog.Find("Title Text")?.GetComponent<TMP_Text>();
        TMP_Text message = dialog.Find("DescriptionTxt")?.GetComponent<TMP_Text>();
        if (title != null) title.text = "LEAVE GAME?";
        if (message != null) message.text = "Are you sure you want to leave this game?";
        Button cancel = dialog.Find("CancelBtn")?.GetComponent<Button>();
        Button confirm = dialog.Find("ConfirmBtn")?.GetComponent<Button>();
        if (cancel != null)
        {
            cancel.onClick = new Button.ButtonClickedEvent();
            cancel.onClick.AddListener(CloseAllPanels);
        }
        if (confirm != null)
        {
            confirm.onClick = new Button.ButtonClickedEvent();
            confirm.onClick.AddListener(ConfirmLeaveGame);
        }
    }

    private void ConfirmLeaveGame()
    {
        if (NetworkRunnerHandler.Instance != null)
            NetworkRunnerHandler.Instance.Disconnect();
        SceneManager.LoadScene("MainMenu");
    }

    // ==========================================
    // CLOSING & CURSOR
    // ==========================================

    public void CloseAllPanels()
    {
        // Close expanded dropdowns and their generated blocker/list objects too.
        foreach (TMP_Dropdown dropdown in GetComponentsInChildren<TMP_Dropdown>(true))
        {
            if (dropdown.IsExpanded) dropdown.Hide();
        }
        HideSubPanels();
        SetCanvasVisible(false);
        // The HUD settings buttons can remain visible while gameplay owns the cursor.
        LockCursor();
    }

    private void SetCanvasVisible(bool visible)
    {
        if (optionsCanvas != null) optionsCanvas.enabled = visible;
        if (optionsRaycaster != null) optionsRaycaster.enabled = visible;
        // Keep this controller enabled while the canvas is hidden so it can
        // still listen for Escape and reopen the settings menu.
        if (visible) EnableMenuBlur();
        else DisableMenuBlur();
    }

    private void InitializeMenuBackdrop()
    {
        GameObject backdrop = new GameObject("Menu Backdrop",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backdrop.layer = gameObject.layer;
        backdrop.transform.SetParent(transform, false);
        backdrop.transform.SetAsFirstSibling();

        menuBackdrop = backdrop.GetComponent<Image>();
        menuBackdrop.color = new Color(0f, 0f, 0f, menuDimOpacity);
        menuBackdrop.raycastTarget = true;
        RectTransform rect = menuBackdrop.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        menuBlurVolume = backdrop.AddComponent<Volume>();
        menuBlurVolume.isGlobal = true;
        menuBlurVolume.priority = 10000f;
        menuBlurVolume.sharedProfile = menuBlurProfile;
        menuBlurVolume.enabled = false;
    }

    private void EnableMenuBlur()
    {
        menuBackdrop.color = new Color(0f, 0f, 0f, menuDimOpacity);
        // Switching between options panels must not overwrite the original camera state.
        if (blurredCamera != null || menuBlurProfile == null) return;
        Camera camera = Camera.main;
        if (camera == null) return;

        blurredCamera = camera.GetUniversalAdditionalCameraData();
        previousPostProcessing = blurredCamera.renderPostProcessing;
        previousVolumeMask = blurredCamera.volumeLayerMask;
        blurredCamera.renderPostProcessing = true;
        blurredCamera.volumeLayerMask = previousVolumeMask.value |
            (1 << menuBlurVolume.gameObject.layer);
        menuBlurVolume.enabled = true;
    }

    private void OnDisable()
    {
        DisableMenuBlur();
    }

    private void DisableMenuBlur()
    {
        if (menuBlurVolume != null) menuBlurVolume.enabled = false;
        if (blurredCamera != null)
        {
            blurredCamera.renderPostProcessing = previousPostProcessing;
            blurredCamera.volumeLayerMask = previousVolumeMask;
        }
        blurredCamera = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
