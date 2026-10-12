using System.Collections;
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
    [SerializeField] private LayerMask photoPointMask = ~0;
    [SerializeField, Min(0.1f)] private float photoPointRange = 30f;

    [Header("Flash")]
    [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;
    [SerializeField] private AudioSource shutterAudio;

    private PlayerInventory inventory;
    private PlayerInventoryUI inventoryUI;
    private CameraViewfinderUI viewfinderUI;
    private Texture2D capturedPhoto;
    private bool isCameraOpen;
    private bool captureInProgress;

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
        if (!photoRenderTexture.IsCreated())
            photoRenderTexture.Create();

        // Configured points represent fixed evidence/photos. Any other view
        // is captured from the live camera render below.
        Ray ray = new Ray(cameraViewCamera.transform.position,
            cameraViewCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, photoPointRange,
                photoPointMask, QueryTriggerInteraction.Collide))
        {
            CameraPhotoPoint photoPoint =
                hit.collider.GetComponentInParent<CameraPhotoPoint>();
            if (photoPoint != null && photoPoint.Photo != null)
            {
                if (capturedPhoto != null)
                    Destroy(capturedPhoto);
                capturedPhoto = Instantiate(photoPoint.Photo);
                yield break;
            }
        }

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

    private void OnDestroy()
    {
        if (isCameraOpen)
            SetQuickBarVisible(true);

        if (capturedPhoto != null)
            Destroy(capturedPhoto);
    }
}
