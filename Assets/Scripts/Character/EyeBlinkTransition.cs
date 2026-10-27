using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>Creates a simple top-and-bottom eyelid wipe for local transitions.</summary>
public sealed class EyeBlinkTransition : MonoBehaviour
{
    private const float DefaultCloseDuration = 0.28f;
    private const float DefaultOpenDuration = 0.42f;
    private static EyeBlinkTransition instance;

    private readonly Queue<Action> pendingTeleports = new Queue<Action>();
    private RectTransform upperLid;
    private RectTransform lowerLid;
    private Coroutine transition;
    private float coverage;

    public static void OpenFromBlack()
    {
        EyeBlinkTransition blink = EnsureInstance();
        if (blink.transition != null)
            return;

        blink.SetLidColor(Color.black);
        blink.coverage = 1f;
        blink.transition = blink.StartCoroutine(
            blink.AnimateCoverage(0f, DefaultOpenDuration, false));
    }

    public static void PlayTeleport(Action teleport)
    {
        if (teleport == null)
            return;

        EyeBlinkTransition blink = EnsureInstance();
        blink.pendingTeleports.Enqueue(teleport);

        if (blink.transition != null)
            blink.StopCoroutine(blink.transition);

        blink.transition = blink.StartCoroutine(blink.PlayQueuedTeleports());
    }

    public static void PlayPhaseCompletion(
        TMP_FontAsset font,
        string message,
        Action teleport,
        Action onComplete,
        float messageDuration = 2.25f)
    {
        EyeBlinkTransition blink = EnsureInstance();
        if (blink.transition != null)
            blink.StopCoroutine(blink.transition);
        blink.transition = blink.StartCoroutine(
            blink.PlayPhaseCompletionSequence(font, message, teleport, onComplete, messageDuration));
    }

