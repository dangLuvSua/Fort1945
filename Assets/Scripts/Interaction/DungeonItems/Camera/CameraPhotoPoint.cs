using UnityEngine;

/// <summary>
/// Place on a collider (or one of its parents) to substitute a fixed photo
/// when the camera is pointed at this location.
/// </summary>
public sealed class CameraPhotoPoint : MonoBehaviour
{
    [SerializeField] private Texture2D photo;

    public Texture2D Photo => photo;
}
