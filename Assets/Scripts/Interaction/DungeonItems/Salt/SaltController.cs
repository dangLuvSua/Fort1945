
using Fusion;
using UnityEngine;
using UnityEngine.Audio;
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

    [Header("Audio")]
    [SerializeField] private AudioClip saltSpraySfx;
    [SerializeField, Range(0f, 1f)] private float saltSprayVolume = 0.8f;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    private AudioSource saltAudioSource;

    [Tooltip("Minimum time between spray requests.")]
    [SerializeField, Min(0.05f)]
    private float sprayCooldown = 2f;

    [Header("Salt Repel")]
    [Tooltip("Gaano kalayo ang naaabot ng asin. Tinutugma sa SaltSprayVFX particles.")]
    [SerializeField, Min(0.1f)]
    private float sprayHitRange = 3.5f;

    [Tooltip("Half-angle ng hit cone. Tinutugma sa particle cone ng SaltSprayVFX.")]
    [SerializeField, Range(1f, 90f)]
    private float sprayHitHalfAngle = 20f;

    [Tooltip("Gaano katagal natutulala at lumalayo ang crawler pagkatapos matamaan.")]
    [SerializeField, Min(0.1f)]
    private float saltStunDuration = 3f;

    [Tooltip("Gaano kalayo lumayo ang crawler mula sa player.")]
    [SerializeField, Min(0.1f)]
    private float saltRepelDistance = 5f;

    [Tooltip("Mga layer na humaharang sa salt spray. Iwanang Everything kung walang wall check.")]
    [SerializeField]
    private LayerMask sprayObstacleMask = ~0;

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
        SetupSaltAudio();
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

        RepelCrawlersNearSpray();

        RPC_PlaySaltSpray();
    }

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_PlaySaltSpray()
    {
        if (saltSpraySfx != null && saltAudioSource != null)
            saltAudioSource.PlayOneShot(saltSpraySfx, saltSprayVolume);

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

    private void SetupSaltAudio()
    {
        if (saltSprayVFX == null)
            FindSaltSprayVisual();

        Transform audioParent = saltSprayVFX != null
            ? saltSprayVFX.transform
            : transform;

        saltAudioSource = audioParent.GetComponent<AudioSource>();
        if (saltAudioSource == null)
            saltAudioSource = audioParent.gameObject.AddComponent<AudioSource>();

        saltAudioSource.playOnAwake = false;
        saltAudioSource.loop = false;
        saltAudioSource.spatialBlend = 1f;
        saltAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        saltAudioSource.minDistance = 1.5f;
        saltAudioSource.maxDistance = 18f;
        saltAudioSource.dopplerLevel = 0f;
        saltAudioSource.outputAudioMixerGroup = sfxMixerGroup;
    }

    private void RepelCrawlersNearSpray()
    {
        Vector3 origin;
        Vector3 forward;

        if (saltSprayVFX != null)
        {
            // Gamitin ang totoong VFX transform para tugma ang hit sa nakikita.
            origin = saltSprayVFX.transform.position;
            forward = saltSprayVFX.transform.forward;
        }
        else
        {
            origin =
                transform.position +
                Vector3.up * 1.2f +
                transform.forward * 0.6f;

            forward = transform.forward;
        }

        float maxDistanceSqr = sprayHitRange * sprayHitRange;

        PrisonerCrawler[] crawlers =
            FindObjectsByType<PrisonerCrawler>();

        foreach (PrisonerCrawler crawler in crawlers)
        {
            if (crawler == null)
                continue;

            Vector3 target =
                crawler.transform.position + Vector3.up * 1f;

            Vector3 toTarget = target - origin;

            if (toTarget.sqrMagnitude > maxDistanceSqr)
                continue;

            if (Vector3.Angle(forward, toTarget) > sprayHitHalfAngle)
                continue;

            if (!HasSaltLineOfSight(origin, target, crawler))
                continue;

            crawler.ApplySaltRepel(
                origin,
                saltStunDuration,
                saltRepelDistance
            );
        }
    }

    private bool HasSaltLineOfSight(
        Vector3 origin,
        Vector3 target,
        PrisonerCrawler crawler)
    {
        if (sprayObstacleMask.value == 0)
            return true;

        if (!Physics.Linecast(
                origin,
                target,
                out RaycastHit hit,
                sprayObstacleMask,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        // Okay lang kung ang crawler mismo ang unang natamaan.
        return hit.collider.GetComponentInParent<PrisonerCrawler>() ==
               crawler;
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

