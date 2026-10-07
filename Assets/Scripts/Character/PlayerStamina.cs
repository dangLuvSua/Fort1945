using Fusion;
using UnityEngine;

public class PlayerStamina : NetworkBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;

    [SerializeField]
    private float sprintDrainPerSecond = 20f;

    [SerializeField]
    private float regenDelay = 1f;

    [SerializeField]
    private float regenPerSecond = 15f;

    [SerializeField]
    private float minToStartSprint = 10f;


    // =====================================================
    // NETWORKED STAMINA
    // =====================================================

    [Networked]
    public float Current { get; private set; }


    public float Max => maxStamina;


    // Local-only timer/state.
    // These do NOT need to be networked because only the
    // owning player calculates their own stamina.
    private float regenTimer;
    private bool exhausted;


    // =====================================================
    // SPAWNED
    // =====================================================

    public override void Spawned()
    {
        // Only the player who owns this player object
        // initializes and changes its stamina.
        if (Object.HasStateAuthority)
        {
            Current = maxStamina;

            regenTimer = 0f;
            exhausted = false;
        }
    }


    // =====================================================
    // TICK
    // =====================================================
    // Call this every frame from PlayerController.
    //
    // Returns TRUE if the player is actually sprinting.
    // =====================================================

    public bool Tick(bool wantsSprint, float dt)
    {
        // Only State Authority is allowed to modify
        // the networked stamina value.
        if (!Object.HasStateAuthority)
        {
            return false;
        }


        // -------------------------------------------------
        // Determine whether sprinting is allowed
        // -------------------------------------------------

        bool sprinting =
            wantsSprint &&
            !exhausted &&
            Current > 0f;


        // -------------------------------------------------
        // SPRINTING
        // -------------------------------------------------

        if (sprinting)
        {
            Current = Mathf.Max(
                0f,
                Current -
                sprintDrainPerSecond * dt
            );


            // Reset regeneration delay.
            regenTimer = regenDelay;


            // Completely exhausted.
            if (Current <= 0f)
            {
                exhausted = true;
            }
        }


        // -------------------------------------------------
        // WAITING FOR REGEN DELAY
        // -------------------------------------------------

        else if (regenTimer > 0f)
        {
            regenTimer -= dt;
        }


        // -------------------------------------------------
        // REGENERATE
        // -------------------------------------------------

        else
        {
            Current = Mathf.Min(
                maxStamina,
                Current +
                regenPerSecond * dt
            );


            // Allow sprint again once minimum stamina
            // has been restored.
            if (exhausted &&
                Current >= minToStartSprint)
            {
                exhausted = false;
            }
        }


        return sprinting;
    }


    // =====================================================
    // OPTIONAL RESET
    // =====================================================

    public void ResetStamina()
    {
        if (!Object.HasStateAuthority)
            return;

        Current = maxStamina;

        regenTimer = 0f;
        exhausted = false;
    }
}