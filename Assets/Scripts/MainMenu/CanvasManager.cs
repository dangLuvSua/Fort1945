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
        SetCharacterSelectionCamera(false);
        SetCharacterSelectionObjects(false);
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