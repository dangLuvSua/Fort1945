using UnityEngine;

public class ChestOpener : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("Drag the 'top' child here.")]
    public Transform top;

    [Header("Opening")]
    [Tooltip("How many degrees the lid rotates on X when open.")]
    public float openAngle = 75f;

    [Tooltip("How fast the lid opens/closes (degrees per second).")]
    public float openSpeed = 120f;

    [Tooltip("Close the lid again when the player leaves the range.")]
    public bool closeWhenPlayerLeaves = true;

    [Header("Range")]
    public float range = 2f;

    [Tooltip("Move the center of the range (local space).")]
    public Vector3 rangeOffset = Vector3.zero;

    [Header("Debug")]
    public bool showGizmo = true;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen = false;
    private float checkTimer = 0f;

    // Para hindi mag-allocate ng memory kada check
    private readonly Collider[] hits = new Collider[16];

    private void Awake()
    {
        if (top == null)
        {
            Debug.LogError("ChestOpener: 'top' is not assigned.", this);
            enabled = false;
            return;
        }

        closedRotation = top.localRotation;
        openRotation = closedRotation * Quaternion.Euler(openAngle, 0f, 0f);
    }

    private void Update()
    {
        // Mag-check lang kada 0.1 seconds, hindi kada frame
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = 0.1f;
            bool playerNear = IsPlayerInRange();

            if (playerNear)
                isOpen = true;
            else if (closeWhenPlayerLeaves)
                isOpen = false;
        }

        // Smooth na pag-rotate papunta sa target
        Quaternion target = isOpen ? openRotation : closedRotation;

        top.localRotation = Quaternion.RotateTowards(
            top.localRotation,
            target,
            openSpeed * Time.deltaTime
        );
    }

    private bool IsPlayerInRange()
    {
        Vector3 center = transform.TransformPoint(rangeOffset);

        int count = Physics.OverlapSphereNonAlloc(
            center,
            range,
            hits,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            if (hits[i].GetComponentInParent<PlayerController>() != null)
                return true;
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        Vector3 center = transform.TransformPoint(rangeOffset);

        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(center, range);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, range);
    }
}