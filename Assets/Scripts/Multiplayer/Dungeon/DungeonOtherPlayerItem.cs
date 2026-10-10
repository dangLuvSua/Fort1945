
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DungeonOtherPlayerItem : MonoBehaviour
{
    // =====================================================
    // PLAYER INFORMATION
    // =====================================================

    [Header("Player Information")]
    [SerializeField] private TMP_Text playerNameText;


    // =====================================================
    // CHARACTER AVATAR
    // =====================================================

    [Header("Character Avatar")]
    [SerializeField] private Image characterAvatarImage;


    // =====================================================
    // PLAYER STATS
    // =====================================================

    [Header("Stamina UI")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private TMP_Text staminaText;


    [Header("Sanity UI")]
    [SerializeField] private Slider sanitySlider;
    [SerializeField] private TMP_Text sanityText;


    // =====================================================
    // REFERENCES
    // =====================================================

    private LobbyPlayerState playerState;

    private PlayerStamina playerStamina;

    private PlayerSanity playerSanity;


    // =====================================================
    // SETUP
    // =====================================================

    public void Setup(
        LobbyPlayerState state,
        Sprite characterAvatar)
    {
        playerState = state;

        // Reset cached components in case this UI entry is reused.
        playerStamina = null;
        playerSanity = null;

        if (playerState == null)
        {
            Debug.LogWarning(
                "[DUNGEON OTHER PLAYER ITEM] " +
                "Player state is missing."
            );

            ClearStatsDisplay();
            return;
        }

        UpdateDisplay(characterAvatar);

        FindNetworkedStats();
        UpdateStaminaDisplay();
        UpdateSanityDisplay();
    }


    // =====================================================
    // UPDATE
    // =====================================================

    private void Update()
    {
        if (playerState == null)
            return;

        // Player objects may spawn after the list entry.
        if (playerStamina == null || playerSanity == null)
        {
            FindNetworkedStats();
        }

        UpdateStaminaDisplay();
        UpdateSanityDisplay();
    }


    // =====================================================
    // FIND NETWORKED PLAYER STATS
    // =====================================================

    private void FindNetworkedStats()
    {
        if (playerState == null)
            return;

        if (playerState.Object == null ||
            !playerState.Object.IsValid)
        {
            return;
        }

        NetworkRunner runner = playerState.Runner;

        if (runner == null || !runner.IsRunning)
            return;

        // Identify the player represented by this list entry.
        PlayerRef targetPlayer =
            playerState.Object.InputAuthority;

        if (!runner.TryGetPlayerObject(
            targetPlayer,
            out NetworkObject playerObject))
        {
            return;
        }

        if (playerObject == null || !playerObject.IsValid)
            return;


        // -------------------------------------------------
        // FIND STAMINA
        // -------------------------------------------------

        if (playerStamina == null)
        {
            playerStamina =
                playerObject.GetComponent<PlayerStamina>();

            if (playerStamina == null)
            {
                playerStamina =
                    playerObject.GetComponentInChildren<PlayerStamina>(
                        true
                    );
            }
        }


        // -------------------------------------------------
        // FIND SANITY
        // -------------------------------------------------

        if (playerSanity == null)
        {
            playerSanity =
                playerObject.GetComponent<PlayerSanity>();

            if (playerSanity == null)
            {
                playerSanity =
                    playerObject.GetComponentInChildren<PlayerSanity>(
                        true
                    );
            }
        }
    }


    // =====================================================
    // STAMINA DISPLAY
    // =====================================================

    private void UpdateStaminaDisplay()
    {
        if (staminaSlider == null || playerStamina == null)
            return;

        staminaSlider.minValue = 0f;
        staminaSlider.maxValue = Mathf.Max(
            0.01f,
            playerStamina.Max
        );

        staminaSlider.SetValueWithoutNotify(
            Mathf.Clamp(
                playerStamina.Current,
                0f,
                playerStamina.Max
            )
        );

        if (staminaText != null)
        {
            staminaText.text =
                $"{Mathf.CeilToInt(playerStamina.Current)}" +
                $"/{Mathf.CeilToInt(playerStamina.Max)}";
        }
    }


    // =====================================================
    // SANITY DISPLAY
    // =====================================================

    private void UpdateSanityDisplay()
    {
        if (sanitySlider == null || playerSanity == null)
            return;

        if (playerSanity.Object == null ||
            !playerSanity.Object.IsValid ||
            !playerSanity.Object.IsInSimulation)
        {
            return;
        }

        float maxSanity = Mathf.Max(
            0.01f,
            playerSanity.MaxSanity
        );

        float currentSanity = Mathf.Clamp(
            playerSanity.CurrentSanity,
            0f,
            maxSanity
        );

        sanitySlider.minValue = 0f;
        sanitySlider.maxValue = maxSanity;

        sanitySlider.SetValueWithoutNotify(currentSanity);

        if (sanityText != null)
        {
            sanityText.text =
                $"{Mathf.CeilToInt(currentSanity)}" +
                $"/{Mathf.CeilToInt(maxSanity)}";
        }
    }


    // =====================================================
    // PLAYER NAME AND AVATAR
    // =====================================================

    public void UpdateDisplay(Sprite characterAvatar)
    {
        if (playerState == null)
            return;

        // Player name
        if (playerNameText != null)
        {
            playerNameText.text =
                playerState.PlayerName.ToString();
        }

        // Character avatar
        if (characterAvatarImage != null)
        {
            if (characterAvatar != null)
            {
                characterAvatarImage.sprite = characterAvatar;
                characterAvatarImage.enabled = true;
            }
            else
            {
                characterAvatarImage.sprite = null;
                characterAvatarImage.enabled = false;
            }
        }
    }


    // =====================================================
    // CLEAR UI
    // =====================================================

    private void ClearStatsDisplay()
    {
        if (staminaSlider != null)
            staminaSlider.SetValueWithoutNotify(0f);

        if (staminaText != null)
            staminaText.text = "--";

        if (sanitySlider != null)
            sanitySlider.SetValueWithoutNotify(0f);

        if (sanityText != null)
            sanityText.text = "--";
    }
}