using UnityEngine;
using System.Collections;

public class NoteInspector : MonoBehaviour
{
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;
    private Transform originalParent;

    private bool isZoomed = false;
    private static bool isAnimating = false;

    void Start()
    {
        // Save the note's original position and parent relative to the board
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;
    }

    void OnMouseDown()
    {
        // Prevent clicking if another note is currently animating
        if (isAnimating) return;

        isZoomed = !isZoomed;
        StopAllCoroutines();
        StartCoroutine(AnimateNote(isZoomed));
    }

    IEnumerator AnimateNote(bool zoomIn)
    {
        isAnimating = true;
        float elapsed = 0f;
        float duration = 0.25f; // Speed of the zoom animation

        // Find the active camera dynamically in the scene
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
            // Unparent temporarily so it moves freely in world space
            transform.SetParent(null);

            // Position it right in front of the player's camera view
            targetPos = cam.transform.position + cam.transform.forward * 1.2f;
            
            // Make the note face directly toward the camera
            targetRot = Quaternion.LookRotation(transform.position - cam.transform.position) * Quaternion.Euler(0, 180, 0);
            
            // Enlarge the note so the text is fully readable
            targetScale = originalLocalScale * 2.5f; 
        }
        else
        {
            // Return the note back to its bulletin board parent
            transform.SetParent(originalParent);
            targetPos = originalParent.TransformPoint(originalLocalPosition);
            targetRot = originalParent.rotation * originalLocalRotation;
            targetScale = originalLocalScale;
        }

        // Smooth transition loop
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        // Snap to exact final values
        transform.position = targetPos;
        transform.rotation = targetRot;
        transform.localScale = targetScale;

        // Reset local coordinate precision when returned to board
        if (!zoomIn)
        {
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
            transform.localScale = originalLocalScale;
        }

        isAnimating = false;
    }
}
