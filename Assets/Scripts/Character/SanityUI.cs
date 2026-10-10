
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SanityUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerSanity playerSanity;
    [SerializeField] private Slider sanitySlider;
    [SerializeField] private TMP_Text sanityText;


    private void Update()
    {
        if (playerSanity == null)
        {
            FindLocalPlayerSanity();
            return;
        }

        if (playerSanity.Object == null ||
            !playerSanity.Object.IsValid ||
            !playerSanity.Object.IsInSimulation)
        {
            playerSanity = null;
            return;
        }

        UpdateDisplay();
    }
    private void FindLocalPlayerSanity()
    {
        PlayerSanity[] players =
            FindObjectsByType<PlayerSanity>(
                FindObjectsSortMode.None
            );

        foreach (PlayerSanity candidate in players)
        {
            if (candidate == null ||
                candidate.Object == null ||
                !candidate.Object.IsValid ||
                !candidate.Object.HasInputAuthority)
            {
                continue;
            }

            playerSanity = candidate;
            UpdateDisplay();
            return;
        }
    }

    private void UpdateDisplay()
    {
        if (sanitySlider != null)
        {
            sanitySlider.minValue = 0f;
            sanitySlider.maxValue = playerSanity.MaxSanity;
            sanitySlider.SetValueWithoutNotify(
                playerSanity.CurrentSanity
            );
        }

        if (sanityText != null)
        {
            sanityText.text =
                $"{Mathf.CeilToInt(playerSanity.CurrentSanity)}";
        }
    }
}