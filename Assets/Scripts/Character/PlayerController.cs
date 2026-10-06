using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Fusion;

public class PlayerController : NetworkBehaviour
{
    // =========================================================
    // INPUT
    // =========================================================

    [Header("Input")]
    [Tooltip("Drag your InputSystem_Actions asset here.")]
    [SerializeField] private InputActionAsset inputActions;


    // =========================================================
    // MOVEMENT
    // =========================================================

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;

    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float deceleration = 14f;

    [SerializeField] private float jumpHeight = 1.0f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private PlayerStamina stamina;


    // =========================================================
    // GROUND / FALL SAFETY
    // =========================================================

    [Header("Ground Detection")]
    [Tooltip("Layers considered ground.")]
    [SerializeField] private LayerMask groundLayers = ~0;



    [Tooltip("How far below the world before the player is reset.")]
    [SerializeField] private float fallLimit = -20f;

    [Tooltip("Extra height added when recovering the player.")]
    [SerializeField] private float recoveryHeight = 1f;


    // =========================================================
    // MOUSE LOOK
    // =========================================================

    [Header("Mouse Look")]
    [Tooltip("Mouse sensitivity.")]
    [SerializeField] private float mouseSensitivity = 0.1f;

    [SerializeField] private float maxLookUp = 80f;
    [SerializeField] private float maxLookDown = 80f;


    // =========================================================
    // CAMERA
    // =========================================================

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;

    [Tooltip("Child of PlayerRoot containing the camera.")]
    [SerializeField] private Transform cameraHolder;


    // =========================================================
    // PLAYER BODY
    // =========================================================

    [Header("Player Body")]
    [SerializeField] private GameObject playerBody;


    // =========================================================
    // NETWORKED ANIMATION
    // =========================================================

    [Header("Network Animation")]
    [Networked]
    private float NetworkAnimationSpeed { get; set; }


    // =========================================================
    // ANIMATOR
    // =========================================================

    [Header("Animator")]
    [SerializeField] private string speedParameter = "Speed";


    // =========================================================
    // INTERNAL
    // =========================================================

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
    private bool inputInitialized;

    private float animatorSearchTimer;

    private const float AnimatorSearchInterval = 0.25f;


    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        // Debug.Log(
        //     $"[PLAYER] Spawned at {transform.position}"
        // );

        // -----------------------------------------------------
        // CHARACTER CONTROLLER
        // -----------------------------------------------------

        characterController =
            GetComponent<CharacterController>();

        if (characterController == null)
        {
            // Debug.LogError(
            //     "[PLAYER ERROR] " +
            //     "No CharacterController found on PlayerRoot!"
            // );

            return;
        }

        characterController.enabled = true;

        // Debug.Log(
        //     $"[PLAYER] CharacterController found. " +
        //     $"Height={characterController.height}, " +
        //     $"Radius={characterController.radius}, " +
        //     $"Center={characterController.center}"
        // );


        // -----------------------------------------------------
        // AUTHORITY
        // -----------------------------------------------------

        isLocal =
            Object.HasInputAuthority;

        // Debug.Log(
        //     $"[PLAYER] Input Authority = {isLocal}"
        // );

        // Debug.Log(
        //     $"[PLAYER] Input Authority = " +
        //     $"{Object.HasInputAuthority}"
        // );


        // -----------------------------------------------------
        // ANIMATOR
        // -----------------------------------------------------

        TryFindAnimator();


        // -----------------------------------------------------
        // LOOK
        // -----------------------------------------------------

        yaw =
            transform.eulerAngles.y;

        pitch = 0f;


        // -----------------------------------------------------
        // CAMERA
        // -----------------------------------------------------

        SetupCamera();


        // -----------------------------------------------------
        // INPUT
        // -----------------------------------------------------

        if (isLocal)
        {
            SetupInput();
        }


        // -----------------------------------------------------
        // GROUND CHECK
        // -----------------------------------------------------

