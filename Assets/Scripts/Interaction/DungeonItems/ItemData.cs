using Fusion;
using UnityEngine;

public enum ItemCategory
{
    Item,
    Gallery,
    PuzzlePiece
}

[CreateAssetMenu(menuName = "Fort1945/Item")]
public class ItemData : ScriptableObject
{
    [Header("Network Identity")]
    [Tooltip("Must be unique for every ItemData asset.")]
    public int itemId;

    [Header("Display")]
    public string itemName;
    public Sprite icon;

    [TextArea]
    public string description;

    [Header("Category")]
    public ItemCategory category;

    [Header("World / Player Visuals")]
    public GameObject heldPrefab;

    [Header("Networked World Prefab")]
    public NetworkObject worldPrefab;
}