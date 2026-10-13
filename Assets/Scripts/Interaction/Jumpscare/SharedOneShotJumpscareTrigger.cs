using Fusion;
using UnityEngine;

/// <summary>
/// A globally one-shot trigger that plays the scare only for the player who
/// wins the State Authority request. Requires a scene NetworkObject.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class SharedOneShotJumpscareTrigger : NetworkBehaviour
{
    [SerializeField] private LocalJumpscarePlayback playbackPrefab;
    [SerializeField] private Collider detectionCollider;
    [Tooltip("Optional bounds used to choose the playback spawn position.")]
    [SerializeField] private BoxCollider randomSpawnArea;

    [Header("Sanity Penalty")]
    [SerializeField, Min(0f)] private float easySanityDamage = 5f;
    [SerializeField, Min(0f)] private float normalSanityDamage = 10f;
    [SerializeField, Min(0f)] private float difficultSanityDamage = 15f;

    [Networked] public NetworkBool HasTriggered { get; private set; }

    // Prevent this client from sending duplicate requests while waiting for
    // State Authority's replicated HasTriggered value.
    private bool requestPending;

    private void Awake()
    {
        if (detectionCollider == null)
            detectionCollider = GetComponent<Collider>();

        if (detectionCollider != null)
            detectionCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (HasTriggered || requestPending)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.Object == null ||
            !player.Object.IsValid || !player.Object.HasInputAuthority)
            return;

        RequestUse(player.Object.InputAuthority);
    }

    /// <summary>Call from an item-placement event on the placing player's client.</summary>
    public void TriggerFromItemPlacement()
    {
        if (HasTriggered || requestPending || Runner == null || !Runner.IsRunning)
            return;

        PlayerRef localPlayer = Runner.LocalPlayer;
        if (localPlayer.IsValid)
            RequestUse(localPlayer);
    }

    private void RequestUse(PlayerRef player)
    {
        if (HasTriggered || requestPending || !player.IsValid ||
            Object == null || !Object.IsValid)
            return;

        requestPending = true;
        RPC_RequestUse(player);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestUse(PlayerRef player, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || HasTriggered ||
            !player.IsValid || player != info.Source)
            return;

        HasTriggered = true;
        ApplySanityPenalty(player);
        RPC_PlayForPlayer(player);
    }

    private void ApplySanityPenalty(PlayerRef player)
    {
        if (Runner == null ||
            !Runner.TryGetPlayerObject(player, out NetworkObject playerObject) ||
            playerObject == null)
            return;

        PlayerSanity sanity = playerObject.GetComponent<PlayerSanity>();
        if (sanity == null)
            return;

        GameDifficulty difficulty = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.Difficulty
            : GameDifficulty.Normal;

        float damage;
        switch (difficulty)
        {
            case GameDifficulty.Easy:
                damage = easySanityDamage;
                break;
            case GameDifficulty.Difficult:
                damage = difficultSanityDamage;
                break;
            default:
                damage = normalSanityDamage;
                break;
        }

        sanity.ApplySanityDamage(damage);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayForPlayer(PlayerRef player)
    {
        // The networked flag consumes the event for everyone; only its winner
        // creates local audiovisual playback.
        if (Runner == null || Runner.LocalPlayer != player || playbackPrefab == null)
            return;

        Vector3 position = randomSpawnArea != null
            ? RandomPoint(randomSpawnArea)
            : transform.position;
        LocalJumpscarePlayback playback = Instantiate(
            playbackPrefab, position, transform.rotation);
        playback.Play();
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
