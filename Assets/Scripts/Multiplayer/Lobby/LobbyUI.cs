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
    // LOADING SCREEN
    // =====================================================

    [Header("Loading Screen")]
    [Tooltip(
        "Assign your existing LoadingScreenUI prefab here."
    )]
    [SerializeField] private LoadingProgressUI loadingScreenPrefab;


    // =====================================================
    // INTERNAL
    // =====================================================

    private LoadingProgressUI loadingScreenInstance;

    private bool isLoading;


    // =====================================================
    // CREATE LOBBY
    // =====================================================

    public void CreateLobby()
    {
        // -------------------------------------------------
        // Prevent multiple clicks
        // -------------------------------------------------

        if (isLoading)
        {
            Debug.LogWarning(
                "[LOBBY UI] A lobby connection is already in progress."
            );

            return;
        }


        // -------------------------------------------------
        // Validate lobby name input
        // -------------------------------------------------

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
            lobbyCodeText.text =
                lobbyCode;
        }


        // -------------------------------------------------
        // Debug information
        // -------------------------------------------------

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
        // Show loading screen
        // -------------------------------------------------

        ShowLoadingScreen(
            "CREATING LOBBY..."
        );


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
        // -------------------------------------------------
        // Prevent multiple clicks
        // -------------------------------------------------

        if (isLoading)
        {
            Debug.LogWarning(
                "[LOBBY UI] A lobby connection is already in progress."
            );

            return;
        }


        // -------------------------------------------------
        // Validate join input
        // -------------------------------------------------

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


        // -------------------------------------------------
        // Debug information
        // -------------------------------------------------

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


        // -------------------------------------------------
        // Show loading screen
        // -------------------------------------------------

        ShowLoadingScreen(
            "JOINING LOBBY..."
        );


        // -------------------------------------------------
        // Join lobby
        // -------------------------------------------------

        NetworkRunnerHandler.Instance.JoinGame(
            lobbyCode
        );
    }


    // =====================================================
    // SHOW LOADING SCREEN
    // =====================================================

    private void ShowLoadingScreen(
     string message)
    {
        if (loadingScreenInstance != null)
        {
            Debug.LogWarning(
                "[LOBBY UI] Loading screen already exists."
            );

            return;
        }

        if (loadingScreenPrefab == null)
        {
            Debug.LogError(
                "[LOBBY UI] Loading Screen prefab is not assigned."
            );

            return;
        }

        isLoading = true;

        loadingScreenInstance =
            Instantiate(
                loadingScreenPrefab
            );

        // Keep the loading screen alive while
        // Fusion changes scenes.
        DontDestroyOnLoad(
            loadingScreenInstance.gameObject
        );

        loadingScreenInstance.BeginLoading(
            message
        );

        Debug.Log(
            $"[LOBBY UI] Loading screen shown: {message}"
        );
    }

    // =====================================================
    // HIDE LOADING SCREEN
    // =====================================================

    public void HideLoadingScreen()
    {
        isLoading = false;


        if (loadingScreenInstance != null)
        {
            Destroy(
                loadingScreenInstance.gameObject
            );

            loadingScreenInstance = null;
        }


        Debug.Log(
            "[LOBBY UI] Loading screen hidden."
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


            code +=
                characters[index];
        }


        return code;
    }
}