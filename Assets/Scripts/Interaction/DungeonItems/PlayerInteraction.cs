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
private bool ownsPromptInstance;
private InteractionPrompt holdPromptPrefab;

private float holdProgress;
private bool holdStarted;
private bool ownsHoldPrompt;

    public override void Spawned()
    {
        isLocal =
            Object.HasInputAuthority;

        if (uiRoot != null)
        {
            uiRoot.SetActive(
                isLocal
            );
        }

        if (!isLocal)
        {
            return;
        }

        // -----------------------------------------------------
        // FIND PLAYER REFERENCES
        // -----------------------------------------------------

        if (cam == null)
        {
            cam =
                GetComponentInChildren<Camera>();
        }

        if (inventory == null)
        {
            inventory =
                GetComponentInParent<PlayerInventory>();
        }

        if (promptLabel == null &&
            promptText != null)
        {
            promptLabel =
                promptText.GetComponent<TMP_Text>();
        }

        // -----------------------------------------------------
        // REMEMBER PROMPT PREFAB
        // -----------------------------------------------------

        if (holdPrompt != null &&
            !holdPrompt.gameObject.scene.IsValid())
        {
            holdPromptPrefab =
                holdPrompt;
        }

        // -----------------------------------------------------
        // CREATE / FIND INTERACTION UI
        // -----------------------------------------------------

        EnsureInteractionUI();

        // -----------------------------------------------------
        // RESET INTERACTION STATE
        // -----------------------------------------------------

        ResetInteractionState();

        // -----------------------------------------------------
        // VALIDATION
        // -----------------------------------------------------

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

    private void OnEnable()
    {
        // OnEnable can run before Fusion Spawned().
        // Do nothing until we know this is the local player.
        if (!isLocal)
        {
            return;
        }

        // Puzzle mode disables PlayerInteractor.
        //
        // When PuzzleModeController enables it again,
        // rebuild/refresh the interaction UI instead of relying
        // on Spawned(), because Spawned() only runs once.
        EnsureInteractionUI();

        ResetInteractionState();

        Debug.Log(
            "[INTERACTOR] Interaction system re-enabled and refreshed."
        );
    }

private void Update()
{
    if (!isLocal || cam == null || inventory == null)
        return;

    if (Keyboard.current == null)
        return;

    if (PlayerInventoryUI.EvidenceSelectionOpen)
    {
        CancelHold();
        UpdatePrompt(null, null);
        return;
    }

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

        InterrogationRoomGateUnlock[] roomGates =
            FindObjectsByType<InterrogationRoomGateUnlock>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

        Transform playerRoot =
            inventory.transform.root;

        // -----------------------------------------------------
        // INSIDE INTERACTION ZONE
        // -----------------------------------------------------

        foreach (InterrogationRoomGateUnlock gate
                 in roomGates)
        {
            if (gate != null &&
                gate.CanInteract &&
                gate.IsPlayerInInsideInteractionZone(
                    playerRoot
                ))
            {
                target =
                    gate;

                break;
            }
        }

        // =====================================================
        // PROXIMITY / TRIGGER FALLBACKS
        // =====================================================

        if (target == null)
        {
            DungeonGateUnlock[] gates =
                FindObjectsByType<DungeonGateUnlock>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                );

            // -------------------------------------------------
            // DUNGEON LOCK TRIGGER
            // -------------------------------------------------

            foreach (DungeonGateUnlock gate
                     in gates)
            {
                if (gate != null &&
                    gate.CanInteract &&
                    gate.IsPlayerInsideLockTrigger(
                        playerRoot
                    ))
                {
                    target =
                        gate;

                    break;
                }
            }

            // -------------------------------------------------
            // INTERROGATION GATE PROXIMITY
            // -------------------------------------------------

            if (target == null)
            {
                foreach (InterrogationRoomGateUnlock gate
                         in roomGates)
                {
                    if (gate != null &&
                        gate.CanInteract &&
                        gate.IsPlayerNearGate(
                            playerRoot,
                            range
                        ))
                    {
                        target =
                            gate;

                        break;
                    }
                }
            }
        }

        // =====================================================
        // HOLD TARGET
        // =====================================================

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

    if (holdTarget is IQuickTapHoldInteractable quickTapTarget &&
        quickTapTarget.CanTapInteract)
    {
        if (currentHoldTarget != null)
            CancelHold();

        if (Keyboard.current.eKey.wasPressedThisFrame)
            quickTapTarget.TapInteract(inventory);

        return;
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
        (target is PolaroidInteractable
            ? Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            : Keyboard.current.eKey.wasPressedThisFrame))
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
        if (currentHoldTarget == null ||
            Keyboard.current == null)
        {
            return;
        }

        Key holdKey =
            currentHoldTarget is IKeyHoldInteractable keyHold
                ? keyHold.HoldKey
                : Key.E;

        bool keyPressed =
            Keyboard.current[
                holdKey
            ].wasPressedThisFrame;

        bool keyHeld =
            Keyboard.current[
                holdKey
            ].isPressed;

        // -----------------------------------------------------
        // HOLD START
        // -----------------------------------------------------

        if (keyPressed &&
            !holdStarted)
        {
            holdStarted =
                true;

            holdProgress =
                0f;

            currentHoldTarget.HoldStarted(
                inventory
            );

            if (holdPrompt != null)
            {
                holdPrompt.ShowProgressFor(
                    this,
                    currentHoldTarget.Prompt,
                    0f
                );

                ownsHoldPrompt =
                    true;
            }
            else if (holdSlider != null)
            {
                holdSlider.Show();

                holdSlider.SetProgress(
                    0f
                );
            }
        }

        if (!holdStarted)
        {
            return;
        }

        // -----------------------------------------------------
        // PLAYER RELEASED KEY
        // -----------------------------------------------------

        if (!keyHeld)
        {
            CancelHold();

            return;
        }

        // -----------------------------------------------------
        // ADVANCE HOLD
        // -----------------------------------------------------

        holdProgress +=
            Time.deltaTime;

        float duration =
            Mathf.Max(
                0.01f,
                currentHoldTarget.HoldDuration
            );

        float progress =
            Mathf.Clamp01(
                holdProgress /
                duration
            );

        if (holdPrompt != null)
        {
            holdPrompt.SetProgressFor(
                this,
                progress
            );
        }
        else if (holdSlider != null)
        {
            holdSlider.SetProgress(
                progress
            );
        }

        // -----------------------------------------------------
        // HOLD COMPLETE
        // -----------------------------------------------------

        if (holdProgress >= duration)
        {
            IHoldInteractable completedTarget =
                currentHoldTarget;

            holdStarted =
                false;

            holdProgress =
                0f;

            currentHoldTarget =
                null;

            if (holdPrompt != null &&
                ownsHoldPrompt)
            {
                holdPrompt.HideFor(
                    this
                );

                ownsHoldPrompt =
                    false;
            }

            if (holdSlider != null)
            {
                holdSlider.Hide();
            }

            completedTarget.HoldCompleted(
                inventory
            );
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

    private void EnsureInteractionUI()
    {
        if (!isLocal)
        {
            return;
        }

        // -----------------------------------------------------
        // NORMAL TEXT
        // -----------------------------------------------------

        if (promptLabel == null &&
            promptText != null)
        {
            promptLabel =
                promptText.GetComponent<TMP_Text>();
        }

        // -----------------------------------------------------
        // VALID LIVE HOLD PROMPT ALREADY EXISTS
        // -----------------------------------------------------

        if (holdPrompt != null &&
            holdPrompt.gameObject.scene.IsValid())
        {
            holdPrompt.gameObject.SetActive(
                true
            );

            holdPrompt.Hide();

            ownsHoldPrompt =
                false;

            return;
        }

        // -----------------------------------------------------
        // REMEMBER PREFAB
        // -----------------------------------------------------

        if (holdPrompt != null &&
            !holdPrompt.gameObject.scene.IsValid())
        {
            holdPromptPrefab =
                holdPrompt;
        }

        // -----------------------------------------------------
        // LOOK UNDER THIS PLAYER'S UI FIRST
        // -----------------------------------------------------

        holdPrompt =
            FindInteractionPromptUnderUIRoot();

        // -----------------------------------------------------
        // LEGACY GLOBAL FALLBACK
        // -----------------------------------------------------

        if (holdPrompt == null)
        {
            holdPrompt =
                FindInteractionPrompt();
        }

        // -----------------------------------------------------
        // CREATE LOCAL PROMPT
        // -----------------------------------------------------

        if (holdPrompt == null &&
            holdPromptPrefab != null &&
            uiRoot != null)
        {
            holdPrompt =
                Instantiate(
                    holdPromptPrefab,
                    uiRoot.transform,
                    false
                );

            holdPrompt.gameObject.name =
                "InteractionPrompt";

            ownsPromptInstance =
                true;
        }

        // -----------------------------------------------------
        // INITIALIZE HOLD PROMPT
        // -----------------------------------------------------

        if (holdPrompt != null)
        {
            holdPrompt.gameObject.SetActive(
                true
            );

            holdPrompt.Hide();

            ownsHoldPrompt =
                false;

            return;
        }

        // -----------------------------------------------------
        // FALLBACK SLIDER
        // -----------------------------------------------------

        if (createFallbackSlider &&
            uiRoot != null &&
            holdSlider == null)
        {
            holdSlider =
                HoldInteractionSliderUI.Create(
                    uiRoot.transform
                );
        }
    }

    private InteractionPrompt FindInteractionPromptUnderUIRoot()
    {
        if (uiRoot == null)
        {
            return null;
        }

        InteractionPrompt[] prompts =
            uiRoot.GetComponentsInChildren<InteractionPrompt>(
                true
            );

        foreach (InteractionPrompt prompt
                 in prompts)
        {
            if (prompt != null &&
                prompt.gameObject.name ==
                "InteractionPrompt")
            {
                return prompt;
            }
        }

        return null;
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
        // IMPORTANT:
        //
        // Do NOT destroy the InteractionPrompt here.
        //
        // PuzzleModeController temporarily disables this
        // component. If we destroy the prompt here, Fusion's
        // Spawned() will not run again when this component is
        // re-enabled.

        ResetInteractionState();
    }

    public void ResetInteractionState()
    {
        // -----------------------------------------------------
        // CANCEL ACTIVE HOLD
        // -----------------------------------------------------

        if (holdStarted &&
            currentHoldTarget != null)
        {
            currentHoldTarget.HoldCancelled();
        }

        holdStarted =
            false;

        holdProgress =
            0f;

        currentHoldTarget =
            null;

        // -----------------------------------------------------
        // RESET NORMAL PROMPT
        // -----------------------------------------------------

        if (promptText != null)
        {
            promptText.SetActive(
                false
            );
        }

        if (promptLabel != null)
        {
            promptLabel.text =
                string.Empty;
        }

        // -----------------------------------------------------
        // RESET HOLD PROMPT
        // -----------------------------------------------------

        if (holdPrompt != null)
        {
            holdPrompt.HideFor(
                this
            );

            holdPrompt.Hide();

            ownsHoldPrompt =
                false;
        }

        // -----------------------------------------------------
        // RESET FALLBACK SLIDER
        // -----------------------------------------------------

        if (holdSlider != null)
        {
            holdSlider.Hide();
        }
    }

    private void OnDestroy()
    {
        // Only clean up the prompt when the actual player object
        // is being destroyed, NOT when puzzle mode temporarily
        // disables PlayerInteractor.

        if (ownsPromptInstance &&
            holdPrompt != null)
        {
            Destroy(
                holdPrompt.gameObject
            );

            holdPrompt =
                null;

            ownsPromptInstance =
                false;
        }
    }

}
