using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
//  MissionPanel  —  MonoBehaviour
//  Owns and drives all UI elements of the mission-objective
//  panel.  Supports an optional Animator for transitions;
//  falls back to instant show/hide if none is assigned.
//
//  Driven externally by MissionManager — do not call
//  Show/Hide directly from gameplay code; use the static
//  MissionManager API instead.
// =========================================================

/// <summary>
/// Controls the visibility and content of the mission-objective
/// HUD panel.  Attach this component to the root Canvas GameObject
/// that contains the panel UI.
/// </summary>
public class MissionPanel : MonoBehaviour
{
    // =========================================================
    //  Inspector Fields — References
    // =========================================================

    [Header("References")]

    [Tooltip("The root GameObject of the panel. Activated/deactivated to show or hide the panel.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("TextMeshPro label used for the mission title.")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("TextMeshPro label used for the mission description body.")]
    [SerializeField] private TMP_Text descriptionText;

    [Tooltip("Image component used to display the optional mission icon sprite.")]
    [SerializeField] private Image iconImage;          // Can be null

    [Tooltip("Container GameObject that wraps the icon — hidden when no icon is provided.")]
    [SerializeField] private GameObject iconContainer; // Hidden when no icon

    // =========================================================
    //  Inspector Fields — Animation
    // =========================================================

    [Header("Animation")]

    [Tooltip("Uses Animator if assigned, otherwise instant show/hide.")]
    [SerializeField] private Animator panelAnimator;

    [Tooltip("Animator trigger parameter name used to play the show animation.")]
    [SerializeField] private string showTrigger = "Show";

    [Tooltip("Animator trigger parameter name used to play the hide animation.")]
    [SerializeField] private string hideTrigger = "Hide";

    // =========================================================
    //  Inspector Fields — Auto-hide
    // =========================================================

    [Header("Auto-hide")]

    [Tooltip("Seconds before auto-hiding. Overridden per-call. 0 = persistent.")]
    [SerializeField] private float defaultDuration = 0f;

    // =========================================================
    //  Internal State
    // =========================================================

    /// <summary>Reference to the running auto-hide coroutine, if any.</summary>
    private Coroutine autoHideCoroutine;

    /// <summary>Whether the panel is currently in the shown state.</summary>
    private bool isShowing;

    // =========================================================
    //  Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (panelRoot == null)
        {
            Debug.LogError("[MISSION PANEL] panelRoot is not assigned! Panel will not function correctly.");
            return;
        }

        // Always start hidden so the panel does not flash on scene load.
        panelRoot.SetActive(false);
        isShowing = false;
    }

    // =========================================================
    //  Public API — Show
    // =========================================================

    /// <summary>
    /// Populates all panel UI elements and makes the panel visible.
    /// If the panel is already showing, the content is replaced and
    /// the auto-hide timer (if any) is restarted.
    /// </summary>
    /// <param name="title">Heading text.</param>
    /// <param name="description">Body/description text.</param>
    /// <param name="icon">Optional icon sprite; pass null to hide the icon container.</param>
    /// <param name="duration">Seconds before auto-hide; 0 = persistent until manually hidden.</param>
    /// <param name="titleColor">Color applied to the title label.</param>
    /// <param name="descColor">Color applied to the description label.</param>
    public void Show(string title, string description, Sprite icon, float duration, Color titleColor, Color descColor)
    {
        // ---- Stop any pending auto-hide ----
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        // ---- Populate title ----
        if (titleText != null)
        {
            titleText.text  = title;
            titleText.color = titleColor;
        }
        else
        {
            Debug.LogWarning("[MISSION PANEL] titleText reference is not assigned.");
        }

        // ---- Populate description ----
        if (descriptionText != null)
        {
            descriptionText.text  = description;
            descriptionText.color = descColor;
        }
        else
        {
            Debug.LogWarning("[MISSION PANEL] descriptionText reference is not assigned.");
        }

        // ---- Icon ----
        if (iconImage != null)
        {
            if (icon != null)
            {
                iconImage.sprite = icon;

                if (iconContainer != null)
                    iconContainer.SetActive(true);
            }
            else
            {
                // No icon provided — hide the container so layout stays clean.
                if (iconContainer != null)
                    iconContainer.SetActive(false);
            }
        }

        // ---- Activate panel root ----
        if (panelRoot != null)
            panelRoot.SetActive(true);

        // ---- Trigger show animation (if Animator is assigned) ----
        if (panelAnimator != null)
            panelAnimator.SetTrigger(showTrigger);

        isShowing = true;

        // ---- Schedule auto-hide (use per-call duration; fall back to inspector default) ----
        float effectiveDuration = duration > 0f ? duration : defaultDuration;
        if (effectiveDuration > 0f)
            autoHideCoroutine = StartCoroutine(AutoHideCoroutine(effectiveDuration));

        Debug.Log("[MISSION PANEL] Showing: " + title);
    }

    // =========================================================
    //  Public API — Hide
    // =========================================================

    /// <summary>
    /// Hides the mission panel.  If an Animator is assigned the hide
    /// trigger is fired and the panel root is deactivated after a short
    /// delay to allow the animation to finish; otherwise it is
    /// deactivated immediately.
    /// </summary>
    public void Hide()
    {
        if (!isShowing)
            return;

        // ---- Stop any pending auto-hide ----
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        // ---- Play hide animation or deactivate immediately ----
        if (panelAnimator != null)
        {
            panelAnimator.SetTrigger(hideTrigger);
            StartCoroutine(DeactivateAfterDelay(0.5f));
        }
        else
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        isShowing = false;
        Debug.Log("[MISSION PANEL] Hidden.");
    }

    // =========================================================
    //  Private Coroutines
    // =========================================================

    /// <summary>
    /// Waits for <paramref name="delay"/> seconds then calls <see cref="Hide"/>.
    /// </summary>
    private IEnumerator AutoHideCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        Hide();
    }

    /// <summary>
    /// Waits for <paramref name="delay"/> seconds then deactivates
    /// <see cref="panelRoot"/>. Used to let the hide animation finish
    /// before the GameObject is turned off.
    /// </summary>
    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
