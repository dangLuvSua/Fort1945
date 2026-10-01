using System.Threading.Tasks;
using UnityEngine;
using Fusion;

public class DungeonSpawner : MonoBehaviour
{
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private DungeonGenerator dungeon;

    private async void Start()
    {
        var runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Single,
            SessionName = "DungeonTest",
            SceneManager = sceneManager
        });

        if (!result.Ok)
        {
            Debug.LogError("Runner failed: " + result.ShutdownReason);
            return;
        }

        // Hintayin matapos ang dungeon generation
        while (!dungeon.IsGenerated)
            await Task.Yield();

        dungeon.GetSpawn(0, out Vector3 pos, out Quaternion rot);
        Debug.Log("Spawning at: " + pos);

        NetworkObject player = runner.Spawn(playerPrefab, pos, rot, runner.LocalPlayer);
        player.GetComponent<PlayerController>().Teleport(pos, rot);
    }
}