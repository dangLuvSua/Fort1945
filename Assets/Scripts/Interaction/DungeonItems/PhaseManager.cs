using Fusion;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PhaseManager : NetworkBehaviour
{
    public static PhaseManager Instance { get; private set; }

    [Header("Phase Settings")]
    [SerializeField, Min(1)]
    private int startingPhase = 1;

    [SerializeField, Min(1)]
    private int totalPhases = 3;

    [Header("Ending")]
    [SerializeField, Min(0.1f)] private float endingFadeDuration = 1.5f;
    [SerializeField] private string endingSpawnPointName = "EndingSpawnPoint";
    private bool endingStarted;

    [Header("Testing (Play Mode)")]
    [SerializeField, Tooltip("Turn this on during Play Mode to trigger the all-phases-complete ending. Resets itself after triggering.")]
    private bool testCompletePhases;

    [SerializeField, Tooltip("Turn this on during Play Mode to directly test the white fade, return to Fort2, and ESP spawn placement. Resets itself after triggering.")]
    private bool testReturnToFort2;

    [Networked]
    public int CurrentPhase { get; private set; }

    [Networked]
    public NetworkBool PhaseInitialized { get; private set; }

    [Networked]
    public int PhotoCapturesThisPhase { get; private set; }

    [Networked]
    public NetworkBool SequenceComplete { get; private set; }

    [Networked]
    public NetworkBool PuzzlePiecesCollected { get; private set; }

    [Networked]
    public int PhaseItemsRequired { get; private set; }

    [Networked]
    public int PhaseItemsCollected { get; private set; }

    public override void Spawned()
    {
        Instance = this;

        if (Object.HasStateAuthority && !PhaseInitialized)
        {
            CurrentPhase = Mathf.Max(1, startingPhase);
            PhaseInitialized = true;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (testReturnToFort2)
        {
            testReturnToFort2 = false;
            if (Object == null || !Object.HasStateAuthority)
            {
                Debug.LogWarning("[PHASE TEST] Enable this toggle on the state-authority PhaseManager instance.", this);
                return;
            }

            SequenceComplete = true;
            RPC_BeginEnding(endingFadeDuration, endingSpawnPointName);
            return;
        }

        if (!testCompletePhases)
            return;

        testCompletePhases = false;
        if (Object == null || !Object.HasStateAuthority)
        {
            Debug.LogWarning("[PHASE TEST] Enable this toggle on the state-authority PhaseManager instance.", this);
            return;
        }

        if (SequenceComplete)
            return;

        CurrentPhase = Mathf.Max(1, totalPhases);
        PhaseInitialized = true;
        AdvancePhase();
    }

    public void SetPhase(int phase)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            Debug.LogWarning(
                "[PHASE] Only the state authority can change phases."
            );
            return;
        }

        phase = Mathf.Max(1, phase);

        if (CurrentPhase == phase && PhaseInitialized)
            return;

        CurrentPhase = phase;
        PhotoCapturesThisPhase = 0;
        PuzzlePiecesCollected = false;
        PhaseItemsRequired = 0;
        PhaseItemsCollected = 0;
        PhaseInitialized = true;

        Debug.Log($"[PHASE] Changed to Phase {CurrentPhase}.");
    }

    public void AdvancePhase()
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (SequenceComplete)
            return;

        if (CurrentPhase >= Mathf.Max(1, totalPhases))
        {
            SequenceComplete = true;
            Debug.Log("[PHASE] All mission phases completed.");
            RPC_ShowMission("ALL MISSIONS COMPLETE", "You completed every mission phase.");
            RPC_BeginEnding(endingFadeDuration, endingSpawnPointName);
            return;
        }

        SetPhase(CurrentPhase + 1);
    }

    /// <summary>Reports one successful camera capture to the state authority.</summary>
    public void ReportPhotoCaptured()
    {
        if (Object == null || !Object.IsValid)
            return;

        RPC_ReportPhotoCaptured();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportPhotoCaptured(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || SequenceComplete || CurrentPhase != 2)
            return;

        PhotoCapturesThisPhase++;
    }

    /// <summary>Completes a phase whose configured rule uses ExternalSignal.</summary>
    public void ReportExternalPhaseComplete()
    {
        if (Object == null || !Object.IsValid)
            return;

        RPC_ReportExternalPhaseComplete();
    }

    public void ReportPuzzlePiecesCollected()
    {
        if (Object == null || !Object.IsValid)
            return;

        RPC_ReportPuzzlePiecesCollected();
    }

    public void UpdateItemProgress(int required, int collected)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        PhaseItemsRequired = Mathf.Max(0, required);
        PhaseItemsCollected = Mathf.Clamp(collected, 0, PhaseItemsRequired);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportPuzzlePiecesCollected(RpcInfo info = default)
    {
        if (Object.HasStateAuthority && CurrentPhase == 2 && !SequenceComplete)
            PuzzlePiecesCollected = true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportExternalPhaseComplete(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || SequenceComplete ||
            CurrentPhase < Mathf.Max(1, totalPhases))
            return;

        AdvancePhase();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowMission(string title, string description)
    {
        MissionManager.ShowRaw(title, description, 0f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BeginEnding(float fadeDuration, string spawnPointName)
    {
        if (endingStarted)
            return;

        endingStarted = true;
        CreateEndingCrossGlow();
        EndingSceneTransition.Begin(fadeDuration, "EndingSpawnPoint");
    }

    private static void CreateEndingCrossGlow()
    {
        Transform cross = FindTransformByName("White Cross");
        if (cross == null)
            return;

        foreach (Renderer renderer in cross.GetComponentsInChildren<Renderer>())
        {
            foreach (Material material in renderer.materials)
            {
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color(3.5f, 3.5f, 3.5f, 1f));
                }
            }
        }

        Transform existing = cross.Find("EndingGlowParticles");
        if (existing != null)
            return;

        var particlesObject = new GameObject("EndingGlowParticles");
        particlesObject.transform.SetParent(cross, false);
        ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.startLifetime = 1.6f;
        main.startSpeed = 0.25f;
        main.startSize = 0.12f;
        main.startColor = new Color(1f, 0.92f, 0.55f, 0.9f);
        main.maxParticles = 80;
        var emission = particles.emission;
        emission.rateOverTime = 18f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.65f;
        var color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.82f, 0.35f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        particles.Play();
    }

    private IEnumerator PlayEndingTransition(float fadeDuration, string spawnPointName)
    {
        Image overlay = CreateFadeOverlay();
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOverlayAlpha(overlay, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        SetOverlayAlpha(overlay, 1f);
        if (Object != null && Object.HasStateAuthority)
        {
            Transform endingSpawn = FindTransformByName(spawnPointName);
            if (endingSpawn == null)
            {
                Debug.LogError($"[PHASE] Could not find ending spawn point '{spawnPointName}' in scene '{SceneManager.GetActiveScene().name}'.");
            }
            else
            {
                foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                    player.RpcTeleportToEnding(endingSpawn.position, endingSpawn.rotation);
            }
        }

        yield return new WaitForSecondsRealtime(0.35f);
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOverlayAlpha(overlay, 1f - Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        Destroy(overlay.canvas.gameObject);
    }

    private static Transform FindTransformByName(string targetName)
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (candidate.name == targetName)
                return candidate;
        return null;
    }

    private static Image CreateFadeOverlay()
    {
        var canvasObject = new GameObject("EndingWhiteFade", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

    private static void SetOverlayAlpha(Image image, float alpha)
    {
        if (image != null)
            image.color = new Color(1f, 1f, 1f, alpha);
    }
}
