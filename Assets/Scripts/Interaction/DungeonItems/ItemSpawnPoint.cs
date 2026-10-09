using UnityEngine;

public class ItemSpawnPoint : MonoBehaviour
{
    public bool canSpawnItems = true;

    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;
}