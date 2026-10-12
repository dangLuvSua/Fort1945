using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CameraPhotoSystem : NetworkBehaviour
{
    [Header("Inventory")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private int cameraItemId = 1;

    [Header("Camera model")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private GameObject cameraPrefab;
    [SerializeField] private Light flashLight;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shutterSound;

    [Header("Flash")]
    [SerializeField, Min(0f)] private float flashIntensity = 8f;
    [SerializeField, Min(0.1f)] private float flashRange = 8f;
    [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;
    [SerializeField, Min(0f)] private float cooldown = 1.5f;

    [Header("Photo UI")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameObject photoPanel;
    [SerializeField] private RawImage photoImage;
    [SerializeField] private Button closeButton;
    [SerializeField, Min(64)] private int photoWidth = 1280;
    [SerializeField, Min(64)] private int photoHeight = 720;

    private GameObject cameraVisual;
    private Texture2D currentPhoto;
    private float nextShotTime;
    private bool isLocal;
    private bool cameraEquipped;

    public override void Spawned()
    {
        isLocal = Object.HasInputAuthority;

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (photoPanel != null)
            photoPanel.SetActive(false);

        if (flashLight != null)
        {
            flashLight.enabled = false;
            flashLight.intensity = flashIntensity;
            flashLight.range = flashRange;
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePhoto);
    }

    private void Update()
    {
        if (!isLocal || Keyboard.current == null)
            return;

        bool hasCameraSelected =
            inventory != null &&
            inventory.GetItemIdAt(inventory.SelectedSlot) == cameraItemId;

        if (hasCameraSelected != cameraEquipped)
            SetCameraEquipped(hasCameraSelected);

        if (!cameraEquipped)
            return;

        if (Mouse.current != null &&
            Mouse.current.rightButton.wasPressedThisFrame &&
            Time.time >= nextShotTime)
        {
            nextShotTime = Time.time + cooldown;
            RPC_PlayFlash();
            StartCoroutine(CapturePhoto());
        }
    }

    private void SetCameraEquipped(bool equipped)
    {
        cameraEquipped = equipped;

        if (cameraVisual != null)
            Destroy(cameraVisual);

        cameraVisual = null;

        if (equipped && holdPoint != null && cameraPrefab != null)
        {
            cameraVisual = Instantiate(cameraPrefab, holdPoint);
            cameraVisual.transform.localPosition = Vector3.zero;
            cameraVisual.transform.localRotation = Quaternion.identity;
            cameraVisual.transform.localScale = Vector3.one;

            // The first-person model is local-only. Remove physical
            // components so it cannot interfere with movement or interaction.
            foreach (Collider c in cameraVisual.GetComponentsInChildren<Collider>())
                Destroy(c);

            foreach (Rigidbody rb in cameraVisual.GetComponentsInChildren<Rigidbody>())
                Destroy(rb);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayFlash()
    {
        if (flashLight == null)
            return;

        StopCoroutine(nameof(FlashRoutine));
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        flashLight.enabled = true;
        flashLight.intensity = flashIntensity;
        flashLight.range = flashRange;

        if (audioSource != null && shutterSound != null)
            audioSource.PlayOneShot(shutterSound);

        yield return new WaitForSeconds(flashDuration);

        if (flashLight != null)
            flashLight.enabled = false;
    }

    private IEnumerator CapturePhoto()
    {
        if (playerCamera == null || photoImage == null)
            yield break;

        yield return new WaitForEndOfFrame();

        RenderTexture target = RenderTexture.GetTemporary(
            photoWidth,
            photoHeight,
            24,
            RenderTextureFormat.ARGB32
        );

        RenderTexture previous = playerCamera.targetTexture;
        playerCamera.targetTexture = target;
        playerCamera.Render();
        playerCamera.targetTexture = previous;

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = target;

        Texture2D photo = new Texture2D(
            photoWidth,
            photoHeight,
            TextureFormat.RGB24,
            false
        );

        photo.ReadPixels(new Rect(0, 0, photoWidth, photoHeight), 0, 0);
        photo.Apply();

        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(target);

        if (currentPhoto != null)
            Destroy(currentPhoto);

        currentPhoto = photo;
        photoImage.texture = currentPhoto;

        if (photoPanel != null)
            photoPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ClosePhoto()
    {
        if (photoPanel != null)
            photoPanel.SetActive(false);

        if (isLocal)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        if (flashLight != null)
            flashLight.enabled = false;

        if (closeButton != null)
            closeButton.onClick.RemoveListener(ClosePhoto);

        if (currentPhoto != null)
            Destroy(currentPhoto);
    }
}