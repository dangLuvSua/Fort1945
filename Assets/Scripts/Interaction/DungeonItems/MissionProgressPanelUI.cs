using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the user's MissionProgressPanel (MissionTitle + Content container):
/// - sets the mission title from the MissionData asset
/// - builds one ItemRow per required item ([icon] [Name  X/Y])
/// - updates counts live and turns them green when complete
///
/// The rows are generated at runtime inside the "Content" container so no
/// prefab or scene wiring is required. If the panel is missing from the
/// scene, callers fall back to the text-based MissionManager panel.
/// </summary>
public class MissionProgressPanelUI : MonoBehaviour
{
    [Header("Item Rows")]
    [Tooltip("Optional ItemRow prefab instantiated for each required item. Falls back to code-built rows when empty.")]
    [SerializeField] private GameObject itemRowPrefab;

    private GameObject panelRoot;
    private TMP_Text titleText;
    private RectTransform content;

    private readonly List<ItemRowUI> rows =
        new List<ItemRowUI>();

    // Objective progress is calculated from all active players' replicated
    // inventories, so every client's local HUD shows the same team total.
    private readonly List<TeamObjective> teamObjectives =
        new List<TeamObjective>();

    private bool layoutAdded;

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
                "[MISSION PROGRESS] MissionProgressPanel was " +
                "not found in the scene."
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

    private static GameObject FindMissionProgressPanel()
    {
        GameObject active =
            GameObject.Find("MissionProgressPanel");

        if (active != null)
        {
            return active;
        }

        // The panel is deactivated until the mission starts,
        // so GameObject.Find cannot see it.
        Transform[] allTransforms =
            Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include
            );

        for (int i = 0; i < allTransforms.Length; i++)
        {
            if (allTransforms[i].name == "MissionProgressPanel")
            {
                return allTransforms[i].gameObject;
            }
        }

