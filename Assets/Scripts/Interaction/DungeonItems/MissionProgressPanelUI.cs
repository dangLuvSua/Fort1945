using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the MissionProgressPanel.
///
/// Normal mission items use the ItemRow prefab.
///
/// Extra mission progress such as:
/// - VALID STATUE PHOTOS: 0/4
/// - PUZZLE: 0/8
///
/// also uses the same ItemRow prefab layout.
///
/// Puzzle requirements:
/// Easy      = PP1-PP8      = 1001-1008
/// Normal    = MPP1-MPP16   = 2001-2016
/// Difficult = HPP1-HPP24   = 3001-3024
/// </summary>
public class MissionProgressPanelUI : MonoBehaviour
{
    // =========================================================
    // ITEM ROWS
    // =========================================================

    [Header("Item Rows")]

    [Tooltip(
        "ItemRow prefab used for normal objectives and extra " +
        "progress rows such as statue photos and puzzle pieces."
    )]
    [SerializeField]
    private GameObject itemRowPrefab;

    // =========================================================
    // RUNTIME REFERENCES
    // =========================================================

    private GameObject panelRoot;

    private TMP_Text titleText;

    private RectTransform content;

    // =========================================================
    // NORMAL ITEM ROWS
    // =========================================================

    private readonly List<ItemRowUI> rows =
        new List<ItemRowUI>();

    // Team-wide objective tracking.
    private readonly List<TeamObjective> teamObjectives =
        new List<TeamObjective>();

    // =========================================================
    // EXTRA PROGRESS ROWS
    // =========================================================

    // Examples:
    //
    // ValidStatuePhotos
    // PuzzlePieces
    //
    // These now use the exact same ItemRow prefab layout.
    private readonly Dictionary<string, ProgressTextRowUI>
        progressTextRows =
            new Dictionary<string, ProgressTextRowUI>();

    private bool layoutAdded;
    private ItemRandomSpawner networkObjectiveSource;

    // =========================================================
    // COLORS
    // =========================================================

    private static readonly Color CompletedColor =
        new Color(
            0.3f,
            1f,
            0.35f,
            1f
        );

    // =========================================================
    // FACTORY
    // =========================================================

    public static MissionProgressPanelUI Create()
    {
        GameObject panel =
            FindMissionProgressPanel();

        if (panel == null)
        {
            Debug.LogWarning(
                "[MISSION PROGRESS] MissionProgressPanel " +
                "was not found in the scene."
            );

            return null;
        }

        MissionProgressPanelUI ui =
            panel.GetComponent<MissionProgressPanelUI>();

        if (ui == null)
        {
            ui =
                panel.AddComponent<MissionProgressPanelUI>();
        }

        ui.EnsureInitialized();

        return ui;
    }

    // =========================================================
    // FIND PANEL
    // =========================================================

