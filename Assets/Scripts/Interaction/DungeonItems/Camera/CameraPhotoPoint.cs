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

    /// <summary>Returns true only when every renderer bound is fully inside the camera frame.</summary>
    public bool IsWholeObjectInFrame(Camera camera)
    {
        if (camera == null)
            return false;

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

            Bounds bounds = targetRenderer.bounds;
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
        }

        return true;
    }
}
