using UnityEngine;

public class PuzzleTarget : MonoBehaviour
{
    [Header("Required Puzzle Piece")]
    [SerializeField] private ItemData pieceItem;

    public ItemData PieceItem => pieceItem;

    public int ItemId
    {
        get
        {
            if (pieceItem == null)
                return 0;

            return pieceItem.itemId;
        }
    }

    public Vector3 TargetPosition => transform.position;

    public Quaternion TargetRotation => transform.rotation;
}