    private static GameObject FindMissionProgressPanel()
    {
        GameObject active =
            GameObject.Find(
                "MissionProgressPanel"
            );

        if (active != null)
        {
            return active;
        }

        Transform[] allTransforms =
            Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0;
             i < allTransforms.Length;
             i++)
        {
            Transform candidate =
                allTransforms[i];

            if (candidate == null)
            {
                continue;
            }

            if (candidate.name ==
                "MissionProgressPanel")
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    // =========================================================
    // INITIALIZATION
    // =========================================================

    public void EnsureInitialized()
    {
        // -----------------------------------------------------
        // PANEL ROOT
        // -----------------------------------------------------

        if (panelRoot == null)
        {
            panelRoot =
                gameObject;
        }

        // -----------------------------------------------------
        // TITLE
        // -----------------------------------------------------

        if (titleText == null)
        {
            Transform titleTransform =
                transform.Find(
                    "MissionTitle"
                );

            titleText =
                titleTransform != null
                    ? titleTransform.GetComponent<TMP_Text>()
                    : null;

            if (titleText == null)
            {
                titleText =
                    GetComponentInChildren<TMP_Text>(
                        true
                    );
            }
        }

        // -----------------------------------------------------
        // CONTENT
        // -----------------------------------------------------

        if (content == null)
        {
            Transform contentTransform =
                transform.Find(
                    "Content"
                );

            content =
                contentTransform != null
                    ? contentTransform as RectTransform
                    : null;

            if (content == null)
            {
                foreach (Transform child
                         in transform)
                {
                    if (child.name.Equals(
                            "Content",
                            System.StringComparison
                                .OrdinalIgnoreCase))
                    {
                        content =
                            child as RectTransform;

                        break;
                    }
                }
            }
        }
    }

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Update()
    {
        if (networkObjectiveSource == null)
            networkObjectiveSource = Object.FindFirstObjectByType<ItemRandomSpawner>();

        NetworkGameManager manager =
            NetworkGameManager.Instance;

        // -----------------------------------------------------
        // NORMAL MISSION ITEMS
        // -----------------------------------------------------

        for (int i = 0;
             i < teamObjectives.Count;
             i++)
        {
            TeamObjective objective =
                teamObjectives[i];

            if (objective == null)
            {
                continue;
            }

            int collected =
                Mathf.Clamp(
                    GetTeamItemCount(
                        objective.ItemId,
                        manager
                    ) - objective.Baseline,
                    0,
                    objective.Required
                );

            if (networkObjectiveSource != null &&
                networkObjectiveSource.TryGetNetworkObjectiveProgress(
                    objective.ItemId,
                    out int networkCollected,
                    out int networkRequired))
            {
                collected = networkCollected;
                SetProgress(objective.ItemId, collected, networkRequired);
                continue;
            }

            SetProgress(
                objective.ItemId,
                collected,
                objective.Required
            );
        }

        // -----------------------------------------------------
        // PUZZLE COLLECTION
        // -----------------------------------------------------

        UpdatePuzzlePieceProgress(
            manager
        );
    }

    // =========================================================
    // TEAM ITEM COUNT
    // =========================================================

    private static int GetTeamItemCount(
        int itemId,
        NetworkGameManager manager)
    {
        // -----------------------------------------------------
        // NETWORK GAME MANAGER
        // -----------------------------------------------------

        if (manager != null &&
            manager.IsNetworkStateReady)
        {
            return manager.GetTeamItemCount(
                itemId
            );
        }

        // -----------------------------------------------------
        // FALLBACK INVENTORIES
        // -----------------------------------------------------

        PlayerInventory[] inventories =
            Object.FindObjectsByType<PlayerInventory>(
                FindObjectsSortMode.None
            );

        NetworkRunner runner =
            null;

        foreach (PlayerInventory inventory
                 in inventories)
        {
            if (inventory == null ||
                inventory.Runner == null ||
                !inventory.Runner.IsRunning)
            {
                continue;
            }

            runner =
                inventory.Runner;

            break;
        }

        // -----------------------------------------------------
        // MULTIPLAYER FALLBACK
        // -----------------------------------------------------

        if (runner != null)
        {
            int total =
                0;

            foreach (PlayerRef player
                     in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(
                        player,
                        out NetworkObject playerObject))
                {
                    continue;
                }

                if (playerObject == null)
                {
                    continue;
                }

                PlayerInventory inventory =
                    playerObject
                        .GetComponent<PlayerInventory>();

                if (inventory == null)
                {
                    continue;
                }

                total +=
                    inventory.GetQuantityForItemId(
                        itemId
                    );
            }

            return total;
        }

        // -----------------------------------------------------
        // LOCAL / STANDALONE FALLBACK
        // -----------------------------------------------------

        foreach (PlayerInventory inventory
                 in inventories)
        {
            if (inventory == null)
            {
                continue;
            }

            if (inventory.Object == null ||
                inventory.Object.HasInputAuthority)
            {
                return
                    inventory.GetQuantityForItemId(
                        itemId
                    );
            }
        }

        return 0;
    }

    // =========================================================
    // PUZZLE COLLECTION PROGRESS
    // =========================================================

