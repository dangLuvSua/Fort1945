
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class ItemRandomSpawner : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PhaseManager phaseManager;
    [SerializeField] private DifficultyItemHandler itemHandler;
    [SerializeField] private DungeonGenerator dungeonGenerator;

    [Header("Standalone Testing")]
    [Tooltip("Allows item spawning without NetworkGameManager for direct Dungeon scene testing.")]
    [SerializeField] private bool allowStandaloneTesting = true;

    [Tooltip("Difficulty used when NetworkGameManager is unavailable.")]
    [SerializeField]
    private GameDifficulty standaloneDifficulty =
        GameDifficulty.Normal;

    [Tooltip("Phase used when PhaseManager is unavailable during standalone testing.")]
    [SerializeField, Min(1)] private int standalonePhase = 1;

    [Header("Spawn Settings")]
    [SerializeField] private bool spawnOnCurrentPhase = true;
    [SerializeField] private float verticalOffset = 0.1f;

    [Header("Guaranteed Prison Keys")]
    [Tooltip("Items spawned at dungeon start, outside the phase missions. Keep Prison Key 2 out of this list to reserve it for Phase 2.")]
    [SerializeField] private ItemData[] guaranteedPrisonKeys;

    [Header("Difficulty Based Candles")]
    [SerializeField] private ItemData candleItem;

    [Header("Phase 2 Key Items")]
    [SerializeField] private ItemData[] phaseTwoGuaranteedItems;
    [Header("Phase 3 Key Items")]
    [SerializeField] private ItemData[] phaseThreeGuaranteedItems;

    private readonly List<ItemSpawnPoint> availablePoints =
        new List<ItemSpawnPoint>();

    private bool dungeonReady;
    private bool isSubscribed;
    private int lastProcessedPhase = -1;
    private bool prisonKeysSpawned;
    private readonly HashSet<int> spawnedGuaranteedKeyIds = new HashSet<int>();
    private readonly Dictionary<int, int> phaseItemRequirements = new Dictionary<int, int>();
    [Networked]
    private int NetworkObjectivePhase { get; set; }
    [Networked, Capacity(32)]
    private NetworkArray<int> NetworkObjectiveItemIds => default;
    [Networked, Capacity(32)]
    private NetworkArray<int> NetworkObjectiveRequired => default;
    [Networked, Capacity(32)]
    private NetworkArray<int> NetworkObjectiveCollected => default;
    private readonly Dictionary<ItemSpawnProfile.ItemSpawnEntry, int> phaseEntrySpawnCounts =
        new Dictionary<ItemSpawnProfile.ItemSpawnEntry, int>();
    private int displayedPhase = -1;
    private bool phaseItemsSpawned;
    private int phaseCandlesSpawned;
    private int preparingPhase = -1;
    private readonly HashSet<int> phaseTwoSpawnedIds = new HashSet<int>();
    private readonly HashSet<int> phaseThreeSpawnedIds = new HashSet<int>();
    private readonly List<Transform> missionSpawnTargets = new List<Transform>();
    private MissionProgressPanelUI missionProgressPanel;
    private readonly HashSet<int> configuredObjectiveItemIds = new HashSet<int>();
    private bool waitingForMissionAcknowledgement;

    public override void Spawned()
    {
        lastProcessedPhase = -1;

        SubscribeToDungeonEvent();

        if (phaseManager == null)
            phaseManager = PhaseManager.Instance;

        if (dungeonGenerator == null)
            dungeonGenerator = FindFirstObjectByType<DungeonGenerator>();

        if (dungeonGenerator != null && dungeonGenerator.IsGenerated)
            HandleDungeonGenerated(dungeonGenerator);

        Debug.Log(
            $"[ITEM SPAWNER] Network object spawned. " +
            $"State Authority: {Object.HasStateAuthority}"
        );
    }

    private void OnEnable()
    {
        SubscribeToDungeonEvent();
    }

    private void OnDisable()
    {
        UnsubscribeFromDungeonEvent();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnsubscribeFromDungeonEvent();
    }

    private void SubscribeToDungeonEvent()
    {
        if (isSubscribed)
            return;

        DungeonGenerator.OnDungeonGenerated += HandleDungeonGenerated;
        isSubscribed = true;
    }

    private void UnsubscribeFromDungeonEvent()
    {
        if (!isSubscribed)
            return;

        DungeonGenerator.OnDungeonGenerated -= HandleDungeonGenerated;
        isSubscribed = false;
    }

    private void HandleDungeonGenerated(DungeonGenerator generator)
    {
        if (generator == null)
            return;

        if (dungeonGenerator != null && generator != dungeonGenerator)
            return;

        dungeonGenerator = generator;
        availablePoints.Clear();
        missionSpawnTargets.Clear();
        missionSpawnTargets.AddRange(generator.GetMissionItemSpawnTargets());

        ItemSpawnPoint[] markers =
            generator.GetComponentsInChildren<ItemSpawnPoint>(true);

        foreach (ItemSpawnPoint marker in markers)
        {
            if (marker == null || !marker.canSpawnItems)
                continue;

            if (!marker.gameObject.activeInHierarchy)
                continue;

            availablePoints.Add(marker);
        }

        ShufflePoints();

        dungeonReady = true;
        lastProcessedPhase = -1;

        Debug.Log(
            $"[ITEM SPAWNER] Dungeon ready. " +
            $"Found {availablePoints.Count} item spawn points."
        );
    }

    public override void FixedUpdateNetwork()
    {
        // This script must be attached to a spawned NetworkObject.
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (dungeonReady && !prisonKeysSpawned && guaranteedPrisonKeys != null && guaranteedPrisonKeys.Length > 0)
        {
            if (SpawnGuaranteedPrisonKeys())
                prisonKeysSpawned = true;
        }

        if (!spawnOnCurrentPhase || !dungeonReady)
            return;

        if (Runner == null || !Runner.IsRunning)
            return;

        if (phaseManager == null)
            phaseManager = PhaseManager.Instance;

        if (phaseManager != null && !phaseManager.IsNetworkReady)
            return;

        if (itemHandler == null)
        {
            Debug.LogWarning(
                "[ITEM SPAWNER] DifficultyItemHandler is missing."
            );
            return;
        }

        // Resolve difficulty and phase, supporting standalone testing.
        GameDifficulty difficulty;
        int phase;

        NetworkGameManager gameManager = NetworkGameManager.Instance;

        if (gameManager != null && gameManager.IsNetworkStateReady)
        {
            difficulty = gameManager.Difficulty;
        }
        else if (allowStandaloneTesting)
        {
            difficulty = standaloneDifficulty;
        }
        else
        {
            return;
        }

        if (phaseManager != null && phaseManager.IsNetworkReady && phaseManager.PhaseInitialized)
        {
            phase = phaseManager.CurrentPhase;
        }
        else if (allowStandaloneTesting)
        {
            phase = Mathf.Max(1, standalonePhase);
        }
        else
        {
            return;
        }

        // Wait for carried inventories before checking the first mission's items.
        if (phase == 1 && gameManager != null && gameManager.IsNetworkStateReady &&
            !AreAllDungeonInventoriesReady())
            return;

        ItemSpawnProfile.PhaseSpawnRule rule =
            itemHandler.GetSpawnRule(difficulty, phase);

        if (rule == null)
        {
            Debug.LogWarning(
                $"[ITEM SPAWNER] No matching rule for " +
                $"{difficulty}, Phase {phase}. Will retry."
            );
            return;
        }

        if (phase != lastProcessedPhase && phase != preparingPhase)
        {
            phaseItemRequirements.Clear();
            phaseEntrySpawnCounts.Clear();
            phaseCandlesSpawned = 0;
            phaseItemsSpawned = false;
            preparingPhase = phase;
            ClearNetworkObjectiveRequirements();
        }

        if (phase != lastProcessedPhase)
        {
            if (phase == 2 && !SpawnPhaseTwoGuaranteedItems())
                return;
            if (phase == 3 && !SpawnGuaranteedPhaseItems(phaseThreeGuaranteedItems, phaseThreeSpawnedIds, 3))
                return;

            bool spawned = SpawnPhaseItems(difficulty, phase, rule, phaseItemRequirements);
            if (!spawned)
                return;

            // Candles remain usable supplies, never collection requirements.
            ItemData optionalCandle = ResolveCandleItem(rule, gameManager);
            if (optionalCandle != null)
                phaseItemRequirements.Remove(optionalCandle.itemId);

            PublishPhaseObjectiveRequirements();
            phaseItemsSpawned = true;
            lastProcessedPhase = phase;
            preparingPhase = -1;

            Debug.Log(
                $"[ITEM SPAWNER] Started mission phase {phase}: " +
                rule.missionTitle,
                this);
        }

        UpdateNetworkedPhaseItemProgress(gameManager);

        if (phaseItemsSpawned &&
            rule.completionMode != ItemSpawnProfile.PhaseCompletionMode.ExternalSignal &&
            HasCollectedPhaseItems(gameManager) &&
            phaseManager != null &&
            phaseManager.IsNetworkReady &&
            (rule.completionMode != ItemSpawnProfile.PhaseCompletionMode.CollectItemsAndPhotos ||
             phaseManager.PhotoCapturesThisPhase >= rule.requiredPhotoCaptures) &&
            (!rule.requirePuzzlePieces || phaseManager.PuzzlePiecesCollected))
        {
            Debug.Log($"[ITEM SPAWNER] Phase {phase} mission complete.", this);
            phaseItemsSpawned = false;
            phaseManager.AdvancePhase();
        }
    }

    private bool AreAllDungeonInventoriesReady()
    {
        if (Runner == null || !Runner.IsRunning)
            return false;

        bool foundPlayer = false;
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            foundPlayer = true;
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) ||
                playerObject == null)
            {
                return false;
            }

            PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.DungeonTransferReady)
                return false;
        }

        return foundPlayer;
    }

    private void Update()
    {
        if (phaseManager == null)
            phaseManager = PhaseManager.Instance;

        if (phaseManager != null && !phaseManager.IsNetworkReady)
            return;

        if (itemHandler == null)
            return;

        int phase = phaseManager != null && phaseManager.IsNetworkReady && phaseManager.PhaseInitialized
            ? phaseManager.CurrentPhase
            : Mathf.Max(1, standalonePhase);

        NetworkGameManager manager = NetworkGameManager.Instance;
        GameDifficulty difficulty = manager != null && manager.IsNetworkStateReady
            ? manager.Difficulty
            : standaloneDifficulty;
        ItemSpawnProfile.PhaseSpawnRule rule = itemHandler.GetSpawnRule(difficulty, phase);
        if (rule == null)
            return;

        if (phase != displayedPhase)
        {
            // The scene component can exist before Fusion calls Spawned().
            // Wait for the intro's replicated completion state on every client.
            if (displayedPhase < 0)
            {
                DungeonNetworkGameManager dungeonManager = DungeonNetworkGameManager.Instance;
                if (dungeonManager == null)
                    dungeonManager = FindFirstObjectByType<DungeonNetworkGameManager>(FindObjectsInactive.Include);
                if (dungeonManager != null && !dungeonManager.CanShowInitialMissionBriefing)
                    return;
            }

            // Wait until the completion screen opens before presenting the briefing.
            // Keep displayedPhase unchanged so this client retries on the next frame.
            if (phaseManager != null && phaseManager.IsShowingPhaseCompletion)
                return;

            displayedPhase = phase;
            configuredObjectiveItemIds.Clear();
            missionProgressPanel = MissionProgressPanelUI.Create();
            if (missionProgressPanel != null)
                missionProgressPanel.Hide();

            bool hasMissionContent = rule.missionData != null ||
                !string.IsNullOrWhiteSpace(rule.missionTitle);
            bool hasBriefingPanel = hasMissionContent && MissionManager.HasMissionPanel;
            waitingForMissionAcknowledgement = hasBriefingPanel;
            if (rule.missionData != null)
            {
                MissionManager.Show(rule.missionData,
                    hasBriefingPanel ? () => ShowPhaseProgressPanel(phase, rule) : null);
            }
            else if (!string.IsNullOrWhiteSpace(rule.missionTitle))
            {
                MissionManager.ShowRaw(
                    rule.missionTitle,
                    rule.missionDescription,
                    0f,
                    hasBriefingPanel ? () => ShowPhaseProgressPanel(phase, rule) : null);
            }

            // If the scene has no briefing UI, don't leave the progress HUD hidden.
            if (!hasBriefingPanel)
                ShowPhaseProgressPanel(phase, rule);
        }

        if (missionProgressPanel == null)
            return;

        if (!waitingForMissionAcknowledgement)
            EnsurePhaseObjectiveRows(rule);

        if (phase == 2)
        {
            PhaseManager currentPhaseManager = phaseManager;
            int photoCount = currentPhaseManager != null
                ? currentPhaseManager.PhotoCapturesThisPhase
                : 0;
            missionProgressPanel.SetProgressText(
                "PhotoProgress",
                $"Photos: {Mathf.Min(photoCount, rule.requiredPhotoCaptures)}/{rule.requiredPhotoCaptures}");

            bool puzzlePiecesCollected = currentPhaseManager != null &&
                currentPhaseManager.PuzzlePiecesCollected;
            missionProgressPanel.SetProgressText(
                "PuzzlePieceProgress",
                puzzlePiecesCollected ? "Puzzle: complete" : "Puzzle: in progress");
        }
        else if (phase == 3)
        {
            bool complete = phaseManager != null && phaseManager.SequenceComplete;
            missionProgressPanel.SetProgressText(
                "BoardProgress",
                complete ? "Puzzle and persona identification: complete" : "Complete the puzzle and identify the persona");
        }
    }

    private void ShowPhaseProgressPanel(
        int phase,
        ItemSpawnProfile.PhaseSpawnRule rule)
    {
        if (phase != displayedPhase || missionProgressPanel == null)
            return;

        waitingForMissionAcknowledgement = false;
        configuredObjectiveItemIds.Clear();
        if (rule.missionData != null)
            missionProgressPanel.SetMission(rule.missionData);
        else
            missionProgressPanel.SetMission(rule.missionTitle);

        EnsurePhaseObjectiveRows(rule);
    }

    private void EnsurePhaseObjectiveRows(ItemSpawnProfile.PhaseSpawnRule rule)
    {
        if (missionProgressPanel == null)
            return;

        NetworkGameManager manager = NetworkGameManager.Instance;
        ItemData phaseCandle = ResolveCandleItem(rule, manager);
        List<KeyValuePair<int, int>> requirements = GetVisibleObjectiveRequirements();
        foreach (KeyValuePair<int, int> requirement in requirements)
        {
            if ((phaseCandle != null && requirement.Key == phaseCandle.itemId) ||
                configuredObjectiveItemIds.Contains(requirement.Key))
                continue;

            ItemData item = FindPhaseItemData(rule, requirement.Key);
            if (item == null)
                continue;

            missionProgressPanel.AddItemRow(item, requirement.Value);
            configuredObjectiveItemIds.Add(requirement.Key);
        }

    }

    private List<KeyValuePair<int, int>> GetVisibleObjectiveRequirements()
    {
        List<KeyValuePair<int, int>> requirements =
            new List<KeyValuePair<int, int>>();

        if (Object != null && Object.IsValid && Runner != null && Runner.IsRunning)
        {
            int currentPhase = phaseManager != null && phaseManager.IsNetworkReady && phaseManager.PhaseInitialized
                ? phaseManager.CurrentPhase
                : -1;

            for (int i = 0; i < 32 && NetworkObjectivePhase == currentPhase; i++)
            {
                int itemId = NetworkObjectiveItemIds.Get(i);
                int required = NetworkObjectiveRequired.Get(i);
                if (itemId > 0 && required > 0)
                    requirements.Add(new KeyValuePair<int, int>(itemId, required));
            }
        }

        if (requirements.Count == 0 && (Object == null || Object.HasStateAuthority))
        {
            foreach (KeyValuePair<int, int> requirement in phaseItemRequirements)
                requirements.Add(requirement);
        }

        return requirements;
    }

    private void ClearNetworkObjectiveRequirements()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        for (int i = 0; i < 32; i++)
        {
            NetworkObjectiveItemIds.Set(i, 0);
            NetworkObjectiveRequired.Set(i, 0);
            NetworkObjectiveCollected.Set(i, 0);
        }
        NetworkObjectivePhase = 0;
    }

    private void PublishPhaseObjectiveRequirements()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        ClearNetworkObjectiveRequirements();
        NetworkObjectivePhase = phaseManager != null && phaseManager.IsNetworkReady && phaseManager.PhaseInitialized
            ? phaseManager.CurrentPhase
            : Mathf.Max(1, standalonePhase);
        int index = 0;
        foreach (KeyValuePair<int, int> requirement in phaseItemRequirements)
        {
            if (index >= 32)
            {
                Debug.LogWarning(
                    "[ITEM SPAWNER] More than 32 objective item types; " +
                    "some progress rows cannot be synchronized.", this);
                break;
            }

            NetworkObjectiveItemIds.Set(index, requirement.Key);
            NetworkObjectiveRequired.Set(index, requirement.Value);
            NetworkObjectiveCollected.Set(index, 0);
            index++;
        }
    }

    /// <summary>Reads an authoritative team objective count on every client.</summary>
    public bool TryGetNetworkObjectiveProgress(int itemId, out int collected, out int required)
    {
        collected = 0;
        required = 0;
        if (itemId <= 0 || Object == null || !Object.IsValid || Runner == null || !Runner.IsRunning)
            return false;

        int currentPhase = phaseManager != null && phaseManager.IsNetworkReady && phaseManager.PhaseInitialized
            ? phaseManager.CurrentPhase
            : -1;
        if (currentPhase <= 0 || NetworkObjectivePhase != currentPhase)
            return false;

        for (int index = 0; index < 32; index++)
        {
            if (NetworkObjectiveItemIds.Get(index) != itemId)
                continue;

            required = NetworkObjectiveRequired.Get(index);
            collected = Mathf.Clamp(NetworkObjectiveCollected.Get(index), 0, required);
            return required > 0;
        }

        return false;
    }

    /// <summary>Returns eligible loot items configured for the active phase.</summary>
    public List<ItemData> GetCurrentPhaseRequiredItems()
    {
        List<ItemData> items = new List<ItemData>();
        if (itemHandler == null)
            return items;

        if (phaseManager == null)
            phaseManager = PhaseManager.Instance;

        NetworkGameManager manager = NetworkGameManager.Instance;
        GameDifficulty difficulty = manager != null && manager.IsNetworkStateReady
            ? manager.Difficulty
            : standaloneDifficulty;
        int phase = phaseManager != null && phaseManager.IsNetworkReady && phaseManager.PhaseInitialized
            ? phaseManager.CurrentPhase
            : Mathf.Max(1, standalonePhase);
        ItemSpawnProfile.PhaseSpawnRule rule = itemHandler.GetSpawnRule(difficulty, phase);

        if (rule?.items != null)
        {
            foreach (ItemSpawnProfile.ItemSpawnEntry entry in rule.items)
                AddUniqueLootItem(items, entry?.item);
        }

        AddUniqueLootItem(items, ResolveCandleItem(rule, manager));
        if (phase == 2 && phaseTwoGuaranteedItems != null)
        {
            foreach (ItemData item in phaseTwoGuaranteedItems)
                AddUniqueLootItem(items, item);
        }
        if (phase == 3 && phaseThreeGuaranteedItems != null)
        {
            foreach (ItemData item in phaseThreeGuaranteedItems)
                AddUniqueLootItem(items, item);
        }

        return items;
    }

    private static void AddUniqueLootItem(List<ItemData> items, ItemData item)
    {
        if (item != null && item.itemId > 0 && !items.Contains(item))
            items.Add(item);
    }

    private ItemData FindPhaseItemData(ItemSpawnProfile.PhaseSpawnRule rule, int itemId)
    {
        ItemData configuredCandle = ResolveCandleItem(rule, NetworkGameManager.Instance);
        if (configuredCandle != null && configuredCandle.itemId == itemId)
            return configuredCandle;

        if (rule?.items != null)
        {
            foreach (ItemSpawnProfile.ItemSpawnEntry entry in rule.items)
            {
                if (entry?.item != null && entry.item.itemId == itemId)
                    return entry.item;
            }
        }

        if (phaseThreeGuaranteedItems != null)
        {
            foreach (ItemData item in phaseThreeGuaranteedItems)
            {
                if (item != null && item.itemId == itemId)
                    return item;
            }
        }

        if (phaseTwoGuaranteedItems != null)
        {
            foreach (ItemData item in phaseTwoGuaranteedItems)
            {
                if (item != null && item.itemId == itemId)
                    return item;
            }
        }

        return null;
    }

    private bool HasCollectedPhaseItems(NetworkGameManager manager)
    {
        if (manager == null)
            return phaseItemRequirements.Count == 0;

        foreach (KeyValuePair<int, int> requirement in phaseItemRequirements)
        {
            int collected = manager.GetTeamItemCount(requirement.Key);
            if (collected < requirement.Value)
                return false;
        }

        return true;
    }

    private void UpdateNetworkedPhaseItemProgress(NetworkGameManager manager)
    {
        if (phaseManager == null || !phaseItemsSpawned)
            return;

        int required = 0;
        int collected = 0;
        foreach (KeyValuePair<int, int> requirement in phaseItemRequirements)
        {
            required += requirement.Value;
            int owned = manager != null ? manager.GetTeamItemCount(requirement.Key) : 0;
            int itemCollected = Mathf.Clamp(owned, 0, requirement.Value);
            collected += itemCollected;

            if (Object != null && Object.HasStateAuthority)
            {
                for (int index = 0; index < 32; index++)
                {
                    if (NetworkObjectiveItemIds.Get(index) == requirement.Key)
                    {
                        NetworkObjectiveCollected.Set(index, itemCollected);
                        break;
                    }
                }
            }
        }

        phaseManager.UpdateItemProgress(required, collected);
    }

    private bool SpawnGuaranteedPrisonKeys()
    {
        int requiredCount = 0;
        foreach (ItemData key in guaranteedPrisonKeys)
        {
            if (key != null && !spawnedGuaranteedKeyIds.Contains(key.itemId))
                requiredCount++;
        }

        if (requiredCount == 0)
            return true;

        if (availablePoints.Count < requiredCount)
            return false;

        foreach (ItemData key in guaranteedPrisonKeys)
        {
            if (key == null || spawnedGuaranteedKeyIds.Contains(key.itemId))
                continue;

            if (!SpawnAtRandomPoint(key))
            {
                Debug.LogWarning(
                    $"[ITEM SPAWNER] Guaranteed prison key '{key.itemName}' failed to spawn; retrying next network tick.", this);
                continue;
            }

            spawnedGuaranteedKeyIds.Add(key.itemId);
        }

        bool complete = true;
        foreach (ItemData key in guaranteedPrisonKeys)
        {
            if (key != null && !spawnedGuaranteedKeyIds.Contains(key.itemId))
            {
                complete = false;
                break;
            }
        }

        if (complete)
            Debug.Log($"[ITEM SPAWNER] Guaranteed prison keys spawned: {spawnedGuaranteedKeyIds.Count}.", this);

        return complete;
    }

    private bool SpawnPhaseTwoGuaranteedItems()
    {
        return SpawnGuaranteedPhaseItems(phaseTwoGuaranteedItems, phaseTwoSpawnedIds, 2);
    }

    private bool SpawnGuaranteedPhaseItems(ItemData[] items, HashSet<int> spawnedIds, int phase)
    {
        if (items == null)
            return true;

        foreach (ItemData item in items)
        {
            if (item == null || spawnedIds.Contains(item.itemId))
                continue;

            if (!SpawnAtMissionTarget(item))
            {
                Debug.LogWarning(
                    $"[ITEM SPAWNER] Could not spawn Phase {phase} item '{item.itemName}' at a mystery/statue point; retrying.",
                    this);
                return false;
            }

            spawnedIds.Add(item.itemId);
            phaseItemRequirements[item.itemId] = 1;
        }

        return true;
    }

    private bool SpawnAtMissionTarget(ItemData item)
    {
        // Gate keys must remain accessible before any locked room is opened.
        if (RequiresCorridorSpawn(item))
            return SpawnAtRandomPoint(item);

        while (missionSpawnTargets.Count > 0)
        {
            int index = Random.Range(0, missionSpawnTargets.Count);
            Transform target = missionSpawnTargets[index];
            missionSpawnTargets.RemoveAt(index);
            if (target == null)
                continue;

            Vector2 offset = Random.insideUnitCircle * 1.25f;
            Vector3 candidate = target.position + new Vector3(offset.x, 0f, offset.y);
            Vector3 position = target.position + Vector3.up * verticalOffset;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                position = hit.position + Vector3.up * verticalOffset;

            if (SpawnAtPosition(item, position, target.rotation))
                return true;
        }

        return SpawnAtRandomPoint(item);
    }

    private bool SpawnPhaseItems(
        GameDifficulty difficulty,
        int phase,
        ItemSpawnProfile.PhaseSpawnRule rule,
        Dictionary<int, int> spawnedRequirements)
    {
        if (rule.items == null)
            rule.items = new List<ItemSpawnProfile.ItemSpawnEntry>();

        if (rule.items.Count == 0 && rule.spawnEveryEntry)
        {
            Debug.LogWarning(
                $"[ITEM SPAWNER] No item entries configured for " +
                $"{difficulty}, Phase {phase}."
            );
            return false;
        }

        ItemData phaseCandle = ResolveCandleItem(rule, NetworkGameManager.Instance);
        int spawnedCount = 0;
        bool hadFailure = false;

        if (phaseCandle != null)
        {
            int targetCandles = GetPhaseCandleCount(difficulty);
            int currentCandles = phaseCandlesSpawned;
            while (currentCandles < targetCandles)
            {
                if (!SpawnAtRandomPoint(phaseCandle))
                {
                    Debug.LogWarning(
                        $"[ITEM SPAWNER] Could not spawn all {targetCandles} difficulty candles for Phase {phase}; retrying.",
                        this);
                    return false;
                }

                currentCandles++;
                spawnedCount++;
                phaseCandlesSpawned = currentCandles;
            }
        }

        if (rule.spawnEveryEntry)
        {
            foreach (ItemSpawnProfile.ItemSpawnEntry entry in rule.items)
            {
                if (entry?.item == null || entry.item.worldPrefab == null)
                    continue;

                // Guaranteed keys spawn in their own pass, but if the active
                // phase lists one as a required entry it still belongs in the
                // objective totals and progress panel.
                if (IsGuaranteedPrisonKey(entry.item))
                {
                    spawnedRequirements[entry.item.itemId] = 1;
                    continue;
                }

                if (phaseCandle != null && entry.item.itemId == phaseCandle.itemId)
                    continue;

                int requiredForEntry = Mathf.Max(0, entry.minQuantity);
                if (entry.maxSpawnsPerPhase > 0)
                    requiredForEntry = Mathf.Min(requiredForEntry, entry.maxSpawnsPerPhase);

                phaseEntrySpawnCounts.TryGetValue(entry, out int entrySpawned);
                while (entrySpawned < requiredForEntry)
                {
                    if (!SpawnAtRandomPoint(entry.item))
                    {
                        hadFailure = true;
                        break;
                    }

                    entrySpawned++;
                    spawnedCount++;
                    phaseEntrySpawnCounts[entry] = entrySpawned;
                    spawnedRequirements.TryGetValue(entry.item.itemId, out int itemSpawned);
                    spawnedRequirements[entry.item.itemId] = itemSpawned + 1;
                }
            }
        }

        if (hadFailure)
            return false;

        int spawnRolls = Mathf.Max(0, rule.totalItemsToSpawn);

        for (int i = 0; i < spawnRolls; i++)
        {
            if (availablePoints.Count == 0)
            {
                Debug.LogWarning(
                    "[ITEM SPAWNER] No unused item spawn points remain."
                );

                break;
            }

            int excludedItemId = phaseCandle != null ? phaseCandle.itemId : -1;
            ItemSpawnProfile.ItemSpawnEntry entry =
                SelectWeightedItem(rule.items, phaseEntrySpawnCounts, excludedItemId);

            if (entry == null || entry.item == null)
            {
                if (HasSelectableEntry(rule.items, phaseEntrySpawnCounts, excludedItemId))
                {
                    Debug.LogWarning(
                        "[ITEM SPAWNER] No valid weighted item entry found."
                    );
                    hadFailure = true;
                }

                // All eligible entries reached their per-phase caps.
                break;
            }

            int min = Mathf.Max(0, entry.minQuantity);
            int max = Mathf.Max(min, entry.maxQuantity);

            int quantity = Random.Range(min, max + 1);
            if (entry.maxSpawnsPerPhase > 0)
            {
                int spawnedForEntry = phaseEntrySpawnCounts.TryGetValue(entry, out int count)
                    ? count
                    : 0;
                quantity = Mathf.Min(quantity, entry.maxSpawnsPerPhase - spawnedForEntry);
            }

            for (int q = 0; q < quantity; q++)
            {
                if (availablePoints.Count == 0)
                    break;

                if (SpawnAtRandomPoint(entry.item))
                {
                    spawnedCount++;
                    if (!spawnedRequirements.ContainsKey(entry.item.itemId))
                        spawnedRequirements[entry.item.itemId] = 0;
                    spawnedRequirements[entry.item.itemId]++;
                    phaseEntrySpawnCounts.TryGetValue(entry, out int entryCount);
                    phaseEntrySpawnCounts[entry] = entryCount + 1;
                }
                else
                {
                    hadFailure = true;
                }
            }
        }

        Debug.Log(
            $"[ITEM SPAWNER] {difficulty}, Phase {phase}: " +
            $"spawned {spawnedCount} item(s)."
        );

        // A failed attempt can be retried. Successful spawns from a
        // partially failed attempt are not rolled back.
        return !hadFailure;
    }

    private ItemData ResolveCandleItem(
        ItemSpawnProfile.PhaseSpawnRule rule,
        NetworkGameManager manager)
    {
        if (manager != null && manager.CandleItem != null)
            return manager.CandleItem;

        if (candleItem != null)
            return candleItem;

        if (rule?.items != null)
        {
            foreach (ItemSpawnProfile.ItemSpawnEntry entry in rule.items)
            {
                if (entry?.item != null && entry.item.itemId == 203)
                    return entry.item;
            }
        }

        return null;
    }

    private int GetPhaseCandleCount(GameDifficulty difficulty)
    {
        NetworkGameManager manager = NetworkGameManager.Instance;
        if (manager != null)
            return manager.GetDifficultyAdjustedCandleCount(difficulty);

        return Mathf.Max(1, 2 + DifficultyDatabase.Get(difficulty).additionalItems);
    }

    private bool SpawnAtRandomPoint(ItemData item)
    {
        if (item == null || item.worldPrefab == null)
        {
            Debug.LogWarning(
                "[ITEM SPAWNER] Item or World Prefab is missing."
            );
            return false;
        }

        if (availablePoints.Count == 0)
            return false;

        List<int> eligibleIndices = new List<int>();
        for (int candidateIndex = 0; candidateIndex < availablePoints.Count; candidateIndex++)
        {
            ItemSpawnPoint candidate = availablePoints[candidateIndex];
            if (candidate == null || !candidate.canSpawnItems || !candidate.gameObject.activeInHierarchy)
                continue;
            if (RequiresCorridorSpawn(item) && !IsCorridorSpawnPoint(candidate.transform))
                continue;
            eligibleIndices.Add(candidateIndex);
        }
        if (eligibleIndices.Count == 0)
        {
            Debug.LogWarning($"[ITEM SPAWNER] No eligible unused spawn point for '{item.itemName}'. Gate keys require corridor markers; no locked-room fallback is allowed.", this);
            return false;
        }

        int index = eligibleIndices[Random.Range(0, eligibleIndices.Count)];
        ItemSpawnPoint point = availablePoints[index];

        if (point == null)
        {
            availablePoints.RemoveAt(index);
            return false;
        }

        Vector3 position = point.Position + Vector3.up * verticalOffset;
        if (!SpawnAtPosition(item, position, point.Rotation))
            return false;

        availablePoints.RemoveAt(index);
        return true;
    }

    private static bool RequiresCorridorSpawn(ItemData item)
    {
        return item != null && (item.itemId == 102 || item.itemId == 104 || item.itemId == 105);
    }

    private bool IsCorridorSpawnPoint(Transform point)
    {
        if (dungeonGenerator == null)
            return false;
        foreach (GameObject piece in dungeonGenerator.GeneratedPieces)
        {
            if (piece != null && point.IsChildOf(piece.transform) && dungeonGenerator.IsCorridorPiece(piece))
                return true;
        }
        return false;
    }

    private bool SpawnAtPosition(ItemData item, Vector3 position, Quaternion rotation)
    {
        if (item == null || item.worldPrefab == null)
            return false;

        NetworkObject spawned = Runner.Spawn(
            item.worldPrefab,
            position,
            rotation,
            null,
            (runner, networkObject) =>
            {
                NetworkedDroppedItem networkedItem =
                    networkObject.GetComponent<NetworkedDroppedItem>();
                if (networkedItem != null)
                    networkedItem.Initialize(item.itemId);

                ItemPickup pickup = networkObject.GetComponent<ItemPickup>();
                if (pickup != null)
                    pickup.Initialize(item.itemId);
            }
        );

        if (spawned == null)
        {
            Debug.LogWarning(
                $"[ITEM SPAWNER] Failed to spawn {item.itemName}."
            );

            // Keep the point available so a later attempt can retry.
            return false;
        }

        Debug.Log(
            $"[ITEM SPAWNER] Spawned {item.itemName} at {position}."
        );

        return true;
    }

    private ItemSpawnProfile.ItemSpawnEntry SelectWeightedItem(
        List<ItemSpawnProfile.ItemSpawnEntry> entries,
        Dictionary<ItemSpawnProfile.ItemSpawnEntry, int> spawnedCounts,
        int excludedItemId)
    {
        if (entries == null || entries.Count == 0)
            return null;

        float totalWeight = 0f;

        foreach (var entry in entries)
        {
            if (entry == null ||
                entry.item == null ||
                entry.item.worldPrefab == null ||
                IsGuaranteedPrisonKey(entry.item) ||
                entry.item.itemId == excludedItemId ||
                IsEntryAtSpawnCap(entry, spawnedCounts))
                continue;

            totalWeight += Mathf.Max(0f, entry.weight);
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.Range(0f, totalWeight);

        foreach (var entry in entries)
        {
            if (entry == null ||
                entry.item == null ||
                entry.item.worldPrefab == null ||
                IsGuaranteedPrisonKey(entry.item) ||
                entry.item.itemId == excludedItemId ||
                IsEntryAtSpawnCap(entry, spawnedCounts))
                continue;

            roll -= Mathf.Max(0f, entry.weight);

            if (roll <= 0f)
                return entry;
        }

        return null;
    }

    private bool HasSelectableEntry(
        List<ItemSpawnProfile.ItemSpawnEntry> entries,
        Dictionary<ItemSpawnProfile.ItemSpawnEntry, int> spawnedCounts,
        int excludedItemId)
    {
        if (entries == null)
            return false;

        foreach (ItemSpawnProfile.ItemSpawnEntry entry in entries)
        {
            if (entry != null && entry.item != null && entry.item.worldPrefab != null &&
                !IsGuaranteedPrisonKey(entry.item) && entry.item.itemId != excludedItemId &&
                !IsEntryAtSpawnCap(entry, spawnedCounts) &&
                entry.weight > 0f)
                return true;
        }

        return false;
    }

    private static bool IsEntryAtSpawnCap(
        ItemSpawnProfile.ItemSpawnEntry entry,
        Dictionary<ItemSpawnProfile.ItemSpawnEntry, int> spawnedCounts)
    {
        if (entry.maxSpawnsPerPhase <= 0)
            return false;

        return spawnedCounts.TryGetValue(entry, out int count) &&
               count >= entry.maxSpawnsPerPhase;
    }

    private bool IsGuaranteedPrisonKey(ItemData item)
    {
        if (item == null || guaranteedPrisonKeys == null)
            return false;

        foreach (ItemData key in guaranteedPrisonKeys)
        {
            if (key != null && key.itemId == item.itemId)
                return true;
        }

        return false;
    }

    private void ShufflePoints()
    {
        for (int i = availablePoints.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            ItemSpawnPoint temp = availablePoints[i];
            availablePoints[i] = availablePoints[j];
            availablePoints[j] = temp;
        }
    }
}
