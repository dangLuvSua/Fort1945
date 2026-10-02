using TMPro;
using UnityEngine;

public class LobbyUI : MonoBehaviour
{
    // =====================================================
    // CREATE LOBBY
    // =====================================================

    [Header("Create Lobby")]
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private TMP_Text lobbyCodeText;


    // =====================================================
    // JOIN LOBBY
    // =====================================================

    [Header("Join Lobby")]
    [SerializeField] private TMP_InputField joinLobbyCodeInput;


    // =====================================================
    // CREATE LOBBY
    // =====================================================

    public void CreateLobby()
    {
        if (lobbyNameInput == null)
        {
            Debug.LogError(
                "Lobby name input field is not assigned."
            );

            return;
        }

        string lobbyName =
            lobbyNameInput.text.Trim();

        if (string.IsNullOrEmpty(lobbyName))
        {
            Debug.LogWarning(
                "Lobby name cannot be empty."
            );

            return;
        }

        // -------------------------------------------------
        // Generate lobby code
        // -------------------------------------------------

        string lobbyCode =
            GenerateLobbyCode();

        // -------------------------------------------------
        // Display generated code
        // -------------------------------------------------

        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = lobbyCode;
        }

        Debug.Log(
            $"Creating lobby: {lobbyName}"
        );

        Debug.Log(
            $"Lobby code: {lobbyCode}"
        );

        Debug.Log(
            $"Player name: {PlayerProfile.PlayerName}"
        );

        // -------------------------------------------------
        // Network Runner Handler
        // -------------------------------------------------

        if (NetworkRunnerHandler.Instance == null)
        {
            Debug.LogError(
                "NetworkRunnerHandler instance was not found."
            );

            return;
        }

        // -------------------------------------------------
        // Send BOTH lobby code and lobby name
        // -------------------------------------------------

        NetworkRunnerHandler.Instance.CreateGame(
            lobbyCode,
            lobbyName
        );
    }


    // =====================================================
    // JOIN LOBBY
    // =====================================================

    public void JoinLobby()
    {
        if (joinLobbyCodeInput == null)
        {
            Debug.LogError(
                "Join lobby code input field is not assigned."
            );

            return;
        }

        string lobbyCode =
            joinLobbyCodeInput.text.Trim();

        if (string.IsNullOrEmpty(lobbyCode))
        {
            Debug.LogWarning(
                "Lobby code cannot be empty."
            );

            return;
        }

        // -------------------------------------------------
        // Convert code to uppercase
        // -------------------------------------------------

        lobbyCode =
            lobbyCode.ToUpperInvariant();

        // -------------------------------------------------
        // Update input field
        // -------------------------------------------------

        joinLobbyCodeInput.text =
            lobbyCode;

        Debug.Log(
            $"Joining lobby: {lobbyCode}"
        );

        Debug.Log(
            $"Player name: {PlayerProfile.PlayerName}"
        );

        // -------------------------------------------------
        // Network Runner Handler
        // -------------------------------------------------

        if (NetworkRunnerHandler.Instance == null)
        {
            Debug.LogError(
                "NetworkRunnerHandler instance was not found."
            );

            return;
        }

        NetworkRunnerHandler.Instance.JoinGame(
            lobbyCode
        );
    }


    // =====================================================
    // GENERATE LOBBY CODE
    // =====================================================

    private string GenerateLobbyCode()
    {
        const string characters =
            "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        string code = "";

        for (int i = 0; i < 6; i++)
        {
            int index =
                Random.Range(
                    0,
                    characters.Length
                );

            code += characters[index];
        }

        return code;
    }
}