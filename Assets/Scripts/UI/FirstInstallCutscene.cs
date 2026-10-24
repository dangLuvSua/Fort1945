using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays an assigned cutscene once per install, then remembers completion.
/// Put this on the cutscene UI object in the scene that should show the intro.
/// </summary>
public sealed class FirstInstallCutscene : MonoBehaviour
{
    private const string PlayedKey = "Fort1945.FirstInstallCutscene.Played";

    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject cutsceneRoot;
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
#if UNITY_EDITOR
    [SerializeField] private bool replayForTesting;
#endif

    private bool isPlaying;

    private void Awake()
    {
        if (cutsceneRoot == null)
            cutsceneRoot = gameObject;

        bool hasAlreadyPlayed = PlayerPrefs.GetInt(PlayedKey, 0) != 0;
#if UNITY_EDITOR
        if (replayForTesting)
            hasAlreadyPlayed = false;
#endif

        if (videoPlayer == null || videoPlayer.clip == null || hasAlreadyPlayed)
        {
            cutsceneRoot.SetActive(false);
            return;
        }

        Button[] buttons = cutsceneRoot.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == "SkipBtn")
                button.onClick.AddListener(SkipCutscene);
        }

        videoPlayer.loopPointReached += OnVideoFinished;
        cutsceneRoot.SetActive(true);
        videoPlayer.Play();
        isPlaying = true;
    }

    private void Update()
    {
        if (isPlaying && allowSkip && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space)))
            SkipCutscene();
    }

    /// <summary>Ends the intro when the Skip button is clicked.</summary>
    public void SkipCutscene()
    {
        if (allowSkip)
            FinishCutscene();
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        FinishCutscene();
    }

    private void FinishCutscene()
    {
        if (!isPlaying)
            return;

        isPlaying = false;
#if UNITY_EDITOR
        if (!replayForTesting)
#endif
        {
            PlayerPrefs.SetInt(PlayedKey, 1);
            PlayerPrefs.Save();
        }
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.Stop();
        }

        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            Debug.LogError($"Cannot load scene '{mainMenuSceneName}'. Add it to Build Settings.", this);
            if (cutsceneRoot != null)
                cutsceneRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;

        if (cutsceneRoot != null)
        {
            foreach (Button button in cutsceneRoot.GetComponentsInChildren<Button>(true))
                button.onClick.RemoveListener(SkipCutscene);
        }
    }
}
