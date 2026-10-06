using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    public const int SlotCount = 8;

    [SerializeField] Transform holdPoint;

    [SerializeField] Transform dropOrigin; // Main Camera

    ItemData[] slots = new ItemData[SlotCount];
    int selected = 0;
    GameObject heldObject;
    bool isLocal;

    public override void Spawned()
    {
        isLocal = HasInputAuthority;
    }

    void Update()
    {
        if (!isLocal || Keyboard.current == null) return;

        for (int i = 0; i < SlotCount; i++)
        {
            var key = (Key)((int)Key.Digit1 + i);
            if (Keyboard.current[key].wasPressedThisFrame)
            {
                Select(i);
                break;
            }
        }
        if (Keyboard.current.gKey.wasPressedThisFrame) Drop();
    }

    [SerializeField] float throwForce = 4f;
    [SerializeField] float throwUp = 1.5f;

    void Drop()
    {
        var item = slots[selected];
        if (item == null || item.worldPrefab == null) return;

        Vector3 pos = dropOrigin.position + dropOrigin.forward * 0.8f;
        var obj = Instantiate(item.worldPrefab, pos, Quaternion.Euler(0f, dropOrigin.eulerAngles.y, 0f));

        // kailangan convex ang MeshCollider bago lagyan ng Rigidbody
        foreach (var mc in obj.GetComponentsInChildren<MeshCollider>()) mc.convex = true;

        var rb = obj.AddComponent<Rigidbody>();
        rb.mass = 0.2f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.AddForce(dropOrigin.forward * throwForce + Vector3.up * throwUp, ForceMode.VelocityChange);
        rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);

        // para hindi tumama sa sarili mong collider
        foreach (var a in obj.GetComponentsInChildren<Collider>())
            foreach (var b in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(a, b);

        slots[selected] = null;
        Refresh();
    }

    public bool TryAdd(ItemData item)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = item;
                Debug.Log($"{item.itemName} napunta sa slot {i + 1}");
                if (i == selected) Refresh();
                return true;
            }
        }

        Debug.Log("Puno na ang inventory!");
        return false;
    }

    public bool Has(ItemData item)
    {
        foreach (var s in slots)
            if (s == item) return true;
        return false;
    }

    void Select(int index)
    {
        if (index == selected) return;
        selected = index;
        Refresh();
    }

    void Refresh()
    {
        if (heldObject != null) Destroy(heldObject);

        var item = slots[selected];
        if (item == null || item.heldPrefab == null) return;

        heldObject = Instantiate(item.heldPrefab, holdPoint);
        heldObject.transform.localPosition = Vector3.zero;
        heldObject.transform.localRotation = Quaternion.identity;

        // visual lang ito, kaya alisin ang collider at pickup script
        foreach (var c in heldObject.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var p in heldObject.GetComponentsInChildren<KeyPickup>()) Destroy(p);
    }
}