        return null;
    }

    // =========================================================
    // UNITY
    // =========================================================

    public void EnsureInitialized()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (titleText == null)
        {
            Transform titleTransform =
                transform.Find("MissionTitle");

            titleText =
                titleTransform != null
                    ? titleTransform.GetComponent<TMP_Text>()
                    : null;

            if (titleText == null)
            {
                titleText = GetComponentInChildren<TMP_Text>(true);
            }
        }

        if (content == null)
        {
            Transform contentTransform =
                transform.Find("Content");

            content =
                contentTransform != null
                    ? contentTransform as RectTransform
                    : null;

            if (content == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.Equals("Content", System.StringComparison.OrdinalIgnoreCase))
                    {
                        content = child as RectTransform;
                        break;
                    }
                }
            }
        }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Update()
    {
        NetworkGameManager manager = NetworkGameManager.Instance;
        if (manager == null || !manager.IsNetworkStateReady)
            return;

        for (int i = 0; i < teamObjectives.Count; i++)
        {
            TeamObjective objective = teamObjectives[i];
            if (objective == null)
                continue;

            SetProgress(
                objective.ItemId,
                manager.GetTeamItemCount(objective.ItemId),
                objective.Required);
        }
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public void Show(string title)
    {
        EnsureInitialized();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        if (titleText != null)
        {
            titleText.text = title;
        }
    }

    public void Hide()
    {
        EnsureInitialized();

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void SetMission(string title)
    {
        EnsureInitialized();
        ClearRows();
        Show(title);
        EnsureContentLayout();
    }

    public void AddItemRow(ItemData itemData, int required)
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
                "[MISSION PROGRESS] Content container is null, cannot add item row.",
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

        rows.Add(row);
        teamObjectives.Add(new TeamObjective(itemData.itemId, required));
    }

    public void SetProgress(
        int itemId,
        int collected,
        int required)
    {
        EnsureInitialized();

        foreach (ItemRowUI row in rows)
        {
            if (row != null &&
                row.ItemId == itemId)
            {
                row.SetProgress(collected, required);
            }
        }
    }

    public void ClearRows()
    {
        foreach (ItemRowUI row in rows)
        {
            if (row != null)
            {
                row.Destroy();
            }
        }

        rows.Clear();
        teamObjectives.Clear();
    }

    // =========================================================
    // CONTENT LAYOUT
    // =========================================================

    private void EnsureContentLayout()
    {
        if (layoutAdded || content == null)
        {
            return;
        }

        layoutAdded = true;

        if (content.GetComponent<VerticalLayoutGroup>() == null)
        {
            VerticalLayoutGroup vlg =
                content.gameObject.AddComponent<VerticalLayoutGroup>();

            vlg.spacing = 8f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
        }

        ContentSizeFitter fitter =
            content.GetComponent<ContentSizeFitter>();

        if (fitter == null)
        {
            fitter =
                content.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
    }

    // =========================================================
    // ITEM ROW
    // =========================================================

    private class ItemRowUI
    {
        public int ItemId { get; }

        private readonly GameObject rowGameObject;
        private readonly string itemName;
        private readonly TMP_Text nameText;
        private readonly TMP_Text countText;

        public static ItemRowUI CreateFromPrefab(
            Transform parent,
            GameObject prefab,
            ItemData itemData,
            int required)
        {
            GameObject rowGo =
                Object.Instantiate(prefab, parent, false);

            RectTransform rowRt =
                rowGo.GetComponent<RectTransform>();

            if (rowRt != null)
            {
                rowRt.anchoredPosition = Vector2.zero;
                rowRt.sizeDelta =
                    new Vector2(0f, 50f);
            }

            TMP_Text nameText =
                FindChildText(rowGo.transform, "ItemName");

            TMP_Text countText =
                FindChildText(rowGo.transform, "ItemCount");

            if (nameText != null)
            {
                nameText.text = itemData.itemName;
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

        public static ItemRowUI CreateCodeBuilt(
            Transform parent,
            ItemData itemData,
            int required,
            TMP_Text fontSource)
        {
            GameObject rowGo =
                new GameObject(
                    "ItemRow",
                    typeof(RectTransform)
                );

            rowGo.transform.SetParent(parent, false);

            RectTransform rowRt =
                rowGo.GetComponent<RectTransform>();

            rowRt.sizeDelta =
                new Vector2(0f, 40f);

            HorizontalLayoutGroup hlg =
                rowGo.AddComponent<HorizontalLayoutGroup>();

            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            LayoutElement rowLayout =
                rowGo.AddComponent<LayoutElement>();

            rowLayout.preferredHeight = 40f;

            // Icon
            GameObject iconGo =
                new GameObject(
                    "ItemIcon",
                    typeof(RectTransform)
                );

            iconGo.transform.SetParent(rowGo.transform, false);

            RectTransform iconRt =
                iconGo.GetComponent<RectTransform>();

            iconRt.sizeDelta =
                new Vector2(32f, 32f);

            LayoutElement iconLayout =
                iconGo.AddComponent<LayoutElement>();

            iconLayout.preferredWidth = 32f;
            iconLayout.preferredHeight = 32f;

            Image icon =
                iconGo.AddComponent<Image>();

            if (itemData.icon != null)
            {
                icon.sprite = itemData.icon;
                icon.preserveAspect = true;
            }
            else
            {
                icon.color =
                    new Color(0.2f, 0.2f, 0.2f, 1f);
            }

            // Count label
            GameObject labelGo =
                new GameObject(
                    "ItemCount",
                    typeof(RectTransform)
                );

            labelGo.transform.SetParent(rowGo.transform, false);

            RectTransform labelRt =
                labelGo.GetComponent<RectTransform>();

            labelRt.sizeDelta =
                new Vector2(220f, 32f);

            LayoutElement labelLayout =
                labelGo.AddComponent<LayoutElement>();

            labelLayout.preferredWidth = 220f;
            labelLayout.preferredHeight = 32f;

            TMP_Text label =
                labelGo.AddComponent<TextMeshProUGUI>();

            if (fontSource != null)
            {
                label.font = fontSource.font;
                label.fontSharedMaterial =
                    fontSource.fontSharedMaterial;
            }

            label.fontSize = 26f;
            label.color = Color.white;
            label.text =
                $"{itemData.itemName}   0/{required}";

            return new ItemRowUI(
                rowGo,
                itemData.itemId,
                itemData.itemName,
                null,
                label
            );
        }

        private static TMP_Text FindChildText(
            Transform root,
            string childName)
        {
            Transform child = root.Find(childName);

            if (child == null)
            {
                return null;
            }

            TMP_Text text =
                child.GetComponent<TMP_Text>();

            return text != null
                ? text
                : child.GetComponentInChildren<TMP_Text>(true);
        }

        private ItemRowUI(
            GameObject rowGameObject,
            int itemId,
            string itemName,
            TMP_Text nameText,
            TMP_Text countText)
        {
            this.rowGameObject = rowGameObject;
            ItemId = itemId;
            this.itemName = itemName;
            this.nameText = nameText;
            this.countText = countText;
        }

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
                    : $"{itemName}   {collected}/{required}";

            Color targetColor =
                collected >= required
                    ? new Color(0.3f, 1f, 0.35f, 1f)
                    : Color.white;

            countText.color = targetColor;

            if (nameText != null)
            {
                nameText.color = targetColor;
            }
        }

        public void Destroy()
        {
            if (rowGameObject != null)
            {
                Object.Destroy(rowGameObject);
            }
        }
    }

    private sealed class TeamObjective
    {
        public int ItemId { get; }
        public int Required { get; }

        public TeamObjective(int itemId, int required)
        {
            ItemId = itemId;
            Required = required;
        }
    }
}

