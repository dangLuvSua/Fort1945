using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;

public class CanvasManager : MonoBehaviour
{
    public static CanvasManager Instance { get; private set; }

    [Header("Confirmation / Information Modal")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private TMP_Text modalTitle;
    [SerializeField] private TMP_Text modalMessage;
    [SerializeField] private Button modalConfirmButton;
    [SerializeField] private TMP_Text modalConfirmLabel;
    [SerializeField] private Button modalCancelButton;
    private Image modalBackdrop;
    // =====================================================
    // MAIN CANVASES
    // =====================================================

    [Header("Main Canvases")]
    [SerializeField] private GameObject mainMenuCanvas;
    [SerializeField] private GameObject multiplayerCanvas;
    [SerializeField] private GameObject optionsCanvas;
    [SerializeField] private GameObject creditsCanvas;


    // =====================================================
    // CHARACTER SELECTION
    // =====================================================

    [Header("Character Selection")]
    [SerializeField] private GameObject characterSelectionCanvas;
    [SerializeField] private TMP_InputField characterSelectionPlayerNameInput;


    // =====================================================
    // CHARACTER SELECTION OBJECTS
    // =====================================================

    [Header("Character Selection Objects")]
    [SerializeField] private GameObject characterDisplay;
    [SerializeField] private GameObject prisonStatue;


    // =====================================================
    // CHARACTER SELECTION CAMERAS
    // =====================================================

    [Header("Character Selection Cameras")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera characterCamera;


    // =====================================================
    // MULTIPLAYER PANELS
    // =====================================================

    [Header("Multiplayer Panels")]
    [SerializeField] private GameObject multiplayerPanel;
    [SerializeField] private GameObject createLobbyPanel;
    [SerializeField] private GameObject joinLobbyPanel;
    [SerializeField] private GameObject findLobbyPanel;


    // =====================================================
    // OPTIONS PANELS
    // =====================================================

    [Header("Options Panels")]
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject graphicsPanel;
    [SerializeField] private GameObject controlsPanel;


    // =====================================================
    // PLAYER PROFILE
    // =====================================================

    [Header("Player Profile")]
    [SerializeField] private TMP_InputField playerNameInput;

    [Header("Multiplayer Player Name")]
    [SerializeField] private TMP_InputField multiplayerPlayerNameInput;


    // =====================================================
    // UI CLICK AUDIO
    // =====================================================

    [Header("UI Click Audio")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip clickSound;

    [SerializeField]
    [Range(0f, 1f)]
    private float clickVolume = 0.7f;


    // =====================================================
    // GAME SCENE
    // =====================================================

    [Header("Game Scene")]
    [SerializeField] private string gameSceneName = "GameScene";


    // =====================================================
    // UNITY START
    // =====================================================

    private void Start()
    {
        Instance = this;
        if (modalRoot != null) modalRoot.SetActive(false);
        SetCharacterSelectionCamera(false);
        SetCharacterSelectionObjects(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ShowInfo(string title, string message)
    {
        ShowModal(title, message, "OK", null, false);
    }

    public void ShowConfirmation(string title, string message, string confirmText, UnityAction onConfirm)
    {
        ShowModal(title, message, confirmText, onConfirm, true);
    }

    public void ConfirmQuitGame()
    {
        ShowConfirmation("QUIT GAME?", "Are you sure you want to quit?", "QUIT", QuitGame);
    }

    public void ConfirmLeaveLobby()
    {
        ShowConfirmation("LEAVE LOBBY?", "Are you sure you want to leave this lobby?", "LEAVE", () =>
        {
            if (NetworkRunnerHandler.Instance != null) NetworkRunnerHandler.Instance.Disconnect();
            SceneManager.LoadScene("MainMenu");
        });
    }

    private void QuitGame()
    {
        Application.Quit();
    }

    private void ShowModal(string title, string message, string confirmText, UnityAction onConfirm, bool confirmation)
    {
        if (modalRoot == null || modalTitle == null || modalMessage == null || modalConfirmButton == null || modalCancelButton == null)
        {
            Debug.LogWarning("CanvasManager: Assign the modal root, title, message, confirm button, and cancel button in the Inspector.");
            return;
        }

        modalTitle.text = title;
        modalMessage.text = message;
        if (modalConfirmLabel != null) modalConfirmLabel.text = confirmText;
        modalCancelButton.gameObject.SetActive(confirmation);
        modalConfirmButton.onClick.RemoveAllListeners();
        modalConfirmButton.onClick.AddListener(() => { CloseModal(); onConfirm?.Invoke(); });
        modalCancelButton.onClick.RemoveAllListeners();
        modalCancelButton.onClick.AddListener(CloseModal);
        Canvas modalCanvas = modalRoot.GetComponent<Canvas>();
        if (modalCanvas == null) modalCanvas = modalRoot.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = short.MaxValue;
        modalRoot.GetComponent<RectTransform>().localScale = Vector3.one;
        if (modalRoot.GetComponent<GraphicRaycaster>() == null) modalRoot.AddComponent<GraphicRaycaster>();
        EnsureModalBackdrop();
        modalRoot.transform.SetAsLastSibling();
        modalRoot.SetActive(true);
    }

    private void EnsureModalBackdrop()
    {
        if (modalBackdrop == null)
        {
            Transform existing = modalRoot.transform.Find("Modal Backdrop");
            if (existing != null) modalBackdrop = existing.GetComponent<Image>();
        }

        if (modalBackdrop == null)
        {
            GameObject backdrop = new GameObject("Modal Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(modalRoot.transform, false);
            modalBackdrop = backdrop.GetComponent<Image>();
        }

        RectTransform backdropRect = modalBackdrop.rectTransform;
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        modalBackdrop.color = new Color(0f, 0f, 0f, 0.88f);
        modalBackdrop.raycastTarget = true;
        modalBackdrop.transform.SetAsFirstSibling();
    }

    private void CloseModal()
    {
        if (modalRoot != null) modalRoot.SetActive(false);
    }


    // =====================================================
    // CLICK SOUND
    // =====================================================

    private void PlayClickSound()
    {
        if (uiAudioSource != null && clickSound != null)
        {
            uiAudioSource.PlayOneShot(
                clickSound,
                clickVolume
            );
        }
    }


    // =====================================================
    // PLAY GAME
    // =====================================================

    public void PlayGame()
    {
        StartCoroutine(LoadGameAfterClick());
    }


    private IEnumerator LoadGameAfterClick()
    {
        PlayClickSound();

        yield return new WaitForSeconds(0.15f);

        SceneManager.LoadScene(gameSceneName);
    }


    // =====================================================
    // MAIN MENU CANVASES
    // =====================================================

    public void ShowMultiplayer()
    {
        PlayClickSound();

        HideAllCanvases();

        if (multiplayerCanvas != null)
            multiplayerCanvas.SetActive(true);

        SetCharacterSelectionCamera(false);
        SetCharacterSelectionObjects(false);

        LoadMultiplayerPlayerName();

        ShowMultiplayerMainPanelWithoutSound();
    }


    public void ShowOptions()
    {
        PlayClickSound();

        HideAllCanvases();

        if (optionsCanvas != null)
            optionsCanvas.SetActive(true);

        SetCharacterSelectionCamera(false);
        SetCharacterSelectionObjects(false);

        LoadPlayerName();

        ShowAudioPanelWithoutSound();
    }


    public void ShowCredits()
    {
        PlayClickSound();

        HideAllCanvases();

        if (creditsCanvas != null)
            creditsCanvas.SetActive(true);

        SetCharacterSelectionCamera(false);
        SetCharacterSelectionObjects(false);
    }


    public void ShowMainMenu()
    {
        PlayClickSound();

        HideAllCanvases();

        if (mainMenuCanvas != null)
            mainMenuCanvas.SetActive(true);

        SetCharacterSelectionCamera(false);
        SetCharacterSelectionObjects(false);
    }


    // =====================================================
    // CHARACTER SELECTION
    // =====================================================

    public void ShowCharacterSelection()
    {
        PlayClickSound();

        HideAllCanvases();

        if (characterSelectionCanvas != null)
            characterSelectionCanvas.SetActive(true);

        // Load the CURRENTLY SAVED player name.
        LoadCharacterSelectionPlayerName();

        SetCharacterSelectionCamera(true);
        SetCharacterSelectionObjects(true);
    }


    // =====================================================
    // BACK FROM CHARACTER SELECTION
    // =====================================================

    public void BackFromCharacterSelection()
    {
        PlayClickSound();

        HideAllCanvases();

        if (mainMenuCanvas != null)
            mainMenuCanvas.SetActive(true);

        SetCharacterSelectionCamera(false);
        SetCharacterSelectionObjects(false);
    }


    // =====================================================
    // CHARACTER SELECTION CAMERA
    // =====================================================

    private void SetCharacterSelectionCamera(bool characterSelection)
    {
        if (mainCamera == null && characterCamera == null)
            return;

        if (mainCamera != null)
        {
            mainCamera.gameObject.SetActive(!characterSelection);
        }

        if (characterCamera != null)
        {
            characterCamera.gameObject.SetActive(characterSelection);
        }
    }


    // =====================================================
    // CHARACTER SELECTION OBJECTS
    // =====================================================

    private void SetCharacterSelectionObjects(bool characterSelection)
    {
        if (characterDisplay != null)
        {
            characterDisplay.SetActive(characterSelection);
        }

        if (prisonStatue != null)
        {
            prisonStatue.SetActive(!characterSelection);
        }
    }


    // =====================================================
    // CHARACTER SELECTION PLAYER NAME
    // =====================================================

    private void LoadCharacterSelectionPlayerName()
    {
        if (characterSelectionPlayerNameInput == null)
        {
            Debug.LogWarning(
                "CanvasManager: Character Selection Player Name Input is not assigned."
            );

            return;
        }

        characterSelectionPlayerNameInput.text =
            PlayerProfile.PlayerName;
    }


    // =====================================================
    // SAVE NAME FROM CHARACTER SELECTION
    // =====================================================

    public void SaveCharacterSelectionPlayerName()
    {
        if (characterSelectionPlayerNameInput == null)
        {
            Debug.LogWarning(
                "CanvasManager: Character Selection Player Name Input is not assigned."
            );

            return;
        }

        string playerName =
            characterSelectionPlayerNameInput.text.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Player";
        }

        PlayerProfile.SetPlayerName(playerName);

        // Make sure the input shows exactly what was saved.
        characterSelectionPlayerNameInput.text = playerName;

        PlayClickSound();

        Debug.Log(
            $"Character Selection Name Saved: {PlayerProfile.PlayerName}"
        );
    }


    // =====================================================
    // BACK TO MAIN MENU
    // =====================================================

    public void BackToMainMenu()
    {
        PlayClickSound();

        HideAllCanvases();

        if (mainMenuCanvas != null)
            mainMenuCanvas.SetActive(true);

        SetCharacterSelectionCamera(false);
        SetCharacterSelectionObjects(false);
    }


    // =====================================================
    // MULTIPLAYER NAVIGATION
    // =====================================================

    public void ShowCreateLobby()
    {
        PlayClickSound();

        // Keep compatibility with your existing system.
        SaveMultiplayerPlayerName();

        HideAllMultiplayerPanels();

        if (createLobbyPanel != null)
            createLobbyPanel.SetActive(true);
    }


    public void ShowJoinLobby()
    {
        PlayClickSound();

        // Keep compatibility with your existing system.
        SaveMultiplayerPlayerName();

        HideAllMultiplayerPanels();

        if (joinLobbyPanel != null)
            joinLobbyPanel.SetActive(true);
    }


    public void ShowFindLobby()
    {
        PlayClickSound();

        SaveMultiplayerPlayerName();

        HideAllMultiplayerPanels();

        if (findLobbyPanel != null)
        {
            findLobbyPanel.SetActive(true);
        }

        if (NetworkRunnerHandler.Instance != null)
        {
            NetworkRunnerHandler.Instance.FindLobbies();
        }
        else
        {
            Debug.LogError(
                "CanvasManager: NetworkRunnerHandler not found."
            );
        }
    }


    public void BackToMultiplayer()
    {
        PlayClickSound();

        ShowMultiplayerMainPanelWithoutSound();
    }


    // =====================================================
    // MULTIPLAYER MAIN PANEL
    // =====================================================

    private void ShowMultiplayerMainPanelWithoutSound()
    {
        HideAllMultiplayerPanels();

        if (multiplayerPanel != null)
            multiplayerPanel.SetActive(true);

        LoadMultiplayerPlayerName();
    }


    // =====================================================
    // HIDE MULTIPLAYER PANELS
    // =====================================================

    private void HideAllMultiplayerPanels()
    {
        if (multiplayerPanel != null)
            multiplayerPanel.SetActive(false);

        if (createLobbyPanel != null)
            createLobbyPanel.SetActive(false);

        if (joinLobbyPanel != null)
            joinLobbyPanel.SetActive(false);

        if (findLobbyPanel != null)
            findLobbyPanel.SetActive(false);
    }


    // =====================================================
    // OPTIONS PANELS
    // =====================================================

    public void ShowAudioPanel()
    {
        PlayClickSound();

        HideAllOptionPanels();

        if (audioPanel != null)
            audioPanel.SetActive(true);
    }


    public void ShowGraphicsPanel()
    {
        PlayClickSound();

        HideAllOptionPanels();

        if (graphicsPanel != null)
            graphicsPanel.SetActive(true);

        LoadPlayerName();
    }


    public void ShowControlsPanel()
    {
        PlayClickSound();

        HideAllOptionPanels();

        if (controlsPanel != null)
            controlsPanel.SetActive(true);
    }


    private void ShowAudioPanelWithoutSound()
    {
        HideAllOptionPanels();

        if (audioPanel != null)
            audioPanel.SetActive(true);
    }


    // =====================================================
    // HIDE OPTIONS PANELS
    // =====================================================

    private void HideAllOptionPanels()
    {
        if (audioPanel != null)
            audioPanel.SetActive(false);

        if (graphicsPanel != null)
            graphicsPanel.SetActive(false);

        if (controlsPanel != null)
            controlsPanel.SetActive(false);
    }


    // =====================================================
    // PLAYER NAME - SETTINGS
    // =====================================================

    private void LoadPlayerName()
    {
        if (playerNameInput == null)
        {
            Debug.LogWarning(
                "CanvasManager: Player Name Input is not assigned."
            );

            return;
        }

        playerNameInput.text =
            PlayerProfile.PlayerName;
    }


    // =====================================================
    // SAVE PLAYER NAME - SETTINGS
    // =====================================================

    public void SavePlayerName()
    {
        if (playerNameInput == null)
        {
            Debug.LogWarning(
                "CanvasManager: Player Name Input is not assigned."
            );

            return;
        }

        string playerName =
            playerNameInput.text.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Player";
        }

        // Save ONLY the player name.
        PlayerProfile.SetPlayerName(playerName);

        // Update input field.
        playerNameInput.text =
            playerName;

        PlayClickSound();

        Debug.Log(
            $"Player Name Saved: {PlayerProfile.PlayerName}"
        );
    }


    // =====================================================
    // PLAYER NAME - MULTIPLAYER
    // =====================================================

    private void LoadMultiplayerPlayerName()
    {
        if (multiplayerPlayerNameInput == null)
        {
            Debug.LogWarning(
                "CanvasManager: Multiplayer Player Name Input is not assigned."
            );

            return;
        }

        multiplayerPlayerNameInput.text =
            PlayerProfile.PlayerName;
    }


    private void SaveMultiplayerPlayerName()
    {
        if (multiplayerPlayerNameInput == null)
        {
            Debug.LogWarning(
                "CanvasManager: Multiplayer Player Name Input is not assigned."
            );

            return;
        }

        string playerName =
            multiplayerPlayerNameInput.text.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Player";
        }

        PlayerProfile.SetPlayerName(playerName);

        multiplayerPlayerNameInput.text =
            playerName;

        Debug.Log(
            $"Multiplayer Player Name Saved: {PlayerProfile.PlayerName}"
        );
    }


    // =====================================================
    // HIDE ALL MAIN CANVASES
    // =====================================================

    private void HideAllCanvases()
    {
        if (mainMenuCanvas != null)
            mainMenuCanvas.SetActive(false);

        if (multiplayerCanvas != null)
            multiplayerCanvas.SetActive(false);

        if (optionsCanvas != null)
            optionsCanvas.SetActive(false);

        if (creditsCanvas != null)
            creditsCanvas.SetActive(false);

        if (characterSelectionCanvas != null)
            characterSelectionCanvas.SetActive(false);
    }
}
