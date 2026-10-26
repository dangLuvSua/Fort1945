using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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

    private struct ResolutionOption
    {
        public int width;
        public int height;

        public ResolutionOption(int width, int height)
        {
            this.width = width;
            this.height = height;
        }
    }

    private ResolutionOption[] resolutions;

    private const string QualityKey = "GraphicsQuality";
    private const string ResolutionWidthKey = "ResolutionWidth";
    private const string ResolutionHeightKey = "ResolutionHeight";
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
        if (qualityNames == null || qualityNames.Length == 0)
            return;

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

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
    }


    // =========================
    // RESOLUTION
    // =========================

    private void SetupResolution()
    {
        if (resolutionDropdown == null)
            return;

        Resolution[] displayModes = Screen.resolutions;
        List<ResolutionOption> supportedModes = new List<ResolutionOption>();

        // Keep the graphics menu from offering tiny modes. Preserve distinct
        // width/height choices only, since refresh-rate variants look identical
        // in this dropdown.
        for (int i = 0; i < displayModes.Length; i++)
        {
            Resolution mode = displayModes[i];
            if (mode.width < 1920 || mode.height < 1080)
                continue;

            bool alreadyAdded = false;
            for (int j = 0; j < supportedModes.Count; j++)
            {
                if (supportedModes[j].width == mode.width &&
                    supportedModes[j].height == mode.height)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
                supportedModes.Add(new ResolutionOption(mode.width, mode.height));
        }

        // A windowed 1920x1080 option still provides the requested minimum on
        // displays that do not report a matching fullscreen mode.
        if (supportedModes.Count == 0)
        {
            supportedModes.Add(new ResolutionOption(1920, 1080));
        }

        resolutions = supportedModes.ToArray();

        resolutionDropdown.ClearOptions();

        var options =
            new System.Collections.Generic.List<string>();

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option =
                resolutions[i].width +
                " x " +
                resolutions[i].height;

            options.Add(option);

        }

        resolutionDropdown.AddOptions(options);

        int savedWidth = PlayerPrefs.GetInt(ResolutionWidthKey, 1920);
        int savedHeight = PlayerPrefs.GetInt(ResolutionHeightKey, 1080);
        int savedResolution = FindResolutionIndex(savedWidth, savedHeight);
        if (savedResolution < 0)
            savedResolution = FindDefaultResolutionIndex();

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

        resolutionIndex = Mathf.Clamp(resolutionIndex, 0, resolutions.Length - 1);

        ResolutionOption resolution =
            resolutions[resolutionIndex];

        Screen.SetResolution(
            resolution.width,
            resolution.height,
            Screen.fullScreenMode
        );

        PlayerPrefs.SetInt(ResolutionWidthKey, resolution.width);
        PlayerPrefs.SetInt(ResolutionHeightKey, resolution.height);

        PlayerPrefs.Save();

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == width && resolutions[i].height == height)
                return i;
        }

        return -1;
    }

    private int FindDefaultResolutionIndex()
    {
        int bestIndex = 0;
        long bestArea = long.MaxValue;

        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == 1920 && resolutions[i].height == 1080)
                return i;

            long area = (long)resolutions[i].width * resolutions[i].height;
            if (area < bestArea)
            {
                bestArea = area;
                bestIndex = i;
            }
        }

        return bestIndex;
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
