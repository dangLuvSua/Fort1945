using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class CanvasManager : MonoBehaviour
{
    // =====================================================
    // MAIN CANVASES
    // =====================================================

    [Header("Main Canvases")]
    [SerializeField] private GameObject mainMenuCanvas;
    [SerializeField] private GameObject multiplayerCanvas;
    [SerializeField] private GameObject optionsCanvas;
    [SerializeField] private GameObject creditsCanvas;


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

        multiplayerCanvas.SetActive(true);

        // Load saved name into Multiplayer panel
        LoadMultiplayerPlayerName();

        // Always start at Multiplayer main panel
        ShowMultiplayerMainPanelWithoutSound();
    }


    public void ShowOptions()
    {
        PlayClickSound();

        HideAllCanvases();

        optionsCanvas.SetActive(true);

        // Load saved player name
        LoadPlayerName();

        // Show Audio panel by default
        ShowAudioPanelWithoutSound();
    }


    public void ShowCredits()
    {
        PlayClickSound();

        HideAllCanvases();

        creditsCanvas.SetActive(true);
    }


    public void ShowMainMenu()
    {
        PlayClickSound();

        HideAllCanvases();

        mainMenuCanvas.SetActive(true);
    }


    // =====================================================
    // BACK TO MAIN MENU
    // =====================================================

    public void BackToMainMenu()
    {
        PlayClickSound();

        HideAllCanvases();

        mainMenuCanvas.SetActive(true);
    }


    // =====================================================
    // MULTIPLAYER NAVIGATION
    // =====================================================

    // Opens Create Lobby
    public void ShowCreateLobby()
    {
        PlayClickSound();

        SaveMultiplayerPlayerName();

        HideAllMultiplayerPanels();

        if (createLobbyPanel != null)
            createLobbyPanel.SetActive(true);
    }


    // Opens Join Lobby
    public void ShowJoinLobby()
    {
        PlayClickSound();

        SaveMultiplayerPlayerName();

        HideAllMultiplayerPanels();

        if (joinLobbyPanel != null)
            joinLobbyPanel.SetActive(true);
    }


    // Opens Find Lobby
    public void ShowFindLobby()
    {
        PlayClickSound();

        SaveMultiplayerPlayerName();

        HideAllMultiplayerPanels();

        if (findLobbyPanel != null)
            findLobbyPanel.SetActive(true);
    }


    // Returns from Create / Join / Find
    // back to the main Multiplayer panel
    public void BackToMultiplayer()
    {
        PlayClickSound();

        ShowMultiplayerMainPanelWithoutSound();
    }


    // =====================================================
    // SHOW MULTIPLAYER MAIN PANEL WITHOUT SOUND
    // =====================================================

    private void ShowMultiplayerMainPanelWithoutSound()
    {
        HideAllMultiplayerPanels();

        if (multiplayerPanel != null)
            multiplayerPanel.SetActive(true);

        // Always show the latest saved name
        LoadMultiplayerPlayerName();
    }


    // =====================================================
    // HIDE ALL MULTIPLAYER PANELS
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


    // =====================================================
    // SHOW AUDIO PANEL WITHOUT CLICK SOUND
    // =====================================================

    private void ShowAudioPanelWithoutSound()
    {
        HideAllOptionPanels();

        if (audioPanel != null)
            audioPanel.SetActive(true);
    }


    // =====================================================
    // HIDE ALL OPTIONS PANELS
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
                "CanvasManager: Player Name Input " +
                "is not assigned."
            );

            return;
        }

        playerNameInput.text =
            PlayerProfile.PlayerName;
    }


    public void SavePlayerName()
    {
        if (playerNameInput == null)
        {
            Debug.LogWarning(
                "CanvasManager: Player Name Input " +
                "is not assigned."
            );

            return;
        }

        string playerName =
            playerNameInput.text.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Player";
        }

        PlayerProfile.SetPlayerName(
            playerName
        );

        playerNameInput.text =
            playerName;

        PlayClickSound();

        Debug.Log(
            $"Player name saved: {playerName}"
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
                "CanvasManager: Multiplayer Player Name " +
                "Input is not assigned."
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
                "CanvasManager: Multiplayer Player Name " +
                "Input is not assigned."
            );

            return;
        }

        string playerName =
            multiplayerPlayerNameInput.text.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Player";
        }

        PlayerProfile.SetPlayerName(
            playerName
        );

        multiplayerPlayerNameInput.text =
            playerName;

        Debug.Log(
            $"Multiplayer player name saved: {playerName}"
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
    }
}