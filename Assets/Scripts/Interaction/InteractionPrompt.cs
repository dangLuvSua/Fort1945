using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractionPrompt : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;

    [Header("Screen Size")]
    [Tooltip("Fixed prompt dimensions in reference Canvas units.")]
    [SerializeField] private Vector2 fixedPromptSize = new Vector2(466f, 96f);
    [Tooltip("Prompt offset from screen center in reference Canvas units.")]
    [SerializeField] private Vector2 fixedPromptOffset = new Vector2(-61.75f, -19.23f);

    [Header("Default Text")]
    [SerializeField] private string defaultMessage = "[E] INTERACT";

    [Header("Hold Progress")]
    [Tooltip("Optional slider shown while the player holds an interaction. Auto-found in children.")]
    [SerializeField] private Slider progressSlider;

    // Different interactions can share one prompt instance. Only the owner
    // currently showing it may update or hide it.
    private object activeOwner;
    private RectTransform promptRect;
    private Canvas rootCanvas;
    private Vector3 authoredScale;

    private void Awake()
    {
        ApplyFixedLayout();

        if (progressSlider == null)
        {
            progressSlider =
                GetComponentInChildren<Slider>(true);
        }

        if (progressSlider != null)
        {
            progressSlider.value = 0f;
            progressSlider.gameObject.SetActive(false);
        }

        Hide();
    }

    private void LateUpdate()
    {
        if (promptRect == null)
            return;

        if (rootCanvas == null && promptRoot != null)
        {
            Canvas parentCanvas = promptRoot.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                rootCanvas = parentCanvas.rootCanvas;
        }

        if (rootCanvas == null || rootCanvas.scaleFactor <= 0f)
            return;

        // CanvasScaler enlarges all UI on large displays. Counter its scale
        // only for this prompt so its on-screen dimensions remain unchanged.
        promptRect.localScale = authoredScale / rootCanvas.scaleFactor;
    }

    private void ApplyFixedLayout()
    {
        if (promptRoot == null)
            return;

        promptRect = promptRoot.GetComponent<RectTransform>();
        if (promptRect == null)
            return;

        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.sizeDelta = fixedPromptSize;
        promptRect.anchoredPosition = fixedPromptOffset;
        authoredScale = promptRect.localScale;

        Canvas parentCanvas = promptRoot.GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            rootCanvas = parentCanvas.rootCanvas;
    }

    // =============================================================
    // SHOW DEFAULT MESSAGE
    // =============================================================

    public void Show()
    {
        Show(defaultMessage);
    }

    // =============================================================
    // SHOW CUSTOM MESSAGE
    // =============================================================

    public void Show(string message)
    {
        if (promptText != null)
        {
            promptText.text = message;
        }

        if (promptRoot != null)
        {
            promptRoot.SetActive(true);
        }
    }

    public void ShowFor(object owner, string message)
    {
        if (owner == null)
            return;

        activeOwner = owner;
        Show(message);
    }

    // =============================================================
    // HOLD PROGRESS
    // =============================================================

    public bool HasProgressSlider => progressSlider != null;

    /// <summary>
    /// Shows the prompt plus the hold-progress slider at the given value.
    /// </summary>
    public void ShowProgress(string message, float progress)
    {
        Show(message);

        if (progressSlider != null)
        {
            progressSlider.gameObject.SetActive(true);
            progressSlider.value = Mathf.Clamp01(progress);
        }
    }

    public void ShowProgressFor(object owner, string message, float progress)
    {
        if (owner == null)
            return;

        activeOwner = owner;
        ShowProgress(message, progress);
    }

    /// <summary>
    /// Updates only the hold-progress slider value.
    /// </summary>
    public void SetProgress(float progress)
    {
        if (progressSlider != null)
        {
            progressSlider.value = Mathf.Clamp01(progress);
        }
    }

    public void SetProgressFor(object owner, float progress)
    {
        if (activeOwner == owner)
            SetProgress(progress);
    }

    // =============================================================
    // HIDE
    // =============================================================

    public void Hide()
    {
        activeOwner = null;

        if (promptRoot != null)
        {
            promptRoot.SetActive(false);
        }

        if (progressSlider != null)
        {
            progressSlider.value = 0f;
            progressSlider.gameObject.SetActive(false);
        }
    }

    public void HideFor(object owner)
    {
        if (activeOwner == owner)
            Hide();
    }
}
