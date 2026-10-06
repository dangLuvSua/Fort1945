using UnityEngine;

public abstract class DifficultyEnemy : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] protected float baseHealth = 100f;
    [SerializeField] protected float baseDamage = 10f;
    [SerializeField] protected float baseSpeed = 3f;
    [SerializeField] protected float baseDetectionRange = 8f;

    protected float Health { get; private set; }
    protected float Damage { get; private set; }
    protected float Speed { get; private set; }
    protected float DetectionRange { get; private set; }

    protected virtual void Start()
    {
        ApplyDifficulty();
    }

    protected void ApplyDifficulty()
    {
        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager == null)
            return;

        DifficultySettings settings =
            DifficultyService.GetSettings(manager);

        Health =
            baseHealth *
            settings.enemyHealthMultiplier;

        Damage =
            baseDamage *
            settings.enemyDamageMultiplier;

        Speed =
            baseSpeed *
            settings.enemySpeedMultiplier;

        DetectionRange =
            baseDetectionRange *
            settings.enemyDetectionMultiplier;
    }
}