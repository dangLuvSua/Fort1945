using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Fusion;

public class PlayerController : NetworkBehaviour
{
    [Header("Input")]
    [Tooltip("I-drag dito ang InputSystem_Actions asset.")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float deceleration = 14f;
    [SerializeField] private float jumpHeight = 1.0f;
    [SerializeField] private float gravity = -20f;

    [Header("Mouse Look")]
    [Tooltip("Mouse delta ay pixels, kaya maliit ang tamang value (0.05 - 0.2).")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxLookUp = 80f;
    [SerializeField] private float maxLookDown = 80f;

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;
    [Tooltip("Child ng player na may camera. Ito ang titingin pataas/pababa.")]
    [SerializeField] private Transform cameraHolder;

    [Header("Player Body")]
    [Tooltip("Model ng player. Itatago sa sariling camera pero makikita ng ibang players.")]
    [SerializeField] private GameObject playerBody;

    [Header("Animator")]
    [SerializeField] private string speedParameter = "Speed";

    // Internal
    private Animator animator;
    private CharacterController characterController;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private Vector2 moveInput;
    private bool runInput;
    private bool jumpRequested;

    private Vector3 currentVelocity;
    private float verticalVelocity;

    private float yaw;
    private float pitch;

    private bool isLocal;

    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        animator = GetComponentInChildren<Animator>();
        characterController = GetComponent<CharacterController>();

        if (characterController == null)
            Debug.LogError("NO CHARACTER CONTROLLER FOUND ON PLAYER!");

        if (animator == null)
            Debug.LogError("NO ANIMATOR FOUND IN PLAYER MODEL!");

        yaw = transform.eulerAngles.y;
        pitch = 0f;

        isLocal = Object.HasStateAuthority;

        SetupLocalPlayer();
    }

    private void SetupLocalPlayer()
    {
        if (playerCamera != null) playerCamera.enabled = isLocal;
        if (audioListener != null) audioListener.enabled = isLocal;

        if (!isLocal)
            return;

        // Clone ng asset para hindi mag-share ng state ang ibang instances
        InputActionAsset actions = Instantiate(inputActions);
        InputActionMap map = actions.FindActionMap("Player", true);

        moveAction = map.FindAction("Move", true);
        lookAction = map.FindAction("Look", true);
        sprintAction = map.FindAction("Sprint", true);
        jumpAction = map.FindAction("Jump", true);

        map.Enable();
        inputActions = actions; // para ma-disable natin sa OnDisable

        // Itago ang sariling katawan, pero may shadow pa rin
        if (playerBody != null)
        {
            foreach (var r in playerBody.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        LockCursor(true);
    }

    // =========================================================
    // UPDATE (input + look)
    // =========================================================

    private void Update()
    {
        if (Object == null || !isLocal)
            return;

        // Esc para lumabas ang cursor, click para bumalik
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            LockCursor(false);

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            moveInput = Vector2.zero;
            return;
        }

        ReadInput();
        HandleLook();
    }

    private void ReadInput()
    {
        moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        runInput = sprintAction.IsPressed();

        // I-latch ang jump kasi ang FixedUpdateNetwork ay hindi tumatakbo kada frame
        if (jumpAction.WasPressedThisFrame())
            jumpRequested = true;
    }

    private void HandleLook()
    {
        Vector2 look = lookAction.ReadValue<Vector2>() * mouseSensitivity;

        // Yaw: ikot ng buong katawan
        yaw += look.x;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Pitch: tingin pataas/pababa sa camera holder lang
        pitch = Mathf.Clamp(pitch - look.y, -maxLookUp, maxLookDown);

        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // =========================================================
    // FIXED NETWORK UPDATE (movement)
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        if (!isLocal)
            return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        Vector3 desiredDirection = transform.forward * moveInput.y + transform.right * moveInput.x;
        desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);

        bool isRunning = runInput && moveInput.magnitude > 0.01f;
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 targetVelocity = desiredDirection * targetSpeed;

        float rate = desiredDirection.magnitude > 0.01f ? acceleration : deceleration;
        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * Runner.DeltaTime);

        // Gravity + Jump
        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (jumpRequested)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        jumpRequested = false;
        verticalVelocity += gravity * Runner.DeltaTime;

        Vector3 finalMovement = currentVelocity;
        finalMovement.y = verticalVelocity;

        characterController.Move(finalMovement * Runner.DeltaTime);

        UpdateAnimator(currentVelocity, isRunning);
    }

    // =========================================================
    // ANIMATOR
    // =========================================================

    private void UpdateAnimator(Vector3 velocity, bool isRunning)
    {
        if (animator == null)
            return;

        float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude / runSpeed;

        if (horizontalSpeed < 0.01f)
            animator.SetFloat(speedParameter, 0f);
        else
            animator.SetFloat(speedParameter, isRunning ? 0.8f : 0.3f);
    }

    // =========================================================
    // CURSOR + CLEANUP
    // =========================================================

    private void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnDisable()
    {
        if (isLocal)
        {
            inputActions?.FindActionMap("Player")?.Disable();
            LockCursor(false);
        }
    }
}