
using Fusion;
using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

    [Header("Cross Revive Feedback")]
    [SerializeField, Min(0.01f), Tooltip("Sanity restored when the Cross is used to revive or heal.")]
    private float reviveSanity = 25f;
    [SerializeField, Range(0f, 1f), Tooltip("Cross can heal a teammate at or below this sanity percentage.")]
    private float crossHealThresholdPercent = 0.5f;
    [SerializeField, Tooltip("Audio played for every player when a Cross revive completes.")]
    private AudioClip chantSfx;
    [SerializeField, Tooltip("Mixer group used to route the revive chant.")]
    private AudioMixerGroup chantMixerGroup;
    [SerializeField, Min(0.1f)] private float glowDuration = 5f;
    [SerializeField] private Color reviveGlowColor = new Color(1f, 0.8f, 0.1f);

    [Header("Low Sanity Vision")]
    [SerializeField, Range(0f, 1f)] private float visionEffectsStartPercent = 0.5f;
    [SerializeField, Range(0f, 1f)] private float visionPulseSpeed = 1.6f;
    [SerializeField, Range(0f, 1f)] private float maximumRedVignette = 0.55f;
    [SerializeField, Range(0f, 1f)] private float maximumBlur = 0.8f;
    [SerializeField, Range(0f, 1f)] private float maximumChromaticAberration = 0.45f;
    [SerializeField, Range(0f, 0.5f)] private float maximumDizzyDistortion = 0.12f;

    [Header("Low Sanity Audio")]
    [SerializeField, Tooltip("Assign up to three looping whisper clips. Assigned clips overlap.")]
    private AudioClip[] whisperSfxClips = new AudioClip[3];
    [SerializeField, Tooltip("Looping heartbeat layer. Assign a heartbeat clip.")]
    private AudioClip heartbeatSfx;
    [SerializeField, Tooltip("Mixer group used by both low-sanity sounds.")]
    private AudioMixerGroup lowSanityMixerGroup;
    [SerializeField, Range(0f, 1f)] private float maximumWhisperVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float maximumHeartbeatVolume = 0.65f;
    [SerializeField, Min(0.1f)] private float lowSanityAudioFadeSpeed = 2f;

    private Camera localCamera;
    private Volume sanityVolume;
    private VolumeProfile sanityProfile;
    private Vignette sanityVignette;
    private DepthOfField sanityDepthOfField;
    private ChromaticAberration sanityChromatic;
    private LensDistortion sanityDistortion;
    private AudioSource[] whisperSources;
    private AudioSource heartbeatSource;

    [Networked]
    public float CurrentSanity { get; set; }

    public float MaxSanity => maxSanity;

    public float SanityPercent =>
        maxSanity > 0f
            ? Mathf.Clamp01(CurrentSanity / maxSanity)
            : 0f;

    public bool IsInsane => CurrentSanity <= 0f;
    public bool CanBeHealedByCross =>
        CurrentSanity < maxSanity && SanityPercent <= crossHealThresholdPercent;

    public float LowSanitySeverity =>
        Mathf.InverseLerp(visionEffectsStartPercent, 0f, SanityPercent);

    private void Update()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (localCamera == null)
            SetupLowSanityVision();

        float severity = LowSanitySeverity;
        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * visionPulseSpeed * Mathf.PI * 2f);
        if (sanityVolume != null)
        {
            sanityVolume.weight = severity * pulse;

            sanityVignette.intensity.value = maximumRedVignette;
            sanityDepthOfField.gaussianMaxRadius.value = Mathf.Lerp(0.5f, 1.5f, maximumBlur);
            sanityChromatic.intensity.value = maximumChromaticAberration;
            sanityDistortion.intensity.value = Mathf.Sin(Time.time * visionPulseSpeed * Mathf.PI * 2f) * maximumDizzyDistortion;
        }

        if (whisperSources == null || heartbeatSource == null)
            SetupLowSanityAudio();

        int whisperCount = whisperSfxClips != null ? whisperSfxClips.Length : 0;
        int assignedWhisperCount = 0;
        for (int i = 0; i < whisperCount; i++)
        {
            if (whisperSfxClips[i] != null)
                assignedWhisperCount++;
        }

        for (int i = 0; i < whisperSources.Length; i++)
        {
            AudioClip clip = i < whisperCount ? whisperSfxClips[i] : null;
            float clipVolume = clip != null && assignedWhisperCount > 0
                ? maximumWhisperVolume * severity / assignedWhisperCount
                : 0f;
            UpdateLoopingSanityAudio(whisperSources[i], clip, clipVolume);
        }
        UpdateLoopingSanityAudio(heartbeatSource, heartbeatSfx, maximumHeartbeatVolume * severity);
    }

    private void SetupLowSanityAudio()
    {
        int whisperCount = Mathf.Max(3, whisperSfxClips != null ? whisperSfxClips.Length : 0);
        whisperSources = new AudioSource[whisperCount];
        for (int i = 0; i < whisperSources.Length; i++)
            whisperSources[i] = CreateLowSanityAudioSource();
        heartbeatSource = CreateLowSanityAudioSource();
    }

    private AudioSource CreateLowSanityAudioSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.outputAudioMixerGroup = lowSanityMixerGroup;
        return source;
    }

    private void UpdateLoopingSanityAudio(AudioSource source, AudioClip clip, float targetVolume)
    {
        if (source == null)
            return;

        if (source.clip != clip)
        {
            source.Stop();
            source.clip = clip;
            source.volume = 0f;
        }

        source.outputAudioMixerGroup = lowSanityMixerGroup;
        if (clip == null)
            return;

        if (targetVolume > 0.005f && !source.isPlaying)
            source.Play();

        source.volume = Mathf.MoveTowards(
            source.volume,
            targetVolume,
            lowSanityAudioFadeSpeed * Time.deltaTime
        );

        if (targetVolume <= 0.005f && source.volume <= 0.005f && source.isPlaying)
            source.Stop();
    }

    private void SetupLowSanityVision()
    {
        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        foreach (Camera candidate in cameras)
        {
            if (candidate != null && candidate.CompareTag("MainCamera"))
            {
                localCamera = candidate;
                break;
            }
        }

        if (localCamera == null)
            return;

        sanityVolume = localCamera.gameObject.AddComponent<Volume>();
        sanityVolume.isGlobal = true;
        sanityVolume.priority = 100f;
        sanityProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        sanityVolume.profile = sanityProfile;

        sanityVignette = sanityProfile.Add<Vignette>(true);
        sanityVignette.active = true;
        sanityVignette.color.overrideState = true;
        sanityVignette.color.value = new Color(0.65f, 0.015f, 0.02f, 1f);
        sanityVignette.intensity.overrideState = true;
        sanityVignette.smoothness.overrideState = true;
        sanityVignette.smoothness.value = 0.55f;

        sanityDepthOfField = sanityProfile.Add<DepthOfField>(true);
        sanityDepthOfField.active = true;
        sanityDepthOfField.mode.overrideState = true;
        sanityDepthOfField.mode.value = DepthOfFieldMode.Gaussian;
        sanityDepthOfField.gaussianStart.overrideState = true;
        sanityDepthOfField.gaussianStart.value = 0.1f;
        sanityDepthOfField.gaussianEnd.overrideState = true;
        sanityDepthOfField.gaussianEnd.value = 2f;
        sanityDepthOfField.gaussianMaxRadius.overrideState = true;

        sanityChromatic = sanityProfile.Add<ChromaticAberration>(true);
        sanityChromatic.active = true;
        sanityChromatic.intensity.overrideState = true;

        sanityDistortion = sanityProfile.Add<LensDistortion>(true);
        sanityDistortion.active = true;
        sanityDistortion.intensity.overrideState = true;
        sanityDistortion.scale.overrideState = true;
        sanityDistortion.scale.value = 0.9f;
    }

    private void OnDestroy()
    {
        if (sanityProfile != null)
            Destroy(sanityProfile);
    }

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

    /// <summary>Apply sanity damage. Must be called by State Authority.</summary>
    public void ApplySanityDamage(float damage)
    {
        if (Object == null ||
            !Object.IsValid ||
            !Object.HasStateAuthority ||
            damage <= 0f)
            return;

        CurrentSanity = Mathf.Clamp(
            CurrentSanity - damage,
            0f,
            maxSanity
        );

        Debug.Log(
            $"[SANITY] Current sanity: {CurrentSanity}/{maxSanity}"
        );
    }

    // Retained for existing test scripts and Inspector events.
    public void TestSanityDamage(float damage)
    {
        ApplySanityDamage(damage);
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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestCrossRevive(PlayerRef reviver)
    {
        if (!Object.HasStateAuthority || !CanBeHealedByCross)
            return;

        PlayerInventory reviverInventory = FindInventory(reviver);
        if (reviverInventory == null ||
            reviverInventory.SelectedSlot < 0 ||
            reviverInventory.GetQuickBarItemIdAt(reviverInventory.SelectedSlot) != 201 ||
            Vector3.Distance(transform.position, reviverInventory.transform.position) > 3.5f)
            return;

        CurrentSanity = Mathf.Min(maxSanity, CurrentSanity + reviveSanity);
        float feedbackDuration = chantSfx != null
            ? chantSfx.length
            : glowDuration;
        RPC_PlayCrossReviveFeedback(feedbackDuration);
    }

    private PlayerInventory FindInventory(PlayerRef player)
    {
        PlayerInventory[] inventories = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
        foreach (PlayerInventory inventory in inventories)
        {
            if (inventory != null && inventory.Object != null &&
                inventory.Object.IsValid && inventory.Object.InputAuthority == player)
                return inventory;
        }
        return null;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayCrossReviveFeedback(float duration)
    {
        StartCoroutine(PlayReviveFeedback(duration));
    }

    private IEnumerator PlayReviveFeedback(float duration)
    {
        AudioSource source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.minDistance = 1f;
        source.maxDistance = 20f;
        source.playOnAwake = false;
        source.outputAudioMixerGroup = chantMixerGroup;
        if (chantSfx != null)
            source.PlayOneShot(chantSfx);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        int emissionId = Shader.PropertyToID("_EmissionColor");
        foreach (Renderer renderer in renderers)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor(emissionId, reviveGlowColor * 1.8f);
            renderer.SetPropertyBlock(block);
        }

        yield return new WaitForSeconds(Mathf.Max(0.1f, duration));

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(block);
            block.SetColor(emissionId, Color.black);
            renderer.SetPropertyBlock(block);
        }
    }
}
