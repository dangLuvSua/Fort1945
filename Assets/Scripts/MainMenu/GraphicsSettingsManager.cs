using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GraphicsSettingsManager : MonoBehaviour
{
    [Header("Graphics UI")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private Slider brightnessSlider;

    [Header("Brightness")]
    [SerializeField] private float minBrightness = 0.5f;
    [SerializeField] private float maxBrightness = 1.5f;

    [Header("Brightness Overlay")]
[SerializeField] private Image brightnessOverlay;

    private Resolution[] resolutions;

    private const string QualityKey = "GraphicsQuality";
    private const string ResolutionKey = "Resolution";
    private const string FullscreenKey = "Fullscreen";
    private const string VSyncKey = "VSync";
    private const string BrightnessKey = "Brightness";

    private void Start()
    {
        SetupQuality();
        SetupResolution();
        SetupFullscreen();
        SetupVSync();
        SetupBrightness();
    }

    // =========================
    // QUALITY
    // =========================

    private void SetupQuality()
    {
        if (qualityDropdown == null)
            return;

        qualityDropdown.ClearOptions();

        string[] qualityNames = QualitySettings.names;

        qualityDropdown.AddOptions(
            new System.Collections.Generic.List<string>(qualityNames)
        );

        int savedQuality = PlayerPrefs.GetInt(
            QualityKey,
            QualitySettings.GetQualityLevel()
        );

        savedQuality = Mathf.Clamp(
            savedQuality,
            0,
            qualityNames.Length - 1
        );

        qualityDropdown.SetValueWithoutNotify(savedQuality);

        QualitySettings.SetQualityLevel(savedQuality);

        qualityDropdown.onValueChanged.AddListener(SetQuality);
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);

        PlayerPrefs.SetInt(QualityKey, qualityIndex);
        PlayerPrefs.Save();
    }


    // =========================
    // RESOLUTION
    // =========================

    private void SetupResolution()
    {
        if (resolutionDropdown == null)
            return;

        resolutions = Screen.resolutions;

        resolutionDropdown.ClearOptions();

        var options =
            new System.Collections.Generic.List<string>();

        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option =
                resolutions[i].width +
                " x " +
                resolutions[i].height;

            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);

        int savedResolution = PlayerPrefs.GetInt(
            ResolutionKey,
            currentResolutionIndex
        );

        savedResolution = Mathf.Clamp(
            savedResolution,
            0,
            resolutions.Length - 1
        );

        resolutionDropdown.SetValueWithoutNotify(
            savedResolution
        );

        ApplyResolution(savedResolution);

        resolutionDropdown.onValueChanged.AddListener(
            ApplyResolution
        );
    }

    public void ApplyResolution(int resolutionIndex)
    {
        if (resolutions == null ||
            resolutions.Length == 0)
            return;

        Resolution resolution =
            resolutions[resolutionIndex];

        Screen.SetResolution(
            resolution.width,
            resolution.height,
            Screen.fullScreenMode
        );

        PlayerPrefs.SetInt(
            ResolutionKey,
            resolutionIndex
        );

        PlayerPrefs.Save();
    }


    // =========================
    // FULLSCREEN
    // =========================

    private void SetupFullscreen()
    {
        if (fullscreenToggle == null)
            return;

        bool fullscreen = PlayerPrefs.GetInt(
            FullscreenKey,
            Screen.fullScreen ? 1 : 0
        ) == 1;

        fullscreenToggle.SetIsOnWithoutNotify(
            fullscreen
        );

        Screen.fullScreen = fullscreen;

        fullscreenToggle.onValueChanged.AddListener(
            SetFullscreen
        );
    }

    public void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;

        PlayerPrefs.SetInt(
            FullscreenKey,
            fullscreen ? 1 : 0
        );

        PlayerPrefs.Save();
    }


    // =========================
    // VSYNC
    // =========================

    private void SetupVSync()
    {
        if (vSyncToggle == null)
            return;

        bool vSync = PlayerPrefs.GetInt(
            VSyncKey,
            QualitySettings.vSyncCount > 0 ? 1 : 0
        ) == 1;

        vSyncToggle.SetIsOnWithoutNotify(
            vSync
        );

        QualitySettings.vSyncCount =
            vSync ? 1 : 0;

        vSyncToggle.onValueChanged.AddListener(
            SetVSync
        );
    }

    public void SetVSync(bool enabled)
    {
        QualitySettings.vSyncCount =
            enabled ? 1 : 0;

        PlayerPrefs.SetInt(
            VSyncKey,
            enabled ? 1 : 0
        );

        PlayerPrefs.Save();
    }


    // =========================
    // BRIGHTNESS
    // =========================

    private void SetupBrightness()
    {
        if (brightnessSlider == null)
            return;

        brightnessSlider.minValue =
            minBrightness;

        brightnessSlider.maxValue =
            maxBrightness;

        float brightness = PlayerPrefs.GetFloat(
            BrightnessKey,
            1f
        );

        brightnessSlider.SetValueWithoutNotify(
            brightness
        );

        ApplyBrightness(brightness);

        brightnessSlider.onValueChanged.AddListener(
            ApplyBrightness
        );
    }

   public void ApplyBrightness(float brightness)
{
    if (brightnessOverlay == null)
        return;

    float alpha = 1f - brightness;

    alpha = Mathf.Clamp01(alpha);

    Color color = brightnessOverlay.color;

    color.a = alpha;

    brightnessOverlay.color = color;

    PlayerPrefs.SetFloat(
        BrightnessKey,
        brightness
    );

    PlayerPrefs.Save();
}
}