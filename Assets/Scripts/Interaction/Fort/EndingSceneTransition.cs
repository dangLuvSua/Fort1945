using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Persists the final white fade while Fusion changes from Dungeon to Fort2.</summary>
public sealed class EndingSceneTransition : MonoBehaviour
{
    private static EndingSceneTransition instance;
    private Image overlay;
    private float fadeDuration;
    private string spawnParentName;
    private bool sceneAuthority;

    public static void Begin(float duration, string spawnParent)
    {
        if (instance != null)
            return;

        var root = new GameObject("EndingSceneTransition");
        instance = root.AddComponent<EndingSceneTransition>();
        DontDestroyOnLoad(root);
        instance.fadeDuration = Mathf.Max(0.1f, duration);
        instance.spawnParentName = spawnParent;
        NetworkRunnerHandler handler = NetworkRunnerHandler.Instance;
        instance.sceneAuthority = handler != null && handler.IsSceneAuthority;
        instance.StartCoroutine(instance.Run());
    }

    private IEnumerator Run()
    {
        overlay = CreateOverlay();
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(elapsed / fadeDuration);
            yield return null;
        }
        SetAlpha(1f);

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

        yield return new WaitForSecondsRealtime(0.35f);
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(1f - elapsed / fadeDuration);
            yield return null;
        }
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

    private static Image CreateOverlay()
    {
        var canvasObject = new GameObject("EndingWhiteFadeCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(instance.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        var imageObject = new GameObject("White", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        return image;
    }

    private void SetAlpha(float alpha)
    {
        if (overlay != null)
            overlay.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
    }
}
