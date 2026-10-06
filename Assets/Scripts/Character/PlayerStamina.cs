using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [SerializeField] float maxStamina = 100f;
    [SerializeField] float sprintDrainPerSecond = 20f; // i-tweak mo ito
    [SerializeField] float regenDelay = 1f;            // wait bago mag-regen
    [SerializeField] float regenPerSecond = 15f;
    [SerializeField] float minToStartSprint = 10f;     // para hindi mag-flicker kapag naubos

    public float Current { get; private set; }
    public float Max => maxStamina;

    float regenTimer;
    bool exhausted;

    void Awake()
    {
        Current = maxStamina;
    }

    // tawagin ito every frame ng PlayerController
    // return value: true kung puwede talagang mag-sprint
    public bool Tick(bool wantsSprint, float dt)
    {
        bool sprinting = wantsSprint && !exhausted && Current > 0f;

        if (sprinting)
        {
            Current = Mathf.Max(0f, Current - sprintDrainPerSecond * dt);
            regenTimer = regenDelay; // i-reset ang 1 second na hintay

            if (Current <= 0f) exhausted = true;
        }
        else if (regenTimer > 0f)
        {
            regenTimer -= dt;
        }
        else
        {
            Current = Mathf.Min(maxStamina, Current + regenPerSecond * dt);

            if (exhausted && Current >= minToStartSprint) exhausted = false;
        }

        return sprinting;
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20), $"Stamina: {Current:0}");
    }
}