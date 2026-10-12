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
private bool ownsHoldPrompt;

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

    // PlayerRoot can serialize a reference to the prompt prefab asset rather
    // than the live prompt instance in this client's loaded scene. UI methods
    // must target the scene instance so the prompt and slider are visible.
    if (holdPrompt == null || !holdPrompt.gameObject.scene.IsValid())
        holdPrompt = FindInteractionPrompt();

    if (holdPrompt != null)
    {
        ownsHoldPrompt = false;
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

        if (candidate is IHoldInteractable candidateHold)
            candidateHold.UpdateContext(inventory);

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

    // Locktrigger is intentionally a trigger volume and is ignored by the
    // raycast above. Allow the gate interaction to activate from proximity
    // while the player stands in that volume, without requiring a precise aim.
    if (target == null)
    {
        DungeonGateUnlock[] gates = FindObjectsByType<DungeonGateUnlock>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        Transform playerRoot = inventory.transform.root;
        foreach (DungeonGateUnlock gate in gates)
        {
            if (gate != null && gate.CanInteract &&
                gate.IsPlayerInsideLockTrigger(playerRoot))
            {
                target = gate;
                break;
            }
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

        holdPrompt.ShowFor(this, holdTarget.Prompt);
        ownsHoldPrompt = true;
        return;
    }

    if (holdPrompt != null && ownsHoldPrompt)
    {
        holdPrompt.HideFor(this);
        ownsHoldPrompt = false;
    }

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
    Key holdKey = currentHoldTarget is IKeyHoldInteractable keyHold
        ? keyHold.HoldKey
        : Key.E;
    bool ePressed = Keyboard.current[holdKey].wasPressedThisFrame;
    bool eHeld = Keyboard.current[holdKey].isPressed;

    if (ePressed && !holdStarted)
    {
        holdStarted = true;
        holdProgress = 0f;

        currentHoldTarget.HoldStarted(inventory);

        if (holdPrompt != null)
        {
            holdPrompt.ShowProgressFor(
                this,
                currentHoldTarget.Prompt,
                0f
            );
            ownsHoldPrompt = true;
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
        holdPrompt.SetProgressFor(this, progress);
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

        if (holdPrompt != null && ownsHoldPrompt)
        {
            holdPrompt.HideFor(this);
            ownsHoldPrompt = false;
        }

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

    if (holdPrompt != null && ownsHoldPrompt)
    {
        holdPrompt.HideFor(this);
        ownsHoldPrompt = false;
    }

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

    if (holdPrompt != null && ownsHoldPrompt)
    {
        holdPrompt.HideFor(this);
        ownsHoldPrompt = false;
    }

    if (holdSlider != null)
        holdSlider.Hide();
}


}
