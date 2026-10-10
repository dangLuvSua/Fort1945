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

    [Tooltip(
        "Label shown next to the difficulty dropdown. " +
        "Hidden together with the dropdown once the game starts."
    )]
    [SerializeField] private GameObject difficultyLabel;

    [Header("Game-Started HUD Transition")]
    [Tooltip(
        "Quick bar slots row shown once the game starts."
    )]
    [SerializeField] private GameObject quickBarSlotsRoot;

    [Tooltip(
        "Header that contains the lobby name + lobby code. " +
        "Hidden once the game starts."
    )]
    [SerializeField] private RectTransform lobbyHeader;

    [Tooltip(
        "Local player panel hidden once the game starts."
    )]
    [SerializeField] private RectTransform myPlayerPanel;

    [Tooltip(
        "Player list panel controller opened automatically " +
        "when the game starts. Tab still toggles the panel."
    )]
    [SerializeField] private PlayerListPanelController playerListPanelController;

    private NetworkGameManager manager;

    private bool hudTransitionApplied;

    private void Update()
    {
        FindManager();

        if (manager == null)
            return;

        UpdateHudTransition();

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

        bool showDifficulty =
            isHost &&
            !manager.GameStarted;

        // Only the host can see/change the difficulty, and only
        // before the game has started.
        difficultyDropdown.gameObject.SetActive(
            showDifficulty
        );

        // The label next to the dropdown hides together with it.
        GameObject difficultyLabelObject =
            ResolveDifficultyLabel();

        if (difficultyLabelObject != null)
        {
            difficultyLabelObject.SetActive(
                showDifficulty
            );
        }

        if (!showDifficulty)
        {
            return;
        }

        difficultyDropdown.SetValueWithoutNotify(
            (int)manager.Difficulty
        );

        difficultyDropdown.interactable =
            true;
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


    // =====================================================
    // GAME-STARTED HUD TRANSITION
    // =====================================================

    private void UpdateHudTransition()
    {
        if (hudTransitionApplied)
            return;

        if (manager == null)
            return;

        if (!manager.GameStarted)
            return;

        hudTransitionApplied = true;

        ApplyGameStartedHudTransition();
    }

    private void ApplyGameStartedHudTransition()
    {
        // -------------------------------------------------
        // SHOW QUICK BAR SLOTS
        // -------------------------------------------------

        GameObject quickBar =
            ResolveQuickBarSlotsRoot();

        if (quickBar != null)
        {
            ActivateHierarchy(quickBar);

            Debug.Log(
                "[LOBBY START] Quick bar slots shown."
            );
        }
        else
        {
            Debug.LogWarning(
                "[LOBBY START] Quick bar slots root was not found."
            );
        }

        // -------------------------------------------------
        // HIDE LOBBY HEADER (LOBBY NAME + CODE)
        // -------------------------------------------------

        RectTransform header =
            ResolveLobbyHeader();

        if (header != null)
        {
            header.gameObject.SetActive(false);

            Debug.Log(
                "[LOBBY START] Lobby header (name + code) hidden."
            );
        }
        else
        {
            Debug.LogWarning(
                "[LOBBY START] Lobby header not found."
            );
        }

        // -------------------------------------------------
        // HIDE MY PLAYER PANEL
        // -------------------------------------------------

        RectTransform panel =
            ResolveMyPlayerPanel();

        if (panel != null)
        {
            panel.gameObject.SetActive(false);

            Debug.Log(
                "[LOBBY START] MyPlayerPanel hidden."
            );
        }
        else
        {
            Debug.LogWarning(
                "[LOBBY START] MyPlayerPanel not found."
            );
        }

        // -------------------------------------------------
        // HIDE DIFFICULTY OPTIONS
        // -------------------------------------------------

        if (difficultyDropdown != null)
        {
            difficultyDropdown.gameObject.SetActive(false);

            Debug.Log(
                "[LOBBY START] Difficulty options hidden."
            );
        }

        GameObject difficultyLabelObject =
            ResolveDifficultyLabel();

        if (difficultyLabelObject != null)
        {
            difficultyLabelObject.SetActive(false);

            Debug.Log(
                "[LOBBY START] Difficulty label hidden."
            );
        }

        // -------------------------------------------------
        // OPEN PLAYER LIST (TAB STILL TOGGLES IT)
        // -------------------------------------------------

        PlayerListPanelController listController =
            playerListPanelController;

        if (listController == null)
        {
            listController =
                FindAnyObjectByType<PlayerListPanelController>();
        }

        if (listController != null)
        {
            listController.Open();

            Debug.Log(
                "[LOBBY START] Player list opened."
            );
        }
        else
        {
            Debug.LogWarning(
                "[LOBBY START] PlayerListPanelController not found."
            );
        }
    }

    private void ActivateHierarchy(GameObject root)
    {
        if (root == null)
            return;

        Transform current =
            root.transform;

        // Make sure every inactive parent is active too,
        // otherwise SetActive(true) on the row does nothing.
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }

            current =
                current.parent;
        }

        root.SetActive(true);
    }

    private GameObject ResolveQuickBarSlotsRoot()
    {
        if (quickBarSlotsRoot != null)
            return quickBarSlotsRoot;

        quickBarSlotsRoot =
            GameObject.Find("InventorySlots");

        if (quickBarSlotsRoot != null)
            return quickBarSlotsRoot;

        // Fallback: walk into the Inventory root by name.
        GameObject inventoryRoot =
            GameObject.Find("Inventory");

        if (inventoryRoot != null)
        {
            Transform slots =
                inventoryRoot.transform.Find(
                    "InventorySlots"
                );

            if (slots != null)
            {
                quickBarSlotsRoot =
                    slots.gameObject;
            }
        }

        return quickBarSlotsRoot;
    }

    private GameObject ResolveDifficultyLabel()
    {
        if (difficultyLabel != null)
            return difficultyLabel;

        difficultyLabel =
            GameObject.Find("DiffLbl");

        return difficultyLabel;
    }

    private RectTransform ResolveLobbyHeader()
    {
        if (lobbyHeader != null)
            return lobbyHeader;

        GameObject lobbyPanel =
            GameObject.Find("LobbyPanel");

        if (lobbyPanel != null)
        {
            Transform header =
                lobbyPanel.transform.Find("Header");

            if (header != null)
            {
                lobbyHeader =
                    header as RectTransform;
            }
        }

        return lobbyHeader;
    }

    private RectTransform ResolveMyPlayerPanel()
    {
        if (myPlayerPanel != null)
            return myPlayerPanel;

        GameObject panel =
            GameObject.Find("MyPlayerPanel");

        if (panel != null)
        {
            myPlayerPanel =
                panel.GetComponent<RectTransform>();
        }

        return myPlayerPanel;
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