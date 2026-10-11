using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonHoverEffect : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IDeselectHandler
{
    [Header("Text Reference")]
    [SerializeField] private TMP_Text buttonText;

    [Header("Normal State Colors")]
    [SerializeField] private Color normalColor = new Color(0.85f, 0.85f, 0.85f, 1f); // Muted silver/white

    [Header("Horror / Blood Gradient Colors (Hover)")]
    [Tooltip("Top half of letters: Crisp silver-white so text stays readable")]
    [SerializeField] private Color bloodGradientTop = new Color(0.92f, 0.92f, 0.95f, 1f);

    [Tooltip("Bottom half of letters: Deep crimson / blood red")]
    [SerializeField] private Color bloodGradientBottom = new Color(0.82f, 0.08f, 0.12f, 1f);

    [Header("Blood Glow / Underlay (Hover)")]
    [ColorUsage(true, true)]
    [SerializeField] private Color bloodGlowColor = new Color(0.95f, 0.05f, 0.08f, 0.9f);
    [SerializeField] private float glowPower = 0.5f;
    [SerializeField] private float glowOuter = 0.35f;

    [Header("Scale Animation")]
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float transitionSpeed = 8f;

    [Header("Hover SFX")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField][Range(0f, 1f)] private float hoverVolume = 0.5f;

    private Vector3 originalScale;
    private Coroutine transitionCoroutine;
    private AudioSource audioSource;
    private Material textMaterialInstance;

    // Shader property cache
    private static readonly int ShaderProp_GlowColor = Shader.PropertyToID("_GlowColor");
    private static readonly int ShaderProp_GlowPower = Shader.PropertyToID("_GlowPower");
    private static readonly int ShaderProp_GlowOuter = Shader.PropertyToID("_GlowOuter");
    private static readonly int ShaderProp_UnderlayColor = Shader.PropertyToID("_UnderlayColor");

    private void Awake()
    {
        if (buttonText == null)
            buttonText = GetComponent<TMP_Text>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        originalScale = transform.localScale;

        // Instance material so only this button's glow is affected
        if (buttonText != null)
        {
            textMaterialInstance = buttonText.fontMaterial;
            textMaterialInstance.EnableKeyword("GLOW_ON");
            textMaterialInstance.EnableKeyword("UNDERLAY_ON");
        }
    }

    private void Start()
    {
        ResetButtonInstant();
    }

    // =====================================================
    // HOVER ENTER / EXIT
    // =====================================================

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSound != null)
            audioSource.PlayOneShot(hoverSound, hoverVolume);

        StartTransition(
            targetScale: originalScale * hoverScale,
            targetTopColor: bloodGradientTop,
            targetBottomColor: bloodGradientBottom,
            targetGlowColor: bloodGlowColor,
            targetGlowPower: glowPower,
            targetGlowOuter: glowOuter
        );
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ReturnToNormal();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        ReturnToNormal();
    }

    private void OnDisable()
    {
        ResetButtonInstant();
    }

    // =====================================================
    // TRANSITION LOGIC
    // =====================================================

    private void ReturnToNormal()
    {
        Color clearGlow = new Color(bloodGlowColor.r, bloodGlowColor.g, bloodGlowColor.b, 0f);
        StartTransition(
            targetScale: originalScale,
            targetTopColor: normalColor,
            targetBottomColor: normalColor,
            targetGlowColor: clearGlow,
            targetGlowPower: 0f,
            targetGlowOuter: 0f
        );
    }

    private void ResetButtonInstant()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        transform.localScale = originalScale;

        if (buttonText != null)
        {
            buttonText.enableVertexGradient = true;
            buttonText.colorGradient = new VertexGradient(normalColor, normalColor, normalColor, normalColor);
        }

        if (textMaterialInstance != null)
        {
            Color clearGlow = new Color(bloodGlowColor.r, bloodGlowColor.g, bloodGlowColor.b, 0f);
            textMaterialInstance.SetColor(ShaderProp_GlowColor, clearGlow);
            textMaterialInstance.SetColor(ShaderProp_UnderlayColor, clearGlow);
            textMaterialInstance.SetFloat(ShaderProp_GlowPower, 0f);
            textMaterialInstance.SetFloat(ShaderProp_GlowOuter, 0f);
        }
    }

    private void StartTransition(Vector3 targetScale, Color targetTopColor, Color targetBottomColor, Color targetGlowColor, float targetGlowPower, float targetGlowOuter)
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(
            AnimateTransition(targetScale, targetTopColor, targetBottomColor, targetGlowColor, targetGlowPower, targetGlowOuter)
        );
    }

    private IEnumerator AnimateTransition(Vector3 targetScale, Color targetTopColor, Color targetBottomColor, Color targetGlowColor, float targetPower, float targetOuter)
    {
        VertexGradient currentGradient = buttonText != null ? buttonText.colorGradient : new VertexGradient(normalColor);
        Color startTopColor = currentGradient.topLeft;
        Color startBottomColor = currentGradient.bottomLeft;

        Color startGlowColor = textMaterialInstance != null ? textMaterialInstance.GetColor(ShaderProp_GlowColor) : Color.clear;
        float startPower = textMaterialInstance != null ? textMaterialInstance.GetFloat(ShaderProp_GlowPower) : 0f;
        float startOuter = textMaterialInstance != null ? textMaterialInstance.GetFloat(ShaderProp_GlowOuter) : 0f;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * transitionSpeed;

            // Scale
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);

            // Blood Gradient Interpolation (Top remains crisp silver, bottom dips into blood red)
            if (buttonText != null)
            {
                Color lerpedTop = Color.Lerp(startTopColor, targetTopColor, t);
                Color lerpedBottom = Color.Lerp(startBottomColor, targetBottomColor, t);

                buttonText.enableVertexGradient = true;
                buttonText.colorGradient = new VertexGradient(lerpedTop, lerpedTop, lerpedBottom, lerpedBottom);
            }

            // Glow / Underlay interpolation
            if (textMaterialInstance != null)
            {
                Color currentGlow = Color.Lerp(startGlowColor, targetGlowColor, t);
                textMaterialInstance.SetColor(ShaderProp_GlowColor, currentGlow);
                textMaterialInstance.SetColor(ShaderProp_UnderlayColor, currentGlow);
                textMaterialInstance.SetFloat(ShaderProp_GlowPower, Mathf.Lerp(startPower, targetPower, t));
                textMaterialInstance.SetFloat(ShaderProp_GlowOuter, Mathf.Lerp(startOuter, targetOuter, t));
            }

            yield return null;
        }

        transform.localScale = targetScale;
        if (buttonText != null)
        {
            buttonText.colorGradient = new VertexGradient(targetTopColor, targetTopColor, targetBottomColor, targetBottomColor);
        }

        transitionCoroutine = null;
    }
}
