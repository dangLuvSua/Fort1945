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
    private bool previousAllPlayersEnteredGate;
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

        bool allPlayersEnteredGate =
            manager.AllPlayersEnteredGate;

        previousGameStarted =
            gameStarted;

        previousAllPlayersEnteredGate =
            allPlayersEnteredGate;

        gateInitialized = true;

        UpdateGate(gameStarted, allPlayersEnteredGate, gameStarted);
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

        bool allPlayersEnteredGate =
            manager.AllPlayersEnteredGate;

        // Initialize the gate if Spawned() happened before
        // NetworkGameManager became ready.
        if (!gateInitialized)
        {
            previousGameStarted =
                gameStarted;

            previousAllPlayersEnteredGate =
                allPlayersEnteredGate;

            gateInitialized = true;

            UpdateGate(gameStarted, allPlayersEnteredGate, gameStarted);

            return;
        }

        // Open when the game starts, then close once all players have entered.
        if (gameStarted != previousGameStarted ||
            allPlayersEnteredGate != previousAllPlayersEnteredGate)
        {
            bool shouldPlayOpenAnimation =
                gameStarted && !previousGameStarted;

            previousGameStarted =
                gameStarted;

            previousAllPlayersEnteredGate =
                allPlayersEnteredGate;

            UpdateGate(gameStarted, allPlayersEnteredGate, shouldPlayOpenAnimation);
        }
    }

    private void UpdateGate(
        bool gameStarted,
        bool allPlayersEnteredGate,
        bool playOpenAnimation)
    {
        // Keep the main gate blocked until the game starts, then block it
        // again after every player has crossed into the starting area.
        if (gateBlocker != null)
        {
            gateBlocker.SetActive(!gameStarted || allPlayersEnteredGate);
        }

        // Play the open animation when the game starts.
        if (playOpenAnimation &&
            gateAnimator != null)
        {
            gateAnimator.SetTrigger(
                openTriggerName
            );
        }
    }
}
