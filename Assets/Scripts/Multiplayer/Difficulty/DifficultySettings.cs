using System;

[Serializable]
public class DifficultySettings
{
    // Enemy
    public float enemyHealthMultiplier;
    public float enemyDamageMultiplier;
    public float enemySpeedMultiplier;
    public float enemyDetectionMultiplier;
    public int soldierSpawnCount;

    // Player
    public float playerStaminaMultiplier;

    // Items
    public int additionalItems;

    // Puzzle
    public float puzzleTimeMultiplier;

    public DifficultySettings(
        float enemyHealthMultiplier,
        float enemyDamageMultiplier,
        float enemySpeedMultiplier,
        float enemyDetectionMultiplier,
        float playerStaminaMultiplier,
        int additionalItems,
        float puzzleTimeMultiplier,
        int soldierSpawnCount)
    {
        this.enemyHealthMultiplier = enemyHealthMultiplier;
        this.enemyDamageMultiplier = enemyDamageMultiplier;
        this.enemySpeedMultiplier = enemySpeedMultiplier;
        this.enemyDetectionMultiplier = enemyDetectionMultiplier;
        this.soldierSpawnCount = soldierSpawnCount;
        this.playerStaminaMultiplier = playerStaminaMultiplier;
        this.additionalItems = additionalItems;
        this.puzzleTimeMultiplier = puzzleTimeMultiplier;
    }
}
