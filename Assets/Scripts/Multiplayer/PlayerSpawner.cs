using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [SerializeField]
    private NetworkObject playerPrefab;

    public void PlayerJoined(PlayerRef player)
    {
        if (player != Runner.LocalPlayer)
            return;

        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (player.PlayerId == 1)
        {
            // Player 1
            spawnPosition = new Vector3(-3f, 1f, 0f);

            // Face toward Player 2 (+Z)
            spawnRotation = Quaternion.Euler(0f, 90f, 0f);
        }
        else if (player.PlayerId == 2)
        {
            // Player 2
            spawnPosition = new Vector3(3f, 1f, 0f);

            // Face toward Player 1 (-X)
            spawnRotation = Quaternion.Euler(0f, -90f, 0f);
        }
        else
        {
            // Extra players
            spawnPosition = new Vector3((player.PlayerId - 1) * 3f, 1f, 0f);
            spawnRotation = Quaternion.identity;
        }

        Runner.Spawn(
            playerPrefab,
            spawnPosition,
            spawnRotation,
            player
        );

        Debug.Log(
            $"SPAWNED {player} | Position: {spawnPosition} | Rotation: {spawnRotation.eulerAngles}"
        );
    }
}