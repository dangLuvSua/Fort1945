using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerItem : MonoBehaviour
{
    [Header("Player Information")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text readyStatusText;
    [SerializeField] private TMP_Text hostText;

    [Header("Character Avatar")]
    [SerializeField] private Image characterAvatarImage;

    private LobbyPlayerState playerState;


    public void Setup(
        LobbyPlayerState state,
        Sprite characterAvatar)
    {
        playerState = state;

        if (playerState == null)
        {
            Debug.LogWarning(
                "[LOBBY PLAYER ITEM] " +
                "Player state is missing."
            );

            return;
        }

        UpdateDisplay(characterAvatar);
    }


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
        // READY STATUS
        // ==========================================

        if (readyStatusText != null)
        {
            readyStatusText.text =
                playerState.IsReady
                    ? "READY"
                    : "NOT READY";
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

                characterAvatarImage.enabled = true;
            }
            else
            {
                characterAvatarImage.enabled = false;
            }
        }
    }
}