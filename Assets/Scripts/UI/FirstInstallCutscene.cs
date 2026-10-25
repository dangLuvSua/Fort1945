using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays an assigned cutscene on first launch, or every launch when configured.
/// Put this on the cutscene UI object in the first scene of the build.
/// </summary>
public sealed class FirstInstallCutscene : MonoBehaviour
{
    private const string PlayedKey = "Fort1945.FirstInstallCutscene.Played";

    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject cutsceneRoot;
    [SerializeField] private bool allowSkip = true;
    [Tooltip("When enabled, play the cutscene every time the app opens, including release builds.")]
    [SerializeField] private bool playEveryLaunch;
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
        bool shouldPlayCutscene = playEveryLaunch || !hasAlreadyPlayed;
#if UNITY_EDITOR
        if (replayForTesting)
            shouldPlayCutscene = true;
#endif

        if (videoPlayer == null || videoPlayer.clip == null || !shouldPlayCutscene)
        {
            // This scene is the build's first scene. Hiding the cutscene alone
            // leaves the player with an empty scene, so continue to the menu.
            if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                Debug.LogError($"Cannot load scene '{mainMenuSceneName}'. Add it to Build Settings.", this);
            }
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
        bool rememberCompletion = !playEveryLaunch;
#if UNITY_EDITOR
        if (replayForTesting)
            rememberCompletion = false;
#endif
        if (rememberCompletion)
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
