using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

/// <summary>Keeps the teleport transition visible across the network scene load.</summary>
public sealed class DungeonTeleportLoadingOverlay : MonoBehaviour
{
    private const float FadeOutDuration = 0.25f;

    private CanvasGroup canvasGroup;
    private float fadeDuration;
    private bool fadingOut;

    public static void Show(
        float duration,
        Color backgroundColor,
        AudioClip sound,
        AudioMixerGroup mixerGroup)
    {
        DungeonTeleportLoadingOverlay existing =
            FindFirstObjectByType<DungeonTeleportLoadingOverlay>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        Debug.Log("[TELEPORT OVERLAY] Showing persistent Dungeon transition overlay.");
        EyeBlinkTransition.CloseAndHold(backgroundColor);

        GameObject root = new GameObject(
            "Dungeon Teleport Loading Overlay",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup),
            typeof(DungeonTeleportLoadingOverlay));
        DontDestroyOnLoad(root);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32001;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        DungeonTeleportLoadingOverlay overlay =
            root.GetComponent<DungeonTeleportLoadingOverlay>();
        overlay.canvasGroup = root.GetComponent<CanvasGroup>();
        overlay.fadeDuration = Mathf.Max(0.01f, duration);

        GameObject labelObject = new GameObject(
            "Teleporting Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = Resources.Load<TMP_FontAsset>(
            "Fonts & Materials/LiberationSans SDF");
        if (label.font == null)
            label.font = TMP_Settings.defaultFontAsset;
        label.text = "Teleporting...";
        label.fontSize = 36f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        if (sound != null)
        {
            AudioSource source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = mixerGroup;
            source.PlayOneShot(sound);
        }
    }

    public static void Hide()
    {
        EyeBlinkTransition.Open();
        DungeonTeleportLoadingOverlay overlay =
            FindFirstObjectByType<DungeonTeleportLoadingOverlay>(FindObjectsInactive.Include);
        if (overlay != null)
            overlay.fadingOut = true;
    }

    private void Update()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            return;

        if (fadingOut)
        {
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha,
                0f,
                Time.unscaledDeltaTime / FadeOutDuration);
            if (canvasGroup.alpha <= 0f)
                Destroy(gameObject);
            return;
        }

        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha,
            1f,
            Time.unscaledDeltaTime / fadeDuration);
    }

}
