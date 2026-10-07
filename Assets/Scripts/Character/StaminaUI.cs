using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StaminaUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private TMP_Text staminaNumber;

    private PlayerStamina playerStamina;

    private void Update()
    {
        // Find the local player's stamina if we don't have it yet.
        if (playerStamina == null)
        {
            FindLocalPlayerStamina();

            if (playerStamina == null)
                return;
        }

        // Update slider.
        staminaSlider.maxValue = playerStamina.Max;
        staminaSlider.value = playerStamina.Current;

        // Update number.
        if (staminaNumber != null)
        {
            staminaNumber.text = Mathf.RoundToInt(
                playerStamina.Current
            ).ToString();
        }
    }

    private void FindLocalPlayerStamina()
    {
        PlayerStamina[] players =
            FindObjectsByType<PlayerStamina>(FindObjectsSortMode.None);

        foreach (PlayerStamina stamina in players)
        {
            if (stamina.Object != null &&
                stamina.Object.HasInputAuthority)
            {
                playerStamina = stamina;
                break;
            }
        }
    }
}