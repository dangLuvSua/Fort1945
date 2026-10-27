using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Local visual overlay for collectible world items. Adds no network state.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Fort1945/Items/Pickup Highlight")]
public sealed class PickupHighlight : MonoBehaviour
{
    public const string DefaultMaterialPath = "PickupHighlight/PickupGoldRim";

    [SerializeField, Tooltip("Shared rim material. Falls back to the default Resources material when empty.")]
    private Material rimMaterial;

    [SerializeField, Tooltip("Leave empty to find child mesh renderers automatically. Particles and UI are ignored.")]
    private Renderer[] targetRenderers;

    [SerializeField, Tooltip("Only highlight an initialized, collectible network pickup. Turn off for a standalone visual test.")]
    private bool requireCollectiblePickup = true;

    [SerializeField] private bool highlightEnabled = true;

    private readonly List<Overlay> overlays = new List<Overlay>();
    private ItemPickup itemPickup;
    private KeyPickup keyPickup;
    private bool built;

    private sealed class Overlay
    {
        public Renderer source;
        public Renderer renderer;
        public Mesh mesh;
    }

    private void Awake()
    {
        itemPickup = GetComponent<ItemPickup>();
        keyPickup = GetComponent<KeyPickup>();
    }

    private void LateUpdate()
    {
        bool visible = highlightEnabled && IsAvailable();
        if (visible && !built)
            BuildOverlays();

        foreach (Overlay overlay in overlays)
        {
            if (overlay.renderer == null)
                continue;

            Renderer source = overlay.source;
            overlay.renderer.enabled = visible && source != null &&
                source.enabled && !source.forceRenderingOff && source.gameObject.activeInHierarchy;

            if (!overlay.renderer.enabled)
                continue;

            overlay.renderer.gameObject.layer = source.gameObject.layer;
            overlay.renderer.renderingLayerMask = source.renderingLayerMask;

            if (source is SkinnedMeshRenderer skinned &&
                overlay.renderer is SkinnedMeshRenderer rim)
            {
                rim.localBounds = skinned.localBounds;
                for (int i = 0; i < overlay.mesh.blendShapeCount; i++)
                    rim.SetBlendShapeWeight(i, skinned.GetBlendShapeWeight(i));
            }
        }
    }

    /// <summary>Controls this instance without changing the shared material.</summary>
    public void SetHighlighted(bool value)
    {
        highlightEnabled = value;
        if (!value)
            HideOverlays();
    }

    private bool IsAvailable()
    {
        if (!requireCollectiblePickup)
            return true;

        // Guard networked getters: held clones and unspawned prefabs have no state buffer.
        if (itemPickup != null && itemPickup.isActiveAndEnabled &&
            itemPickup.Object != null && itemPickup.Object.IsValid)
            return itemPickup.NetworkItemId > 0 && itemPickup.CanCollect;

        if (keyPickup != null && keyPickup.isActiveAndEnabled &&
            keyPickup.Object != null && keyPickup.Object.IsValid)
            return keyPickup.NetworkItemId > 0 && keyPickup.CanCollect;

        return false;
    }

    private void BuildOverlays()
    {
        built = true;
        Material material = rimMaterial != null
            ? rimMaterial
            : Resources.Load<Material>(DefaultMaterialPath);

        if (material == null)
        {
            Debug.LogWarning("[PICKUP HIGHLIGHT] Assign a rim material or restore the default Resources material.", this);
            return;
        }

        // Snapshot before creating children so overlays can never become their own sources.
        Renderer[] sources = targetRenderers != null && targetRenderers.Length > 0
            ? targetRenderers
            : GetComponentsInChildren<Renderer>(true);
        var seen = new HashSet<Renderer>();

        foreach (Renderer source in sources)
        {
            if (source == null || !seen.Add(source) ||
                source.GetComponentInParent<PickupHighlight>() != this)
                continue;

            Mesh mesh = null;
            if (source is SkinnedMeshRenderer skinned)
                mesh = skinned.sharedMesh;
            else if (source is MeshRenderer && source.TryGetComponent(out MeshFilter filter))
                mesh = filter.sharedMesh;

            if (mesh == null || mesh.subMeshCount == 0)
                continue;

            var child = new GameObject("Pickup Gold Rim")
            {
                layer = source.gameObject.layer,
                hideFlags = HideFlags.DontSave
            };
            child.transform.SetParent(source.transform, false);

            Renderer renderer;
            if (source is SkinnedMeshRenderer skin)
            {
                var rim = child.AddComponent<SkinnedMeshRenderer>();
                rim.sharedMesh = mesh;
                rim.bones = skin.bones;
                rim.rootBone = skin.rootBone;
                rim.localBounds = skin.localBounds;
                rim.quality = skin.quality;
                rim.updateWhenOffscreen = skin.updateWhenOffscreen;
                renderer = rim;
            }
            else
            {
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = child.AddComponent<MeshRenderer>();
            }

            // One entry per submesh, so multi-material models get complete coverage.
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.enabled = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.renderingLayerMask = source.renderingLayerMask;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            overlays.Add(new Overlay { source = source, renderer = renderer, mesh = mesh });
        }
    }

    private void HideOverlays()
    {
        foreach (Overlay overlay in overlays)
            if (overlay.renderer != null)
                overlay.renderer.enabled = false;
    }

    private void OnDisable() => HideOverlays();

    private void OnDestroy()
    {
        foreach (Overlay overlay in overlays)
            if (overlay.renderer != null)
            {
                if (Application.isPlaying)
                    Destroy(overlay.renderer.gameObject);
                else
                    DestroyImmediate(overlay.renderer.gameObject);
            }
        overlays.Clear();
    }
}
