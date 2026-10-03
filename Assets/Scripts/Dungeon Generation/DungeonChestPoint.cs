using UnityEngine;

// Marker lang ito. Ilagay sa empty GameObject sa loob ng room prefab.
public class DungeonChestPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.25f, new Vector3(0.8f, 0.5f, 0.5f));
        Gizmos.DrawRay(transform.position, transform.forward);
    }
}