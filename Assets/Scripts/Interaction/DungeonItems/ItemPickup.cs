using Fusion;
using UnityEngine;

public class ItemPickup : NetworkBehaviour, IInteractable
{
    // =========================================================
    // ITEM DATA
    // =========================================================

    [Header("Item")]

    [Tooltip(
        "The ItemData represented by this pickup. " +
        "Works for keys, cameras, documents, batteries, etc."
    )]
    [SerializeField] private ItemData itemData;


    // =========================================================
    // OPTIONAL CHEST REQUIREMENT
    // =========================================================

    [Header("Optional Source Chest")]

    [Tooltip(
        "Optional. Assign this if the item can only be collected " +
        "after its source chest/container has been opened."
    )]
    [SerializeField] private ChestState sourceChest;


    // =========================================================
    // NETWORKED DATA
    // =========================================================

    /*
     * The actual ItemData ID.
     *
     * Example:
     *
     * 101 = Front Gate Key
     * 102 = Prison Cell Key
     * 103 = Camera
     * 104 = Document
     *
     * The pickup prefab does NOT need to be different
     * for every item.
     */

    [Networked]
    public int ItemId { get; private set; }


    /*
     * If this item was generated from a networked chest,
     * remember which chest generated it.
     */

    [Networked]
    private NetworkObject SourceChestObject { get; set; }


    /*
     * Prevent two players from collecting the same
     * networked item at the same time.
     */

    [Networked]
    private NetworkBool IsCollecting { get; set; }


    // =========================================================
    // LOCAL STATE
    // =========================================================

    private PlayerRef pendingCollector;


    // =========================================================
    // PUBLIC INFORMATION
    // =========================================================

    public int NetworkItemId => ItemId;

    public ItemData Item => itemData;


    // =========================================================
    // INTERACTABLE
    // =========================================================

    public string Prompt
    {
        get
        {
            if (itemData != null &&
                !string.IsNullOrWhiteSpace(itemData.itemName))
            {
                return $"Press E to collect {itemData.itemName}";
            }

            return "Press E to collect";
        }
    }


    public bool CanInteract => CanCollect;


    /*
     * Higher priority means this object is selected first
     * if multiple IInteractable objects overlap.
     */

    public int Priority => 10;


    public void Interact(PlayerInventory inventory)
    {
        RequestCollect();
    }


    // =========================================================
    // CAN COLLECT
    // =========================================================

    public bool CanCollect
    {
        get
        {
            // -------------------------------------------------
            // Invalid item
            // -------------------------------------------------

            if (ItemId <= 0)
            {
                return false;
            }


            // -------------------------------------------------
            // Generated source chest
            // -------------------------------------------------

            if (SourceChestObject != null)
            {
                ChestState chest =
                    SourceChestObject.GetComponent<ChestState>();

                if (chest == null)
                {
                    return false;
                }

                return chest.IsOpen;
            }


            // -------------------------------------------------
            // Scene-placed source chest
            // -------------------------------------------------

            if (sourceChest == null)
            {
                sourceChest =
                    GetComponentInParent<ChestState>();
            }

            if (sourceChest != null)
            {
                return sourceChest.IsOpen;
            }


            // -------------------------------------------------
            // Normal item
            //
            // No chest requirement.
            // -------------------------------------------------

            return true;
        }
    }


    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            return;


        // -----------------------------------------------------
        // Scene-placed item
        //
        // If ItemId hasn't been initialized through
        // Initialize(), use the assigned ItemData.
        // -----------------------------------------------------

        if (ItemId <= 0 && itemData != null)
        {
            if (itemData.itemId <= 0)
            {
                Debug.LogError(
                    $"[ITEM PICKUP] " +
                    $"{name} has invalid ItemData ID."
                );

                return;
            }

            ItemId =
                itemData.itemId;


            Debug.Log(
                $"[ITEM PICKUP] " +
                $"{name} initialized from ItemData: " +
                $"{itemData.itemName} " +
                $"(ID {ItemId})"
            );
        }


        // -----------------------------------------------------
        // Final validation
        // -----------------------------------------------------

