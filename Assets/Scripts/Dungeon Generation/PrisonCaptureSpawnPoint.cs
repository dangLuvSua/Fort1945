using UnityEngine;

/// <summary>Marks the cell position used when a soldier captures a player.</summary>
public sealed class PrisonCaptureSpawnPoint : MonoBehaviour
{
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawRay(transform.position, transform.forward * 0.75f);
    }
}
