using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class CanvasManager : MonoBehaviour
{
    [Header("Main Canvases")]
    [SerializeField] private GameObject mainMenuCanvas;
    [SerializeField] private GameObject multiplayerCanvas;
    [SerializeField] private GameObject optionsCanvas;
    [SerializeField] private GameObject creditsCanvas;

    [Header("Options Panels")]
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject graphicsPanel;
    [SerializeField] private GameObject controlsPanel;

    [Header("UI Click Audio")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip clickSound;

    [SerializeField]
    [Range(0f, 1f)]
    private float clickVolume = 0.7f;

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
    }


    public void ShowOptions()
    {
        PlayClickSound();

        HideAllCanvases();

        optionsCanvas.SetActive(true);

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
    // OPTIONS PANELS
    // =====================================================

    public void ShowAudioPanel()
    {
        PlayClickSound();

        HideAllOptionPanels();

        audioPanel.SetActive(true);
    }


    public void ShowGraphicsPanel()
    {
        PlayClickSound();

        HideAllOptionPanels();

        graphicsPanel.SetActive(true);
    }


    public void ShowControlsPanel()
    {
        PlayClickSound();

        HideAllOptionPanels();

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