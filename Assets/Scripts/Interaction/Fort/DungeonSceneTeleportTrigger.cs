using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Place this on a trigger just beyond the unlocked dungeon gate. The host
/// loads the Dungeon scene after the gate is unlocked and all active players
/// walk into the trigger volume. The transition applies to the whole session.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class DungeonSceneTeleportTrigger : MonoBehaviour
{
    [Header("Teleport Effect")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.8f;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private AudioClip teleportSfx;
    [SerializeField] private AudioMixerGroup teleportMixerGroup;

    private Collider teleportTrigger;
    private bool effectPlayed;

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

        if (manager.TeleportStarted && !effectPlayed)
        {
            effectPlayed = true;
            NetworkRunnerHandler.Instance?.CaptureLocalInventoryForDungeon();
            DungeonTeleportLoadingOverlay.Show(
                fadeDuration,
                fadeColor,
                teleportSfx,
                teleportMixerGroup);
        }

        manager.TryLoadDungeonFromTeleportTrigger(teleportTrigger);
    }

}
