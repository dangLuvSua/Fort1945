using UnityEngine;

/// <summary>
/// Place this on a trigger just beyond the unlocked dungeon gate. The host
/// loads the Dungeon scene after the gate is unlocked and every active player
/// has walked into the trigger volume.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class DungeonSceneTeleportTrigger : MonoBehaviour
{
    private Collider teleportTrigger;

    private void Awake()
    {
        teleportTrigger = GetComponent<Collider>();
        teleportTrigger.isTrigger = true;
    }

    private void Update()
    {
        if (teleportTrigger == null || !teleportTrigger.enabled)
            return;

        NetworkGameManager manager = NetworkGameManager.Instance;
        if (manager == null || !manager.IsNetworkStateReady)
            return;

        manager.TryLoadDungeonFromTeleportTrigger(teleportTrigger);
    }
}