        if (ItemId <= 0)
        {
            Debug.LogError(
                $"[ITEM PICKUP] " +
                $"{name} has no valid Item ID. " +
                "Assign ItemData or call Initialize()."
            );

            return;
        }
    }


    // =========================================================
    // INITIALIZE GENERATED ITEM
    // =========================================================

    /*
     * Use this when spawning an item through:
     *
     * - ChestLoot
     * - Dungeon generation
     * - Procedural generation
     * - Other networked systems
     *
     * Example:
     *
     * pickup.Initialize(
     *     item.itemId,
     *     chestNetworkObject
     * );
     */

    public void Initialize(
        int itemId,
        NetworkObject sourceChest = null)
    {
        if (!Object.HasStateAuthority)
            return;


        // -----------------------------------------------------
        // Validate ID
        // -----------------------------------------------------

        if (itemId <= 0)
        {
            Debug.LogError(
                $"[ITEM PICKUP] " +
                $"Cannot initialize {name}. " +
                $"Invalid Item ID: {itemId}"
            );

            return;
        }


        // -----------------------------------------------------
        // Set identity
        // -----------------------------------------------------

        ItemId =
            itemId;


        // -----------------------------------------------------
        // Set source chest
        // -----------------------------------------------------

        SourceChestObject =
            sourceChest;


        // -----------------------------------------------------
        // Debug
        // -----------------------------------------------------

        if (sourceChest != null)
        {
            Debug.Log(
                $"[ITEM PICKUP] " +
                $"{name} initialized with Item ID {itemId} " +
                $"from chest {sourceChest.name}."
            );
        }
        else
        {
            Debug.Log(
                $"[ITEM PICKUP] " +
                $"{name} initialized with Item ID {itemId}."
            );
        }
    }


    // =========================================================
    // PLAYER REQUESTS PICKUP
    // =========================================================

    public void RequestCollect()
    {
        if (Object == null)
            return;


        /*
         * Never directly modify the inventory here.
         *
         * The request goes to the pickup's
         * State Authority first.
         */

        RPC_RequestCollect();
    }


    // =========================================================
    // PLAYER -> PICKUP STATE AUTHORITY
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    private void RPC_RequestCollect(
        RpcInfo info = default)
    {
        // -----------------------------------------------------
        // Prevent duplicate pickup requests
        // -----------------------------------------------------

        if (IsCollecting)
            return;


        // -----------------------------------------------------
        // Validate item
        // -----------------------------------------------------

        if (ItemId <= 0)
        {
            Debug.LogError(
                $"[ITEM PICKUP] " +
                $"{name} has invalid Item ID: {ItemId}"
            );

            return;
        }


        // -----------------------------------------------------
        // Validate chest requirement
        // -----------------------------------------------------

        if (!CanCollect)
        {
            Debug.Log(
                $"[ITEM PICKUP] " +
                $"{name} cannot be collected yet."
            );

            return;
        }


        // -----------------------------------------------------
        // Validate player
        // -----------------------------------------------------

        if (info.Source == PlayerRef.None)
            return;


        // -----------------------------------------------------
        // Lock pickup
        // -----------------------------------------------------

        pendingCollector =
            info.Source;

        IsCollecting =
            true;


        Debug.Log(
            $"[ITEM PICKUP] " +
            $"Player {info.Source} requested " +
            $"Item ID {ItemId}."
        );


        // -----------------------------------------------------
        // Deliver item to player
        // -----------------------------------------------------

        RPC_DeliverItem(
            info.Source,
            ItemId
        );
    }


    // =========================================================
    // PICKUP AUTHORITY -> PLAYER
    // =========================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_DeliverItem(
        [RpcTarget] PlayerRef targetPlayer,
        int itemId)
    {
        /*
         * This RPC executes on all clients,
         * but only the intended player continues.
         */

        if (Runner.LocalPlayer != targetPlayer)
            return;


        // -----------------------------------------------------
        // Find player's NetworkObject
        // -----------------------------------------------------

        if (!Runner.TryGetPlayerObject(
                targetPlayer,
                out NetworkObject playerObject))
        {
            Debug.LogError(
                "[ITEM PICKUP] " +
                "Could not find player NetworkObject."
            );

            RPC_CollectResult(false);

            return;
        }


        if (playerObject == null)
        {
            RPC_CollectResult(false);

            return;
        }


        // -----------------------------------------------------
        // Find PlayerInventory
        // -----------------------------------------------------

        PlayerInventory inventory =
            playerObject.GetComponent<PlayerInventory>();


        if (inventory == null)
        {
            Debug.LogError(
                "[ITEM PICKUP] " +
                "PlayerInventory missing from player."
            );

            RPC_CollectResult(false);

            return;
        }


        // -----------------------------------------------------
        // Add item to networked inventory
        // -----------------------------------------------------

        bool added =
            inventory.TryAddNetworkedById(itemId);


        // -----------------------------------------------------
        // Result
        // -----------------------------------------------------

        if (added)
        {
            Debug.Log(
                $"[ITEM PICKUP] " +
                $"Item ID {itemId} added to " +
                $"Player {targetPlayer}'s inventory."
            );
        }
        else
        {
            Debug.Log(
                $"[ITEM PICKUP] " +
                $"Player {targetPlayer}'s inventory is full."
            );
        }


        // -----------------------------------------------------
        // Tell pickup authority the result
        // -----------------------------------------------------

        RPC_CollectResult(added);
    }


    // =========================================================
    // PLAYER -> PICKUP AUTHORITY
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    private void RPC_CollectResult(
        bool success,
        RpcInfo info = default)
    {
        // -----------------------------------------------------
        // No active pickup
        // -----------------------------------------------------

        if (!IsCollecting)
            return;


        // -----------------------------------------------------
        // Only original collector can answer
        // -----------------------------------------------------

        if (info.Source != pendingCollector)
            return;


        // -----------------------------------------------------
        // Unlock
        // -----------------------------------------------------

        IsCollecting =
            false;


        // -----------------------------------------------------
        // Failed pickup
        // -----------------------------------------------------

        if (!success)
        {
            Debug.Log(
                $"[ITEM PICKUP] " +
                $"Item ID {ItemId} remains in the world. " +
                $"Inventory is full."
            );

            pendingCollector =
                PlayerRef.None;

            return;
        }


        // -----------------------------------------------------
        // Successful pickup
        // -----------------------------------------------------

        Debug.Log(
            $"[ITEM PICKUP] " +
            $"Item ID {ItemId} successfully collected " +
            $"by {pendingCollector}."
        );


        pendingCollector =
            PlayerRef.None;


        // -----------------------------------------------------
        // Despawn network item
        // -----------------------------------------------------

        Runner.Despawn(Object);
    }
}