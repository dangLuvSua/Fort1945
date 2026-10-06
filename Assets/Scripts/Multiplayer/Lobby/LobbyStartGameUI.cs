using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class LobbyStartGameUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private TMP_Text readyCountText;
    [SerializeField] private TMP_Text hostStatusText;
    [SerializeField] private TMP_Dropdown difficultyDropdown;

    private NetworkGameManager manager;

    private void Update()
    {
        FindManager();

        if (manager == null)
            return;

        // Hide lobby ready count once the game starts
        if (readyCountText != null)
        {
            readyCountText.gameObject.SetActive(!manager.GameStarted);
        }

        bool isHost = manager.IsHost;
        bool allReady = manager.AreAllPlayersReady();

        UpdateReadyCount();
        UpdateDifficulty(isHost);
        UpdateStartButton(isHost, allReady);
        UpdateHostStatus(isHost, allReady);
    }

    private void FindManager()
    {
        if (manager == null)
            manager = FindAnyObjectByType<NetworkGameManager>();
    }

    private void UpdateReadyCount()
    {
        if (readyCountText == null)
            return;

        if (manager.GameStarted)
        {
            readyCountText.gameObject.SetActive(false);
            hostStatusText.gameObject.SetActive(false);
            return;
        }

        readyCountText.gameObject.SetActive(true);

        readyCountText.text =
            $"{manager.GetReadyPlayerCount()} / {manager.GetPlayerCount()} READY";
    }

    private void UpdateDifficulty(bool isHost)
    {
        if (difficultyDropdown == null)
            return;

        // Only the host can see/change difficulty.
        difficultyDropdown.gameObject.SetActive(isHost);

        if (!isHost)
            return;

        difficultyDropdown.SetValueWithoutNotify(
            (int)manager.Difficulty
        );

        difficultyDropdown.interactable =
            !manager.GameStarted;
    }

    private void UpdateStartButton(bool isHost, bool allReady)
    {
        if (startGameButton == null)
            return;

        bool canStart =
            isHost &&
            allReady &&
            !manager.GameStarted;

        startGameButton.gameObject.SetActive(canStart);
        startGameButton.interactable = canStart;
    }

    private void UpdateHostStatus(bool isHost, bool allReady)
    {
        if (hostStatusText == null)
            return;

        if (!isHost)
            hostStatusText.text = "WAITING FOR HOST...";
        else if (manager.GameStarted)
            hostStatusText.text = "GAME STARTED";
        else if (!allReady)
            hostStatusText.text =
                "WAITING FOR ALL PLAYERS TO BE READY";
        else
            hostStatusText.text = "ALL PLAYERS ARE READY";
    }

    public void OnDifficultyChanged(int value)
    {
        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning(
                "[DIFFICULTY UI] NetworkGameManager not found."
            );

            return;
        }

        if (!manager.IsNetworkStateReady)
        {
            Debug.LogWarning(
                "[DIFFICULTY UI] NetworkGameManager is not spawned yet."
            );

            return;
        }

        if (!manager.IsHost)
        {
            Debug.LogWarning(
                "[DIFFICULTY UI] Only the host can change difficulty."
            );

            return;
        }

        GameDifficulty selectedDifficulty =
            (GameDifficulty)value;

        Debug.Log(
            $"[DIFFICULTY UI] Host selected: {selectedDifficulty}"
        );

        manager.SetDifficulty(selectedDifficulty);
    }

    public void StartGame()
    {
        if (manager == null)
        {
            Debug.LogWarning(
                "[LOBBY START] NetworkGameManager not found."
            );
            return;
        }

        if (!manager.IsHost)
        {
            Debug.LogWarning(
                "[LOBBY START] Only the host can start the game."
            );
            return;
        }

        if (!manager.AreAllPlayersReady())
        {
            Debug.LogWarning(
                "[LOBBY START] Not all players are ready."
            );
            return;
        }

        manager.RequestStartGame();
    }
}