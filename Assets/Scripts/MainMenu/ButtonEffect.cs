using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonHoverEffect : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IDeselectHandler
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
    [SerializeField][Range(0f, 1f)] private float hoverVolume = 0.5f;

    private Vector3 originalScale;
    private Coroutine scaleCoroutine;
    private AudioSource audioSource;

    private void Awake()
    {
        // Automatically find TMP text
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

        originalScale = transform.localScale;
    }

    private void Start()
    {
        ResetButton();
    }

    // =====================================================
    // HOVER ENTER
    // =====================================================

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

    // =====================================================
    // HOVER EXIT
    // =====================================================

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetButton();
    }

    // =====================================================
    // DESELECT
    // =====================================================

    public void OnDeselect(BaseEventData eventData)
    {
        ResetButton();
    }

    // =====================================================
    // WHEN CANVAS / BUTTON IS DISABLED
    // =====================================================

    private void OnDisable()
    {
        ResetButton();
    }

    // =====================================================
    // RESET BUTTON
    // =====================================================

    private void ResetButton()
    {
        if (buttonText != null)
            buttonText.color = normalColor;

        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }

        transform.localScale = originalScale;
    }

    // =====================================================
    // SCALE
    // =====================================================

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
        scaleCoroutine = null;
    }
}