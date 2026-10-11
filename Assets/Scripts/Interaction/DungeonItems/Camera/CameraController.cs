
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class CameraController : NetworkBehaviour
{
    [Header("Inventory")]
    [SerializeField] private ItemData cameraItem;

    [Header("Camera Rig")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera cameraViewCamera;

    [Header("Viewfinder UI")]
    [SerializeField] private GameObject cameraPanel;
    [SerializeField] private RawImage viewfinderImage;
    [SerializeField] private Button shutterButton;
    [SerializeField] private Button closeCameraButton;

    [Header("Photo Preview UI")]
    [SerializeField] private GameObject photoPreviewPanel;
    [SerializeField] private RawImage photoImage;
    [SerializeField] private Button closePhotoButton;

    [Header("Capture")]
    [SerializeField] private RenderTexture cameraViewTexture;
    [SerializeField] private RenderTexture photoRenderTexture;

    [Tooltip("Layer containing ghosts invisible to the normal player view.")]
    [SerializeField] private LayerMask photoGhostLayer;

    [Header("Flash")]
    [SerializeField] private Image flashOverlay;
    [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;
    [SerializeField] private AudioSource shutterAudio;

    private PlayerInventory inventory;
    private Texture2D capturedPhoto;
    private float flashTimer;

    public bool IsCameraOpen { get; private set; }

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();

        if (cameraPanel != null)
            cameraPanel.SetActive(false);

        if (photoPreviewPanel != null)
            photoPreviewPanel.SetActive(false);

        if (flashOverlay != null)
        {
            flashOverlay.gameObject.SetActive(false);
            flashOverlay.raycastTarget = false;
        }

        if (cameraViewCamera != null)
        {
            cameraViewCamera.enabled = false;
            cameraViewCamera.targetTexture = cameraViewTexture;
            cameraViewCamera.GetComponent<AudioListener>()?.enabled.Equals(false);
        }

        if (viewfinderImage != null && cameraViewTexture != null)
            viewfinderImage.texture = cameraViewTexture;

        if (shutterButton != null)
            shutterButton.onClick.AddListener(TakePhoto);

        if (closeCameraButton != null)
            closeCameraButton.onClick.AddListener(CloseCamera);

        if (closePhotoButton != null)
            closePhotoButton.onClick.AddListener(ClosePhoto);
    }

    public override void Spawned()
    {
        inventory = GetComponent<PlayerInventory>();

        // Camera controls and UI belong to the local player only.
        if (!Object.HasInputAuthority)
        {
            if (cameraViewCamera != null)
                cameraViewCamera.enabled = false;

            if (cameraPanel != null)
                cameraPanel.SetActive(false);

            if (photoPreviewPanel != null)
                photoPreviewPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!Object.HasInputAuthority)
            return;

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0f && flashOverlay != null)
                flashOverlay.gameObject.SetActive(false);
        }

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (IsCameraOpen)
            {
                CloseCamera();
            }
            else if (IsHoldingCamera())
            {
                OpenCamera();
            }
        }

        // If the player switches away from the camera, close its UI.
        if (IsCameraOpen && !IsHoldingCamera())
            CloseCamera();
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

        ItemData selected = inventory.GetStorageItemAt(storageSlot);

        return selected != null &&
               selected.itemId == cameraItem.itemId;
    }

    public void OpenCamera()
    {
        if (!Object.HasInputAuthority || !IsHoldingCamera())
            return;

        IsCameraOpen = true;

        if (cameraPanel != null)
            cameraPanel.SetActive(true);

        if (photoPreviewPanel != null)
            photoPreviewPanel.SetActive(false);

        if (cameraViewCamera != null)
        {
            cameraViewCamera.enabled = true;
            cameraViewCamera.targetTexture = cameraViewTexture;
        }

        // Do not disable player movement or mouse-look here.
        // Configure your UI input so the viewfinder does not block aiming.
    }

    public void CloseCamera()
    {
        IsCameraOpen = false;

        if (cameraViewCamera != null)
            cameraViewCamera.enabled = false;

        if (cameraPanel != null)
            cameraPanel.SetActive(false);

        if (photoPreviewPanel != null)
            photoPreviewPanel.SetActive(false);
    }

    public void TakePhoto()
    {
        if (!Object.HasInputAuthority ||
            !IsCameraOpen ||
            !IsHoldingCamera())
            return;

        if (cameraViewCamera == null || photoRenderTexture == null)
        {
            Debug.LogError("[CAMERA] Assign the camera and photo RenderTexture.");
            return;
        }

        CapturePhoto();

        if (flashOverlay != null)
        {
            flashOverlay.gameObject.SetActive(true);
            flashOverlay.color = Color.white;
            flashTimer = flashDuration;
        }

        if (shutterAudio != null)
            shutterAudio.Play();

        if (photoPreviewPanel != null)
            photoPreviewPanel.SetActive(true);

        if (photoImage != null && capturedPhoto != null)
            photoImage.texture = capturedPhoto;
    }

    private void CapturePhoto()
    {
        Camera cam = cameraViewCamera;

        RenderTexture previousTarget = cam.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        int previousMask = cam.cullingMask;

        try
        {
            // Render the ghost only into this photograph.
            cam.cullingMask = previousMask | photoGhostLayer.value;
            cam.targetTexture = photoRenderTexture;
            cam.Render();

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
        }
        finally
        {
            cam.cullingMask = previousMask;
            cam.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
        }
    }

    public void ClosePhoto()
    {
        if (photoPreviewPanel != null)
            photoPreviewPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (shutterButton != null)
            shutterButton.onClick.RemoveListener(TakePhoto);

        if (closeCameraButton != null)
            closeCameraButton.onClick.RemoveListener(CloseCamera);

        if (closePhotoButton != null)
            closePhotoButton.onClick.RemoveListener(ClosePhoto);

        if (capturedPhoto != null)
            Destroy(capturedPhoto);
    }
}
