using UnityEngine;
using Fusion;

public class MouseLook : NetworkBehaviour
{
    public Transform playerBody;
    public float mouseSensitivity = 150f;

    float xRotation = 0f;

    public override void Spawned()
    {
        if (!Object.HasStateAuthority)
            enabled = false;

        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        transform.localRotation = Quaternion.Euler(xRotation, 0, 0);

        playerBody.Rotate(Vector3.up * mouseX);
    }
}