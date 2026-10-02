using Fusion;
using TMPro;
using UnityEngine;

public class LobbyInfoUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text lobbyCodeText;


    private NetworkRunner runner;


    private void Update()
    {
        FindRunner();

        if (runner == null)
            return;

        UpdateLobbyInfo();
    }


    private void FindRunner()
    {
        if (runner != null)
            return;

        runner =
            FindAnyObjectByType<NetworkRunner>();
    }


    private void UpdateLobbyInfo()
    {
        if (runner == null)
            return;


        SessionInfo session =
            runner.SessionInfo;


        // ==========================================
        // LOBBY CODE
        // ==========================================

        if (lobbyCodeText != null)
        {
            lobbyCodeText.text =
                $"CODE: {session.Name}";
        }


        // ==========================================
        // LOBBY NAME
        // ==========================================

        if (lobbyNameText != null)
        {
            string lobbyName =
                session.Name;


            if (session.Properties != null &&
                session.Properties.TryGetValue(
                    "LobbyName",
                    out SessionProperty lobbyNameProperty))
            {
                lobbyName =
                    lobbyNameProperty.PropertyValue?.ToString();
            }


            lobbyNameText.text =
                lobbyName;
        }
    }
}