        CheckGroundAtSpawn();
    }


    // =========================================================
    // RENDER
    // =========================================================

    public override void Render()
    {
        if (animator == null)
        {
            animatorSearchTimer -= Time.deltaTime;

            if (animatorSearchTimer <= 0f)
            {
                animatorSearchTimer =
                    AnimatorSearchInterval;

                TryFindAnimator();
            }
        }

        if (animator != null)
        {
            animator.SetFloat(
                speedParameter,
                NetworkAnimationSpeed
            );
        }
    }


    // =========================================================
    // FIND ANIMATOR
    // =========================================================

    private void TryFindAnimator()
    {
        if (animator != null)
            return;

        animator =
            GetComponentInChildren<Animator>();

        if (animator != null)
        {
            // Debug.Log(
            //     $"[PLAYER] Animator found: " +
            //     $"{animator.gameObject.name}"
            // );

            animatorSearchTimer = 0f;
        }
    }


    // =========================================================
    // CAMERA SETUP
    // =========================================================

    private void SetupCamera()
    {
        if (playerCamera != null)
        {
            playerCamera.enabled =
                isLocal;
        }

        if (audioListener != null)
        {
            audioListener.enabled =
                isLocal;
        }

        if (!isLocal)
            return;

        // Debug.Log(
        //     "[PLAYER] Local camera enabled."
        // );
    }


    // =========================================================
    // INPUT SETUP
    // =========================================================

    private void SetupInput()
    {
        if (inputActions == null)
        {
            // Debug.LogError(
            //     "[INPUT ERROR] " +
            //     "InputActionAsset is NOT assigned " +
            //     "on PlayerController!"
            // );

            return;
        }

        // Clone the asset so every player has its own input state.
        InputActionAsset actions =
            Instantiate(inputActions);

        InputActionMap map =
            actions.FindActionMap(
                "Player",
                true
            );

        moveAction =
            map.FindAction(
                "Move",
                true
            );

        lookAction =
            map.FindAction(
                "Look",
                true
            );

        sprintAction =
            map.FindAction(
                "Sprint",
                true
            );

        jumpAction =
            map.FindAction(
                "Jump",
                true
            );

        map.Enable();

        inputActions =
            actions;

        inputInitialized =
            true;

        // Debug.Log(
        //     "[INPUT] Player input initialized."
        // );

        // Debug.Log(
        //     $"[INPUT] Move Action: {moveAction != null}"
        // );

        // Debug.Log(
        //     $"[INPUT] Look Action: {lookAction != null}"
        // );

        // Debug.Log(
        //     $"[INPUT] Sprint Action: {sprintAction != null}"
        // );

        // Debug.Log(
        //     $"[INPUT] Jump Action: {jumpAction != null}"
        // );

        LockCursor(true);
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (Object == null)
            return;

        if (!Object.HasInputAuthority)
            return;

        // Debug.Log(
        //     $"[PLAYER UPDATE] " +
        //     $"InputAuthority={Object.HasInputAuthority} " +
        //     $"Initialized={inputInitialized}"
        // );

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            bool clickedUI =
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject();

            if (!clickedUI)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        ReadInput();

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            HandleLook();
        }
    }
    // =========================================================
    // READ INPUT
    // =========================================================

    private void ReadInput()
    {
        if (Keyboard.current == null)
        {
            // Debug.LogWarning("[INPUT TEST] Keyboard.current is NULL!");
            return;
        }

        Vector2 keyboardInput = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            keyboardInput.y += 1f;

        if (Keyboard.current.sKey.isPressed)
            keyboardInput.y -= 1f;

        if (Keyboard.current.dKey.isPressed)
            keyboardInput.x += 1f;

        if (Keyboard.current.aKey.isPressed)
            keyboardInput.x -= 1f;

        moveInput = keyboardInput.normalized;

        if (moveInput.sqrMagnitude > 0.01f)
        {
            // Debug.Log(
            //     $"[KEYBOARD TEST] WASD = {moveInput}"
            // );
        }

        runInput =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        if (runInput)
        {
            // Debug.Log("[KEYBOARD TEST] SPRINT = TRUE");
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpRequested = true;
            // Debug.Log("[KEYBOARD TEST] JUMP = TRUE");
        }
    }

    // =========================================================
    // MOUSE LOOK
    // =========================================================

    private void HandleLook()
    {
        if (lookAction == null)
            return;

        Vector2 look =
            lookAction.ReadValue<Vector2>() *
            mouseSensitivity;

        // Store horizontal rotation
        yaw += look.x;

        // Vertical camera rotation
        pitch = Mathf.Clamp(
            pitch - look.y,
            -maxLookUp,
            maxLookDown
        );

        // Camera only handles up/down here
        if (cameraHolder != null)
        {
            cameraHolder.localRotation =
                Quaternion.Euler(
                    pitch,
                    0f,
                    0f
                );
        }
    }


    // =========================================================
    // FIXED NETWORK UPDATE
    // =========================================================
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        // Apply horizontal player rotation
        transform.rotation =
            Quaternion.Euler(
                0f,
                yaw,
                0f
            );

        if (transform.position.y <= fallLimit)
        {
            // Debug.Log(
            //     $"[NETWORK MOVE TEST] " +
            //     $"Move={moveInput}, " +
            //     $"Position={transform.position}"
            // );
        }

        HandleMovement();
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    private void HandleMovement()
    {
        Vector3 desiredDirection =
            transform.forward *
            moveInput.y
            +
            transform.right *
            moveInput.x;

        desiredDirection =
            Vector3.ClampMagnitude(
                desiredDirection,
                1f
            );

        bool wantsRun =
            runInput &&
            moveInput.magnitude > 0.01f;

        bool isRunning =
            stamina != null
                ? stamina.Tick(wantsRun, Runner.DeltaTime)
                : wantsRun;

        float targetSpeed =
            isRunning
                ? runSpeed
                : walkSpeed;

        Vector3 targetVelocity =
            desiredDirection *
            targetSpeed;

        float rate =
            desiredDirection.magnitude > 0.01f
                ? acceleration
                : deceleration;

        currentVelocity =
            Vector3.MoveTowards(
                currentVelocity,
                targetVelocity,
                rate *
                Runner.DeltaTime
            );


        // -----------------------------------------------------
        // GROUND
        // -----------------------------------------------------

        bool isGrounded =
            characterController.isGrounded;


        // -----------------------------------------------------
        // JUMP
        // -----------------------------------------------------

        if (isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (jumpRequested)
            {
                verticalVelocity =
                    Mathf.Sqrt(
                        jumpHeight *
                        -2f *
                        gravity
                    );
            }
        }

        jumpRequested = false;


        // -----------------------------------------------------
        // GRAVITY
        // -----------------------------------------------------

        verticalVelocity +=
            gravity *
            Runner.DeltaTime;


        // -----------------------------------------------------
        // FINAL MOVEMENT
        // -----------------------------------------------------

        Vector3 finalMovement =
            currentVelocity;

        finalMovement.y =
            verticalVelocity;


        characterController.Move(
            finalMovement *
            Runner.DeltaTime
        );

        //         Debug.Log(
        //     $"[CHARACTER MOVE TEST] " +
        //     $"Velocity={currentVelocity}, " +
        //     $"MoveInput={moveInput}"
        // );


        // -----------------------------------------------------
        // ANIMATION
        // -----------------------------------------------------

        UpdateNetworkAnimation(
            currentVelocity,
            isRunning
        );
    }


    // =========================================================
    // NETWORK ANIMATION
    // =========================================================

    private void UpdateNetworkAnimation(
        Vector3 velocity,
        bool isRunning)
    {
        if (!isLocal)
            return;

        float horizontalSpeed =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            ).magnitude;

        horizontalSpeed /=
            Mathf.Max(
                runSpeed,
                0.01f
            );

        if (horizontalSpeed < 0.01f)
        {
            NetworkAnimationSpeed = 0f;
            return;
        }

        if (isRunning)
        {
            NetworkAnimationSpeed = 0.8f;
            return;
        }

        NetworkAnimationSpeed = 0.3f;
    }


    // =========================================================
    // GROUND CHECK
    // =========================================================

    private void CheckGroundAtSpawn()
    {
        if (!isLocal)
            return;

        Vector3 rayOrigin =
            transform.position +
            Vector3.up * 2f;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            10f,
            groundLayers,
            QueryTriggerInteraction.Ignore))
        {
            // Debug.Log(
            //     $"[GROUND SPAWN CHECK] " +
            //     $"Ground found at {hit.point}. " +
            //     $"Distance = {hit.distance:F2}"
            // );

            Vector3 safePosition =
                hit.point +
                Vector3.up *
                recoveryHeight;

            CharacterController cc =
                characterController;

            if (cc != null)
                cc.enabled = false;

            transform.position =
                safePosition;

            if (cc != null)
                cc.enabled = true;

            verticalVelocity = 0f;
            currentVelocity = Vector3.zero;
        }
        else
        {
            // Debug.LogWarning(
            //     "[GROUND SPAWN CHECK] " +
            //     "NO GROUND FOUND BELOW PLAYER!"
            // );
        }
    }


    // =========================================================
    // FALL RECOVERY
    // =========================================================

    private void RecoverFromFall()
    {
        if (!isLocal)
            return;

        // Debug.LogWarning(
        //     $"[FALL RECOVERY] " +
        //     $"Player fell to Y = " +
        //     $"{transform.position.y:F2}"
        // );

        Vector3 recoveryPosition =
            FindNearestSpawnPoint();

        CharacterController cc =
            characterController;

        if (cc != null)
            cc.enabled = false;

        transform.SetPositionAndRotation(
            recoveryPosition,
            Quaternion.Euler(
                0f,
                yaw,
                0f
            )
        );

        if (cc != null)
            cc.enabled = true;

        currentVelocity = Vector3.zero;
        verticalVelocity = 0f;
        jumpRequested = false;
    }


    // =========================================================
    // FIND SPAWN POINT
    // =========================================================

    private Vector3 FindNearestSpawnPoint()
    {
        GameObject[] spawnPoints =
            GameObject.FindGameObjectsWithTag(
                "PlayerSpawn"
            );

        if (spawnPoints.Length == 0)
        {
            // Debug.LogWarning(
            //     "[FALL RECOVERY] " +
            //     "No PlayerSpawn found."
            // );

            return Vector3.up * 5f;
        }

        GameObject closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (
            GameObject spawnPoint
            in spawnPoints)
        {
            float distance =
                Vector3.Distance(
                    transform.position,
                    spawnPoint.transform.position
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = spawnPoint;
            }
        }

        Vector3 rayOrigin =
            closest.transform.position +
            Vector3.up * 10f;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            50f,
            groundLayers,
            QueryTriggerInteraction.Ignore))
        {
            return
                hit.point +
                Vector3.up *
                recoveryHeight;
        }

        return
            closest.transform.position +
            Vector3.up *
            recoveryHeight;
    }


    // =========================================================
    // CURSOR
    // =========================================================

    private void LockCursor(bool locked)
    {
        Cursor.lockState =
            locked
                ? CursorLockMode.Locked
                : CursorLockMode.None;

        Cursor.visible =
            !locked;
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDisable()
    {
        if (isLocal)
        {
            inputActions?
                .FindActionMap("Player")?
                .Disable();

            LockCursor(false);
        }
    }


    // =========================================================
    // TELEPORT
    // =========================================================

    public void Teleport(
        Vector3 position,
        Quaternion rotation)
    {
        if (characterController == null)
            return;

        characterController.enabled = false;

        transform.SetPositionAndRotation(
            position,
            rotation
        );

        characterController.enabled = true;

        yaw =
            rotation.eulerAngles.y;

        pitch = 0f;

        currentVelocity = Vector3.zero;
        verticalVelocity = 0f;
        jumpRequested = false;

        if (isLocal)
        {
            NetworkAnimationSpeed = 0f;
        }
    }
}