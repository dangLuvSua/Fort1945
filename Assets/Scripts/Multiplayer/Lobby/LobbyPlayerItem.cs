using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyPlayerItem : MonoBehaviour
{
    [Header("Player Information")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text readyStatusText;
    [SerializeField] private TMP_Text hostText;


    [Header("Character Avatar")]
    [SerializeField] private Image characterAvatarImage;


    [Header("Player Stats")]
    [SerializeField] private Slider staminaSlider;


    // =====================================================
    // REFERENCES
    // =====================================================

    private LobbyPlayerState playerState;

    // Cached reference to the actual networked player's
    // stamina component.
    private PlayerStamina playerStamina;


    // =====================================================
    // SETUP
    // =====================================================

    public void Setup(
        LobbyPlayerState state,
        Sprite characterAvatar)
    {
        playerState = state;

        // Reset cached reference when this UI item is
        // reused for another player.
        playerStamina = null;


        if (playerState == null)
        {
            Debug.LogWarning(
                "[LOBBY PLAYER ITEM] " +
                "Player state is missing."
            );

            return;
        }


        UpdateDisplay(characterAvatar);


        // Try immediately.
        FindPlayerStamina();
    }


    // =====================================================
    // UPDATE
    // =====================================================

    private void Update()
    {
        if (playerState == null)
            return;


        // The actual player object can spawn slightly later
        // than LobbyPlayerState.
        if (playerStamina == null)
        {
            FindPlayerStamina();
        }


        UpdateStaminaDisplay();
    }


    // =====================================================
    // FIND PLAYER STAMINA
    // =====================================================

    private void FindPlayerStamina()
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


        // Ask Fusion for the actual network player object.
        if (!runner.TryGetPlayerObject(
            targetPlayer,
            out NetworkObject playerObject))
        {
            return;
        }


        if (playerObject == null)
            return;


        // PlayerStamina should be on the networked
        // player prefab.
        PlayerStamina stamina =
            playerObject.GetComponent<PlayerStamina>();


        // Also support PlayerStamina being on a child.
        if (stamina == null)
        {
            stamina =
                playerObject.GetComponentInChildren<PlayerStamina>(
                    true
                );
        }


        if (stamina == null)
        {
            return;
        }


        playerStamina = stamina;


        Debug.Log(
            $"[LOBBY PLAYER ITEM] " +
            $"Connected stamina for " +
            $"{playerState.PlayerName} " +
            $"({targetPlayer})"
        );


        // -------------------------------------------------
        // Initialize slider
        // -------------------------------------------------

        if (staminaSlider != null)
        {
            staminaSlider.minValue = 0f;
            staminaSlider.maxValue =
                playerStamina.Max;

            staminaSlider.value =
                playerStamina.Current;
        }
    }


    // =====================================================
    // STAMINA DISPLAY
    // =====================================================

    private void UpdateStaminaDisplay()
    {
        if (staminaSlider == null)
            return;


        if (playerStamina == null)
            return;


        // Keep slider range synchronized with the
        // player's configured maximum.
        staminaSlider.minValue = 0f;

        staminaSlider.maxValue =
            playerStamina.Max;


        // IMPORTANT:
        // Current is now [Networked], so this value can be
        // read by other players.
        staminaSlider.value =
            playerStamina.Current;
    }


    // =====================================================
    // UPDATE DISPLAY
    // =====================================================

    public void UpdateDisplay(
        Sprite characterAvatar)
    {
        if (playerState == null)
            return;


        // ==========================================
        // PLAYER NAME
        // ==========================================

        if (playerNameText != null)
        {
            playerNameText.text =
                playerState.PlayerName.ToString();
        }


        // ==========================================
        // CHECK GAME STATE
        // ==========================================

        NetworkGameManager manager =
            NetworkGameManager.Instance;


        bool gameStarted =
            manager != null &&
            manager.IsNetworkStateReady &&
            manager.GameStarted;


        bool dungeonScene =
            SceneManager.GetActiveScene().name ==
            "Dungeon";


        // ==========================================
        // READY / IN-GAME STATUS
        // ==========================================

        if (readyStatusText != null)
        {
            // Dungeon players are always shown as
            // IN-GAME.
            if (dungeonScene)
            {
                readyStatusText.text =
                    "IN-GAME";
            }
            else if (gameStarted)
            {
                readyStatusText.text =
                    "IN-GAME";
            }
            else
            {
                readyStatusText.text =
                    playerState.IsReady
                        ? "READY"
                        : "NOT READY";
            }
        }


        // ==========================================
        // HOST
        // ==========================================

        if (hostText != null)
        {
            hostText.text =
                playerState.IsHost
                    ? "HOST"
                    : "";
        }


        // ==========================================
        // CHARACTER AVATAR
        // ==========================================

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