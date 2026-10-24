using Fusion;
using UnityEngine;

/// <summary>
/// Fires a jumpscare once for the local player on this client.
/// Do not network-destroy this trigger: each client consumes its own copy.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class LocalJumpscareTrigger : MonoBehaviour
{
    [SerializeField] private LocalJumpscarePlayback playbackPrefab;
    [SerializeField] private bool disableColliderAfterUse = true;
    [Tooltip("Optional bounds for choosing a random world spawn position.")]
    [SerializeField] private BoxCollider randomSpawnArea;
    [Header("After Effect")]
    [SerializeField, Min(0f)] private float extremeDizzinessRange = 8f;
    [SerializeField, Min(0f)] private float mildDizzinessDuration = 2f;
    [SerializeField, Min(0f)] private float extremeDizzinessDuration = 5f;

    private bool hasTriggered;
    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.Object == null ||
            !player.Object.IsValid || !player.Object.HasInputAuthority)
            return;

        Fire();
    }

    /// <summary>Call on this client when an item placement should cause the scare.</summary>
    public void TriggerFromItemPlacement()
    {
        Fire();
    }

    private void Fire()
    {
        if (hasTriggered)
            return;

        hasTriggered = true;
        if (disableColliderAfterUse && triggerCollider != null)
            triggerCollider.enabled = false;

        if (playbackPrefab == null)
        {
            Debug.LogWarning($"[{nameof(LocalJumpscareTrigger)}] No playback prefab assigned on {name}.", this);
            return;
        }

        PlayerController localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            bool extreme = Vector3.Distance(localPlayer.transform.position, transform.position) >
                           extremeDizzinessRange;
            localPlayer.ApplyJumpscareDizziness(extreme ? 1f : 0.45f,
                extreme ? extremeDizzinessDuration : mildDizzinessDuration);
        }

        Vector3 position = randomSpawnArea != null
            ? RandomPoint(randomSpawnArea)
            : transform.position;
        LocalJumpscarePlayback playback = Instantiate(
            playbackPrefab, position, transform.rotation);
        playback.Play();
    }

    private static PlayerController FindLocalPlayer()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (PlayerController player in players)
            if (player.Object != null && player.Object.IsValid && player.Object.HasInputAuthority)
                return player;
        return null;
    }

    private static Vector3 RandomPoint(BoxCollider box)
    {
        Vector3 half = box.size * 0.5f;
        Vector3 local = box.center + new Vector3(
            Random.Range(-half.x, half.x),
            Random.Range(-half.y, half.y),
            Random.Range(-half.z, half.z));
        return box.transform.TransformPoint(local);
    }
}
