using Fusion;
using UnityEngine;

public class KeyPickup : NetworkBehaviour, IInteractable
{
    // =========================================================
    // SCENE / DEFAULT ITEM
    // =========================================================

    [Header("Scene Item")]
    [Tooltip(
        "Optional. Only use this if the key exists as a " +
        "pre-placed scene object. For procedurally generated " +
        "keys, leave this empty."
    )]
    [SerializeField] private ItemData sceneItem;


    // =========================================================
    // CHEST
    // =========================================================

    [Header("Chest Requirement")]
    [Tooltip(
        "Optional for pre-placed keys. " +
        "Generated chest keys receive their chest automatically."
    )]
    [SerializeField] private ChestState chest;


    // =========================================================
    // NETWORKED DATA
    // =========================================================

    /*
     * This is the identity of the actual item.
     *
     * Example:
     *
     * 101 = Front Gate Key
     * 102 = Prison Cell Key
     * 103 = Storage Key
     *
     * All of them can use the SAME rust_key prefab.
     */
    [Networked]
    public int ItemId { get; private set; }


    /*
     * If this key came from a chest, this stores the
     * NetworkObject of that chest.
     *
     * This allows the generated key to know:
     *
     * "I belong to THIS chest."
     */
    [Networked]
    private NetworkObject ChestObject { get; set; }


    /*
     * Prevents two players from collecting the same
     * key simultaneously.
     */
    [Networked]
    private NetworkBool IsCollecting { get; set; }


    // =========================================================
    // LOCAL STATE
    // =========================================================

    private PlayerRef pendingCollector;


    // =========================================================
    // PUBLIC ITEM INFORMATION
    // =========================================================

    public int NetworkItemId => ItemId;

    public ItemData SceneItem => sceneItem;

    // =========================================================
    // INTERACTION (IInteractable)
    // =========================================================

    public string Prompt => "Press E to collect";

    public bool CanInteract => CanCollect;

    public int Priority => 1; // inuuna ang key kaysa chest

    public void Interact(PlayerInventory inv)
    {
        RequestCollect(); // dumadaan sa RPC mo, hindi na direktang Collect
    }


    // =========================================================
    // CHEST CHECK
    // =========================================================

