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


    // =====================================================
    // UNITY
    // =====================================================

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
        // -------------------------------------------------
        // Find Runner
        // -------------------------------------------------

        if (runner == null)
        {
            FindRunner();
        }

        // -------------------------------------------------
        // Find Local Player
        // -------------------------------------------------

        if (localPlayerState == null)
        {
            FindLocalPlayer();
        }

        // -------------------------------------------------
        // Update Player Information
        // -------------------------------------------------

        if (localPlayerState != null)
        {
            UpdateFromNetworkState();
        }
        else
        {
            UpdateReadyFromProfileFallback();
        }
    }


    // =====================================================
    // FIND NETWORK RUNNER
    // =====================================================

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


    // =====================================================
    // FIND LOCAL PLAYER
    // =====================================================

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


    // =====================================================
    // PLAYER PROFILE
    // =====================================================

    private void UpdateFromPlayerProfile()
    {
        // -------------------------------------------------
        // PLAYER NAME
        // -------------------------------------------------

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


        // -------------------------------------------------
        // CHARACTER AVATAR
        // -------------------------------------------------

        string characterId =
            PlayerProfile.SelectedCharacter;

        SetCharacterAvatar(characterId);
    }


    // =====================================================
    // NETWORK PLAYER STATE
    // =====================================================

    private void UpdateFromNetworkState()
    {
        if (localPlayerState == null)
            return;


        // -------------------------------------------------
        // PLAYER NAME
        // -------------------------------------------------

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


        // -------------------------------------------------
        // CHARACTER
        // -------------------------------------------------

        string characterId =
            localPlayerState.SelectedCharacter.ToString();

        if (string.IsNullOrWhiteSpace(characterId))
        {
            characterId =
                PlayerProfile.SelectedCharacter;
        }

        SetCharacterAvatar(characterId);


        // -------------------------------------------------
        // READY / IN-GAME
        // -------------------------------------------------

        UpdateReadyUI();
    }


    // =====================================================
    // FALLBACK READY UI
    // =====================================================

    private void UpdateReadyFromProfileFallback()
    {
        // If the NetworkGameManager exists and the game
        // has already started, show In-Game instead.
        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager != null &&
            manager.IsNetworkStateReady &&
            manager.GameStarted)
        {
            SetInGameUI();
            return;
        }


        // Normal lobby fallback.
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(true);
            readyButton.interactable = true;
        }

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


    // =====================================================
    // READY UI
    // =====================================================

    private void UpdateReadyUI()
    {
        if (localPlayerState == null)
            return;


        // -------------------------------------------------
        // IMPORTANT:
        // Check GameStarted only after the manager has
        // completed Fusion Spawned().
        // -------------------------------------------------

        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager != null &&
            manager.IsNetworkStateReady &&
            manager.GameStarted)
        {
            SetInGameUI();
            return;
        }


        // -------------------------------------------------
        // GAME HAS NOT STARTED
        // -------------------------------------------------

        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(true);
            readyButton.interactable = true;
        }


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


    // =====================================================
    // IN-GAME UI
    // =====================================================

    private void SetInGameUI()
    {
        // -------------------------------------------------
        // Hide Ready button
        // -------------------------------------------------

        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(false);
        }


        // -------------------------------------------------
        // Change status
        // -------------------------------------------------

        if (readyStatusText != null)
        {
            readyStatusText.text =
                "IN-GAME";
        }
    }


    // =====================================================
    // CHARACTER AVATAR
    // =====================================================

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


    // =====================================================
    // READY BUTTON
    // =====================================================

    public void ToggleReady()
    {
        FindRunner();
        FindLocalPlayer();


        // -------------------------------------------------
        // Do not allow Ready after game starts.
        // -------------------------------------------------

        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager != null &&
            manager.IsNetworkStateReady &&
            manager.GameStarted)
        {
            Debug.Log(
                "[MY PLAYER PANEL] " +
                "Cannot toggle Ready. Game is already in-game."
            );

            return;
        }


        // -------------------------------------------------
        // Find player state
        // -------------------------------------------------

        if (localPlayerState == null)
        {
            Debug.LogWarning(
                "[MY PLAYER PANEL] " +
                "Cannot toggle Ready. " +
                "Local LobbyPlayerState not found."
            );

            return;
        }


        // -------------------------------------------------
        // Toggle Ready
        // -------------------------------------------------

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