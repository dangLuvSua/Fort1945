using UnityEngine;
using Fusion;

public class Spawner : MonoBehaviour
{
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private Transform spawnPoint;

    private async void Start()
    {
        var runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;

        var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Single,   // pang-test, walang internet o App ID na kailangan
            SessionName = "TestRoom",
            SceneManager = sceneManager
        });


        if (result.Ok)
        {
            runner.Spawn(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                runner.LocalPlayer
            );
        }
        else
        {
            Debug.LogError("Runner failed: " + result.ShutdownReason);
        }
    }
}