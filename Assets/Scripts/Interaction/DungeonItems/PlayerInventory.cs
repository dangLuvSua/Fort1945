using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public bool HasRustKey { get; private set; }

    public void AddKey()
    {
        HasRustKey = true;
        Debug.Log("Nakuha na ang Rust Key!");
    }
}