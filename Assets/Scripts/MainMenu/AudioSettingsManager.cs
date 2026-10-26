using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioSettingsManager : MonoBehaviour
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Volume Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider mainMenuSlider;

    [Header("Default Volumes")]
    [Range(0f, 1f)]
    [SerializeField] private float defaultMasterVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float defaultBGMVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float defaultSFXVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float defaultMainMenuVolume = 1f;

    private const string MasterVolume = "MasterVolume";
    private const string BGMVolume = "BGMVolume";
    private const string SFXVolume = "SFXVolume";
    private const string MainMenuVolume = "MainMenuVolume";

    private void Start()
    {
        // The in-game options prefab has no persistent event targets for its
        // sliders, so bind them here on the live manager instance.
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(SetMasterVolume);
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        if (mainMenuSlider != null) mainMenuSlider.onValueChanged.AddListener(SetMainMenuVolume);

        // Load saved values
        float master = PlayerPrefs.GetFloat(
            MasterVolume,
            defaultMasterVolume
        );

        float bgm = PlayerPrefs.GetFloat(
            BGMVolume,
            defaultBGMVolume
        );

        float sfx = PlayerPrefs.GetFloat(
            SFXVolume,
            defaultSFXVolume
        );

        float mainMenu = PlayerPrefs.GetFloat(
            MainMenuVolume,
            defaultMainMenuVolume
        );

        // Set slider positions
        if (masterSlider != null)
            masterSlider.SetValueWithoutNotify(master);

        if (bgmSlider != null)
            bgmSlider.SetValueWithoutNotify(bgm);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(sfx);

        if (mainMenuSlider != null)
            mainMenuSlider.SetValueWithoutNotify(mainMenu);

        // Apply volumes
        SetMasterVolume(master);
        SetBGMVolume(bgm);
        SetSFXVolume(sfx);
        SetMainMenuVolume(mainMenu);
    }

    public void SetMasterVolume(float value)
    {
        if (audioMixer != null)
            audioMixer.SetFloat(MasterVolume, ConvertToDecibel(value));

        PlayerPrefs.SetFloat(MasterVolume, value);
        PlayerPrefs.Save();
    }

    public void SetBGMVolume(float value)
    {
        if (audioMixer != null)
            audioMixer.SetFloat(BGMVolume, ConvertToDecibel(value));

        PlayerPrefs.SetFloat(BGMVolume, value);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float value)
    {
        if (audioMixer != null)
            audioMixer.SetFloat(SFXVolume, ConvertToDecibel(value));

        PlayerPrefs.SetFloat(SFXVolume, value);
        PlayerPrefs.Save();
    }

    public void SetMainMenuVolume(float value)
    {
        if (audioMixer != null)
            audioMixer.SetFloat(MainMenuVolume, ConvertToDecibel(value));

        PlayerPrefs.SetFloat(MainMenuVolume, value);
        PlayerPrefs.Save();
    }

    private float ConvertToDecibel(float value)
    {
        if (value <= 0.0001f)
            return -80f;

        return Mathf.Log10(value) * 20f;
    }
}
