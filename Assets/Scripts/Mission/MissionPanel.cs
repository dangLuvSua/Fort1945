using System.Collections;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
//  MissionPanel — Mission Objective UI
// =========================================================

public class MissionPanel : MonoBehaviour
{
    private static MissionPanel activePanel;

    public static bool IsOpen => activePanel != null &&
        activePanel.panelRoot != null && activePanel.panelRoot.activeInHierarchy;

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
    private Coroutine deactivateCoroutine;
    private bool isShowing;
    private Action pendingAcknowledge;
    private GameObject canvasBackgroundObject;
    private Image canvasBackgroundImage;
    private bool ownsCursor;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorVisible;


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
        SetCanvasBackgroundVisible(false);

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


    private void LateUpdate()
    {
        if (!ownsCursor)
            return;

        if (!IsOpen)
        {
            ReleaseCursor();
            return;
        }

        // Keep the OK button usable if another gameplay system locks the mouse.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDisable()
    {
        ReleaseCursor();
    }

    private void OnDestroy()
    {
        ReleaseCursor();

        if (canvasBackgroundObject != null)
            canvasBackgroundObject.SetActive(false);

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

        Action acknowledge = pendingAcknowledge;
        pendingAcknowledge = null;

        MissionManager.Hide();

        if (acknowledge != null)
        {
            acknowledge.Invoke();
            return;
        }

        // Each client starts its own local progress HUD only after
        // acknowledging the briefing.
        NetworkGameManager manager = NetworkGameManager.Instance;
        if (manager == null || !manager.IsNetworkStateReady ||
            !manager.SoldierReachedDoor || !manager.MissionItemsSpawned)
            return;

        FortMissionItemSpawner spawner =
            FindFirstObjectByType<FortMissionItemSpawner>(FindObjectsInactive.Include);
        if (spawner != null)
            spawner.BeginProgressTracking();
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
        Color descColor,
        Action onAcknowledge = null)
    {
        pendingAcknowledge = onAcknowledge;
        // Stop previous auto-hide.
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        if (deactivateCoroutine != null)
        {
            StopCoroutine(deactivateCoroutine);
            deactivateCoroutine = null;
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

        if (iconImage != null && icon != null)
        {
            iconImage.sprite = icon;
        }

        // ShowRaw (used for interaction errors) passes a null icon. Always
        // toggle the container independently so stale mission icons cannot
        // remain visible when the panel is reused.
        if (iconContainer != null)
            iconContainer.SetActive(icon != null);


        // -----------------------------------------------------
        // Show Panel
        // -----------------------------------------------------

        SetCanvasBackgroundVisible(true);
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            EnsurePanelOnTop();
            AcquireCursor();
        }


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

            deactivateCoroutine = StartCoroutine(DeactivateAfterDelay(0.5f));
        }
        else
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            SetCanvasBackgroundVisible(false);
            ReleaseCursor();
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

        SetCanvasBackgroundVisible(false);
        ReleaseCursor();
        deactivateCoroutine = null;
    }

    private void AcquireCursor()
    {
        // Replacing the message must preserve the state from the first Show.
        if (!ownsCursor)
        {
            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            ownsCursor = true;
        }

        activePanel = this;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ReleaseCursor()
    {
        if (!ownsCursor)
            return;

        ownsCursor = false;
        if (activePanel == this)
            activePanel = null;

        bool menuOpen = InGameOptionsController.Instance != null &&
            InGameOptionsController.Instance.IsMenuOpen;
        Cursor.lockState = menuOpen ? CursorLockMode.None : previousCursorLockState;
        Cursor.visible = menuOpen || previousCursorVisible;
    }

    private void SetCanvasBackgroundVisible(bool visible)
    {
        if (canvasBackgroundObject == null)
        {
            Canvas targetCanvas = GetComponentInParent<Canvas>(true);
            if (targetCanvas == null)
                return;

            Transform existing = targetCanvas.transform.Find("MissionPanelBackgroundOverlay");
            if (existing != null)
            {
                canvasBackgroundObject = existing.gameObject;
                canvasBackgroundImage = existing.GetComponent<Image>();
            }
            else
            {
                canvasBackgroundObject = new GameObject(
                    "MissionPanelBackgroundOverlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                canvasBackgroundObject.transform.SetParent(targetCanvas.transform, false);
                canvasBackgroundObject.transform.SetAsFirstSibling();

                RectTransform rect = (RectTransform)canvasBackgroundObject.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                canvasBackgroundImage = canvasBackgroundObject.GetComponent<Image>();
                canvasBackgroundImage.color = new Color(0f, 0f, 0f, 0.75f);
                canvasBackgroundImage.raycastTarget = false;
            }
        }

        if (canvasBackgroundImage != null)
        {
            canvasBackgroundImage.color = new Color(0f, 0f, 0f, 0.75f);
            canvasBackgroundImage.raycastTarget = false;
        }

        if (canvasBackgroundObject != null)
            canvasBackgroundObject.SetActive(visible);
    }

    private void EnsurePanelOnTop()
    {
        Canvas sortingCanvas = panelRoot.GetComponent<Canvas>();
        if (sortingCanvas == null)
            sortingCanvas = panelRoot.AddComponent<Canvas>();

        sortingCanvas.overrideSorting = true;
        sortingCanvas.sortingOrder = 100;

        // A nested Canvas needs its own GraphicRaycaster for the OK button.
        GraphicRaycaster raycaster = panelRoot.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = panelRoot.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;

        // The full-panel background is visual only; let clicks reach buttons.
        Image background = panelRoot.GetComponent<Image>();
        if (background != null)
            background.raycastTarget = false;

        panelRoot.transform.SetAsLastSibling();
    }
}
