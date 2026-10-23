using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemSpawnProfile",
    menuName = "Fort1945/Item Spawn Profile"
)]
public class ItemSpawnProfile : ScriptableObject
{
    public enum PhaseCompletionMode
    {
        CollectAllSpawnedItems,
        CollectItemsAndPhotos,
        ExternalSignal
    }

    [Serializable]
    public class ItemSpawnEntry
    {
        [Tooltip("ItemData asset for this item.")]
        public ItemData item;

        [Min(0.01f)]
        [Tooltip("Higher weight means a higher chance of selection.")]
        public float weight = 1f;

        [Min(0)]
        public int minQuantity = 1;

        [Min(0)]
        public int maxQuantity = 1;

        [Min(0), Tooltip("Maximum copies of this entry spawned during the phase. Set to 1 to guarantee at most one instance; 0 means unlimited.")]
        public int maxSpawnsPerPhase;
    }

    [Serializable]
    public class PhaseSpawnRule
    {
        [Min(1)]
        public int phase = 1;

        public GameDifficulty difficulty = GameDifficulty.Normal;

        [Tooltip("Mission heading shown when this phase becomes active.")]
        public string missionTitle = "MISSION";

        [TextArea, Tooltip("Mission details shown when this phase becomes active.")]
        public string missionDescription;

        [Tooltip("MissionData asset used to display this phase's mission.")]
        public MissionData missionData;

        public PhaseCompletionMode completionMode =
            PhaseCompletionMode.CollectAllSpawnedItems;

        [Min(0), Tooltip("Number of successful camera captures required for this phase.")]
        public int requiredPhotoCaptures;

        [Tooltip("Wait for every puzzle piece in the selected difficulty set to be collected.")]
        public bool requirePuzzlePieces;

        [Min(0)]
        [Tooltip("Number of additional weighted item rolls after guaranteed entries and difficulty candles spawn.")]
        public int totalItemsToSpawn = 3;

        [Tooltip("Spawn at least each configured item entry's minimum quantity before weighted extra rolls.")]
        public bool spawnEveryEntry;

        [Tooltip("Possible items for this difficulty and phase.")]
        public List<ItemSpawnEntry> items =
            new List<ItemSpawnEntry>();
    }

    [SerializeField]
    private List<PhaseSpawnRule> rules =
        new List<PhaseSpawnRule>();

    public PhaseSpawnRule GetRule(
        GameDifficulty difficulty,
        int phase)
    {
        return rules.Find(rule =>
            rule != null &&
            rule.phase == phase &&
            rule.difficulty == difficulty
        );
    }
}
