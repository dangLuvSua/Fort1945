using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
//  MissionPanel — Mission Objective UI
// =========================================================

public class MissionPanel : MonoBehaviour
{
    // =========================================================
    // Inspector Fields — References
    // =========================================================

    [Header("References")]

    [Tooltip("The root GameObject of the panel.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("TextMeshPro label used for the mission title.")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("TextMeshPro label used for the mission description.")]
    [SerializeField] private TMP_Text descriptionText;

    [Tooltip("Image component used for the optional mission icon.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Container GameObject that wraps the icon.")]
    [SerializeField] private GameObject iconContainer;

    [Tooltip("Button used to acknowledge and close the mission panel.")]
    [SerializeField] private Button okButton;


    // =========================================================
    // Inspector Fields — Animation
    // =========================================================

    [Header("Animation")]

    [Tooltip("Uses Animator if assigned, otherwise instant show/hide.")]
    [SerializeField] private Animator panelAnimator;

    [Tooltip("Animator trigger parameter name used to play the show animation.")]
    [SerializeField] private string showTrigger = "Show";

    [Tooltip("Animator trigger parameter name used to play the hide animation.")]
    [SerializeField] private string hideTrigger = "Hide";


    // =========================================================
    // Inspector Fields — Auto-hide
    // =========================================================

    [Header("Auto-hide")]

    [Tooltip("Seconds before auto-hiding. Overridden per-call. 0 = persistent.")]
    [SerializeField] private float defaultDuration = 0f;


    // =========================================================
    // Internal State
    // =========================================================

    private Coroutine autoHideCoroutine;
    private bool isShowing;


    // =========================================================
    // Unity Lifecycle
    // =========================================================

    private void Awake()
    {
        if (panelRoot == null)
        {
            Debug.LogError(
                "[MISSION PANEL] panelRoot is not assigned! Panel will not function correctly."
            );

            return;
        }

        // Start hidden.
        panelRoot.SetActive(false);
        isShowing = false;

        // Connect OK button automatically.
        if (okButton != null)
        {
            okButton.onClick.RemoveListener(OnOkButtonClicked);
            okButton.onClick.AddListener(OnOkButtonClicked);
        }
        else
        {
            Debug.LogWarning(
                "[MISSION PANEL] OK Button is not assigned."
            );
        }
    }


    private void OnDestroy()
    {
        if (okButton != null)
        {
            okButton.onClick.RemoveListener(OnOkButtonClicked);
        }
    }


    // =========================================================
    // OK Button
    // =========================================================

    /// <summary>
    /// Called when the player presses the OK button.
    /// </summary>
    private void OnOkButtonClicked()
    {
        Debug.Log("[MISSION PANEL] OK button pressed.");

        MissionManager.Hide();
    }


    // =========================================================
    // Public API — Show
    // =========================================================

    public void Show(
        string title,
        string description,
        Sprite icon,
        float duration,
        Color titleColor,
        Color descColor)
    {
        // Stop previous auto-hide.
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }


        // -----------------------------------------------------
        // Title
        // -----------------------------------------------------

        if (titleText != null)
        {
            titleText.text = title;
            titleText.color = titleColor;
        }
        else
        {
            Debug.LogWarning(
                "[MISSION PANEL] titleText reference is not assigned."
            );
        }


        // -----------------------------------------------------
        // Description
        // -----------------------------------------------------

        if (descriptionText != null)
        {
            descriptionText.text = description;
            descriptionText.color = descColor;
        }
        else
        {
            Debug.LogWarning(
                "[MISSION PANEL] descriptionText reference is not assigned."
            );
        }


        // -----------------------------------------------------
        // Icon
        // -----------------------------------------------------

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
                if (iconContainer != null)
                    iconContainer.SetActive(false);
            }
        }


        // -----------------------------------------------------
        // Show Panel
        // -----------------------------------------------------

        if (panelRoot != null)
            panelRoot.SetActive(true);


        // -----------------------------------------------------
        // Show Animation
        // -----------------------------------------------------

        if (panelAnimator != null)
            panelAnimator.SetTrigger(showTrigger);


        isShowing = true;


        // -----------------------------------------------------
        // Auto Hide
        // -----------------------------------------------------

        float effectiveDuration =
            duration > 0f ? duration : defaultDuration;

        if (effectiveDuration > 0f)
        {
            autoHideCoroutine =
                StartCoroutine(AutoHideCoroutine(effectiveDuration));
        }


        Debug.Log("[MISSION PANEL] Showing: " + title);
    }


    // =========================================================
    // Public API — Hide
    // =========================================================

    public void Hide()
    {
        if (!isShowing)
            return;


        // Stop auto-hide.
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }


        // -----------------------------------------------------
        // Hide Animation
        // -----------------------------------------------------

        if (panelAnimator != null)
        {
            panelAnimator.SetTrigger(hideTrigger);

            StartCoroutine(
                DeactivateAfterDelay(0.5f)
            );
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
    // Coroutines
    // =========================================================

    private IEnumerator AutoHideCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        Hide();
    }


    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}