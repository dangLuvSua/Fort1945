using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
