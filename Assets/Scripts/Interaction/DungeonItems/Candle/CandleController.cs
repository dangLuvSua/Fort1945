
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class CandleController : NetworkBehaviour
{
    [Header("Toggle")]
    [SerializeField] private Key toggleKey = Key.F;

    [Header("Lifetime (Minutes)")]
    [SerializeField, Min(0.1f)] private float easyLifeMinutes = 15f;
    [SerializeField, Min(0.1f)] private float normalLifeMinutes = 10f;
    [SerializeField, Min(0.1f)] private float difficultLifeMinutes = 5f;

    [Header("Visual Search")]
    [SerializeField, Min(0.05f)] private float visualSearchInterval = 0.2f;

    [Networked]
    public NetworkBool CandleOn { get; private set; }

    private PlayerInventory inventory;
    private CandleVisual currentCandleVisual;
    private float searchTimer;

    private int SelectedCandleStorageSlot =>
     inventory != null
         ? inventory.GetSelectedStorageSlot()
         : -1;

    public bool IsHoldingCandle =>
        inventory != null &&
        SelectedCandleStorageSlot >= 0 &&
        inventory.IsCandleSlot(SelectedCandleStorageSlot);

    public float RemainingSeconds =>
        IsHoldingCandle
            ? inventory.GetCandleRemaining(SelectedCandleStorageSlot)
            : 0f;

    public float MaxCandleLife =>
        IsHoldingCandle
            ? inventory.GetCandleMaxLife(SelectedCandleStorageSlot)
            : 0f;

    public bool IsCandleLit =>
        CandleOn && RemainingSeconds > 0f;

    public override void Spawned()
    {
        inventory = GetComponent<PlayerInventory>();

        if (Object.HasStateAuthority)
            CandleOn = false;

        FindCurrentCandle();
        ApplyCandleVisual();
    }

    private void Update()
    {
        if (!Object.HasInputAuthority)
            return;

        searchTimer -= Time.deltaTime;

        if (searchTimer <= 0f)
        {
            searchTimer = visualSearchInterval;
            FindCurrentCandle();
            ApplyCandleVisual();
        }

        if (Keyboard.current != null &&
            Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            ToggleCandle();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (!IsHoldingCandle)
        {
            CandleOn = false;
            return;
        }

        int slot = inventory.GetSelectedStorageSlot();

        if (!inventory.IsCandleInitialized(slot))
        {
            inventory.InitializeCandleSlot(
                slot,
                GetLifetimeForCurrentDifficulty()
            );
        }

        if (!CandleOn)
            return;

        float remaining = inventory.GetCandleRemaining(slot);

        remaining = Mathf.Max(0f, remaining - Runner.DeltaTime);

        inventory.SetCandleRemaining(slot, remaining);

        if (remaining <= 0f)
        {
            CandleOn = false;

            Debug.Log(
                $"[CANDLE] Player {Object.InputAuthority}'s candle burned out."
            );
        }
    }

    public override void Render()
    {
        FindCurrentCandle();
        ApplyCandleVisual();
    }

    private void ToggleCandle()
    {
        if (!IsHoldingCandle)
        {
            Debug.Log("[CANDLE] Select the candle before pressing F.");
            return;
        }

        RPC_ToggleCandle();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_ToggleCandle()
    {
        if (!IsHoldingCandle)
        {
            CandleOn = false;
            return;
        }

        int slot = inventory.GetSelectedStorageSlot();

        if (!inventory.IsCandleInitialized(slot))
        {
            inventory.InitializeCandleSlot(
                slot,
                GetLifetimeForCurrentDifficulty()
            );
        }

        if (inventory.GetCandleRemaining(slot) <= 0f)
        {
            CandleOn = false;
            Debug.Log("[CANDLE] This candle has burned out.");
            return;
        }

        CandleOn = !CandleOn;

        Debug.Log(
            $"[CANDLE] {(CandleOn ? "ON" : "OFF")} | " +
            $"Remaining: {FormatTime(inventory.GetCandleRemaining(slot))}"
        );
    }

    private void FindCurrentCandle()
    {
        CandleVisual found = null;

        if (IsHoldingCandle)
        {
            CandleVisual[] candles =
                GetComponentsInChildren<CandleVisual>(true);

            foreach (CandleVisual candle in candles)
            {
                if (candle != null)
                {
                    found = candle;
                    break;
                }
            }
        }

        if (found != currentCandleVisual)
        {
            if (currentCandleVisual != null)
                currentCandleVisual.SetCandleState(false);

            currentCandleVisual = found;
        }
    }

    private void ApplyCandleVisual()
    {
        if (currentCandleVisual != null)
            currentCandleVisual.SetCandleState(IsCandleLit);
    }

    private float GetLifetimeForCurrentDifficulty()
    {
        NetworkGameManager manager = NetworkGameManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning(
                "[CANDLE] NetworkGameManager not found; using Normal duration."
            );

            return normalLifeMinutes * 60f;
        }

        switch (manager.Difficulty)
        {
            case GameDifficulty.Easy:
                return easyLifeMinutes * 60f;

            case GameDifficulty.Difficult:
                return difficultLifeMinutes * 60f;

            case GameDifficulty.Normal:
            default:
                return normalLifeMinutes * 60f;
        }
    }

    public string GetRemainingTimeText()
    {
        return FormatTime(RemainingSeconds);
    }

    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));

        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}