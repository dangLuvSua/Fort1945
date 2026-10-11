using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerInteractor : NetworkBehaviour
{
[Header("Interaction")]
[SerializeField] private Camera cam;
[SerializeField] private PlayerInventory inventory;
[SerializeField] private GameObject uiRoot;
[SerializeField] private GameObject promptText;
[SerializeField] private TMP_Text promptLabel;
[SerializeField, Min(0.1f)] private float range = 3f;
[SerializeField] private LayerMask mask = ~0;

[Header("Hold Interaction UI")]
[SerializeField] private InteractionPrompt holdPrompt;
[SerializeField] private bool createFallbackSlider = true;

private bool isLocal;

private IHoldInteractable currentHoldTarget;
private HoldInteractionSliderUI holdSlider;

private float holdProgress;
private bool holdStarted;

public override void Spawned()
{
    isLocal = Object.HasInputAuthority;

    if (uiRoot != null)
        uiRoot.SetActive(isLocal);

    if (!isLocal)
        return;

    if (cam == null)
        cam = GetComponentInChildren<Camera>();

    if (inventory == null)
        inventory = GetComponentInParent<PlayerInventory>();

    if (promptLabel == null && promptText != null)
        promptLabel = promptText.GetComponent<TMP_Text>();

    if (promptText != null)
        promptText.SetActive(false);

    if (holdPrompt == null)
        holdPrompt = FindInteractionPrompt();

    if (holdPrompt != null)
    {
        holdPrompt.Hide();
    }
    else if (createFallbackSlider && uiRoot != null)
    {
        holdSlider =
            HoldInteractionSliderUI.Create(uiRoot.transform);
    }

    if (cam == null)
    {
        Debug.LogError(
            "[INTERACTOR] No camera assigned or found.",
            this
        );
    }

    if (inventory == null)
    {
        Debug.LogError(
            "[INTERACTOR] PlayerInventory not found.",
            this
        );
    }
}

private void Update()
{
    if (!isLocal || cam == null || inventory == null)
        return;

    if (Keyboard.current == null)
        return;

    Ray ray = cam.ViewportPointToRay(
        new Vector3(0.5f, 0.5f, 0f)
    );

    IInteractable target = null;
    int bestPriority = int.MinValue;
    float bestDist = float.MaxValue;

    RaycastHit[] hits = Physics.RaycastAll(
        ray,
        range,
        mask,
        QueryTriggerInteraction.Ignore
    );

    foreach (RaycastHit hit in hits)
    {
        IInteractable candidate =
            hit.collider.GetComponentInParent<IInteractable>();

        if (candidate == null || !candidate.CanInteract)
            continue;

        if (candidate.Priority > bestPriority ||
            (candidate.Priority == bestPriority &&
             hit.distance < bestDist))
        {
            target = candidate;
            bestPriority = candidate.Priority;
            bestDist = hit.distance;
        }
    }

    IHoldInteractable holdTarget =
        target as IHoldInteractable;

    if (holdTarget != null)
        holdTarget.UpdateContext(inventory);

    UpdatePrompt(target, holdTarget);

    // Cancel only when the player stops targeting the object.
    if (currentHoldTarget != null &&
        currentHoldTarget != holdTarget)
    {
        CancelHold();
    }

    if (holdTarget != null &&
        currentHoldTarget == null)
    {
        currentHoldTarget = holdTarget;
        holdProgress = 0f;
        holdStarted = false;
    }

    if (currentHoldTarget != null)
        ProcessHoldInput();

    // Preserve normal press-to-interact behavior.
    if (target != null &&
        holdTarget == null &&
        Keyboard.current.eKey.wasPressedThisFrame)
    {
        target.Interact(inventory);
    }
}

private void UpdatePrompt(
    IInteractable target,
    IHoldInteractable holdTarget)
{
    if (holdTarget != null && holdPrompt != null)
    {
        if (promptText != null)
            promptText.SetActive(false);

        holdPrompt.Show(holdTarget.Prompt);
        return;
    }

    if (holdPrompt != null)
        holdPrompt.Hide();

    if (promptLabel != null)
    {
        promptLabel.text =
            target != null ? target.Prompt : string.Empty;
    }

    if (promptText != null)
        promptText.SetActive(target != null);

    if (holdSlider != null && !holdStarted)
        holdSlider.Hide();
}

private void ProcessHoldInput()
{
    bool ePressed =
        Keyboard.current.eKey.wasPressedThisFrame;

    bool eHeld =
        Keyboard.current.eKey.isPressed;

    if (ePressed && !holdStarted)
    {
        holdStarted = true;
        holdProgress = 0f;

        currentHoldTarget.HoldStarted(inventory);

        if (holdPrompt != null)
        {
            holdPrompt.ShowProgress(
                currentHoldTarget.Prompt,
                0f
            );
        }
        else if (holdSlider != null)
        {
            holdSlider.Show();
            holdSlider.SetProgress(0f);
        }
    }

    if (!holdStarted)
        return;

    if (!eHeld)
    {
        CancelHold();
        return;
    }

    holdProgress += Time.deltaTime;

    float duration =
        Mathf.Max(0.01f, currentHoldTarget.HoldDuration);

    float progress =
        Mathf.Clamp01(holdProgress / duration);

    if (holdPrompt != null)
    {
        holdPrompt.SetProgress(progress);
    }
    else if (holdSlider != null)
    {
        holdSlider.SetProgress(progress);
    }

    if (holdProgress >= duration)
    {
        IHoldInteractable completedTarget =
            currentHoldTarget;

        holdStarted = false;
        holdProgress = 0f;
        currentHoldTarget = null;

        if (holdPrompt != null)
            holdPrompt.Hide();

        if (holdSlider != null)
            holdSlider.Hide();

        completedTarget.HoldCompleted(inventory);
    }
}

private void CancelHold()
{
    if (holdStarted && currentHoldTarget != null)
        currentHoldTarget.HoldCancelled();

    holdStarted = false;
    holdProgress = 0f;
    currentHoldTarget = null;

    if (holdPrompt != null)
        holdPrompt.Hide();

    if (holdSlider != null)
        holdSlider.Hide();
}

private InteractionPrompt FindInteractionPrompt()
{
    InteractionPrompt[] prompts =
        FindObjectsByType<InteractionPrompt>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

    foreach (InteractionPrompt prompt in prompts)
    {
        if (prompt != null &&
            prompt.gameObject.name == "InteractionPrompt")
        {
            return prompt;
        }
    }

    return null;
}

private void OnDisable()
{
    if (holdStarted && currentHoldTarget != null)
        currentHoldTarget.HoldCancelled();

    holdStarted = false;
    holdProgress = 0f;
    currentHoldTarget = null;

    if (holdPrompt != null)
        holdPrompt.Hide();

    if (holdSlider != null)
        holdSlider.Hide();
}


}
