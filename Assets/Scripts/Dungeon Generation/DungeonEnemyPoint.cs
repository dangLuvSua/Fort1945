using UnityEngine;

public class DungeonEnemyPoint : MonoBehaviour
{
    [Tooltip("Dapat tumugma sa Point Group ng spawner. Iwanang blangko kung para sa default spawner.")]
    public string group = "";

    private void OnDrawGizmos()
    {
        Gizmos.color = string.IsNullOrEmpty(group) ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
    }
}