    public bool CanCollect
    {
        get
        {
            /*
             * -------------------------------------------------
             * GENERATED CHEST KEY
             * -------------------------------------------------
             *
             * If this key was generated from a chest,
             * ChestObject tells us which chest it belongs to.
             */

            if (ChestObject != null)
            {
                ChestState sourceChest =
                    ChestObject.GetComponent<ChestState>();

                if (sourceChest != null)
                {
                    return sourceChest.IsOpen;
                }

                /*
                 * The key says it belongs to a chest,
                 * but the chest doesn't have ChestState.
                 *
                 * Safer to prevent collection.
                 */
                return false;
            }


            /*
             * -------------------------------------------------
             * SCENE-PLACED CHEST KEY
             * -------------------------------------------------
             *
             * This supports your older/manual setup.
             */

            if (chest == null)
            {
                chest =
                    GetComponentInParent<ChestState>();
            }

            if (chest != null)
            {
                return chest.IsOpen;
            }


            /*
             * -------------------------------------------------
             * NORMAL KEY
             * -------------------------------------------------
             *
             * No chest requirement.
             */

            return true;
        }
    }


    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        /*
         * Scene-placed key:
         *
         * If ItemId hasn't already been assigned,
         * get it from Scene Item.
         */
        if (Object.HasStateAuthority &&
            ItemId <= 0 &&
            sceneItem != null)
        {
            if (sceneItem.itemId <= 0)
            {
                Debug.LogError(
                    $"[KEY PICKUP] " +
                    $"{sceneItem.name} has invalid Item ID."
                );

                return;
            }

            ItemId =
                sceneItem.itemId;

            Debug.Log(
                $"[KEY PICKUP] " +
                $"{name} initialized from Scene Item: " +
                $"{sceneItem.itemName} " +
                $"(ID {ItemId})"
            );
        }
    }


    // =========================================================
    // INITIALIZE GENERATED ITEM
    // =========================================================

    /*
     * Use this when your dungeon generator or ChestLoot
     * creates the key at runtime.
     *
     * Example:
     *
     * pickup.Initialize(101, chestObject);
     */
    public void Initialize(
        int itemId,
        NetworkObject sourceChest = null)
    {
        if (!Object.HasStateAuthority)
            return;


        // -----------------------------------------------------
        // Validate Item ID
        // -----------------------------------------------------

        if (itemId <= 0)
        {
            Debug.LogError(
                $"[KEY PICKUP] " +
                $"Cannot initialize {name}. " +
                $"Invalid Item ID: {itemId}"
            );

            return;
        }


        // -----------------------------------------------------
        // Set item identity
        // -----------------------------------------------------

        ItemId =
            itemId;


        // -----------------------------------------------------
        // Set source chest
        // -----------------------------------------------------

        ChestObject =
            sourceChest;


        // -----------------------------------------------------
        // Debug
        // -----------------------------------------------------

        if (sourceChest != null)
        {
            Debug.Log(
                $"[KEY PICKUP] " +
                $"{name} initialized with Item ID {itemId} " +
                $"from chest {sourceChest.name}."
            );
        }
        else
        {
            Debug.Log(
                $"[KEY PICKUP] " +
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
         * We don't directly modify the inventory here.
         *
         * The request goes to the pickup's State Authority.
         */
        RPC_RequestCollect();
    }


    // =========================================================
    // PLAYER -> PICKUP AUTHORITY
    // =========================================================

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    private void RPC_RequestCollect(
        RpcInfo info = default)
    {
        /*
         * Another player may already be collecting it.
         */
        if (IsCollecting)
            return;


        // -----------------------------------------------------
        // Validate chest
        // -----------------------------------------------------

        if (!CanCollect)
        {
            Debug.Log(
                $"[KEY PICKUP] " +
                $"{name} cannot be collected yet. " +
                $"Chest is closed."
            );

            return;
        }


        // -----------------------------------------------------
        // Validate Item ID
        // -----------------------------------------------------

        if (ItemId <= 0)
        {
            Debug.LogError(
                $"[KEY PICKUP] " +
                $"{name} has invalid Item ID: {ItemId}"
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
            $"[KEY PICKUP] " +
            $"Player {info.Source} requested " +
            $"Item ID {ItemId}."
        );


        // -----------------------------------------------------
        // Send item to player
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
                "[KEY PICKUP] " +
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
        // Find inventory
        // -----------------------------------------------------

        PlayerInventory inventory =
            playerObject.GetComponent<PlayerInventory>();


        if (inventory == null)
        {
            Debug.LogError(
                "[KEY PICKUP] " +
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


        if (added)
        {
            Debug.Log(
                $"[KEY PICKUP] " +
                $"Item ID {itemId} added to " +
                $"Player {targetPlayer}'s inventory."
            );
        }
        else
        {
            Debug.Log(
                $"[KEY PICKUP] " +
                $"Player {targetPlayer}'s inventory is full."
            );
        }


        // -----------------------------------------------------
        // Tell pickup authority the result
        // -----------------------------------------------------

        RPC_CollectResult(
            added
        );
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
        /*
         * Ignore if no pickup request is active.
         */
        if (!IsCollecting)
            return;


        /*
         * Only the player who originally requested
         * the pickup may respond.
         */
        if (info.Source != pendingCollector)
            return;


        // -----------------------------------------------------
        // Unlock pickup
        // -----------------------------------------------------

        IsCollecting =
            false;


        // -----------------------------------------------------
        // Failed pickup
        // -----------------------------------------------------

        if (!success)
        {
            Debug.Log(
                $"[KEY PICKUP] " +
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
            $"[KEY PICKUP] " +
            $"Item ID {ItemId} successfully collected " +
            $"by {pendingCollector}."
        );


        pendingCollector =
            PlayerRef.None;


        // -----------------------------------------------------
        // NETWORK DESPAWN
        // -----------------------------------------------------

        /*
         * IMPORTANT:
         *
         * Do NOT use:
         *
         * Destroy(gameObject);
         *
         * because this is a Fusion NetworkObject.
         *
         * Runner.Despawn() makes every client see
         * the pickup disappear.
         */
        Runner.Despawn(
            Object
        );
    }
}