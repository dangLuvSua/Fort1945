
using Fusion;
using UnityEngine;

public class PlayerSanity : NetworkBehaviour
{
    [Header("Sanity Settings")]
    [SerializeField, Min(1f)]
    private float maxSanity = 100f;

    [SerializeField, Min(0f)]
    private float startingSanity = 100f;

    [Header("Optional Recovery")]
    [SerializeField]
    private bool allowRecovery = false;

    [SerializeField, Min(0f)]
    private float recoveryPerSecond = 2f;

    [Networked]
    public float CurrentSanity { get; set; }

    public float MaxSanity => maxSanity;

    public float SanityPercent =>
        maxSanity > 0f
            ? Mathf.Clamp01(CurrentSanity / maxSanity)
            : 0f;

    public bool IsInsane => CurrentSanity <= 0f;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentSanity = Mathf.Clamp(
                startingSanity, 0f, maxSanity
            );
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (allowRecovery &&
            CurrentSanity > 0f &&
            CurrentSanity < maxSanity)
        {
            CurrentSanity = Mathf.Min(
                maxSanity,
                CurrentSanity + recoveryPerSecond * Runner.DeltaTime
            );
        }
    }

    // Temporary test method.
    // Call from a test script or Inspector-connected event.
    public void TestSanityDamage(float damage)
    {
        if (Object == null ||
            !Object.IsValid ||
            !Object.HasStateAuthority)
            return;

        CurrentSanity = Mathf.Clamp(
            CurrentSanity - Mathf.Max(0f, damage),
            0f,
            maxSanity
        );

        Debug.Log(
            $"[SANITY] Current sanity: {CurrentSanity}/{maxSanity}"
        );
    }

    public void RecoverSanity(float amount)
    {
        if (Object == null ||
            !Object.IsValid ||
            !Object.HasStateAuthority ||
            amount <= 0f)
            return;

        CurrentSanity = Mathf.Clamp(
            CurrentSanity + amount,
            0f,
            maxSanity
        );
    }
}