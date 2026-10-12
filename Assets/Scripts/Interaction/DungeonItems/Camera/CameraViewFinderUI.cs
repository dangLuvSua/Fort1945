using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class CameraViewfinderUI : MonoBehaviour
{
    [Header("Viewfinder")]
    [SerializeField] private GameObject cameraPanel;
    [SerializeField] private RawImage viewfinderImage;
    [SerializeField] private Button shutterButton;
    [SerializeField] private Button closeCameraButton;

    [Header("Photo Preview")]
    [SerializeField] private GameObject photoPreviewPanel;
    [SerializeField] private RawImage photoImage;
    [SerializeField] private Button closePhotoButton;
    [SerializeField, Min(0f)] private float photoPreviewDuration = 2.5f;

    [Header("Flash")]
    [SerializeField] private Image flashOverlay;

    private CameraController localCameraController;
    private float flashTimer;
    private Coroutine photoPreviewCoroutine;

    private void Awake()
    {
        if (cameraPanel != null)
            cameraPanel.SetActive(false);

        if (photoPreviewPanel != null)
            photoPreviewPanel.SetActive(false);

        if (flashOverlay != null)
        {
            flashOverlay.gameObject.SetActive(false);
            flashOverlay.raycastTarget = false;
        }

        if (shutterButton != null)
            shutterButton.onClick.AddListener(OnShutterClicked);

        if (closeCameraButton != null)
            closeCameraButton.onClick.AddListener(OnCloseCameraClicked);

        if (closePhotoButton != null)
            closePhotoButton.onClick.AddListener(OnClosePhotoClicked);
    }

    private void Update()
    {
        if (localCameraController == null)
            FindLocalCameraController();

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0f && flashOverlay != null)
                flashOverlay.gameObject.SetActive(false);
        }
    }

    private void FindLocalCameraController()
    {
        CameraController[] controllers =
            FindObjectsByType<CameraController>(
                FindObjectsSortMode.None
            );

        foreach (CameraController controller in controllers)
        {
            if (controller == null || controller.Object == null)
                continue;

            if (!controller.Object.HasInputAuthority)
                continue;

            localCameraController = controller;
            localCameraController.BindUI(this);
            return;
        }
    }

    public void Bind(
     CameraController controller,
     RenderTexture cameraViewTexture)
    {
        localCameraController = controller;

        if (viewfinderImage != null && cameraViewTexture != null)
            viewfinderImage.texture = cameraViewTexture;
    }

    public void SetCameraOpen(bool open)
    {
        if (cameraPanel != null)
            cameraPanel.SetActive(open);

        if (!open && photoPreviewPanel != null)
            photoPreviewPanel.SetActive(false);
    }

    public void ShowPhoto(Texture photo)
    {
        if (photoPreviewCoroutine != null)
        {
            StopCoroutine(photoPreviewCoroutine);
            photoPreviewCoroutine = null;
        }

        if (photoPreviewPanel != null)
            photoPreviewPanel.SetActive(true);

        if (photoImage != null)
            photoImage.texture = photo;

        if (flashOverlay != null)
        {
            flashOverlay.gameObject.SetActive(true);
            flashOverlay.color = Color.white;
            flashTimer = 0.12f;
        }

        if (photoPreviewDuration > 0f)
            photoPreviewCoroutine = StartCoroutine(AutoClosePhotoPreview());
    }

    public void ClosePhoto()
    {
        if (photoPreviewCoroutine != null)
        {
            StopCoroutine(photoPreviewCoroutine);
            photoPreviewCoroutine = null;
        }

        if (photoPreviewPanel != null)
            photoPreviewPanel.SetActive(false);
    }

    private IEnumerator AutoClosePhotoPreview()
    {
        yield return new WaitForSecondsRealtime(photoPreviewDuration);
        photoPreviewCoroutine = null;

        if (localCameraController != null)
            localCameraController.ClosePhoto();
        else
            ClosePhoto();
    }

    private void OnShutterClicked()
    {
        if (localCameraController != null)
            localCameraController.TakePhoto();
    }

    private void OnCloseCameraClicked()
    {
        if (localCameraController != null)
            localCameraController.CloseCamera();
    }

    private void OnClosePhotoClicked()
    {
        if (localCameraController != null)
            localCameraController.ClosePhoto();
        else
            ClosePhoto();
    }

    private void OnDestroy()
    {
        if (photoPreviewCoroutine != null)
            StopCoroutine(photoPreviewCoroutine);

        if (shutterButton != null)
            shutterButton.onClick.RemoveListener(OnShutterClicked);

        if (closeCameraButton != null)
            closeCameraButton.onClick.RemoveListener(OnCloseCameraClicked);

        if (closePhotoButton != null)
            closePhotoButton.onClick.RemoveListener(OnClosePhotoClicked);
    }
}
