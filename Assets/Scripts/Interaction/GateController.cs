using Fusion;
using UnityEngine;

public class GateController : NetworkBehaviour
{
    [Header("Gate")]
    [SerializeField] private GameObject gateBlocker;

    [Header("Optional")]
    [SerializeField] private Animator gateAnimator;

    [SerializeField] private string openTriggerName = "Open";

    private bool previousGameStarted;

    public override void Spawned()
    {
        UpdateGate(
            NetworkGameManager.Instance != null &&
            NetworkGameManager.Instance.GameStarted
        );
    }

    private void Update()
    {
        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager == null)
            return;

        bool gameStarted =
            manager.GameStarted;

        if (gameStarted != previousGameStarted)
        {
            previousGameStarted =
                gameStarted;

            UpdateGate(gameStarted);
        }
    }

    private void UpdateGate(bool open)
    {
        if (gateBlocker != null)
        {
            gateBlocker.SetActive(!open);
        }

        if (open &&
            gateAnimator != null)
        {
            gateAnimator.SetTrigger(
                openTriggerName
            );
        }
    }
}