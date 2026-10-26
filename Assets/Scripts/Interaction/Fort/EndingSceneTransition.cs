using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Keeps the eye-blink closed while Fusion changes from Dungeon to Fort2.</summary>
public sealed class EndingSceneTransition : MonoBehaviour
{
    private static EndingSceneTransition instance;
    private string spawnParentName;
    private bool sceneAuthority;

    public static void Begin(string spawnParent)
    {
        if (instance != null)
            return;

        var root = new GameObject("EndingSceneTransition");
        instance = root.AddComponent<EndingSceneTransition>();
        DontDestroyOnLoad(root);
        instance.spawnParentName = spawnParent;
        NetworkRunnerHandler handler = NetworkRunnerHandler.Instance;
        instance.sceneAuthority = handler != null && handler.IsSceneAuthority;
        instance.StartCoroutine(instance.Run());
    }

    private IEnumerator Run()
    {
        float timeout = 30f;
        bool loadRequested = false;
        while (SceneManager.GetActiveScene().name != "Fort2" && timeout > 0f)
        {
            // Resolve the scene authority after the fade, rather than caching
            // it at Begin(); authority can be assigned or migrate as the
            // network session finishes initializing.
            NetworkRunnerHandler handler = NetworkRunnerHandler.Instance;
            if (!loadRequested && handler != null && handler.IsSceneAuthority)
            {
                loadRequested = handler.LoadFort2Scene();
                if (!loadRequested)
                    Debug.LogError("[ENDING] Scene authority could not start the Fusion load for Fort2.");
            }

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (SceneManager.GetActiveScene().name != "Fort2")
        {
            NetworkRunnerHandler handler = NetworkRunnerHandler.Instance;
            Debug.LogError($"[ENDING] Timed out waiting for Fort2. Active scene: '{SceneManager.GetActiveScene().name}', load requested: {loadRequested}, scene authority: {(handler != null && handler.IsSceneAuthority)}.");
            Destroy(gameObject);
            yield break;
        }

        // Allow scene objects and network players to finish initializing.
        yield return null;
        yield return null;
        CreateEndingCrossGlow();
        NetworkRunnerHandler currentHandler = NetworkRunnerHandler.Instance;
        sceneAuthority = currentHandler != null && currentHandler.IsSceneAuthority;
        if (sceneAuthority)
        {
            Transform parent = FindNamedTransform(spawnParentName);
            PlayerSpawner playerSpawner = FindFirstObjectByType<PlayerSpawner>();
            if (parent == null)
            {
                Debug.LogError($"[ENDING] Spawn group '{spawnParentName}' was not found in Fort2.");
            }
            else if (playerSpawner == null)
            {
                Debug.LogError("[ENDING] Fort2 PlayerSpawner was not found; cannot return players to ending spawns.");
            }
            else
            {
                playerSpawner.PlaceExistingPlayersAt(parent);
            }
        }

        DisableFortGameplaySystems();

        yield return new WaitForSecondsRealtime(0.75f);
        Destroy(gameObject);
    }

    private static void DisableFortGameplaySystems()
    {
        DisableComponents<FortMissionItemSpawner>();
        DisableComponents<MissionItemSpawnArea>();
        DisableComponents<DungeonGateUnlock>();
        DisableComponents<LockedAreaTrigger>();
        DisableComponents<DungeonSceneTeleportTrigger>();
        DisableComponents<GateController>();
        DisableComponents<SoldierInteraction>();
        DisableComponents<SoldierGuidePrompt>();
        DisableComponents<PlayerSpawner>();
        Debug.Log("[ENDING] Disabled Fort2 gameplay spawners, mission interactions, and gate triggers after returning to the cross.");
    }

    private static void DisableComponents<T>() where T : Behaviour
    {
        foreach (T component in FindObjectsByType<T>(FindObjectsSortMode.None))
            component.enabled = false;
    }

    private static Transform FindNamedTransform(string targetName)
    {
        foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (transform.name == targetName)
                return transform;
        return null;
    }

    private static void CreateEndingCrossGlow()
    {
        Transform cross = FindNamedTransform("White Cross");
        if (cross == null)
        {
            Debug.LogWarning("[ENDING] Could not find the White Cross to apply its glow.");
            return;
        }

        foreach (Renderer renderer in cross.GetComponentsInChildren<Renderer>())
        {
            foreach (Material material in renderer.materials)
            {
                if (!material.HasProperty("_EmissionColor"))
                    continue;

                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(3.5f, 3.5f, 3.5f, 1f));
            }
        }

        if (cross.Find("EndingGlowLight") == null)
        {
            GameObject lightObject = new GameObject("EndingGlowLight");
            lightObject.transform.SetParent(cross, false);
            lightObject.transform.localPosition = Vector3.up * 1.2f;
            Light glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.88f, 0.62f);
            glow.intensity = 2.5f;
            glow.range = 5f;
        }

        if (cross.Find("EndingGlowParticles") != null)
            return;

        GameObject particlesObject = new GameObject("EndingGlowParticles");
        particlesObject.transform.SetParent(cross, false);
        ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = 1.6f;
        main.startSpeed = 0.25f;
        main.startSize = 0.12f;
        main.startColor = new Color(1f, 0.92f, 0.55f, 0.9f);
        main.maxParticles = 80;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 18f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.65f;
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.82f, 0.35f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.8f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;
        particles.Play();
    }

}
