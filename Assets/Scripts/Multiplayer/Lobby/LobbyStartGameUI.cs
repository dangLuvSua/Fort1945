using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Fusion;

public class LobbyStartGameUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private TMP_Text readyCountText;
    [SerializeField] private TMP_Text hostStatusText;


    private NetworkGameManager manager;


    private void Update()
    {
        FindManager();

        if (manager == null)
            return;


        int playerCount =
            manager.GetPlayerCount();

        int readyCount =
            manager.GetReadyPlayerCount();

        bool isHost =
            manager.IsHost;

        bool allReady =
            manager.AreAllPlayersReady();


        if (readyCountText != null)
        {
            readyCountText.text =
                $"{readyCount} / {playerCount} READY";
        }


        if (startGameButton != null)
        {
            bool showStartButton =
                isHost &&
                allReady &&
                !manager.GameStarted;

            // Hide the button entirely unless the host can
            // actually start the game (all players ready).
            startGameButton.gameObject.SetActive(
                showStartButton
            );

            startGameButton.interactable =
                showStartButton;
        }


        if (hostStatusText != null)
        {
            if (!isHost)
            {
                hostStatusText.text =
                    "WAITING FOR HOST...";
            }
            else if (manager.GameStarted)
            {
                hostStatusText.text =
                    "GAME STARTED";
            }
            else if (!allReady)
            {
                hostStatusText.text =
                    "WAITING FOR ALL PLAYERS TO BE READY";
            }
            else
            {
                hostStatusText.text =
                    "ALL PLAYERS ARE READY";
            }
        }
    }


    private void FindManager()
    {
        if (manager != null)
            return;


        manager =
            FindAnyObjectByType<NetworkGameManager>();
    }


    public void StartGame()
    {
        if (manager == null)
        {
            Debug.LogWarning(
                "[LOBBY START] " +
                "NetworkGameManager not found."
            );

            return;
        }


        if (!manager.IsHost)
        {
            Debug.LogWarning(
                "[LOBBY START] " +
                "Only the host can start the game."
            );

            return;
        }


        if (!manager.AreAllPlayersReady())
        {
            Debug.LogWarning(
                "[LOBBY START] " +
                "Not all players are ready."
            );

            return;
        }


        manager.RequestStartGame();
    }
}