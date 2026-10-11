using UnityEngine;
using System.Collections;

public class PanelAnimation : MonoBehaviour
{
    [Header("Horror Animation Settings")]
    [Tooltip("Duration of the creepy pop-in.")]
    [SerializeField] private float openDuration = 0.3f;

    [Tooltip("Intensity of the shudder / horror twitch as it opens.")]
    [SerializeField] private float shudderIntensity = 10f;

    [Tooltip("Starting distorted scale (slightly squished/stretched).")]
    [SerializeField] private Vector3 startDistortion = new Vector3(0.85f, 1.12f, 1f);

    [Header("Horror SFX (Optional)")]
    [SerializeField] private AudioClip openSound;
    [SerializeField][Range(0f, 1f)] private float soundVolume = 0.6f;

    private RectTransform rectTransform;
    private AudioSource audioSource;
    private Vector2 originalPosition;
    private Vector3 originalScale;
    private Coroutine animCoroutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            originalPosition = rectTransform.anchoredPosition;
            originalScale = rectTransform.localScale;
        }
        else
        {
            originalScale = transform.localScale;
        }

        if (openSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }

    // Automatically runs EVERY time your existing inventory opens/enables!
    private void OnEnable()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (animCoroutine != null)
            StopCoroutine(animCoroutine);

        animCoroutine = StartCoroutine(PlayHorrorOpen());
    }

    private void OnDisable()
    {
        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }

        // Reset transforms immediately when closed
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = originalPosition;
            rectTransform.localScale = originalScale;
        }
    }

    private IEnumerator PlayHorrorOpen()
    {
        // Play audio if provided
        if (openSound != null && audioSource != null)
            audioSource.PlayOneShot(openSound, soundVolume);

        float elapsed = 0f;
        Vector3 fromScale = Vector3.Scale(originalScale, startDistortion);

        while (elapsed < openDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Unscaled so it works even if game is paused
            float t = Mathf.Clamp01(elapsed / openDuration);

            // Shudder twitch that decays as it snaps into place
            float decay = 1f - t;
            Vector2 shudder = new Vector2(
                Random.Range(-shudderIntensity, shudderIntensity) * decay,
                Random.Range(-shudderIntensity, shudderIntensity) * decay
            );
            rectTransform.anchoredPosition = originalPosition + shudder;

            // Elastic horror snap
            float scaleT = EaseOutBack(t);
            rectTransform.localScale = Vector3.Lerp(fromScale, originalScale, scaleT);

            yield return null;
        }

        // Snap cleanly to rest position
        rectTransform.anchoredPosition = originalPosition;
        rectTransform.localScale = originalScale;
        animCoroutine = null;
    }

    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
