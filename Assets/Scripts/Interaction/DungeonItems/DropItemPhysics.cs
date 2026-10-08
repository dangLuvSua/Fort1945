using Fusion;
using UnityEngine;

public class DroppedItemPhysics : NetworkBehaviour
{
    Rigidbody rb;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        // kailangang convex ang MeshCollider kapag may non-kinematic Rigidbody
        foreach (var mc in GetComponentsInChildren<MeshCollider>())
            mc.convex = true;

        // host lang ang may physics, ang iba ay sumusunod lang sa NetworkTransform
        rb.isKinematic = !Object.HasStateAuthority;
    }

    public void Launch(Vector3 velocity, Vector3 torque)
    {
        if (!Object.HasStateAuthority) return;
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.isKinematic = false;
        rb.AddForce(velocity, ForceMode.VelocityChange);
        rb.AddTorque(torque, ForceMode.VelocityChange);
    }
}