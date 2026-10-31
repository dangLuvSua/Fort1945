using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Returns players to Fort2, plays the ending video, then sends them to the lobby.</summary>
public sealed class EndingSceneTransition : MonoBehaviour
{
    private static EndingSceneTransition instance;
    private string spawnParentName;
    private VideoClip endingVideo;
    private PlayerSpawner fortPlayerSpawner;
    private bool sceneAuthority;

    public static void Begin(string spawnParent, VideoClip video)
    {
        if (instance != null)
            return;

        var root = new GameObject("EndingSceneTransition");
        instance = root.AddComponent<EndingSceneTransition>();
        DontDestroyOnLoad(root);
        instance.spawnParentName = spawnParent;
        instance.endingVideo = video;
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

        // Wait for Fort2's ending markers and network player objects before
        // attempting the coordinated placement. Scene loading can finish
        // before Fusion has finished attaching every player object.
        float readinessTimeout = 20f;
        while (!IsFort2Ready() && readinessTimeout > 0f)
        {
            readinessTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!IsFort2Ready())
            Debug.LogWarning("[ENDING] Fort2 did not become fully ready before the 20 second timeout; continuing with available scene objects.");

        CreateEndingCrossGlow();
        NetworkRunnerHandler currentHandler = NetworkRunnerHandler.Instance;
        sceneAuthority = currentHandler != null && currentHandler.IsSceneAuthority;
        if (sceneAuthority)
        {
            Transform parent = FindNamedTransform(spawnParentName);
            PlayerSpawner playerSpawner = FindFirstObjectByType<PlayerSpawner>();
            fortPlayerSpawner = playerSpawner;
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

        // Keep the White Cross visible and glowing for the requested five
        // seconds after players are placed there.
        EyeBlinkTransition.Open();
        yield return new WaitForSecondsRealtime(5f);
        if (endingVideo != null)
            yield return PlayEndingVideo(endingVideo);
        else
            Debug.LogError("[ENDING] No ending video is assigned to PhaseManager; skipping video playback.");

        // Each client plays the clip locally. Only scene authority issues the
        // networked teleports, so everyone returns to the lobby together.
        if (sceneAuthority)
        {
            if (fortPlayerSpawner != null)
                fortPlayerSpawner.ReturnExistingPlayersToLobby();
            else
                Debug.LogError("[ENDING] Fort2 PlayerSpawner was not found; cannot return players to the lobby.");
        }

        yield return new WaitForSecondsRealtime(0.75f);
        Destroy(gameObject);
    }

    private IEnumerator PlayEndingVideo(VideoClip clip)
    {
        GameObject overlay = new GameObject("EndingVideoOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(RawImage));
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        RawImage image = overlay.GetComponent<RawImage>();
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        image.color = Color.white;
        image.raycastTarget = true;

        RenderTexture renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
        {
            name = "EndingVideoRenderTexture"
        };
        renderTexture.Create();
        image.texture = renderTexture;

        VideoPlayer player = overlay.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.source = VideoSource.VideoClip;
        player.clip = clip;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = renderTexture;
        player.audioOutputMode = VideoAudioOutputMode.Direct;

        bool finished = false;
        player.loopPointReached += _ => finished = true;
        player.errorReceived += (_, message) =>
        {
            Debug.LogError($"[ENDING] Video playback failed: {message}");
            finished = true;
        };

        player.Prepare();
        float prepareTimeout = 20f;
        while (!player.isPrepared && prepareTimeout > 0f)
        {
            prepareTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!player.isPrepared)
        {
            Debug.LogError("[ENDING] Timed out preparing the ending video.");
            finished = true;
        }
        else
        {
            player.Play();
        }

        float playbackTimeout = (float)clip.length + 15f;
        while (!finished && playbackTimeout > 0f)
        {
            playbackTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!finished)
            Debug.LogWarning("[ENDING] Ending video playback timed out; returning players to the lobby.");

        player.Stop();
        renderTexture.Release();
        Destroy(overlay);
        Destroy(renderTexture);
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

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private static Transform FindNamedTransform(string targetName)
    {
        foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (transform.name == targetName)
                return transform;
        return null;
    }

    private bool IsFort2Ready()
    {
        if (SceneManager.GetActiveScene().name != "Fort2")
            return false;

        Transform spawnParent = FindNamedTransform(spawnParentName);
        if (spawnParent == null ||
            spawnParent.Find("ESP_01") == null ||
            spawnParent.Find("ESP_02") == null ||
            spawnParent.Find("ESP_03") == null ||
            spawnParent.Find("ESP_04") == null ||
            FindFirstObjectByType<PlayerSpawner>() == null)
            return false;

        NetworkRunnerHandler handler = NetworkRunnerHandler.Instance;
        if (handler == null || !handler.IsSceneAuthority)
            return true;

        var runner = handler.GameRunner;
        if (runner == null)
            return false;

        foreach (var player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out var playerObject) ||
                playerObject == null ||
                !playerObject.TryGetComponent(out PlayerController _))
                return false;
        }

        return true;
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
