using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class LobbyBrowserUI : MonoBehaviour
{
    [Header("Lobby List")]
    [SerializeField] private Transform content;

    [SerializeField] private LobbyItem lobbyItemPrefab;


    // =========================================================
    // SHOW LOBBIES
    // =========================================================

    public void DisplayLobbies(
        IReadOnlyList<SessionInfo> sessions)
    {
        ClearLobbyItems();

        if (content == null)
        {
            Debug.LogError(
                "[LOBBY UI] Content reference is missing!"
            );

            return;
        }

        if (lobbyItemPrefab == null)
        {
            Debug.LogError(
                "[LOBBY UI] Lobby Item Prefab reference is missing!"
            );

            return;
        }

        if (sessions == null || sessions.Count == 0)
        {
            Debug.Log(
                "[LOBBY UI] No available lobbies."
            );

            return;
        }

        int displayedCount = 0;

        foreach (SessionInfo session in sessions)
        {
            if (!session.IsValid)
            {
                continue;
            }

            if (!session.IsVisible)
            {
                continue;
            }

            LobbyItem lobbyItem = Instantiate(
                lobbyItemPrefab,
                content
            );

            if (lobbyItem == null)
            {
                Debug.LogError(
                    "[LOBBY UI] Failed to instantiate LobbyItem."
                );

                continue;
            }

            lobbyItem.Setup(session);

            displayedCount++;

            Debug.Log(
                $"[LOBBY UI] Created item: {session.Name}"
            );
        }

        Debug.Log(
            $"[LOBBY UI] Displayed {displayedCount} lobby(s)."
        );
    }


    // =========================================================
    // CLEAR LIST
    // =========================================================

    private void ClearLobbyItems()
    {
        if (content == null)
        {
            return;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Destroy(
                content.GetChild(i).gameObject
            );
        }
    }


    // =========================================================
    // REFRESH
    // =========================================================

    public void Refresh()
    {
        if (NetworkRunnerHandler.Instance == null)
        {
            Debug.LogError(
                "[LOBBY UI] NetworkRunnerHandler not found."
            );

            return;
        }

        NetworkRunnerHandler.Instance.RefreshLobbies();
    }
}