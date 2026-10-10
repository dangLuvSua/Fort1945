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
    [SerializeField] private Slider sanitySlider;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private TMP_Text sanityText;


    // =====================================================
    // REFERENCES
    // =====================================================

    private LobbyPlayerState playerState;

    // Cached reference to the actual networked player's
    // stamina component.
    private PlayerStamina playerStamina;

    // Cached reference to the actual networked player's
    // sanity component.
    private PlayerSanity playerSanity;


    // =====================================================
    // SETUP
    // =====================================================

    public void Setup(
        LobbyPlayerState state,
        Sprite characterAvatar)
    {
        playerState = state;

        // Reset cached references when this UI item is
        // reused for another player.
        playerStamina = null;
        playerSanity = null;

        // Wire any stats UI elements that were not assigned
        // in the Inspector (found by name on this prefab).
        ResolveStatElements();


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

        if (playerSanity == null)
        {
            FindPlayerSanity();
        }


        UpdateStaminaDisplay();

        UpdateSanityDisplay();
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
    // FIND PLAYER SANITY
    // =====================================================

    private void FindPlayerSanity()
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


        // PlayerSanity should be on the networked
        // player prefab.
        PlayerSanity sanity =
            playerObject.GetComponent<PlayerSanity>();


        // Also support PlayerSanity being on a child.
        if (sanity == null)
        {
            sanity =
                playerObject.GetComponentInChildren<PlayerSanity>(
                    true
                );
        }


        if (sanity == null)
        {
            return;
        }


        playerSanity = sanity;


        Debug.Log(
            $"[LOBBY PLAYER ITEM] " +
            $"Connected sanity for " +
            $"{playerState.PlayerName} " +
            $"({targetPlayer})"
        );
    }


    // =====================================================
    // STAMINA DISPLAY
    // =====================================================

    private void UpdateStaminaDisplay()
    {
        if (playerStamina == null)
            return;

        // -------------------------------------------------
        // SLIDER
        // -------------------------------------------------

        if (staminaSlider != null)
        {
            // Keep slider range synchronized with the
            // player's configured maximum.
            staminaSlider.minValue = 0f;

            staminaSlider.maxValue =
                playerStamina.Max;


            // IMPORTANT:
            // Current is now [Networked], so this value can
            // be read by other players.
            staminaSlider.value =
                playerStamina.Current;
        }

        // -------------------------------------------------
        // NUMBER TEXT
        // -------------------------------------------------

        if (staminaText != null)
        {
            staminaText.text =
                $"{Mathf.CeilToInt(playerStamina.Current)}";
        }
    }


    // =====================================================
    // SANITY DISPLAY
    // =====================================================

    private void UpdateSanityDisplay()
    {
        if (playerSanity == null)
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

        // -------------------------------------------------
        // SLIDER
        // -------------------------------------------------

        if (sanitySlider != null)
        {
            sanitySlider.minValue = 0f;

            sanitySlider.maxValue = maxSanity;

            sanitySlider.SetValueWithoutNotify(currentSanity);
        }

        // -------------------------------------------------
        // NUMBER TEXT
        // -------------------------------------------------

        if (sanityText != null)
        {
            sanityText.text =
                $"{Mathf.CeilToInt(currentSanity)}";
        }
    }


    // =====================================================
    // RESOLVE STAT ELEMENTS
    // =====================================================

    private void ResolveStatElements()
    {
        if (staminaSlider == null)
        {
            Transform element =
                transform.Find("StaminaSlider");

            if (element != null)
            {
                staminaSlider =
                    element.GetComponent<Slider>();
            }
        }

        if (sanitySlider == null)
        {
            Transform element =
                transform.Find("SanitySlider");

            if (element != null)
            {
                sanitySlider =
                    element.GetComponent<Slider>();
            }
        }

        if (staminaText == null)
        {
            Transform element =
                transform.Find("StaminaNum");

            if (element != null)
            {
                staminaText =
                    element.GetComponent<TMP_Text>();
            }
        }

        if (sanityText == null)
        {
            Transform element =
                transform.Find("SanityNum");

            if (element != null)
            {
                sanityText =
                    element.GetComponent<TMP_Text>();
            }
        }
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