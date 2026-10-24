using Fusion;
using UnityEngine;

// One activation per networked trigger, audiovisual playback only on winner's client.
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public sealed class SharedOneShotJumpscareTrigger : NetworkBehaviour
{
    [SerializeField] private LocalJumpscarePlayback playbackPrefab;
    [SerializeField] private Collider detectionCollider;
    [SerializeField] private BoxCollider randomSpawnArea;
    [Header("After Effect")]
    [SerializeField, Min(0f)] private float extremeDizzinessRange = 8f;
    [SerializeField, Min(0f)] private float mildDizzinessDuration = 2f;
    [SerializeField, Min(0f)] private float extremeDizzinessDuration = 5f;

    [Header("Sanity Penalty")]
    [SerializeField, Min(0f)] private float easySanityDamage = 5f;
    [SerializeField, Min(0f)] private float normalSanityDamage = 10f;
    [SerializeField, Min(0f)] private float difficultSanityDamage = 15f;

    [Networked] public NetworkBool HasTriggered { get; private set; }
    private bool requestPending;

    private void Awake()
    {
        if (detectionCollider == null) detectionCollider = GetComponent<Collider>();
        if (detectionCollider != null) detectionCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (HasTriggered || requestPending) return;
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player.Object == null ||
            !player.Object.IsValid || !player.Object.HasInputAuthority) return;
        RequestUse(player.Object.InputAuthority);
    }

    public void TriggerFromItemPlacement()
    {
        if (HasTriggered || requestPending || Runner == null || !Runner.IsRunning) return;
        PlayerRef localPlayer = Runner.LocalPlayer;
        if (localPlayer.IsValid) RequestUse(localPlayer);
    }

    private void RequestUse(PlayerRef player)
    {
        if (HasTriggered || requestPending || !player.IsValid ||
            Object == null || !Object.IsValid) return;
        requestPending = true;
        RPC_RequestUse(player);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestUse(PlayerRef player, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || HasTriggered ||
            !player.IsValid || player != info.Source) return;
        HasTriggered = true;
        ApplySanityPenalty(player);
        RPC_PlayForPlayer(player);
    }

    private void ApplySanityPenalty(PlayerRef player)
    {
        if (Runner == null ||
            !Runner.TryGetPlayerObject(player, out NetworkObject playerObject) ||
            playerObject == null) return;
        PlayerSanity sanity = playerObject.GetComponent<PlayerSanity>();
        if (sanity == null) return;
        GameDifficulty difficulty = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.Difficulty : GameDifficulty.Normal;
        float damage = difficulty == GameDifficulty.Easy ? easySanityDamage
            : difficulty == GameDifficulty.Difficult ? difficultSanityDamage
            : normalSanityDamage;
        sanity.ApplySanityDamage(damage);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayForPlayer(PlayerRef player)
    {
        if (Runner == null || Runner.LocalPlayer != player || playbackPrefab == null)
            return;
        Vector3 position = randomSpawnArea != null
            ? RandomPoint(randomSpawnArea) : transform.position;
        LocalJumpscarePlayback playback = Instantiate(
            playbackPrefab, position, transform.rotation);
        if (Runner.TryGetPlayerObject(player, out NetworkObject playerObject) && playerObject != null)
        {
            PlayerController playerController = playerObject.GetComponent<PlayerController>();
            if (playerController != null)
            {
                bool extreme = Vector3.Distance(playerObject.transform.position, transform.position) >
                               extremeDizzinessRange;
                playerController.ApplyJumpscareDizziness(extreme ? 1f : 0.45f,
                    extreme ? extremeDizzinessDuration : mildDizzinessDuration);
            }
        }
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