    private void UpdatePuzzlePieceProgress(
        NetworkGameManager manager)
    {
        if (progressTextRows.Count == 0)
        {
            return;
        }

        GameDifficulty difficulty =
            ResolveGameDifficulty(
                manager
            );

        GetPuzzlePieceRange(
            difficulty,
            out int firstItemId,
            out int requiredPieces
        );

        int collectedPieces =
            CountCollectedPuzzlePieces(
                firstItemId,
                requiredPieces,
                manager
            );

        collectedPieces =
            Mathf.Clamp(
                collectedPieces,
                0,
                requiredPieces
            );

        // -----------------------------------------------------
        // UPDATE PUZZLE ROW
        // -----------------------------------------------------

        foreach (
            KeyValuePair<string, ProgressTextRowUI> pair
            in progressTextRows)
        {
            ProgressTextRowUI row =
                pair.Value;

            if (row == null)
            {
                continue;
            }

            if (!IsPuzzleProgressRow(
                    pair.Key,
                    row.Label))
            {
                continue;
            }

            row.SetProgress(
                "Puzzle",
                collectedPieces,
                requiredPieces
            );
        }
    }

    // =========================================================
    // COUNT COLLECTED PUZZLE PIECES
    // =========================================================

    private static int CountCollectedPuzzlePieces(
        int firstItemId,
        int requiredPieces,
        NetworkGameManager manager)
    {
        int collected =
            0;

        for (int i = 0;
             i < requiredPieces;
             i++)
        {
            int itemId =
                firstItemId + i;

            // Puzzle pieces are unique.
            //
            // If at least one player owns this ID,
            // consider that piece collected.
            if (GetTeamItemCount(
                    itemId,
                    manager) > 0)
            {
                collected++;
            }
        }

        return collected;
    }

    // =========================================================
    // RESOLVE GAME DIFFICULTY
    // =========================================================

    private static GameDifficulty ResolveGameDifficulty(
        NetworkGameManager manager)
    {
        if (manager != null &&
            manager.IsNetworkStateReady)
        {
            return manager.Difficulty;
        }

        // -----------------------------------------------------
        // DIRECT DUNGEON TESTING FALLBACK
        // -----------------------------------------------------

        PuzzleTableController[] tables =
            Object.FindObjectsByType<PuzzleTableController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (PuzzleTableController table
                 in tables)
        {
            if (table == null)
            {
                continue;
            }

            switch (table.ActiveDifficulty)
            {
                case PuzzleTableController
                    .PuzzleDifficulty.Medium:

                    return GameDifficulty.Normal;

                case PuzzleTableController
                    .PuzzleDifficulty.Hard:

                    return GameDifficulty.Difficult;

                case PuzzleTableController
                    .PuzzleDifficulty.Easy:

                    return GameDifficulty.Easy;
            }
        }

        return GameDifficulty.Easy;
    }

    // =========================================================
    // PUZZLE ID RANGE
    // =========================================================

    private static void GetPuzzlePieceRange(
        GameDifficulty difficulty,
        out int firstItemId,
        out int requiredPieces)
    {
        switch (difficulty)
        {
            // -------------------------------------------------
            // NORMAL = MEDIUM PUZZLE
            //
            // MPP1 - MPP16
            // IDs 2001 - 2016
            // -------------------------------------------------

            case GameDifficulty.Normal:

                firstItemId =
                    2001;

                requiredPieces =
                    16;

                break;

            // -------------------------------------------------
            // DIFFICULT = HARD PUZZLE
            //
            // HPP1 - HPP24
            // IDs 3001 - 3024
            // -------------------------------------------------

            case GameDifficulty.Difficult:

                firstItemId =
                    3001;

                requiredPieces =
                    24;

                break;

            // -------------------------------------------------
            // EASY
            //
            // PP1 - PP8
            // IDs 1001 - 1008
            // -------------------------------------------------

            case GameDifficulty.Easy:
            default:

                firstItemId =
                    1001;

                requiredPieces =
                    8;

                break;
        }
    }

