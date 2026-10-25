
using UnityEngine;
using System.Collections;

public class NoteInspector : MonoBehaviour, IInteractable
{
    public string Prompt => "Click to read the clue";
    public bool CanInteract => isActiveAndEnabled && originalRenderer != null &&
        activeNote == null && !isZoomed && !isAnimating;
    public int Priority => 10;

    public void Interact(PlayerInventory inventory)
    {
        // This interface supplies the hover prompt. OnMouseDown retains
        // the note's existing click-to-read behavior.
    }

    [Header("Zoom Configuration")]
    [SerializeField] private float zoomDistance = 0.8f;
    [SerializeField] private float zoomScaleMultiplier = 0.5f;
    [SerializeField] private float animationDuration = 0.25f;

    [Header("Sprite Rotation")]
    [SerializeField] private Vector3 rotationOffset =
        new Vector3(0, 180, 0);

    [Header("Camera Follow")]
    [SerializeField] private bool followCamera = true;

    [Header("Controls")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private SpriteRenderer originalRenderer;

    private GameObject inspectionCopy;
    private Transform inspectionTransform;
    private SpriteRenderer inspectionRenderer;

    private Camera activeCamera;
    private Coroutine animationCoroutine;

    private bool isZoomed = false;
    private bool isAnimating = false;

    private int openedFrame = -1;

    // Only one note can be inspected per game client.
    private static NoteInspector activeNote;

    void Awake()
    {
        originalRenderer = GetComponent<SpriteRenderer>();

        if (originalRenderer == null)
        {
            Debug.LogWarning(
                "NoteInspector: SpriteRenderer is missing!"
            );
        }
    }

    void OnMouseDown()
    {
        // Prevent opening another note while inspecting.
        if (activeNote != null && activeNote != this)
            return;

        if (isAnimating)
            return;

        if (!isZoomed)
        {
            Camera cam = Camera.main;

            if (cam == null)
            {
                Debug.LogWarning(
                    "NoteInspector: Main Camera not found!"
                );
                return;
            }

            if (originalRenderer == null)
                return;

            OpenNote(cam);
        }
        else
        {
            CloseNote();
        }
    }

    void Update()
    {
        if (!isZoomed || isAnimating)
            return;

        // Press ESC to close.
        if (Input.GetKeyDown(closeKey))
        {
            CloseNote();
            return;
        }

        // Click again to close.
        if (Input.GetMouseButtonDown(0) &&
            Time.frameCount > openedFrame)
        {
            CloseNote();
        }
    }

    void LateUpdate()
    {
        if (!isZoomed ||
            isAnimating ||
            !followCamera ||
            inspectionTransform == null ||
            activeCamera == null)
            return;

        // Keep the inspected note in front of the camera.
        inspectionTransform.position =
            GetCameraTargetPosition();

        inspectionTransform.rotation =
            GetCameraTargetRotation();
    }

    void OpenNote(Camera cam)
    {
        activeCamera = cam;

        // Create a LOCAL copy.
        // This object is NOT network spawned.
        inspectionCopy = new GameObject(
            "LOCAL_NoteInspection"
        );

        inspectionTransform = inspectionCopy.transform;

        // Start at the original note's position.
        inspectionTransform.position = transform.position;
        inspectionTransform.rotation = transform.rotation;
        inspectionTransform.localScale = transform.lossyScale;

        // Add a SpriteRenderer to the local copy.
        inspectionRenderer =
            inspectionCopy.AddComponent<SpriteRenderer>();

        // Copy sprite appearance.
        inspectionRenderer.sprite = originalRenderer.sprite;
        inspectionRenderer.color = originalRenderer.color;

        inspectionRenderer.flipX = originalRenderer.flipX;
        inspectionRenderer.flipY = originalRenderer.flipY;

        inspectionRenderer.sharedMaterial =
            originalRenderer.sharedMaterial;

        inspectionRenderer.sortingLayerID =
            originalRenderer.sortingLayerID;

        inspectionRenderer.sortingOrder = 999;

        // No NetworkObject, NetworkTransform, or RPC.
        isZoomed = true;
        activeNote = this;

        // Prevent the opening click from closing it.
        openedFrame = Time.frameCount;

        animationCoroutine =
            StartCoroutine(AnimateInspection(true));
    }

    void CloseNote()
    {
        if (!isZoomed || isAnimating)
            return;

        isZoomed = false;

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        animationCoroutine =
            StartCoroutine(AnimateInspection(false));
    }

    IEnumerator AnimateInspection(bool opening)
    {
        isAnimating = true;

        if (inspectionTransform == null)
        {
            isAnimating = false;
            yield break;
        }

        Vector3 startPos = inspectionTransform.position;
        Quaternion startRot = inspectionTransform.rotation;
        Vector3 startScale = inspectionTransform.localScale;

        Vector3 targetPos;
        Quaternion targetRot;
        Vector3 targetScale;

        if (opening)
        {
            targetPos = GetCameraTargetPosition();
            targetRot = GetCameraTargetRotation();

            targetScale =
                transform.lossyScale * zoomScaleMultiplier;
        }
        else
        {
            // Return the local copy to the original note.
            targetPos = transform.position;
            targetRot = transform.rotation;
            targetScale = transform.lossyScale;
        }

        float elapsed = 0f;

        float duration = Mathf.Max(
            0.001f,
            animationDuration
        );

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / duration)
            );

            // Update target while camera moves.
            if (opening && activeCamera != null)
            {
                targetPos = GetCameraTargetPosition();
                targetRot = GetCameraTargetRotation();
            }

            inspectionTransform.position =
                Vector3.Lerp(startPos, targetPos, t);

            inspectionTransform.rotation =
                Quaternion.Slerp(startRot, targetRot, t);

            inspectionTransform.localScale =
                Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        if (inspectionTransform != null)
        {
            inspectionTransform.position = targetPos;
            inspectionTransform.rotation = targetRot;
            inspectionTransform.localScale = targetScale;
        }

        if (!opening)
        {
            CleanupInspection();
        }

        isAnimating = false;
        animationCoroutine = null;
    }

    Vector3 GetCameraTargetPosition()
    {
        if (activeCamera == null)
            return transform.position;

        return activeCamera.transform.position +
               activeCamera.transform.forward * zoomDistance;
    }

    Quaternion GetCameraTargetRotation()
    {
        if (activeCamera == null)
            return transform.rotation;

        return activeCamera.transform.rotation *
               Quaternion.Euler(rotationOffset);
    }

    void CleanupInspection()
    {
        if (inspectionCopy != null)
        {
            Destroy(inspectionCopy);
        }

        inspectionCopy = null;
        inspectionTransform = null;
        inspectionRenderer = null;

        if (activeNote == this)
        {
            activeNote = null;
        }

        activeCamera = null;
    }

    void OnDisable()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        CleanupInspection();

        isZoomed = false;
        isAnimating = false;
    }

    void OnDestroy()
    {
        if (activeNote == this)
            activeNote = null;
    }
}
