using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemSpawnProfile",
    menuName = "Fort1945/Item Spawn Profile"
)]
public class ItemSpawnProfile : ScriptableObject
{
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
    }

    [Serializable]
    public class PhaseSpawnRule
    {
        [Min(1)]
        public int phase = 1;

        public GameDifficulty difficulty = GameDifficulty.Normal;

        [Min(0)]
        [Tooltip("Total number of items to spawn for this phase.")]
        public int totalItemsToSpawn = 3;

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