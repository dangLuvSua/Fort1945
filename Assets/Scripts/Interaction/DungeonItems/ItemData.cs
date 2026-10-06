using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public GameObject heldPrefab; // ito ang makikita sa kamay
    public GameObject worldPrefab; // ito ang lalabas sa lupa kapag dinrop
}