using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : NetworkBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] PlayerInventory inventory;
    [SerializeField] GameObject uiRoot;      // yung Canvas
    [SerializeField] GameObject promptText;  // yung "Press E" text
    [SerializeField] float range = 3f;
    [SerializeField] LayerMask mask = ~0;

    bool isLocal;

    public override void Spawned()
    {
        isLocal = HasInputAuthority;
        if (uiRoot) uiRoot.SetActive(isLocal); // sa sarili mo lang lalabas ang crosshair
        if (promptText) promptText.SetActive(false);
    }

    void Update()
    {
        if (!isLocal) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Debug.DrawRay(ray.origin, ray.direction * range, Color.red);

        KeyPickup target = null;

        if (Physics.Raycast(ray, out RaycastHit hit, range, mask, QueryTriggerInteraction.Ignore))
        {
            var k = hit.collider.GetComponentInParent<KeyPickup>();
            if (k != null && k.CanCollect) target = k;
        }

        promptText.SetActive(target != null);

        if (target != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            target.Collect(inventory);
            promptText.SetActive(false);
        }
    }
}