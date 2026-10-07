using UnityEngine;

public class SurfaceIdentifier : MonoBehaviour
{
    [SerializeField]
    private SurfaceType surfaceType = SurfaceType.Default;

    public SurfaceType SurfaceType => surfaceType;
}