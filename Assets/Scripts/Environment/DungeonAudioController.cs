using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Plays the Dungeon scene's music, ambient bed, and prison capture cue.</summary>
public sealed class DungeonAudioController : MonoBehaviour
{
    private static DungeonAudioController instance;

    [Header("Dungeon Scene Audio")]
    [SerializeField] private AudioClip dungeonBgm;
    [SerializeField] private AudioClip dungeonAmbience;

    [Header("Prison Capture")]
    [SerializeField] private AudioClip prisonCaptureSfx;

    [Header("Prison Whispers")]
    [SerializeField] private AudioClip[] prisonWhisperClips;
    [SerializeField, Range(0f, 1f)] private float prisonWhisperVolume = 0.3f;

    [Header("Mixer Groups")]
    [SerializeField] private AudioMixerGroup bgmMixerGroup;
    [SerializeField] private AudioMixerGroup ambienceMixerGroup;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float prisonSfxVolume = 0.8f;

    private AudioSource prisonSfxSource;
    private readonly List<AudioSource> prisonWhisperSources = new List<AudioSource>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        StartLoop(dungeonBgm, bgmVolume, bgmMixerGroup, "Dungeon BGM");
        StartLoop(dungeonAmbience, ambienceVolume, ambienceMixerGroup, "Dungeon Ambience");

        prisonSfxSource = gameObject.AddComponent<AudioSource>();
        prisonSfxSource.playOnAwake = false;
        prisonSfxSource.loop = false;
        prisonSfxSource.spatialBlend = 0f;
        prisonSfxSource.volume = 1f;
        prisonSfxSource.outputAudioMixerGroup = sfxMixerGroup;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void PlayPrisonCaptureSfx()
    {
        if (instance == null)
            return;

        if (instance.prisonCaptureSfx != null && instance.prisonSfxSource != null)
        {
            instance.prisonSfxSource.PlayOneShot(
                instance.prisonCaptureSfx,
                instance.prisonSfxVolume);
        }

        instance.StartPrisonWhispers();
    }

    public static void StopPrisonWhispers()
    {
        if (instance == null)
            return;

        foreach (AudioSource source in instance.prisonWhisperSources)
        {
            if (source != null && source.isPlaying)
                source.Stop();
        }
    }

    private void StartPrisonWhispers()
    {
        if (prisonWhisperClips == null || prisonWhisperClips.Length == 0)
            return;

        int validClipCount = 0;
        foreach (AudioClip clip in prisonWhisperClips)
        {
            if (clip != null)
                validClipCount++;
        }

        if (validClipCount == 0)
            return;

        while (prisonWhisperSources.Count < prisonWhisperClips.Length)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = ambienceMixerGroup;
            prisonWhisperSources.Add(source);
        }

        float volumePerClip = prisonWhisperVolume / validClipCount;
        for (int i = 0; i < prisonWhisperClips.Length; i++)
        {
            AudioClip clip = prisonWhisperClips[i];
            AudioSource source = prisonWhisperSources[i];
            if (clip == null)
                continue;

            source.clip = clip;
            source.volume = volumePerClip;
            if (!source.isPlaying)
                source.Play();
        }
    }

    private void StartLoop(
        AudioClip clip,
        float volume,
        AudioMixerGroup mixerGroup,
        string sourceName)
    {
        if (clip == null)
            return;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.name = sourceName;
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.outputAudioMixerGroup = mixerGroup;
        source.Play();
    }
}
