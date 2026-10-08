using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : NetworkBehaviour
{
    [Header("Interaction")]
    [SerializeField] private Camera cam;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private GameObject promptText;

    [SerializeField] private float range = 3f;
    [SerializeField] private LayerMask mask = ~0;

    private bool isLocal;

    public override void Spawned()
    {
        isLocal = Object.HasStateAuthority;

        if (uiRoot != null)
            uiRoot.SetActive(isLocal);

        if (promptText != null)
            promptText.SetActive(false);
    }

    private void Update()
    {
        if (!isLocal)
            return;

        if (cam == null)
            return;

        if (inventory == null)
            return;

        Ray ray =
            cam.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

        Debug.DrawRay(
            ray.origin,
            ray.direction * range,
            Color.red
        );

        KeyPickup target = null;

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            range,
            mask,
            QueryTriggerInteraction.Ignore))
        {
            KeyPickup pickup =
                hit.collider.GetComponentInParent<KeyPickup>();

            if (pickup != null &&
                pickup.CanCollect)
            {
                target = pickup;
            }
        }

        if (promptText != null)
        {
            promptText.SetActive(
                target != null
            );
        }

        if (target != null &&
            Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            target.RequestCollect();

            if (promptText != null)
                promptText.SetActive(false);
        }
    }
}