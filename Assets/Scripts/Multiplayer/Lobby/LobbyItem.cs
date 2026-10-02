using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Fusion;

public class LobbyItem : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button joinButton;

    private string sessionName;


    // =====================================================
    // AWAKE
    // =====================================================

    private void Awake()
    {
        // Make sure the entire LobbyItem hierarchy
        // starts enabled.
        EnableEntireHierarchy(transform);
    }


    // =====================================================
    // SETUP
    // =====================================================

    public void Setup(SessionInfo session)
    {
        // Make sure the hierarchy is enabled before
        // displaying the lobby information.
        EnableEntireHierarchy(transform);

        // -------------------------------------------------
        // Store session name/code
        // -------------------------------------------------

        sessionName = session.Name;

        // -------------------------------------------------
        // Lobby Name
        // -------------------------------------------------

        if (lobbyNameText != null)
        {
            lobbyNameText.gameObject.SetActive(true);
            lobbyNameText.enabled = true;

            if (session.Properties.TryGetValue(
                "LobbyName",
                out SessionProperty lobbyNameProperty))
            {
                string lobbyName =
                    lobbyNameProperty.PropertyValue?.ToString();

                if (!string.IsNullOrWhiteSpace(lobbyName))
                {
                    lobbyNameText.text =
                        lobbyName;
                }
                else
                {
                    // Fallback
                    lobbyNameText.text =
                        session.Name;
                }
            }
            else
            {
                // Fallback for sessions created before
                // LobbyName was added.
                lobbyNameText.text =
                    session.Name;
            }
        }

        // -------------------------------------------------
        // Lobby Code
        // -------------------------------------------------

        if (lobbyCodeText != null)
        {
            lobbyCodeText.gameObject.SetActive(true);
            lobbyCodeText.enabled = true;

            lobbyCodeText.text =
                $"{session.Name}";
        }

        // -------------------------------------------------
        // Player Count
        // -------------------------------------------------

        if (playerCountText != null)
        {
            playerCountText.gameObject.SetActive(true);
            playerCountText.enabled = true;

            playerCountText.text =
                $"{session.PlayerCount} / " +
                $"{session.MaxPlayers} P";
        }

        // -------------------------------------------------
        // Join Button
        // -------------------------------------------------

        if (joinButton != null)
        {
            joinButton.gameObject.SetActive(true);
            joinButton.enabled = true;

            joinButton.onClick.RemoveAllListeners();

            joinButton.onClick.AddListener(
                JoinLobby
            );

            joinButton.interactable =
                session.IsOpen &&
                session.PlayerCount <
                session.MaxPlayers;
        }
        else
        {
            Debug.LogError(
                "[LOBBY ITEM] Join Button reference is missing!"
            );
        }

        Debug.Log(
            $"[LOBBY ITEM] Setup complete: " +
            $"{session.Name}"
        );
    }


    // =====================================================
    // ENABLE ENTIRE HIERARCHY
    // =====================================================

    private void EnableEntireHierarchy(
        Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        // -------------------------------------------------
        // Enable GameObject
        // -------------------------------------------------

        parent.gameObject.SetActive(true);

        // -------------------------------------------------
        // Enable components on this object
        // -------------------------------------------------

        MonoBehaviour[] components =
            parent.GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour component in components)
        {
            if (component != null)
            {
                component.enabled = true;
            }
        }

        // -------------------------------------------------
        // Enable children recursively
        // -------------------------------------------------

        for (int i = 0;
             i < parent.childCount;
             i++)
        {
            Transform child =
                parent.GetChild(i);

            EnableEntireHierarchy(child);
        }
    }


    // =====================================================
    // JOIN LOBBY
    // =====================================================

    private void JoinLobby()
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            Debug.LogWarning(
                "[LOBBY ITEM] Session name is empty."
            );

            return;
        }

        Debug.Log(
            $"[LOBBY ITEM] Joining lobby: " +
            $"{sessionName}"
        );

        if (NetworkRunnerHandler.Instance == null)
        {
            Debug.LogError(
                "[LOBBY ITEM] " +
                "NetworkRunnerHandler not found."
            );

            return;
        }

        NetworkRunnerHandler.Instance.JoinGame(
            sessionName
        );
    }
}