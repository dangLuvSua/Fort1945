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
    private bool gateInitialized;

    public override void Spawned()
    {
        NetworkGameManager manager =
            NetworkGameManager.Instance;

        // NetworkGameManager may exist as a Unity object
        // but may not have completed Fusion Spawned() yet.
        if (manager == null ||
            !manager.IsNetworkStateReady)
        {
            return;
        }

        bool gameStarted =
            manager.GameStarted;

        previousGameStarted =
            gameStarted;

        gateInitialized = true;

        UpdateGate(gameStarted);
    }

    private void Update()
    {
        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (manager == null)
            return;

        // IMPORTANT:
        // Do not access any [Networked] property until
        // NetworkGameManager.Spawned() has been called.
        if (!manager.IsNetworkStateReady)
            return;

        bool gameStarted =
            manager.GameStarted;

        // Initialize the gate if Spawned() happened before
        // NetworkGameManager became ready.
        if (!gateInitialized)
        {
            previousGameStarted =
                gameStarted;

            gateInitialized = true;

            UpdateGate(gameStarted);

            return;
        }

        // Only update the gate when GameStarted changes.
        if (gameStarted != previousGameStarted)
        {
            previousGameStarted =
                gameStarted;

            UpdateGate(gameStarted);
        }
    }

    private void UpdateGate(bool open)
    {
        // Block the gate until the game starts.
        if (gateBlocker != null)
        {
            gateBlocker.SetActive(!open);
        }

        // Open animation when the game starts.
        if (open &&
            gateAnimator != null)
        {
            gateAnimator.SetTrigger(
                openTriggerName
            );
        }
    }
}