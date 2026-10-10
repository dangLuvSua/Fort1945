
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class SaltController : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private Key sprayKey = Key.F;

    [Header("Salt Item")]
    [Tooltip("Assign the Salt ItemData asset used by your inventory.")]
    [SerializeField] private ItemData saltItem;

    [Header("Spray Visual")]
    [Tooltip("Assign the salt spray Particle System.")]
    [SerializeField] private ParticleSystem saltSprayVFX;

    [Tooltip("Minimum time between spray requests.")]
    [SerializeField, Min(0.05f)]
    private float sprayCooldown = 0.5f;

    [Header("Visual Search")]
    [SerializeField, Min(0.05f)]
    private float visualSearchInterval = 0.5f;

    private PlayerInventory inventory;
    private float nextSprayTime;
    private float searchTimer;

    public override void Spawned()
    {
        inventory = GetComponent<PlayerInventory>();

        FindSaltSprayVisual();
        StopSaltVisual();
    }

    private void Update()
    {
        // Only the player controlling this character reads keyboard input.
        if (!Object.HasInputAuthority)
            return;

        searchTimer -= Time.deltaTime;

        if (searchTimer <= 0f)
        {
            searchTimer = visualSearchInterval;

            if (saltSprayVFX == null)
                FindSaltSprayVisual();
        }

        if (Keyboard.current == null)
            return;

        // One request per key press, never once per held frame.
        if (Keyboard.current[sprayKey].wasPressedThisFrame)
            TrySpraySalt();
    }

    private void TrySpraySalt()
    {
        if (Time.time < nextSprayTime)
            return;

        if (!IsHoldingSalt())
            return;

        // Start the cooldown immediately to prevent repeated requests.
        nextSprayTime = Time.time + sprayCooldown;

        RPC_RequestSaltSpray();
    }

    private bool IsHoldingSalt()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (inventory == null || saltItem == null)
            return false;

        int storageSlot = inventory.GetSelectedStorageSlot();

        if (storageSlot < 0)
            return false;

        ItemData selectedItem =
            inventory.GetStorageItemAt(storageSlot);

        return selectedItem != null &&
               selectedItem.itemId == saltItem.itemId;
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    private void RPC_RequestSaltSpray()
    {
        // Validate again on the authority before broadcasting the effect.
        if (!IsHoldingSalt())
            return;

        RPC_PlaySaltSpray();
    }

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_PlaySaltSpray()
    {
        if (saltSprayVFX == null)
            FindSaltSprayVisual();

        if (saltSprayVFX == null)
        {
            Debug.LogWarning(
                $"[SALT] No SaltSprayVFX found on player '{name}'."
            );

            return;
        }

        // Ensure a new press creates one fresh burst.
        saltSprayVFX.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        saltSprayVFX.Play(true);
    }

    private void FindSaltSprayVisual()
    {
        ParticleSystem[] systems =
            GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem system in systems)
        {
            if (system != null &&
                system.gameObject.name == "SaltSprayVFX")
            {
                saltSprayVFX = system;
                return;
            }
        }
    }

    private void StopSaltVisual()
    {
        if (saltSprayVFX == null)
            return;

        saltSprayVFX.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }
}

