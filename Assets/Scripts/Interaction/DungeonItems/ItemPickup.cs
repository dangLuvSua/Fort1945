
using Fusion;
using UnityEngine;

public class ItemPickup : NetworkBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private ItemData itemData;

    [Header("Optional Source Chest")]
    [SerializeField] private ChestState sourceChest;

    [Header("Optional Pickup Jumpscare")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Base chance for this item to cause a pickup jumpscare. The selected difficulty scales this chance.")]
    private float pickupJumpscareChance;
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Pickup scare chance multiplier on Easy difficulty.")]
    private float easyJumpscareChanceMultiplier = 0.1f;
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Pickup scare chance multiplier on Normal difficulty.")]
    private float normalJumpscareChanceMultiplier = 0.35f;
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Pickup scare chance multiplier on Difficult difficulty.")]
    private float difficultJumpscareChanceMultiplier = 0.65f;
    [SerializeField]
    [Tooltip("Local screen/video playback prefab. Only the successful collector sees it.")]
    private LocalJumpscarePlayback pickupJumpscarePrefab;
    [SerializeField, Min(0f)] private float easySanityDamage = 5f;
    [SerializeField, Min(0f)] private float normalSanityDamage = 10f;
    [SerializeField, Min(0f)] private float difficultSanityDamage = 15f;

    [Networked]
    public int ItemId { get; private set; }

    [Networked]
    private NetworkObject SourceChestObject { get; set; }

    [Networked]
    private NetworkBool IsCollecting { get; set; }

    private PlayerRef pendingCollector;
    // Read and written by State Authority only; the pending pickup is already
    // serialized by IsCollecting and pendingCollector.
    private bool pendingPickupJumpscare;

    public int NetworkItemId => ItemId;
    public ItemData Item => itemData;

    public string Prompt =>
        itemData != null && !string.IsNullOrWhiteSpace(itemData.itemName)
            ? $"Press E to collect {itemData.itemName}"
            : "Press E to collect";

    public bool CanInteract => CanCollect;
    public int Priority => 10;

    public void Interact(PlayerInventory inventory)
    {
        RequestCollect();
    }

    public bool CanCollect
    {
        get
        {
            if (ItemId <= 0)
                return false;

            if (SourceChestObject != null)
            {
                ChestState chest =
                    SourceChestObject.GetComponent<ChestState>();

                return chest != null && chest.IsOpen;
            }

            if (sourceChest == null)
                sourceChest = GetComponentInParent<ChestState>();

            return sourceChest == null || sourceChest.IsOpen;
        }
    }

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;

        if (ItemId <= 0 && itemData != null)
        {
            if (itemData.itemId <= 0)
            {
                Debug.LogError(
                    $"[ITEM PICKUP] {name} has an invalid ItemData ID."
                );
                return;
            }

            ItemId = itemData.itemId;
        }

        if (ItemId <= 0)
        {
            Debug.LogError(
                $"[ITEM PICKUP] {name} has no valid Item ID."
            );
        }
    }

    public void Initialize(
        int itemId,
        NetworkObject sourceChest = null)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (itemId <= 0)
        {
            Debug.LogError(
                $"[ITEM PICKUP] Invalid item ID {itemId}."
            );
            return;
        }

        ItemId = itemId;
        SourceChestObject = sourceChest;
    }

    public void RequestCollect()
    {
        if (Object == null || !Object.IsValid)
            return;

        RPC_RequestCollect();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCollect(RpcInfo info = default)
    {
        if (IsCollecting || ItemId <= 0 || !CanCollect)
            return;

        if (info.Source == PlayerRef.None)
            return;

        pendingCollector = info.Source;
        IsCollecting = true;
        float difficultyAdjustedChance = GetDifficultyAdjustedJumpscareChance();
        pendingPickupJumpscare = pickupJumpscarePrefab != null &&
            Random.value < difficultyAdjustedChance;

        // Candle state is stored on the dropped world object.
        NetworkedDroppedItem dropped =
            GetComponent<NetworkedDroppedItem>();

        bool hasCandleState =
            dropped != null && dropped.HasCandleState;

        float remaining =
            hasCandleState ? dropped.CandleRemaining : 0f;

        float maxLife =
            hasCandleState ? dropped.CandleMaxLife : 0f;

        RPC_DeliverItem(
            info.Source,
            ItemId,
            hasCandleState,
            remaining,
            maxLife,
            pendingPickupJumpscare
        );
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DeliverItem(
        [RpcTarget] PlayerRef targetPlayer,
        int itemId,
        bool hasCandleState,
        float remaining,
        float maxLife,
        bool playPickupJumpscare)
    {
        if (Runner.LocalPlayer != targetPlayer)
            return;

        if (!Runner.TryGetPlayerObject(
                targetPlayer,
                out NetworkObject playerObject) ||
            playerObject == null)
        {
            RPC_CollectResult(false);
            return;
        }

        PlayerInventory inventory =
            playerObject.GetComponent<PlayerInventory>();

        if (inventory == null)
        {
            Debug.LogError(
                "[ITEM PICKUP] PlayerInventory is missing."
            );
            RPC_CollectResult(false);
            return;
        }

        bool added;

        if (hasCandleState)
        {
            added = inventory.TryAddNetworkedById(
                itemId,
                remaining,
                maxLife
            );
        }
        else
        {
            added = inventory.TryAddNetworkedById(itemId);
        }

        if (added)
        {
            inventory.NotifyItemCollected(itemId);

            if (playPickupJumpscare && pickupJumpscarePrefab != null)
            {
                LocalJumpscarePlayback playback =
                    Instantiate(pickupJumpscarePrefab);
                playback.Play();
            }

            Debug.Log(
                $"[ITEM PICKUP] Item {itemId} collected by {targetPlayer}."
            );
        }
        else
        {
            Debug.Log(
                $"[ITEM PICKUP] Could not add item {itemId}; inventory is full."
            );
        }

        RPC_CollectResult(added);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_CollectResult(
        bool success,
        RpcInfo info = default)
    {
        if (!IsCollecting || info.Source != pendingCollector)
            return;

        IsCollecting = false;

        if (!success)
        {
            pendingCollector = PlayerRef.None;
            pendingPickupJumpscare = false;
            return;
        }

        if (pendingPickupJumpscare)
            ApplyPickupJumpscareSanityDamage(pendingCollector);

        pendingCollector = PlayerRef.None;
        pendingPickupJumpscare = false;

        if (Object != null && Object.IsValid)
            Runner.Despawn(Object);
    }

    private float GetDifficultyAdjustedJumpscareChance()
    {
        GameDifficulty difficulty = NetworkGameManager.Instance != null
            ? NetworkGameManager.Instance.Difficulty
            : GameDifficulty.Normal;

        float multiplier = difficulty switch
        {
            GameDifficulty.Easy => easyJumpscareChanceMultiplier,
            GameDifficulty.Difficult => difficultJumpscareChanceMultiplier,
            _ => normalJumpscareChanceMultiplier
        };

        return Mathf.Clamp01(pickupJumpscareChance * multiplier);
    }

    private void ApplyPickupJumpscareSanityDamage(PlayerRef player)
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
}
