
using UnityEngine;

public class DifficultyItemHandler : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [SerializeField]
    private ItemSpawnProfile spawnProfile;

    public ItemSpawnProfile.PhaseSpawnRule GetSpawnRule(
        GameDifficulty difficulty,
        int phase)
    {
        if (spawnProfile == null)
        {
            Debug.LogError(
                "[ITEM HANDLER] No ItemSpawnProfile assigned.",
                this
            );

            return null;
        }

        ItemSpawnProfile.PhaseSpawnRule rule =
            spawnProfile.GetRule(difficulty, phase);

        if (rule == null)
        {
            Debug.LogWarning(
                $"[ITEM HANDLER] No item rule for " +
                $"{difficulty}, Phase {phase}.",
                this
            );
        }

        return rule;
    }

    public bool HasSpawnProfile()
    {
        return spawnProfile != null;
    }

    public bool ValidateSpawnRule(GameDifficulty difficulty, int phase)
    {
        ItemSpawnProfile.PhaseSpawnRule rule =
            GetSpawnRule(difficulty, phase);

        if (rule == null)
            return false;

        if (rule.items == null || rule.items.Count == 0)
        {
            Debug.LogWarning(
                $"[ITEM HANDLER] The rule for {difficulty}, " +
                $"Phase {phase} has no item entries.",
                this
            );

            return false;
        }

        return true;
    }
}