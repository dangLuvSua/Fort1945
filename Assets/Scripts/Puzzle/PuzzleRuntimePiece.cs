using UnityEngine;

public class PuzzleRuntimePiece : MonoBehaviour
{
    public ItemData Item { get; private set; }
    public Transform SolvedTarget { get; private set; }
    public bool IsLocked { get; private set; }

    private PuzzleTableController owner;

    public void Initialize(
        ItemData item,
        Transform solvedTarget,
        PuzzleTableController table)
    {
        Item = item;
        SolvedTarget = solvedTarget;
        owner = table;
        IsLocked = false;
    }

    public bool TrySnap(
        float positionTolerance,
        float rotationTolerance)
    {
        if (IsLocked || SolvedTarget == null)
            return false;

        float distance = Vector3.Distance(
            transform.position,
            SolvedTarget.position
        );

        float angle = Quaternion.Angle(
            transform.rotation,
            SolvedTarget.rotation
        );

        if (distance > positionTolerance ||
            angle > rotationTolerance)
        {
            return false;
        }

        transform.SetPositionAndRotation(
            SolvedTarget.position,
            SolvedTarget.rotation
        );

        IsLocked = true;

        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);

        foreach (Collider col in colliders)
        {
            if (col != null)
                col.enabled = false;
        }

        owner?.NotifyPieceSnapped(this);

        return true;
    }
}