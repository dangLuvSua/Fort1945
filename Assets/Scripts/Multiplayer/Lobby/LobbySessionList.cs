using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class LobbySessionList : MonoBehaviour
{
    public static LobbySessionList Instance { get; private set; }

    public IReadOnlyList<SessionInfo> Sessions =>
        sessions;

    [Header("UI")]
    [SerializeField] private LobbyBrowserUI lobbyBrowserUI;

    private readonly List<SessionInfo> sessions =
        new List<SessionInfo>();


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }


    // =========================================================
    // UPDATE SESSIONS
    // =========================================================

    public void UpdateSessions(
        List<SessionInfo> sessionList)
    {
        sessions.Clear();

        foreach (SessionInfo session in sessionList)
        {
            if (!session.IsValid)
            {
                continue;
            }

            if (!session.IsVisible)
            {
                continue;
            }

            if (!session.IsOpen)
            {
                continue;
            }

            sessions.Add(session);
        }

        Debug.Log(
            $"[LOBBY LIST] Available lobbies: " +
            $"{sessions.Count}"
        );


        foreach (SessionInfo session in sessions)
        {
            Debug.Log(
                $"[LOBBY LIST] " +
                $"{session.Name} " +
                $"({session.PlayerCount}/{session.MaxPlayers})"
            );
        }


        // ---------------------------------------------------------
        // Update UI
        // ---------------------------------------------------------

        if (lobbyBrowserUI != null)
        {
            lobbyBrowserUI.DisplayLobbies(
                sessions
            );
        }
        else
        {
            Debug.LogWarning(
                "[LOBBY LIST] LobbyBrowserUI is not assigned."
            );
        }
    }
}