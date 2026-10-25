using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CameraController : NetworkBehaviour
{
    [Header("Inventory")]
    [SerializeField] private ItemData cameraItem;

    [Header("Camera Rig")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera cameraViewCamera;

    [Header("Capture")]
    [SerializeField] private RenderTexture cameraViewTexture;
    [SerializeField] private RenderTexture photoRenderTexture;

    [Tooltip("Layer containing ghosts invisible to the normal player view.")]
    [SerializeField] private LayerMask photoGhostLayer;
    [Tooltip("Layers checked to find a configured camera photo point.")]
    // Spawned photo targets can be on Default (or a project-specific layer),
    // so this must include every layer unless deliberately narrowed in data.
    [SerializeField] private LayerMask photoPointMask = ~0;

    [Header("Flash")]
    [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;
    [SerializeField] private AudioSource shutterAudio;

    private PlayerInventory inventory;
    private PlayerInventoryUI inventoryUI;
    private CameraViewfinderUI viewfinderUI;
    private Texture2D capturedPhoto;
    private bool isCameraOpen;
    private bool captureInProgress;
    private bool capturedPhotoValidForMission;
    private readonly Dictionary<GameObject, bool> hudVisibilityBeforeCamera =
        new Dictionary<GameObject, bool>();

    public bool IsCameraOpen => isCameraOpen;

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();

        if (cameraViewCamera != null)
        {
            cameraViewCamera.enabled = false;
            cameraViewCamera.targetTexture = cameraViewTexture;

            AudioListener listener =
                cameraViewCamera.GetComponent<AudioListener>();

            if (listener != null)
                listener.enabled = false;
        }
    }

    public override void Spawned()
    {
        inventory = GetComponent<PlayerInventory>();

        if (!Object.HasInputAuthority && cameraViewCamera != null)
            cameraViewCamera.enabled = false;
    }

    private void Update()
    {
        if (Object == null || !Object.HasInputAuthority)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.xKey.wasPressedThisFrame)
        {
            Debug.Log(
                $"[CAMERA] X pressed. Holding camera: {IsHoldingCamera()}"
            );

            if (isCameraOpen)
                CloseCamera();
            else if (IsHoldingCamera())
                OpenCamera();
        }

        if (isCameraOpen && Mouse.current != null &&
            Mouse.current.rightButton.wasPressedThisFrame)
            TakePhoto();

        // Close the viewfinder if the player switches to another item.
        if (isCameraOpen && !IsHoldingCamera())
            CloseCamera();
    }

    private void LateUpdate()
    {
        if (!isCameraOpen ||
            playerCamera == null ||
            cameraViewCamera == null)
        {
            return;
        }

        SyncViewCamera();
    }

    private void SyncViewCamera()
    {
        if (playerCamera == null || cameraViewCamera == null)
            return;

        cameraViewCamera.transform.SetPositionAndRotation(
            playerCamera.transform.position,
            playerCamera.transform.rotation
        );

        cameraViewCamera.fieldOfView = playerCamera.fieldOfView;
        cameraViewCamera.nearClipPlane = playerCamera.nearClipPlane;
        cameraViewCamera.farClipPlane = playerCamera.farClipPlane;
        cameraViewCamera.orthographic = playerCamera.orthographic;
        cameraViewCamera.orthographicSize = playerCamera.orthographicSize;
        cameraViewCamera.aspect = playerCamera.aspect;
    }

    private bool IsHoldingCamera()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (inventory == null || cameraItem == null)
            return false;

        int storageSlot = inventory.GetSelectedStorageSlot();

        if (storageSlot < 0)
            return false;

        ItemData selectedItem =
            inventory.GetStorageItemAt(storageSlot);

        return selectedItem != null &&
               selectedItem.itemId == cameraItem.itemId;
    }

    public void BindUI(CameraViewfinderUI ui)
    {
        viewfinderUI = ui;

        if (viewfinderUI != null)
            viewfinderUI.Bind(this, cameraViewTexture);
    }

    public void OpenCamera()
    {
        if (Object == null ||
            !Object.HasInputAuthority ||
            !IsHoldingCamera())
        {
            return;
        }

        isCameraOpen = true;
        HideGameplayHud();
        SetQuickBarVisible(false);

        if (inventory != null)
            inventory.SetCameraHeldVisualVisible(false);

        if (cameraItem != null)
            NetworkRunnerHandler.RequestCameraPhotoLibrary(
                Runner, cameraItem.itemId);

        if (cameraViewCamera != null)
        {
            if (cameraViewTexture != null && !cameraViewTexture.IsCreated())
                cameraViewTexture.Create();

            cameraViewCamera.targetTexture = cameraViewTexture;
            cameraViewCamera.enabled = true;
        }

        if (viewfinderUI != null)
            viewfinderUI.SetCameraOpen(true);

        Debug.Log("[CAMERA] Viewfinder opened.");
    }

    public void CloseCamera()
    {
        isCameraOpen = false;
        RestoreGameplayHud();
        SetQuickBarVisible(true);

        if (inventory != null)
            inventory.SetCameraHeldVisualVisible(true);

        if (cameraViewCamera != null)
            cameraViewCamera.enabled = false;

        if (viewfinderUI != null)
            viewfinderUI.SetCameraOpen(false);

        Debug.Log("[CAMERA] Viewfinder closed.");
    }

    public void TakePhoto()
    {
        if (Object == null ||
            !Object.HasInputAuthority ||
            !isCameraOpen ||
            !IsHoldingCamera())
        {
            return;
        }

        if (playerCamera == null || cameraViewCamera == null ||
            cameraViewTexture == null || photoRenderTexture == null)
        {
            Debug.LogError(
                "[CAMERA] Assign Player Camera, Camera View Camera, " +
                "Camera View Texture and Photo Render Texture.",
                this
            );
            return;
        }

        SyncViewCamera();
        if (!captureInProgress)
            StartCoroutine(CaptureAfterCameraRender());
    }

    private IEnumerator CaptureAfterCameraRender()
    {
        captureInProgress = true;

        if (isCameraOpen && cameraViewTexture != null && photoRenderTexture != null)
        {
            yield return CapturePhoto();

            if (capturedPhoto != null)
            {
                byte[] encodedPhoto = capturedPhoto.EncodeToJPG(80);
                bool storedLocally = inventory != null &&
                    inventory.StoreCapturedPhoto(capturedPhoto, encodedPhoto);
                if (!storedLocally)
                {
                    Debug.LogWarning(
                        "[CAMERA] Gallery is full or the local inventory is not " +
                        "ready; photo preview is available but was not added to Gallery.", this);
                }

                if (storedLocally && cameraItem != null &&
                    !NetworkRunnerHandler.PublishCameraPhoto(
                        Runner, cameraItem.itemId, encodedPhoto))
                {
                    Debug.LogWarning(
                        "[CAMERA] Could not share photo with other camera holders.", this);
                }

                if (storedLocally && capturedPhotoValidForMission)
                    PhaseManager.Instance?.ReportPhotoCaptured();

                if (shutterAudio != null)
                    shutterAudio.Play();

                if (viewfinderUI != null)
                    viewfinderUI.ShowPhoto(capturedPhoto);
                else
                    Debug.LogError("[CAMERA] No viewfinder UI is bound to show the photo.", this);
            }
        }

        captureInProgress = false;
    }

    private IEnumerator CapturePhoto()
    {
        capturedPhotoValidForMission = false;

        if (!photoRenderTexture.IsCreated())
            photoRenderTexture.Create();

        // Select the configured photo point actually under the viewfinder
        // center. RaycastAll lets photo triggers still match when bars or
        // other colliders sit in front of the statue.
        CameraPhotoPoint selectedPhotoPoint = null;
        float nearestPhotoPointDepth = float.MaxValue;
        Ray centerRay = cameraViewCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] photoHits = Physics.RaycastAll(
            centerRay,
            Mathf.Infinity,
            ~0,
            QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in photoHits)
        {
            CameraPhotoPoint candidate = hit.collider != null
                ? hit.collider.GetComponentInParent<CameraPhotoPoint>()
                : null;
            if (candidate == null || candidate.Photo == null ||
                (photoPointMask.value & (1 << candidate.gameObject.layer)) == 0 ||
                hit.distance >= nearestPhotoPointDepth)
                continue;

            selectedPhotoPoint = candidate;
            nearestPhotoPointDepth = hit.distance;
        }

        // A small PhotoTrigger can be visible without the center ray crossing
        // its collider. In that case, use the nearest configured point whose
        // trigger center is inside the camera frame.
        bool usedVisiblePointFallback = false;
        if (selectedPhotoPoint == null)
        {
            CameraPhotoPoint[] photoPoints = FindObjectsByType<CameraPhotoPoint>(
                FindObjectsSortMode.None);
            foreach (CameraPhotoPoint candidate in photoPoints)
            {
                if (candidate == null || candidate.Photo == null ||
                    (photoPointMask.value & (1 << candidate.gameObject.layer)) == 0)
                    continue;

                if (TrySelectVisiblePoint(candidate, candidate.PhotoAimPosition,
                        ref selectedPhotoPoint, ref nearestPhotoPointDepth))
                    usedVisiblePointFallback = true;

                // Some imported statue prefabs have a trigger positioned off
                // the visible mesh. Use visible renderer centers as a backup.
                Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>();
                foreach (Renderer targetRenderer in renderers)
                {
                    if (targetRenderer != null && targetRenderer.enabled &&
                        TrySelectVisiblePoint(candidate, targetRenderer.bounds.center,
                            ref selectedPhotoPoint, ref nearestPhotoPointDepth))
                        usedVisiblePointFallback = true;
                }
            }
        }

        if (selectedPhotoPoint != null)
        {
            if (capturedPhoto != null)
                Destroy(capturedPhoto);
            capturedPhotoValidForMission = true;
            capturedPhoto = CreateReadableCopy(selectedPhotoPoint.Photo);
            string selectionMethod = usedVisiblePointFallback
                ? "visible trigger"
                : "viewfinder center hit";
            Debug.Log(
                $"[CAMERA] Showing assigned photo for '{selectedPhotoPoint.name}' " +
                $"({selectionMethod}).", this);
            yield break;
        }

        Debug.Log("[CAMERA] No configured photo point or visible target was found; capturing the scene.", this);

        RenderTexture previousActive = RenderTexture.active;
        float previousAspect = cameraViewCamera.aspect;

        try
        {
            // Render the same scene camera directly into the photo texture.
            // This uses URP's supported request API and gives the Polaroid a
            // square camera projection instead of copying a possibly stale RT.
            cameraViewCamera.aspect =
                (float)photoRenderTexture.width / photoRenderTexture.height;
            var request = new UniversalRenderPipeline.SingleCameraRequest
            {
                destination = photoRenderTexture
            };
            RenderPipeline.SubmitRenderRequest(cameraViewCamera, request);

            // Let URP process the submitted camera render, then read its
            // completed target texture at the end of that frame.
            yield return new WaitForEndOfFrame();

            RenderTexture.active = photoRenderTexture;

            int width = photoRenderTexture.width;
            int height = photoRenderTexture.height;

            if (capturedPhoto != null)
                Destroy(capturedPhoto);

            capturedPhoto = new Texture2D(
                width,
                height,
                TextureFormat.RGB24,
                false
            );

            capturedPhoto.ReadPixels(
                new Rect(0, 0, width, height),
                0,
                0
            );

            capturedPhoto.Apply();

            Debug.Log($"[CAMERA] Captured photo: {width}x{height}.", this);
        }
        finally
        {
            cameraViewCamera.aspect = previousAspect;
            RenderTexture.active = previousActive;
        }
    }

    private bool TrySelectVisiblePoint(
        CameraPhotoPoint candidate,
        Vector3 worldPosition,
        ref CameraPhotoPoint selectedPhotoPoint,
        ref float nearestDepth)
    {
        Vector3 viewportPoint = cameraViewCamera.WorldToViewportPoint(worldPosition);
        if (viewportPoint.z <= 0f || viewportPoint.x < 0f || viewportPoint.x > 1f ||
            viewportPoint.y < 0f || viewportPoint.y > 1f ||
            viewportPoint.z >= nearestDepth)
            return false;

        selectedPhotoPoint = candidate;
        nearestDepth = viewportPoint.z;
        return true;
    }

    public static Texture2D CreateReadableCopy(Texture2D source)
    {
        if (source == null)
            return null;

        if (source.isReadable)
            return Instantiate(source);

        RenderTexture temporary = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            Texture2D readableCopy = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false);
            readableCopy.name = source.name + " (Readable Copy)";
            readableCopy.ReadPixels(
                new Rect(0, 0, source.width, source.height),
                0,
                0);
            readableCopy.Apply();
            return readableCopy;
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    private static Texture2D CreateBlackPhoto()
    {
        Texture2D blackPhoto = new Texture2D(2, 2, TextureFormat.RGB24, false);
        blackPhoto.name = "Black Photo";
        blackPhoto.SetPixels(new[] { Color.black, Color.black, Color.black, Color.black });
        blackPhoto.Apply();
        return blackPhoto;
    }

    public void ClosePhoto()
    {
        if (viewfinderUI != null)
            viewfinderUI.ClosePhoto();
    }

    private void SetQuickBarVisible(bool visible)
    {
        if (inventoryUI == null)
        {
            PlayerInventoryUI[] inventoryUIs =
                FindObjectsByType<PlayerInventoryUI>(FindObjectsSortMode.None);
            foreach (PlayerInventoryUI candidate in inventoryUIs)
            {
                if (candidate != null && candidate.IsForInventory(inventory))
                {
                    inventoryUI = candidate;
                    break;
                }
            }
        }

        if (inventoryUI != null)
            inventoryUI.SetQuickBarVisible(visible);
    }

    private void HideGameplayHud()
    {
        hudVisibilityBeforeCamera.Clear();
        string[] hudObjectNames =
        {
            "MyPlayerPanel",
            "CompassPanel",
            "MissionProgressPanel"
        };

        Transform[] allTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Transform candidate in allTransforms)
        {
            if (candidate == null || !System.Array.Exists(
                    hudObjectNames, objectName => candidate.name == objectName))
                continue;

            GameObject hudObject = candidate.gameObject;
            if (!hudVisibilityBeforeCamera.ContainsKey(hudObject))
                hudVisibilityBeforeCamera.Add(hudObject, hudObject.activeSelf);

            hudObject.SetActive(false);
        }
    }

    private void RestoreGameplayHud()
    {
        foreach (KeyValuePair<GameObject, bool> entry in hudVisibilityBeforeCamera)
        {
            if (entry.Key != null)
                entry.Key.SetActive(entry.Value);
        }

        hudVisibilityBeforeCamera.Clear();
    }

    private void OnDestroy()
    {
        RestoreGameplayHud();

        if (isCameraOpen)
            SetQuickBarVisible(true);

        if (capturedPhoto != null)
            Destroy(capturedPhoto);
    }
}