    private IEnumerator PlayPhaseCompletionSequence(
        TMP_FontAsset font,
        string message,
        Action teleport,
        Action onComplete,
        float messageDuration)
    {
        yield return AnimateCoverage(1f, 0.5f, true);

        GameObject screen = new GameObject("Phase Completed Screen");
        Canvas screenCanvas = screen.AddComponent<Canvas>();
        screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        screenCanvas.overrideSorting = true;
        // Keep the completion card above the persistent eyelid canvas and
        // any scene overlay canvases during the covered part of the blink.
        screenCanvas.sortingOrder = short.MaxValue;
        screen.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        Image background = new GameObject("Black Background", typeof(RectTransform), typeof(Image))
            .GetComponent<Image>();
        background.transform.SetParent(screen.transform, false);
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;
        background.color = Color.black;
        background.raycastTarget = false;

        TextMeshProUGUI label = new GameObject("Phase Completed Text", typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(screen.transform, false);
        label.rectTransform.anchorMin = new Vector2(0.1f, 0.4f);
        label.rectTransform.anchorMax = new Vector2(0.9f, 0.6f);
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        label.text = string.IsNullOrWhiteSpace(message) ? "PHASE COMPLETED" : message;
        label.color = new Color(0.65f, 0.03f, 0.03f, 1f);
        label.fontSize = 42f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 24f;
        label.fontSizeMax = 48f;
        if (font != null)
            label.font = font;

        yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, messageDuration));
        teleport?.Invoke();
        yield return new WaitForSecondsRealtime(0.15f);
        Destroy(screen);
        SetLidColor(Color.black);
        yield return AnimateCoverage(0f, DefaultOpenDuration, false);
        onComplete?.Invoke();
        transition = null;
    }

    public static void CloseAndHold(
        Color lidColor,
        float duration = DefaultCloseDuration)
    {
        EyeBlinkTransition blink = EnsureInstance();
        blink.SetLidColor(lidColor);
        if (blink.transition != null)
            blink.StopCoroutine(blink.transition);

        blink.transition = blink.StartCoroutine(
            blink.AnimateCoverage(1f, duration, true));
    }

    public static void Open()
    {
        EyeBlinkTransition blink = EnsureInstance();

        // A scene loading overlay may request its fade-out as soon as player
        // setup finishes. If a teleport is still queued behind the closing
        // eyelid animation, stopping that coroutine here would cancel the
        // teleport callback and leave PlayerController movement locked.
        // PlayQueuedTeleports will run the callbacks and open the screen itself.
        if (blink.pendingTeleports.Count > 0)
            return;

        if (blink.transition != null)
            blink.StopCoroutine(blink.transition);

        blink.transition = blink.StartCoroutine(
            blink.AnimateCoverage(0f, DefaultOpenDuration, false));
    }

    /// <summary>Opens the local blink after a networked scene load completes.</summary>
    public static void OpenAfterSceneLoads(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        EyeBlinkTransition blink = EnsureInstance();
        blink.StartCoroutine(blink.WaitForSceneThenOpen(sceneName));
    }

    private IEnumerator WaitForSceneThenOpen(string sceneName)
    {
        float timeout = 60f;
        while (SceneManager.GetActiveScene().name != sceneName && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (SceneManager.GetActiveScene().name != sceneName)
        {
            Debug.LogWarning($"[EYE BLINK] Timed out waiting for scene '{sceneName}'; reopening the screen.");
            Open();
            yield break;
        }

        // Give Fort2's network scene objects and player spawn logic a moment
        // to initialize while the black eyelids still cover the transition.
        float spawnTimeout = 20f;
        bool localPlayerReturned = false;
        while (!localPlayerReturned && spawnTimeout > 0f)
        {
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController player in players)
            {
                if (player != null && player.Object != null && player.Object.HasInputAuthority)
                {
                    localPlayerReturned = !player.IsCapturedByKempeitai;
                    break;
                }
            }

            if (localPlayerReturned)
                break;

            spawnTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!localPlayerReturned)
            Debug.LogWarning("[EYE BLINK] Fort2 loaded, but local player return was not confirmed before reopening.");

        Open();
    }

    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(gameObject);
        CreateLids();
    }

    private static EyeBlinkTransition EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject root = new GameObject("Eye Blink Transition");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        root.AddComponent<GraphicRaycaster>();
        return root.AddComponent<EyeBlinkTransition>();
    }

    private void CreateLids()
    {
        upperLid = CreateLid("Upper Eyelid");
        lowerLid = CreateLid("Lower Eyelid");
        SetCoverage(0f);
    }

    private RectTransform CreateLid(string lidName)
    {
        GameObject lid = new GameObject(lidName, typeof(RectTransform), typeof(Image));
        lid.transform.SetParent(transform, false);
        lid.GetComponent<Image>().color = Color.black;
        lid.GetComponent<Image>().raycastTarget = false;
        return lid.GetComponent<RectTransform>();
    }

    private IEnumerator PlayQueuedTeleports()
    {
        yield return AnimateCoverage(1f, DefaultCloseDuration, true);

        while (pendingTeleports.Count > 0)
            pendingTeleports.Dequeue()?.Invoke();

        yield return new WaitForSecondsRealtime(0.08f);
        yield return AnimateCoverage(0f, DefaultOpenDuration, false);
        transition = null;
    }

    private IEnumerator AnimateCoverage(float target, float duration, bool hold)
    {
        float start = coverage;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetCoverage(Mathf.Lerp(start, target, t));
            yield return null;
        }

        SetCoverage(target);
        if (!hold)
            transition = null;
    }

    private void SetCoverage(float value)
    {
        coverage = Mathf.Clamp01(value);
        if (upperLid == null || lowerLid == null)
            return;

        upperLid.anchorMin = new Vector2(0f, 1f - coverage);
        upperLid.anchorMax = Vector2.one;
        upperLid.offsetMin = Vector2.zero;
        upperLid.offsetMax = Vector2.zero;

        lowerLid.anchorMin = Vector2.zero;
        lowerLid.anchorMax = new Vector2(1f, coverage);
        lowerLid.offsetMin = Vector2.zero;
        lowerLid.offsetMax = Vector2.zero;
    }

    private void SetLidColor(Color color)
    {
        if (upperLid != null)
            upperLid.GetComponent<Image>().color = color;
        if (lowerLid != null)
            lowerLid.GetComponent<Image>().color = color;
    }
}
