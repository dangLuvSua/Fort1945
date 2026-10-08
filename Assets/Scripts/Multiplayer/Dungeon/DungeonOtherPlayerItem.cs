using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DungeonOtherPlayerItem : MonoBehaviour
{
    [Header("Player Information")]
    [SerializeField] private TMP_Text playerNameText;


    [Header("Character Avatar")]
    [SerializeField] private Image characterAvatarImage;


    [Header("Player Stats")]
    [SerializeField] private Slider staminaSlider;

    // Sanity temporarily disabled.
    // [SerializeField] private Slider sanitySlider;


    // =====================================================
    // REFERENCES
    // =====================================================

    private LobbyPlayerState playerState;

    private PlayerStamina playerStamina;

    // Sanity temporarily disabled.
    // private PlayerSanity playerSanity;


    // =====================================================
    // SETUP
    // =====================================================

    public void Setup(
        LobbyPlayerState state,
        Sprite characterAvatar)
    {
        playerState = state;

        playerStamina = null;

        // Sanity temporarily disabled.
        // playerSanity = null;


        if (playerState == null)
        {
            Debug.LogWarning(
                "[DUNGEON OTHER PLAYER ITEM] " +
                "Player state is missing."
            );

            return;
        }


        UpdateDisplay(characterAvatar);

        FindNetworkedStats();
    }


    // =====================================================
    // UPDATE
    // =====================================================

    private void Update()
    {
        if (playerState == null)
            return;


        // The network player object might spawn
        // slightly later than the UI item.
        if (playerStamina == null)
        {
            FindNetworkedStats();
        }


        UpdateStaminaDisplay();

        // Sanity temporarily disabled.
        // UpdateSanityDisplay();
    }


    // =====================================================
    // FIND NETWORKED PLAYER STATS
    // =====================================================

    private void FindNetworkedStats()
    {
        if (playerState == null)
            return;


        if (playerState.Object == null)
            return;


        NetworkRunner runner =
            playerState.Runner;

        if (runner == null)
            return;


        PlayerRef targetPlayer =
            playerState.Object.InputAuthority;


        // -------------------------------------------------
        // Find actual network player
        // -------------------------------------------------

        if (!runner.TryGetPlayerObject(
            targetPlayer,
            out NetworkObject playerObject))
        {
            return;
        }


        if (playerObject == null)
            return;


        // -------------------------------------------------
        // Find stamina
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
        // Find sanity
        // -------------------------------------------------

        // Sanity temporarily disabled.
        /*
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
        */
    }


    // =====================================================
    // STAMINA
    // =====================================================

    private void UpdateStaminaDisplay()
    {
        if (staminaSlider == null)
            return;


        if (playerStamina == null)
            return;


        staminaSlider.minValue = 0f;

        staminaSlider.maxValue =
            playerStamina.Max;

        staminaSlider.value =
            playerStamina.Current;
    }


    // =====================================================
    // SANITY
    // =====================================================

    /*
    // Sanity temporarily disabled.

    private void UpdateSanityDisplay()
    {
        if (sanitySlider == null)
            return;


        if (playerSanity == null)
            return;


        sanitySlider.minValue = 0f;

        sanitySlider.maxValue =
            playerSanity.Max;

        sanitySlider.value =
            playerSanity.Current;
    }

    */


    // =====================================================
    // DISPLAY
    // =====================================================

    public void UpdateDisplay(
        Sprite characterAvatar)
    {
        if (playerState == null)
            return;


        // -------------------------------------------------
        // NAME
        // -------------------------------------------------

        if (playerNameText != null)
        {
            playerNameText.text =
                playerState.PlayerName.ToString();
        }


        // -------------------------------------------------
        // AVATAR
        // -------------------------------------------------

        if (characterAvatarImage != null)
        {
            if (characterAvatar != null)
            {
                characterAvatarImage.sprite =
                    characterAvatar;

                characterAvatarImage.enabled =
                    true;
            }
            else
            {
                characterAvatarImage.enabled =
                    false;
            }
        }
    }
}