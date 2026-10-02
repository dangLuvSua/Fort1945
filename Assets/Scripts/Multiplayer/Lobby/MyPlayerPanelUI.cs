using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MyPlayerPanelUI : MonoBehaviour
{
    [Header("Player Information")]
    [SerializeField] private TMP_Text playerNameText;

    [Header("Character Avatar")]
    [SerializeField] private Image characterAvatarImage;
    [SerializeField] private Sprite[] characterAvatars;

    [Header("Ready")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private TMP_Text readyStatusText;

    private LobbyPlayerState localPlayerState;
    private NetworkRunner runner;

    private void Start()
    {
        Debug.Log(
            "[MY PLAYER PANEL] Started."
        );

        Debug.Log(
            $"[MY PLAYER PANEL] " +
            $"PlayerProfile Name: {PlayerProfile.PlayerName}"
        );

        Debug.Log(
            $"[MY PLAYER PANEL] " +
            $"PlayerProfile Character: " +
            $"{PlayerProfile.SelectedCharacter}"
        );

        // Display local saved data immediately.
        UpdateFromPlayerProfile();

        FindRunner();
        FindLocalPlayer();
    }

    private void Update()
    {
        if (runner == null)
        {
            FindRunner();
        }

        if (localPlayerState == null)
        {
            FindLocalPlayer();
        }

        if (localPlayerState != null)
        {
            UpdateFromNetworkState();
        }
        else
        {
            UpdateReadyFromProfileFallback();
        }
    }

    private void FindRunner()
    {
        runner =
            FindAnyObjectByType<NetworkRunner>();

        if (runner != null)
        {
            Debug.Log(
                "[MY PLAYER PANEL] NetworkRunner found."
            );
        }
    }

    private void FindLocalPlayer()
    {
        if (runner == null)
            return;

        if (!runner.LocalPlayer.IsValid)
            return;

        if (!runner.TryGetPlayerObject(
            runner.LocalPlayer,
            out NetworkObject playerObject))
        {
            return;
        }

        localPlayerState =
            playerObject.GetComponent<LobbyPlayerState>();

        if (localPlayerState != null)
        {
            Debug.Log(
                "[MY PLAYER PANEL] " +
                "Local LobbyPlayerState found."
            );

            Debug.Log(
                $"[MY PLAYER PANEL] " +
                $"Network Name: " +
                $"{localPlayerState.PlayerName}"
            );

            Debug.Log(
                $"[MY PLAYER PANEL] " +
                $"Network Character: " +
                $"{localPlayerState.SelectedCharacter}"
            );
        }
    }

    private void UpdateFromPlayerProfile()
    {
        // PLAYER NAME
        if (playerNameText != null)
        {
            string playerName =
                PlayerProfile.PlayerName;

            if (string.IsNullOrWhiteSpace(playerName))
            {
                playerName = "Player";
            }

            playerNameText.text =
                playerName;
        }

        // CHARACTER AVATAR
        string characterId =
            PlayerProfile.SelectedCharacter;

        SetCharacterAvatar(characterId);
    }

    private void UpdateFromNetworkState()
    {
        if (localPlayerState == null)
            return;

        // PLAYER NAME
        if (playerNameText != null)
        {
            string playerName =
                localPlayerState.PlayerName.ToString();

            if (string.IsNullOrWhiteSpace(playerName))
            {
                playerName =
                    PlayerProfile.PlayerName;
            }

            if (string.IsNullOrWhiteSpace(playerName))
            {
                playerName = "Player";
            }

            playerNameText.text =
                playerName;
        }

        // CHARACTER
        string characterId =
            localPlayerState.SelectedCharacter.ToString();

        if (string.IsNullOrWhiteSpace(characterId))
        {
            characterId =
                PlayerProfile.SelectedCharacter;
        }

        SetCharacterAvatar(characterId);

        // READY
        UpdateReadyUI();
    }

    private void UpdateReadyFromProfileFallback()
    {
        if (readyButtonText != null)
        {
            readyButtonText.text =
                "READY";
        }

        if (readyStatusText != null)
        {
            readyStatusText.text =
                "NOT READY";
        }
    }

    private void UpdateReadyUI()
    {
        bool isReady =
            localPlayerState.IsReady;

        if (readyButtonText != null)
        {
            readyButtonText.text =
                isReady
                    ? "NOT READY"
                    : "READY";
        }

        if (readyStatusText != null)
        {
            readyStatusText.text =
                isReady
                    ? "READY"
                    : "NOT READY";
        }
    }

    private void SetCharacterAvatar(
        string characterId)
    {
        if (characterAvatarImage == null)
            return;

        Sprite avatar =
            GetCharacterAvatar(characterId);

        if (avatar != null)
        {
            characterAvatarImage.sprite =
                avatar;

            characterAvatarImage.enabled =
                true;
        }
        else
        {
            characterAvatarImage.enabled =
                false;

            Debug.LogWarning(
                $"[MY PLAYER PANEL] " +
                $"No avatar found for {characterId}."
            );
        }
    }

    private Sprite GetCharacterAvatar(
        string characterId)
    {
        if (characterAvatars == null ||
            characterAvatars.Length == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        if (!characterId.StartsWith(
            "Character"))
        {
            return null;
        }

        string numberPart =
            characterId.Replace(
                "Character",
                ""
            );

        if (!int.TryParse(
            numberPart,
            out int characterNumber))
        {
            return null;
        }

        int index =
            characterNumber - 1;

        if (index < 0 ||
            index >= characterAvatars.Length)
        {
            return null;
        }

        return characterAvatars[index];
    }

    public void ToggleReady()
    {
        FindRunner();
        FindLocalPlayer();

        if (localPlayerState == null)
        {
            Debug.LogWarning(
                "[MY PLAYER PANEL] " +
                "Cannot toggle Ready. " +
                "Local LobbyPlayerState not found."
            );

            return;
        }

        bool newReadyState =
            !localPlayerState.IsReady;

        localPlayerState.SetReady(
            newReadyState
        );

        Debug.Log(
            $"[MY PLAYER PANEL] " +
            $"Ready changed to: {newReadyState}"
        );
    }
}