    // =========================================================
    // DETECT PUZZLE ROW
    // =========================================================

    private static bool IsPuzzleProgressRow(
        string key,
        string label)
    {
        if (!string.IsNullOrEmpty(
                key) &&
            key.IndexOf(
                "puzzle",
                System.StringComparison
                    .OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(
                label) &&
            label.IndexOf(
                "PUZZLE PIECES",
                System.StringComparison
                    .OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    // =========================================================
    // SHOW
    // =========================================================

    public void Show(
        string title)
    {
        EnsureInitialized();

        if (panelRoot != null)
        {
            panelRoot.SetActive(
                true
            );
        }
        else
        {
            gameObject.SetActive(
                true
            );
        }

        CanvasGroup cg =
            GetComponent<CanvasGroup>();

        if (cg != null)
        {
            cg.alpha =
                1f;

            cg.interactable =
                true;

            cg.blocksRaycasts =
                true;
        }

        if (titleText != null)
        {
            titleText.text =
                title;
        }
    }

    // =========================================================
    // HIDE
    // =========================================================

    public void Hide()
    {
        EnsureInitialized();

        if (panelRoot != null)
        {
            panelRoot.SetActive(
                false
            );
        }
        else
        {
            gameObject.SetActive(
                false
            );
        }
    }

    // =========================================================
    // SET MISSION
    // =========================================================

    public void SetMission(
        string title)
    {
        EnsureInitialized();

        ClearRows();

        Show(
            title
        );

        EnsureContentLayout();
    }

    public void SetMission(
        MissionData mission)
    {
        if (mission == null)
        {
            return;
        }

        SetMission(
            mission.title
        );
    }

    // =========================================================
    // EXTRA PROGRESS ROW
    // =========================================================

    public void SetProgressText(
        string key,
        string text)
    {
        EnsureInitialized();

        if (content == null ||
            string.IsNullOrEmpty(
                key))
        {
            return;
        }

        // -----------------------------------------------------
        // CREATE EXTRA ROW
        // -----------------------------------------------------

        if (!progressTextRows.TryGetValue(
                key,
                out ProgressTextRowUI row) ||
            row == null)
        {
            row =
                ProgressTextRowUI.Create(
                    content,
                    itemRowPrefab,
                    titleText,
                    key
                );

            progressTextRows[key] =
                row;
        }

        if (row == null)
        {
            return;
        }

        // -----------------------------------------------------
        // PUZZLE ROW
        // -----------------------------------------------------

        if (IsPuzzleProgressRow(
                key,
                text))
        {
            NetworkGameManager manager =
                NetworkGameManager.Instance;

            GameDifficulty difficulty =
                ResolveGameDifficulty(
                    manager
                );

            GetPuzzlePieceRange(
                difficulty,
                out int firstItemId,
                out int requiredPieces
            );

            int collected =
                CountCollectedPuzzlePieces(
                    firstItemId,
                    requiredPieces,
                    manager
                );

            collected =
                Mathf.Clamp(
                    collected,
                    0,
                    requiredPieces
                );

            row.SetProgress(
                "Puzzle",
                collected,
                requiredPieces
            );

            return;
        }

        // -----------------------------------------------------
        // NORMAL EXTRA PROGRESS
        //
        // Example:
        //
        // VALID STATUE PHOTOS: 0/4
        // -----------------------------------------------------

        ParseProgressText(
            text,
            out string label,
            out string value
        );

        row.SetText(
            label,
            value
        );
    }

    // =========================================================
    // PARSE EXTRA PROGRESS TEXT
    // =========================================================

    private static void ParseProgressText(
        string text,
        out string label,
        out string value)
    {
        label =
            string.Empty;

        value =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                text))
        {
            return;
        }

        int separator =
            text.LastIndexOf(
                ':'
            );

        if (separator < 0)
        {
            label =
                text.Trim();

            return;
        }

        label =
            text.Substring(
                    0,
                    separator)
                .Trim();

        value =
            text.Substring(
                    separator + 1)
                .Trim();
    }

    // =========================================================
    // TOGGLE
    // =========================================================

    public static void ToggleVisibility()
    {
        Transform[] allTransforms =
            Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        bool currentlyVisible =
            false;

        bool found =
            false;

        foreach (Transform candidate
                 in allTransforms)
        {
            if (candidate == null ||
                candidate.name !=
                "MissionProgressPanel")
            {
                continue;
            }

            found =
                true;

            currentlyVisible |=
                candidate.gameObject
                    .activeInHierarchy;
        }

        if (!found)
        {
            Debug.LogWarning(
                "[MISSION PROGRESS] Cannot toggle: " +
                "panel was not found."
            );

            return;
        }

        bool show =
            !currentlyVisible;

        foreach (Transform candidate
                 in allTransforms)
        {
            if (candidate != null &&
                candidate.name ==
                "MissionProgressPanel")
            {
                candidate.gameObject.SetActive(
                    show
                );
            }
        }
    }

    // =========================================================
    // ADD ITEM ROW
    // =========================================================

    public void AddItemRow(
        ItemData itemData,
        int required)
    {
        AddItemRow(
            itemData,
            required,
            0
        );
    }

    public void AddItemRow(
        ItemData itemData,
        int required,
        int baseline)
    {
        EnsureInitialized();

        if (itemData == null ||
            required <= 0)
        {
            return;
        }

        if (content == null)
        {
            Debug.LogWarning(
                "[MISSION PROGRESS] Content container is null, " +
                "cannot add item row.",
                this
            );

            return;
        }

        ItemRowUI row =
            itemRowPrefab != null
                ? ItemRowUI.CreateFromPrefab(
                    content,
                    itemRowPrefab,
                    itemData,
                    required
                )
                : ItemRowUI.CreateCodeBuilt(
                    content,
                    itemData,
                    required,
                    titleText
                );

        rows.Add(
            row
        );

        teamObjectives.Add(
            new TeamObjective(
                itemData.itemId,
                required,
                baseline
            )
        );
    }

    // =========================================================
    // SET NORMAL ITEM PROGRESS
    // =========================================================

    public void SetProgress(
        int itemId,
        int collected,
        int required)
    {
        EnsureInitialized();

        foreach (ItemRowUI row
                 in rows)
        {
            if (row != null &&
                row.ItemId == itemId)
            {
                row.SetProgress(
                    collected,
                    required
                );
            }
        }
    }

    // =========================================================
    // CLEAR
    // =========================================================

    public void ClearRows()
    {
        // -----------------------------------------------------
        // NORMAL ITEMS
        // -----------------------------------------------------

        foreach (ItemRowUI row
                 in rows)
        {
            if (row != null)
            {
                row.Destroy();
            }
        }

        rows.Clear();

        teamObjectives.Clear();

        // -----------------------------------------------------
        // EXTRA ROWS
        // -----------------------------------------------------

        foreach (ProgressTextRowUI row
                 in progressTextRows.Values)
        {
            if (row != null)
            {
                row.Destroy();
            }
        }

        progressTextRows.Clear();
    }

    // =========================================================
    // CONTENT LAYOUT
    // =========================================================

    private void EnsureContentLayout()
    {
        if (layoutAdded ||
            content == null)
        {
            return;
        }

        layoutAdded =
            true;

        // -----------------------------------------------------
        // VERTICAL LAYOUT
        // -----------------------------------------------------

        if (content
                .GetComponent<VerticalLayoutGroup>() ==
            null)
        {
            VerticalLayoutGroup vlg =
                content.gameObject
                    .AddComponent<VerticalLayoutGroup>();

            vlg.spacing =
                8f;

            vlg.childControlWidth =
                true;

            vlg.childControlHeight =
                true;

            vlg.childForceExpandWidth =
                true;

            vlg.childForceExpandHeight =
                false;

            vlg.childAlignment =
                TextAnchor.UpperCenter;
        }

        // -----------------------------------------------------
        // CONTENT SIZE
        // -----------------------------------------------------

        ContentSizeFitter fitter =
            content
                .GetComponent<ContentSizeFitter>();

        if (fitter == null)
        {
            fitter =
                content.gameObject
                    .AddComponent<ContentSizeFitter>();
        }

        fitter.verticalFit =
            ContentSizeFitter
                .FitMode
                .PreferredSize;
    }

    // =========================================================
    // NORMAL ITEM ROW
    // =========================================================

    private class ItemRowUI
    {
        public int ItemId
        {
            get;
        }

        private readonly GameObject
            rowGameObject;

        private readonly string
            itemName;

        private readonly TMP_Text
            nameText;

        private readonly TMP_Text
            countText;

        // =====================================================
        // CREATE FROM PREFAB
        // =====================================================

        public static ItemRowUI CreateFromPrefab(
            Transform parent,
            GameObject prefab,
            ItemData itemData,
            int required)
        {
            GameObject rowGo =
                Object.Instantiate(
                    prefab,
                    parent,
                    false
                );

            RectTransform rowRt =
                rowGo
                    .GetComponent<RectTransform>();

            if (rowRt != null)
            {
                rowRt.anchoredPosition =
                    Vector2.zero;

                rowRt.sizeDelta =
                    new Vector2(
                        0f,
                        50f
                    );
            }

            TMP_Text nameText =
                FindChildText(
                    rowGo.transform,
                    "ItemName"
                );

            TMP_Text countText =
                FindChildText(
                    rowGo.transform,
                    "ItemCount"
                );

            if (nameText != null)
            {
                nameText.text =
                    itemData.itemName;
            }

            if (countText != null)
            {
                countText.text =
                    $"0/{required}";
            }

            return new ItemRowUI(
                rowGo,
                itemData.itemId,
                itemData.itemName,
                nameText,
                countText
            );
        }

        // =====================================================
        // CREATE CODE-BUILT
        // =====================================================

        public static ItemRowUI CreateCodeBuilt(
            Transform parent,
            ItemData itemData,
            int required,
            TMP_Text fontSource)
        {
            GameObject rowGo =
                CreateBaseRow(
                    parent
                );

            // -------------------------------------------------
            // ICON
            // -------------------------------------------------

            GameObject iconGo =
                new GameObject(
                    "ItemIcon",
                    typeof(RectTransform)
                );

            iconGo.transform.SetParent(
                rowGo.transform,
                false
            );

            RectTransform iconRt =
                iconGo
                    .GetComponent<RectTransform>();

            iconRt.sizeDelta =
                new Vector2(
                    32f,
                    32f
                );

            LayoutElement iconLayout =
                iconGo
                    .AddComponent<LayoutElement>();

            iconLayout.preferredWidth =
                32f;

            iconLayout.preferredHeight =
                32f;

            Image icon =
                iconGo
                    .AddComponent<Image>();

            if (itemData.icon != null)
            {
                icon.sprite =
                    itemData.icon;

                icon.preserveAspect =
                    true;
            }
            else
            {
                icon.color =
                    new Color(
                        0.2f,
                        0.2f,
                        0.2f,
                        1f
                    );
            }

            // -------------------------------------------------
            // LABEL
            // -------------------------------------------------

            TMP_Text label =
                CreateText(
                    rowGo.transform,
                    "ItemCount",
                    fontSource,
                    220f
                );

            label.text =
                $"{itemData.itemName}   " +
                $"0/{required}";

            return new ItemRowUI(
                rowGo,
                itemData.itemId,
                itemData.itemName,
                null,
                label
            );
        }

        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        private ItemRowUI(
            GameObject rowGameObject,
            int itemId,
            string itemName,
            TMP_Text nameText,
            TMP_Text countText)
        {
            this.rowGameObject =
                rowGameObject;

            ItemId =
                itemId;

            this.itemName =
                itemName;

            this.nameText =
                nameText;

            this.countText =
                countText;
        }

        // =====================================================
        // SET PROGRESS
        // =====================================================

        public void SetProgress(
            int collected,
            int required)
        {
            if (countText == null)
            {
                return;
            }

            countText.text =
                nameText != null
                    ? $"{collected}/{required}"
                    : $"{itemName}   " +
                      $"{collected}/{required}";

            Color targetColor =
                collected >= required
                    ? CompletedColor
                    : Color.white;

            countText.color =
                targetColor;

            if (nameText != null)
            {
                nameText.color =
                    targetColor;
            }
        }

        // =====================================================
        // DESTROY
        // =====================================================

        public void Destroy()
        {
            if (rowGameObject != null)
            {
                Object.Destroy(
                    rowGameObject
                );
            }
        }
    }

    // =========================================================
    // EXTRA PROGRESS ROW
    // =========================================================

    private class ProgressTextRowUI
    {
        private readonly GameObject
            rowGameObject;

        private readonly TMP_Text
            nameText;

        private readonly TMP_Text
            countText;

        public string Label
        {
            get
            {
                return nameText != null
                    ? nameText.text
                    : string.Empty;
            }
        }

        // =====================================================
        // CREATE
        // =====================================================

        public static ProgressTextRowUI Create(
            Transform parent,
            GameObject prefab,
            TMP_Text fontSource,
            string rowName)
        {
            // -------------------------------------------------
            // USE THE SAME ITEM ROW PREFAB
            // -------------------------------------------------

            if (prefab != null)
            {
                GameObject rowGo =
                    Object.Instantiate(
                        prefab,
                        parent,
                        false
                    );

                rowGo.name =
                    rowName;

                RectTransform rowRt =
                    rowGo.GetComponent<RectTransform>();

                if (rowRt != null)
                {
                    rowRt.anchoredPosition =
                        Vector2.zero;

                    rowRt.sizeDelta =
                        new Vector2(
                            0f,
                            50f
                        );
                }

                TMP_Text nameText =
                    FindChildText(
                        rowGo.transform,
                        "ItemName"
                    );

                TMP_Text countText =
                    FindChildText(
                        rowGo.transform,
                        "ItemCount"
                    );

                // Extra progress objectives don't need
                // an item icon.
                Transform icon =
                    rowGo.transform.Find(
                        "ItemIcon"
                    );

                if (icon != null)
                {
                    icon.gameObject.SetActive(
                        false
                    );
                }

                return new ProgressTextRowUI(
                    rowGo,
                    nameText,
                    countText
                );
            }

            // -------------------------------------------------
            // FALLBACK
            // -------------------------------------------------

            GameObject fallbackRow =
                CreateBaseRow(
                    parent
                );

            fallbackRow.name =
                rowName;

            TMP_Text fallbackName =
                CreateText(
                    fallbackRow.transform,
                    "ItemName",
                    fontSource,
                    230f
                );

            TMP_Text fallbackCount =
                CreateText(
                    fallbackRow.transform,
                    "ItemCount",
                    fontSource,
                    90f
                );

            return new ProgressTextRowUI(
                fallbackRow,
                fallbackName,
                fallbackCount
            );
        }

        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        private ProgressTextRowUI(
            GameObject rowGameObject,
            TMP_Text nameText,
            TMP_Text countText)
        {
            this.rowGameObject =
                rowGameObject;

            this.nameText =
                nameText;

            this.countText =
                countText;
        }

        // =====================================================
        // SET NUMBER PROGRESS
        // =====================================================

        public void SetProgress(
            string label,
            int collected,
            int required)
        {
            if (nameText != null)
            {
                nameText.text =
                    label;
            }

            if (countText != null)
            {
                countText.text =
                    $"{collected}/{required}";
            }

            bool completed =
                required > 0 &&
                collected >= required;

            SetColor(
                completed
                    ? CompletedColor
                    : Color.white
            );
        }

        // =====================================================
        // SET RAW TEXT
        // =====================================================

        public void SetText(
            string label,
            string value)
        {
            if (nameText != null)
            {
                nameText.text =
                    label;
            }

            if (countText != null)
            {
                countText.text =
                    value;
            }

            bool completed =
                TryParseProgressRatio(
                    value,
                    out int collected,
                    out int required
                ) &&
                required > 0 &&
                collected >= required;

            SetColor(
                completed
                    ? CompletedColor
                    : Color.white
            );
        }

        // =====================================================
        // COLOR
        // =====================================================

        private void SetColor(
            Color color)
        {
            if (nameText != null)
            {
                nameText.color =
                    color;
            }

            if (countText != null)
            {
                countText.color =
                    color;
            }
        }

        // =====================================================
        // DESTROY
        // =====================================================

        public void Destroy()
        {
            if (rowGameObject != null)
            {
                Object.Destroy(
                    rowGameObject
                );
            }
        }
    }

    // =========================================================
    // PARSE X/Y
    // =========================================================

    private static bool TryParseProgressRatio(
        string value,
        out int collected,
        out int required)
    {
        collected =
            0;

        required =
            0;

        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        string[] parts =
            value.Split(
                '/'
            );

        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(
                parts[0].Trim(),
                out collected))
        {
            return false;
        }

        if (!int.TryParse(
                parts[1].Trim(),
                out required))
        {
            return false;
        }

        return true;
    }

    // =========================================================
    // SHARED UI HELPERS
    // =========================================================

    private static GameObject CreateBaseRow(
        Transform parent)
    {
        GameObject rowGo =
            new GameObject(
                "ItemRow",
                typeof(RectTransform)
            );

        rowGo.transform.SetParent(
            parent,
            false
        );

        RectTransform rowRt =
            rowGo.GetComponent<RectTransform>();

        rowRt.sizeDelta =
            new Vector2(
                0f,
                40f
            );

        HorizontalLayoutGroup hlg =
            rowGo.AddComponent<HorizontalLayoutGroup>();

        hlg.spacing =
            10f;

        hlg.childAlignment =
            TextAnchor.MiddleLeft;

        hlg.childControlWidth =
            false;

        hlg.childControlHeight =
            true;

        hlg.childForceExpandWidth =
            false;

        hlg.childForceExpandHeight =
            true;

        LayoutElement rowLayout =
            rowGo.AddComponent<LayoutElement>();

        rowLayout.preferredHeight =
            40f;

        return rowGo;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string objectName,
        TMP_Text fontSource,
        float width)
    {
        GameObject textGo =
            new GameObject(
                objectName,
                typeof(RectTransform)
            );

        textGo.transform.SetParent(
            parent,
            false
        );

        RectTransform rt =
            textGo.GetComponent<RectTransform>();

        rt.sizeDelta =
            new Vector2(
                width,
                32f
            );

        LayoutElement layout =
            textGo.AddComponent<LayoutElement>();

        layout.preferredWidth =
            width;

        layout.preferredHeight =
            32f;

        TMP_Text text =
            textGo.AddComponent<TextMeshProUGUI>();

        if (fontSource != null)
        {
            text.font =
                fontSource.font;

            text.fontSharedMaterial =
                fontSource.fontSharedMaterial;
        }

        text.fontSize =
            26f;

        text.color =
            Color.white;

        text.alignment =
            TextAlignmentOptions.Left;

        return text;
    }

    private static TMP_Text FindChildText(
        Transform root,
        string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform child =
            root.Find(
                childName
            );

        if (child == null)
        {
            return null;
        }

        TMP_Text text =
            child.GetComponent<TMP_Text>();

        return text != null
            ? text
            : child.GetComponentInChildren<TMP_Text>(
                true
            );
    }

    // =========================================================
    // TEAM OBJECTIVE
    // =========================================================

    private sealed class TeamObjective
    {
        public int ItemId
        {
            get;
        }

        public int Required
        {
            get;
        }

        public int Baseline
        {
            get;
        }

        public TeamObjective(
            int itemId,
            int required,
            int baseline)
        {
            ItemId =
                itemId;

            Required =
                required;

            Baseline =
                Mathf.Max(0, baseline);
        }
    }
}
