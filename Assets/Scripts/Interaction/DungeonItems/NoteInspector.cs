using UnityEngine;
using System.Collections;

public class NoteInspector : MonoBehaviour
{
    [Header("Zoom Configuration")]
    [Tooltip("Distance from the camera when inspected. 0.5 - 0.7 keeps it in front of nearby props.")]
    [SerializeField] private float zoomDistance = 0.65f;

    [Tooltip("Scale multiplier when inspected. 1.0 - 1.2 is usually enough because moving closer already enlarges it.")]
    [SerializeField] private float zoomScaleMultiplier = 1.15f;

    [Tooltip("Duration of the smooth zoom transition.")]
    [SerializeField] private float animationDuration = 0.25f;

    [Tooltip("Set rotation offset if your model's front face is inverted (e.g., (0, 180, 0)).")]
    [SerializeField] private Vector3 rotationOffset = new Vector3(0, 180, 0);

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;
    private Transform originalParent;
    private int originalSortingOrder;

    private SpriteRenderer spriteRenderer;
    private Collider noteCollider;

    private bool isZoomed = false;
    private static bool isAnimating = false;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        noteCollider = GetComponent<Collider>();
    }

    void Start()
    {
        // Save the note's original state on the board
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;

        if (spriteRenderer != null)
        {
            originalSortingOrder = spriteRenderer.sortingOrder;
        }
    }

    void OnMouseDown()
    {
        if (isAnimating) return;

        isZoomed = !isZoomed;
        StopAllCoroutines();
        StartCoroutine(AnimateNote(isZoomed));
    }

    IEnumerator AnimateNote(bool zoomIn)
    {
        isAnimating = true;
        float elapsed = 0f;

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("No Main Camera found in the scene! Ensure your camera is tagged as 'MainCamera'.");
            isAnimating = false;
            yield break;
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 startScale = transform.localScale;

        Vector3 targetPos;
        Quaternion targetRot;
        Vector3 targetScale;

        if (zoomIn)
        {
            // Unparent to move freely in world space
            transform.SetParent(null);

            // 1. Position closer to camera (0.65m) so it is in front of desks/items
            targetPos = cam.transform.position + (cam.transform.forward * zoomDistance);

            // 2. Make it face directly flat to the camera view
            targetRot = cam.transform.rotation * Quaternion.Euler(rotationOffset);

            // 3. Reasonable scale so it fits nicely inside the screen
            targetScale = originalLocalScale * zoomScaleMultiplier;

            // If 2D Sprite, bring sorting order to the very front
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = 999;
            }
        }
        else
        {
            // Return back to board
            transform.SetParent(originalParent);
            targetPos = originalParent.TransformPoint(originalLocalPosition);
            targetRot = originalParent.rotation * originalLocalRotation;
            targetScale = originalLocalScale;

            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = originalSortingOrder;
            }
        }

        // Smooth transition
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        // Snap to exact values
        transform.position = targetPos;
        transform.rotation = targetRot;
        transform.localScale = targetScale;

        if (!zoomIn)
        {
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
            transform.localScale = originalLocalScale;
        }

        isAnimating = false;
    }
}
