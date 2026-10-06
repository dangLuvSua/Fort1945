using Fusion;
using UnityEngine;

public class SoldierInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    [Header("Interaction Text")]
    [SerializeField] private string interactionMessage = "[E] TALK TO SOLDIER";

    [Header("UI")]
    [SerializeField] private InteractionPrompt interactionPrompt;

    private NetworkGameManager gameManager;

    private void Start()
    {
        gameManager = NetworkGameManager.Instance;

        HidePrompt();
    }

    private void Update()
    {
        // ---------------------------------------------------------
        // Find NetworkGameManager
        // ---------------------------------------------------------

        if (gameManager == null)
        {
            gameManager = NetworkGameManager.Instance;

            if (gameManager == null)
            {
                HidePrompt();
                return;
            }
        }

        // ---------------------------------------------------------
        // IMPORTANT:
        // Never read Networked properties before Spawned().
        // ---------------------------------------------------------

        if (!gameManager.IsNetworkStateReady)
        {
            HidePrompt();
            return;
        }

        // ---------------------------------------------------------
        // Interaction is only available after everyone
        // has entered the gate.
        // ---------------------------------------------------------

        if (!gameManager.AllPlayersEnteredGate)
        {
            HidePrompt();
            return;
        }

        // ---------------------------------------------------------
        // Interaction can only happen once.
        // ---------------------------------------------------------

        if (gameManager.SoldierInteractionStarted)
        {
            HidePrompt();
            return;
        }

        // ---------------------------------------------------------
        // ONLY THE HOST CAN INTERACT.
        // Other players will not see the prompt.
        // ---------------------------------------------------------

        if (!gameManager.IsHost)
        {
            HidePrompt();
            return;
        }

        // ---------------------------------------------------------
        // Get the local player's NetworkObject.
        // ---------------------------------------------------------

        NetworkObject localPlayer = GetLocalPlayer();

        if (localPlayer == null)
        {
            HidePrompt();
            return;
        }

        // ---------------------------------------------------------
        // Check distance between player and soldier.
        // ---------------------------------------------------------

        float distance = Vector3.Distance(
            localPlayer.transform.position,
            transform.position
        );

        if (distance <= interactionDistance)
        {
            ShowPrompt();

            // -----------------------------------------------------
            // Host presses E
            // -----------------------------------------------------

            if (Input.GetKeyDown(interactionKey))
            {
                Debug.Log("[SOLDIER INTERACTION] Host pressed E.");

                gameManager.RequestSoldierInteraction();

                HidePrompt();
            }
        }
        else
        {
            HidePrompt();
        }
    }

    // =============================================================
    // GET LOCAL PLAYER
    // =============================================================

    private NetworkObject GetLocalPlayer()
    {
        if (gameManager == null)
            return null;

        if (gameManager.Runner == null)
            return null;

        PlayerRef localPlayer = gameManager.Runner.LocalPlayer;

        if (!localPlayer.IsValid)
            return null;

        return gameManager.Runner.GetPlayerObject(localPlayer);
    }

    // =============================================================
    // SHOW PROMPT
    // =============================================================

    private void ShowPrompt()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.Show(interactionMessage);
        }
    }

    // =============================================================
    // HIDE PROMPT
    // =============================================================

    private void HidePrompt()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.Hide();
        }
    }
}