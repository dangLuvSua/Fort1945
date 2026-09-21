using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonHoverEffect : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("Text")]
    [SerializeField] private TMP_Text buttonText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(0.8f, 0.1f, 0.1f);

    [Header("Scale")]
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float transitionSpeed = 8f;

    [Header("Hover SFX")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] [Range(0f, 1f)] private float hoverVolume = 0.5f;

    private Vector3 originalScale;
    private Coroutine scaleCoroutine;
    private AudioSource audioSource;

    private void Awake()
    {
        // Automatically get TMP component
        if (buttonText == null)
            buttonText = GetComponent<TMP_Text>();

        // Get AudioSource
        audioSource = GetComponent<AudioSource>();

        // Create AudioSource if none exists
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        originalScale = transform.localScale;

        if (buttonText != null)
            buttonText.color = normalColor;
    }

    // =========================
    // HOVER ENTER
    // =========================

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (buttonText != null)
            buttonText.color = hoverColor;

        if (hoverSound != null)
        {
            audioSource.PlayOneShot(
                hoverSound,
                hoverVolume
            );
        }

        StartScale(originalScale * hoverScale);
    }

    // =========================
    // HOVER EXIT
    // =========================

    public void OnPointerExit(PointerEventData eventData)
    {
        if (buttonText != null)
            buttonText.color = normalColor;

        StartScale(originalScale);
    }

    // =========================
    // SCALE
    // =========================

    private void StartScale(Vector3 targetScale)
    {
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(
            ScaleTo(targetScale)
        );
    }

    private IEnumerator ScaleTo(Vector3 targetScale)
    {
        while (Vector3.Distance(
            transform.localScale,
            targetScale
        ) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                targetScale,
                Time.deltaTime * transitionSpeed
            );

            yield return null;
        }

        transform.localScale = targetScale;
    }
}