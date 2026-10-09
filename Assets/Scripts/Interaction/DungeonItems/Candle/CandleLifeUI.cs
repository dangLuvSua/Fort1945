
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CandleLifeUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject candleLifePanel;
    [SerializeField] private Slider candleLifeSlider;
    [SerializeField] private TMP_Text candleLifeText;

    private CandleController candleController;
    private float searchTimer;

    [SerializeField, Min(0.1f)]
    private float controllerSearchInterval = 0.5f;

    private void Start()
    {
        if (candleLifePanel != null)
            candleLifePanel.SetActive(false);

        if (candleLifeSlider != null)
        {
            candleLifeSlider.minValue = 0f;
            candleLifeSlider.maxValue = 1f;
            candleLifeSlider.value = 1f;
        }
    }

    private void Update()
    {
        if (candleController == null ||
            candleController.Object == null ||
            !candleController.Object.IsValid)
        {
            searchTimer -= Time.deltaTime;

            if (searchTimer <= 0f)
            {
                searchTimer = controllerSearchInterval;
                FindLocalController();
            }
        }

        if (candleController == null ||
            candleController.Object == null ||
            !candleController.Object.IsValid ||
            !candleController.Object.HasInputAuthority)
        {
            SetPanelVisible(false);
            return;
        }

        bool show = candleController.IsHoldingCandle;

        SetPanelVisible(show);

        if (!show)
            return;

        float maxLife = candleController.MaxCandleLife;
        float remaining = candleController.RemainingSeconds;

        float percent = maxLife > 0f
            ? Mathf.Clamp01(remaining / maxLife)
            : 0f;

        if (candleLifeSlider != null)
            candleLifeSlider.value = percent;

        if (candleLifeText != null)
        {
            int totalSeconds = Mathf.CeilToInt(remaining);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            candleLifeText.text =
                $"CANDLE {percent * 100f:0}%  " +
                $"{minutes:00}:{seconds:00}";
        }
    }

    private void FindLocalController()
    {
        CandleController[] controllers =
            FindObjectsByType<CandleController>(
                FindObjectsSortMode.None
            );

        foreach (CandleController controller in controllers)
        {
            if (controller == null ||
                controller.Object == null ||
                !controller.Object.IsValid)
                continue;

            if (controller.Object.HasInputAuthority)
            {
                candleController = controller;
                return;
            }
        }

        candleController = null;
    }

    private void SetPanelVisible(bool visible)
    {
        if (candleLifePanel != null &&
            candleLifePanel.activeSelf != visible)
        {
            candleLifePanel.SetActive(visible);
        }
    }
}