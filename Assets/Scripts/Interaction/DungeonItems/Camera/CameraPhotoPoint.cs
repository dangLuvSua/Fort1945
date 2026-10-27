using UnityEngine;

/// <summary>
/// Place on a collider (or one of its parents) to substitute a fixed photo
/// when the camera is pointed at this location.
/// </summary>
public sealed class CameraPhotoPoint : MonoBehaviour
{
    [SerializeField] private Texture2D photo;
    [Tooltip("The complete object that must fit inside the camera view. Defaults to this object's hierarchy.")]
    [SerializeField] private GameObject objectThatMustBeVisible;

    public Texture2D Photo => photo;

    [Header("Proximity Voice")]
    [SerializeField] private AudioClip proximityVoice;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.8f;
    [SerializeField, Min(0.1f)] private float voiceTriggerDistance = 5f;
    [SerializeField, Min(0.1f)] private float voiceResetDistance = 6f;
    private bool voiceArmed = true;
    private AudioSource voiceSource;
    private float nextVoiceTime;

    // Called only by the phase manager's state authority.
    public bool ShouldPlayProximityVoice(Fusion.NetworkRunner runner)
    {
        if (proximityVoice == null || photo == null)
            return false;
        bool playerNear = false;
        bool playerWithinReset = false;
        float resetDistance = Mathf.Max(voiceTriggerDistance + 0.1f, voiceResetDistance);
        foreach (Fusion.PlayerRef player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out Fusion.NetworkObject playerObject) || playerObject == null)
                continue;
            float distance = Vector3.Distance(playerObject.transform.position, PhotoAimPosition);
            playerNear |= distance <= voiceTriggerDistance;
            playerWithinReset |= distance <= resetDistance;
        }
        if (!playerWithinReset)
            voiceArmed = true;
        if (!voiceArmed || !playerNear || Time.unscaledTime < nextVoiceTime)
            return false;
        voiceArmed = false;
        nextVoiceTime = Time.unscaledTime + proximityVoice.length;
        return true;
    }

    public void PlayProximityVoice()
    {
        if (proximityVoice == null)
            return;
        if (voiceSource == null)
        {
            GameObject voiceObject = new GameObject("Character Proximity Voice");
            voiceObject.transform.SetParent(transform, false);
            voiceObject.transform.position = PhotoAimPosition;
            voiceSource = voiceObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
            // The shared event starts on every client; each listener hears
            // distance attenuation locally, reaching silence at the reset radius.
            voiceSource.spatialBlend = 1f;
            voiceSource.rolloffMode = AudioRolloffMode.Linear;
            voiceSource.maxDistance = Mathf.Max(voiceTriggerDistance + 0.1f, voiceResetDistance);
            voiceSource.minDistance = Mathf.Min(1f, voiceSource.maxDistance * 0.5f);
            voiceSource.dopplerLevel = 0f;
        }
        if (!voiceSource.isPlaying)
            voiceSource.PlayOneShot(proximityVoice, voiceVolume);
    }

    private void LateUpdate()
    {
        if (voiceSource != null && voiceSource.isPlaying)
            voiceSource.transform.position = PhotoAimPosition;
    }

    public bool IsValidMissionCapture(Camera camera, float maximumDistance = 5f,
        float minimumFrameHeight = 0.25f)
    {
        BoxCollider photoTrigger = GetPhotoTrigger();
        if (camera == null || photo == null ||
            Vector3.Distance(camera.transform.position, CaptureAimPosition) > maximumDistance ||
            !IsWholeObjectInFrame(camera) ||
            (photoTrigger == null && !OccupiesEnoughOfFrame(camera, minimumFrameHeight)))
            return false;

        GameObject target = objectThatMustBeVisible != null ? objectThatMustBeVisible : gameObject;
        PlayerController photographer = camera.GetComponentInParent<PlayerController>();
        // Test several points on the subject: a narrow cell bar may obscure
        // its center while the character is still visible through the gaps.
        Bounds subject = photoTrigger != null ? photoTrigger.bounds : default;
        bool hasSubject = photoTrigger != null;
        if (!hasSubject)
            hasSubject = TryGetVisibleSubjectBounds(out subject);
        if (hasSubject)
        {
            Vector3 side = camera.transform.right * subject.extents.magnitude * 0.2f;
            Vector3 up = Vector3.up * subject.extents.y * 0.5f;
            return HasClearView(camera, subject.center, target, photographer) ||
                HasClearView(camera, subject.center + up, target, photographer) ||
                HasClearView(camera, subject.center - up, target, photographer) ||
                HasClearView(camera, subject.center + side, target, photographer) ||
                HasClearView(camera, subject.center - side, target, photographer);
        }
        return HasClearView(camera, CaptureAimPosition, target, photographer);
    }

    private BoxCollider GetPhotoTrigger()
    {
        foreach (BoxCollider box in GetComponentsInChildren<BoxCollider>())
        {
            if (box.enabled && box.isTrigger && box.gameObject.activeInHierarchy &&
                box.name == "PhotoTrigger")
                return box;
        }
        return null;
    }

    public Vector3 CaptureAimPosition
    {
        get
        {
            BoxCollider box = GetPhotoTrigger();
            return box != null ? box.transform.TransformPoint(box.center) : PhotoAimPosition;
        }
    }

    private bool HasClearView(Camera camera, Vector3 point, GameObject target, PlayerController photographer)
    {
        Vector3 direction = point - camera.transform.position;
        foreach (RaycastHit hit in Physics.RaycastAll(camera.transform.position,
                     direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (photographer != null && hit.transform.IsChildOf(photographer.transform))
                continue;
            if (hit.transform.IsChildOf(target.transform) || hit.transform.IsChildOf(transform))
                continue;
            return false;
        }
        return true;
    }

    private bool TryGetVisibleSubjectBounds(out Bounds bounds)
    {
        GameObject target = objectThatMustBeVisible != null ? objectThatMustBeVisible : gameObject;
        bounds = default;
        bool found = false;
        foreach (Renderer surface in target.GetComponentsInChildren<Renderer>())
        {
            if (!surface.enabled || surface is ParticleSystemRenderer)
                continue;
            if (found)
                bounds.Encapsulate(surface.bounds);
            else
                bounds = surface.bounds;
            found = true;
        }
        return found;
    }

    private bool OccupiesEnoughOfFrame(Camera camera, float minimumFrameHeight)
    {
        GameObject target = objectThatMustBeVisible != null ? objectThatMustBeVisible : gameObject;
        Bounds bounds = default;
        bool hasBounds = false;
        // Prefer the visible character geometry rather than a large photo trigger.
        foreach (Renderer surface in target.GetComponentsInChildren<Renderer>())
        {
            if (!surface.enabled || surface is ParticleSystemRenderer)
                continue;
            if (hasBounds)
                bounds.Encapsulate(surface.bounds);
            else
                bounds = surface.bounds;
            hasBounds = true;
        }
        // Some targets are trigger-only markers with no renderer hierarchy.
        if (!hasBounds)
        {
            foreach (Collider marker in GetComponentsInChildren<Collider>())
            {
                if (!marker.enabled || !marker.isTrigger)
                    continue;
                if (hasBounds)
                    bounds.Encapsulate(marker.bounds);
                else
                    bounds = marker.bounds;
                hasBounds = true;
            }
        }
        if (!hasBounds)
            return false;

        float lowest = float.PositiveInfinity;
        float highest = float.NegativeInfinity;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 viewport = camera.WorldToViewportPoint(new Vector3(
                (corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y,
                (corner & 4) == 0 ? min.z : max.z));
            if (viewport.z <= camera.nearClipPlane)
                return false;
            lowest = Mathf.Min(lowest, viewport.y);
            highest = Mathf.Max(highest, viewport.y);
        }
        return highest - lowest >= minimumFrameHeight;
    }

    public Vector3 PhotoAimPosition
    {
        get
        {
            if (TryGetVisibleSubjectBounds(out Bounds subject))
                return subject.center;
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider photoCollider in colliders)
            {
                if (photoCollider != null && photoCollider.enabled && photoCollider.isTrigger)
                    return photoCollider.bounds.center;
            }

            return transform.position;
        }
    }

    /// <summary>Returns true only when this photo point's trigger is fully inside the camera frame.</summary>
    public bool IsWholeObjectInFrame(Camera camera)
    {
        if (camera == null)
            return false;

        BoxCollider box = GetPhotoTrigger();
        if (box != null)
        {
            // Project the actual oriented box, including scaled/rotated face markers.
            Vector3 half = box.size * 0.5f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = box.center + new Vector3(
                    (corner & 1) == 0 ? -half.x : half.x,
                    (corner & 2) == 0 ? -half.y : half.y,
                    (corner & 4) == 0 ? -half.z : half.z);
                Vector3 viewport = camera.WorldToViewportPoint(box.transform.TransformPoint(local));
                if (viewport.z < camera.nearClipPlane || viewport.z > camera.farClipPlane ||
                    viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                    return false;
            }
            return true;
        }

        if (TryGetVisibleSubjectBounds(out Bounds subject))
            return BoundsAreFullyInFrame(camera, subject);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        bool hasTrigger = false;
        foreach (Collider photoCollider in colliders)
        {
            if (photoCollider == null || !photoCollider.enabled || !photoCollider.isTrigger)
                continue;

            hasTrigger = true;
            if (!BoundsAreFullyInFrame(camera, photoCollider.bounds))
                return false;
        }

        if (hasTrigger)
            return true;

        GameObject target = objectThatMustBeVisible != null
            ? objectThatMustBeVisible
            : gameObject;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return false;

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null || !targetRenderer.enabled)
                return false;

            if (!BoundsAreFullyInFrame(camera, targetRenderer.bounds))
                return false;
        }

        return true;
    }

    private static bool BoundsAreFullyInFrame(Camera camera, Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 worldCorner = new Vector3(
                (corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y,
                (corner & 4) == 0 ? min.z : max.z);
            Vector3 viewport = camera.WorldToViewportPoint(worldCorner);

            if (viewport.z < camera.nearClipPlane ||
                viewport.z > camera.farClipPlane ||
                viewport.x < 0f || viewport.x > 1f ||
                viewport.y < 0f || viewport.y > 1f)
                return false;
        }

        return true;
    }
}
