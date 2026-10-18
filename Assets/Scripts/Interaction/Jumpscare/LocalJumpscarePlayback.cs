using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.Video;

[Serializable]
public sealed class JumpscareClipSet
{
    [Tooltip("Video shown for this jumpscare.")]
    public VideoClip video;

    [Tooltip("Scream paired with this video.")]
    public AudioClip scream;

    [Tooltip("Played after the scream finishes, for example a cymbal hit.")]
    public AudioClip followUpSfx;
}

public sealed class LocalJumpscarePlayback : MonoBehaviour
{
    [Header("Screen Display")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoImage;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioMixerGroup mixerGroup;
    [SerializeField] private JumpscareClipSet[] variants;

    [Header("Video Appearance")]
    [SerializeField, Range(0f, 1f)] private float videoOpacity = 0.6f;

    [Header("Vignette")]
    [SerializeField, Range(0f, 1f)] private float vignetteStrength = 0.65f;
    [SerializeField, Min(0f)] private float vignetteFadeIn = 0.12f;
    [SerializeField, Min(0f)] private float vignetteFadeOut = 0.2f;

    private static Texture2D sharedVignetteTexture;

    private int previousVariant = -1;
    private RawImage vignetteImage;
    private Coroutine vignetteFade;
    private bool firstVideoFrameShown;
    private bool videoFinished;

    private void Awake()
    {
        EnsureScreenCanvas();

        // The prefab root has a Background Image. Leaving it enabled would
        // cover the game while the video RawImage waits for its first frame.
        Image backgroundImage = GetComponent<Image>();
        if (backgroundImage != null)
        {
            backgroundImage.enabled = false;
            backgroundImage.raycastTarget = false;
        }

        if (videoImage == null)
            videoImage = GetComponentInChildren<RawImage>(true);

        if (videoImage != null)
        {
            videoImage.enabled = false;
            RectTransform videoRect = videoImage.rectTransform;
            videoRect.anchorMin = Vector2.zero;
            videoRect.anchorMax = Vector2.one;
            videoRect.offsetMin = Vector2.zero;
            videoRect.offsetMax = Vector2.zero;

            if (videoPlayer != null && videoPlayer.targetTexture != null)
                videoImage.texture = videoPlayer.targetTexture;

            ApplyVideoOpacity();
        }

        CreateVignetteOverlay();
    }

    private void EnsureScreenCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null)
            return;

        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
    }

    public void Play()
    {
        JumpscareClipSet variant = ChooseVariant();
        if (variant == null)
        {
            Debug.LogWarning(
                $"[{nameof(LocalJumpscarePlayback)}] No valid variants assigned.",
                this);
            Destroy(gameObject);
            return;
        }

        float audioStartedAt = Time.realtimeSinceStartup;
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.outputAudioMixerGroup = mixerGroup;

            if (variant.scream != null)
                audioSource.PlayOneShot(variant.scream);
            if (variant.followUpSfx != null)
                audioSource.PlayOneShot(variant.followUpSfx);
        }

        if (videoPlayer != null && variant.video != null)
        {
            videoPlayer.Stop();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.sendFrameReadyEvents = true;
            videoPlayer.frameReady += OnVideoFrameReady;
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.errorReceived += OnVideoError;
            videoPlayer.clip = variant.video;
            videoPlayer.Play();
        }

        StartCoroutine(PlayFollowUpAndFinish(variant, audioStartedAt));
    }

    private JumpscareClipSet ChooseVariant()
    {
        if (variants == null || variants.Length == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < variants.Length; i++)
            if (IsValid(variants[i]))
                validCount++;

        if (validCount == 0)
            return null;

        int selected = UnityEngine.Random.Range(0, validCount);
        for (int i = 0; i < variants.Length; i++)
        {
            if (!IsValid(variants[i]))
                continue;

            if (selected-- != 0)
                continue;

            // Avoid playing the same variant twice in a row when possible.
            if (validCount > 1 && i == previousVariant)
            {
                for (int j = 0; j < variants.Length; j++)
                {
                    if (j != previousVariant && IsValid(variants[j]))
                    {
                        i = j;
                        break;
                    }
                }
            }

            previousVariant = i;
            return variants[i];
        }

        return null;
    }

    private static bool IsValid(JumpscareClipSet variant)
    {
        return variant != null &&
               (variant.video != null ||
                variant.scream != null ||
                variant.followUpSfx != null);
    }

    private IEnumerator PlayFollowUpAndFinish(
        JumpscareClipSet variant,
        float audioStartedAt)
    {
        if (variant.video != null)
        {
            // Wait for the actual video end, including its prepare time.
            float timeout = Mathf.Max(1f, (float)variant.video.length + 5f);
            float elapsed = 0f;
            while (!videoFinished && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        float audioLength = Mathf.Max(
            variant.scream != null ? variant.scream.length : 0f,
            variant.followUpSfx != null ? variant.followUpSfx.length : 0f);
        float audioRemaining = audioLength -
            (Time.realtimeSinceStartup - audioStartedAt);
        if (audioRemaining > 0f)
        {
            yield return new WaitForSecondsRealtime(audioRemaining);
        }

        if (videoImage != null)
            videoImage.enabled = false;
        FadeVignette(0f, vignetteFadeOut);
        if (vignetteFadeOut > 0f)
            yield return new WaitForSecondsRealtime(vignetteFadeOut);

        if (videoPlayer != null)
            videoPlayer.Stop();
        Destroy(gameObject);
    }

    private void OnVideoFrameReady(VideoPlayer source, long frameIndex)
    {
        if (firstVideoFrameShown)
            return;

        firstVideoFrameShown = true;
        source.frameReady -= OnVideoFrameReady;

        if (videoImage != null)
        {
            ApplyVideoOpacity();
            videoImage.enabled = true;
        }

        FadeVignette(vignetteStrength, vignetteFadeIn);
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        videoFinished = true;
        if (videoImage != null)
            videoImage.enabled = false;
        FadeVignette(0f, vignetteFadeOut);
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"[{nameof(LocalJumpscarePlayback)}] Video playback failed: {message}", this);
        videoFinished = true;
    }

    private void CreateVignetteOverlay()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null)
            canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            return;

        GameObject overlay = new GameObject(
            "Jumpscare Vignette",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();

        RectTransform rect = (RectTransform)overlay.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        vignetteImage = overlay.GetComponent<RawImage>();
        vignetteImage.texture = GetVignetteTexture();
        vignetteImage.color = new Color(0f, 0f, 0f, 0f);
        vignetteImage.raycastTarget = false;
    }

    private static Texture2D GetVignetteTexture()
    {
        if (sharedVignetteTexture != null)
            return sharedVignetteTexture;

        const int size = 128;
        sharedVignetteTexture = new Texture2D(
            size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Runtime Jumpscare Vignette",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (size - 1f) - 0.5f) * 2f;
                float ny = (y / (size - 1f) - 0.5f) * 2f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny) / Mathf.Sqrt(2f);
                float alpha = Mathf.SmoothStep(0.3f, 0.95f, radius);
                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        sharedVignetteTexture.SetPixels(pixels);
        sharedVignetteTexture.Apply(false, true);
        return sharedVignetteTexture;
    }

    private void FadeVignette(float targetAlpha, float duration)
    {
        if (vignetteImage == null)
            return;

        if (vignetteFade != null)
            StopCoroutine(vignetteFade);

        vignetteFade = StartCoroutine(FadeVignetteRoutine(targetAlpha, duration));
    }

    private IEnumerator FadeVignetteRoutine(float targetAlpha, float duration)
    {
        Color color = vignetteImage.color;
        float startAlpha = color.a;

        if (duration <= 0f)
        {
            color.a = targetAlpha;
            vignetteImage.color = color;
            vignetteFade = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            vignetteImage.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        vignetteImage.color = color;
        vignetteFade = null;
    }

    private void OnDestroy()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.frameReady -= OnVideoFrameReady;
        videoPlayer.loopPointReached -= OnVideoFinished;
        videoPlayer.errorReceived -= OnVideoError;
    }

    private void OnValidate()
    {
        ApplyVideoOpacity();
    }

    private void ApplyVideoOpacity()
    {
        if (videoImage != null)
        {
            Color color = videoImage.color;
            color.a = Mathf.Clamp01(videoOpacity);
            videoImage.color = color;
        }
    }
}
