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
    [SerializeField] TMP_Text promptLabel; // yung TMP component ng PromptText
    [SerializeField] private float range = 3f;
    [SerializeField] private LayerMask mask = ~0;

    private bool isLocal;

    public override void Spawned()
    {
        isLocal = Object.HasStateAuthority;

        if (uiRoot != null)
            uiRoot.SetActive(isLocal);

        if (promptText != null)
            promptLabel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isLocal) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        IInteractable target = null;
        int bestPriority = int.MinValue;
        float bestDist = float.MaxValue;

        var hits = Physics.RaycastAll(ray, range, mask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            var it = h.collider.GetComponentInParent<IInteractable>();
            if (it == null || !it.CanInteract) continue;

            if (it.Priority > bestPriority ||
                (it.Priority == bestPriority && h.distance < bestDist))
            {
                target = it;
                bestPriority = it.Priority;
                bestDist = h.distance;
            }
        }

        promptLabel.gameObject.SetActive(target != null);
        if (target != null) promptLabel.text = target.Prompt;

        if (target != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            target.Interact(inventory);
        }
    }
}