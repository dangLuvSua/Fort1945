using UnityEngine;
using UnityEngine.InputSystem;
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
    [Tooltip("Mouse sensitivity. 0.05 - 0.2 is usually good.")]
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
    [Tooltip(
        "Visual player model/container. " +
        "It remains visible to other players."
    )]
    [SerializeField] private GameObject playerBody;


    // =========================================================
    // NETWORKED ANIMATION
    // =========================================================

    [Header("Network Animation")]

    [Tooltip(
        "Networked animation speed. " +
        "0 = Idle, 0.3 = Walk, 0.8 = Run."
    )]
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
        Debug.Log(
            $"[PLAYER] Spawned at {transform.position}"
        );


        // -----------------------------------------------------
        // CHARACTER CONTROLLER
        // -----------------------------------------------------

        characterController =
            GetComponent<CharacterController>();

        if (characterController == null)
        {
            Debug.LogError(
                "[PLAYER ERROR] " +
                "No CharacterController found on PlayerRoot!"
            );

            return;
        }

        characterController.enabled = true;

        Debug.Log(
            $"[PLAYER] CharacterController found. " +
            $"Height={characterController.height}, " +
            $"Radius={characterController.radius}, " +
            $"Center={characterController.center}"
        );


        // -----------------------------------------------------
        // ANIMATOR
        // -----------------------------------------------------

        TryFindAnimator();


        // -----------------------------------------------------
        // LOCAL / STATE AUTHORITY
        // -----------------------------------------------------

        isLocal =
            Object.HasStateAuthority;

        Debug.Log(
            $"[PLAYER] Local/State Authority = {isLocal}"
        );


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
        // INITIAL GROUND CHECK
        // -----------------------------------------------------

        CheckGroundAtSpawn();
    }


    // =========================================================
    // RENDER
    // =========================================================
    //
    // Render() runs on every client.
    //
    // This is where we apply the networked animation value
    // to the local visual Animator.
    //
    // =========================================================

    public override void Render()
    {
        // -----------------------------------------------------
        // The character visual is instantiated by PlayerNetwork.
        // It may not exist yet when Spawned() executes.
        // -----------------------------------------------------

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


        // -----------------------------------------------------
        // APPLY NETWORKED ANIMATION
        // -----------------------------------------------------

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
            Debug.Log(
                $"[PLAYER] Animator found: " +
                $"{animator.gameObject.name}"
            );

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

        Debug.Log(
            "[PLAYER] Local camera enabled."
        );
    }


    // =========================================================
    // INPUT SETUP
    // =========================================================

    private void SetupInput()
    {
        if (inputActions == null)
        {
            Debug.LogError(
                "[INPUT ERROR] " +
                "InputActionAsset is not assigned!"
            );

            return;
        }


        // -----------------------------------------------------
        // Clone the asset so each player has its own state.
        // -----------------------------------------------------

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

        Debug.Log(
            "[INPUT] Player input initialized."
        );

        LockCursor(true);
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (Object == null)
            return;

        if (!isLocal)
            return;

        if (!inputInitialized)
            return;


        // -----------------------------------------------------
        // Escape = unlock cursor
        // -----------------------------------------------------

        Keyboard kb =
            Keyboard.current;

        if (kb != null &&
            kb.escapeKey.wasPressedThisFrame)
        {
            LockCursor(false);
        }


        // -----------------------------------------------------
        // Left click = lock cursor again
        // -----------------------------------------------------

        Mouse mouse =
            Mouse.current;

        if (mouse != null &&
            mouse.leftButton.wasPressedThisFrame &&
            Cursor.lockState !=
            CursorLockMode.Locked)
        {
            LockCursor(true);
        }


        // -----------------------------------------------------
        // Don't read gameplay input while cursor is unlocked.
        // -----------------------------------------------------

        if (Cursor.lockState !=
            CursorLockMode.Locked)
        {
            moveInput =
                Vector2.zero;

            runInput =
                false;

            return;
        }


        ReadInput();

        HandleLook();
    }


    // =========================================================
    // READ INPUT
    // =========================================================

    private void ReadInput()
    {
        if (moveAction == null)
            return;


        // -----------------------------------------------------
        // Movement
        // -----------------------------------------------------

        moveInput =
            Vector2.ClampMagnitude(
                moveAction.ReadValue<Vector2>(),
                1f
            );


        // -----------------------------------------------------
        // Sprint
        // -----------------------------------------------------

        if (sprintAction != null)
        {
            runInput =
                sprintAction.IsPressed();
        }


        // -----------------------------------------------------
        // Jump
        // -----------------------------------------------------

        if (jumpAction != null &&
            jumpAction.WasPressedThisFrame())
        {
            jumpRequested =
                true;
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


        // -----------------------------------------------------
        // Store yaw.
        //
        // The actual networked PlayerRoot rotation is applied
        // inside FixedUpdateNetwork().
        // -----------------------------------------------------

        yaw +=
            look.x;


        // -----------------------------------------------------
        // Camera pitch remains local.
        // -----------------------------------------------------

        pitch =
            Mathf.Clamp(
                pitch - look.y,
                -maxLookUp,
                maxLookDown
            );


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
        if (!isLocal)
            return;


        // -----------------------------------------------------
        // FALL RECOVERY
        // -----------------------------------------------------

        if (transform.position.y <= fallLimit)
        {
            RecoverFromFall();

            return;
        }


        // -----------------------------------------------------
        // ROTATION
        // -----------------------------------------------------

        transform.rotation =
            Quaternion.Euler(
                0f,
                yaw,
                0f
            );


        // -----------------------------------------------------
        // MOVEMENT
        // -----------------------------------------------------

        HandleMovement();
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    private void HandleMovement()
    {
        // -----------------------------------------------------
        // MOVEMENT DIRECTION
        // -----------------------------------------------------

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


        // -----------------------------------------------------
        // RUNNING
        // -----------------------------------------------------

        bool isRunning =
            runInput &&
            moveInput.magnitude > 0.01f;


        float targetSpeed =
            isRunning
                ? runSpeed
                : walkSpeed;


        Vector3 targetVelocity =
            desiredDirection *
            targetSpeed;


        // -----------------------------------------------------
        // ACCELERATION / DECELERATION
        // -----------------------------------------------------

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
        // FALLING / JUMPING
        // -----------------------------------------------------

        if (isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity =
                    -2f;
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


        jumpRequested =
            false;


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


        // -----------------------------------------------------
        // CHARACTER CONTROLLER MOVE
        // -----------------------------------------------------

        CollisionFlags collisionFlags =
            characterController.Move(
                finalMovement *
                Runner.DeltaTime
            );


        // -----------------------------------------------------
        // GROUND DIAGNOSTICS
        // -----------------------------------------------------

        if (isLocal)
        {
            if ((collisionFlags &
                CollisionFlags.Below) != 0)
            {
                Debug.Log(
                    $"[GROUND] " +
                    $"Player touching ground. " +
                    $"Y = {transform.position.y:F2}"
                );
            }
        }


        // -----------------------------------------------------
        // NETWORK ANIMATION STATE
        // -----------------------------------------------------

        UpdateNetworkAnimation(
            currentVelocity,
            isRunning
        );
    }


    // =========================================================
    // NETWORK ANIMATION
    // =========================================================
    //
    // Only the State Authority calculates the animation state.
    //
    // The value is then synchronized through Fusion.
    //
    // 0.0 = Idle
    // 0.3 = Walk
    // 0.8 = Run
    //
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


        // -----------------------------------------------------
        // IDLE
        // -----------------------------------------------------

        if (horizontalSpeed < 0.01f)
        {
            NetworkAnimationSpeed =
                0f;

            return;
        }


        // -----------------------------------------------------
        // RUN
        // -----------------------------------------------------

        if (isRunning)
        {
            NetworkAnimationSpeed =
                0.8f;

            return;
        }


        // -----------------------------------------------------
        // WALK
        // -----------------------------------------------------

        NetworkAnimationSpeed =
            0.3f;
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
            Debug.Log(
                $"[GROUND SPAWN CHECK] " +
                $"Ground found at {hit.point}. " +
                $"Distance = {hit.distance:F2}"
            );


            // -------------------------------------------------
            // Put the CharacterController above the ground.
            // -------------------------------------------------

            Vector3 safePosition =
                hit.point +
                Vector3.up *
                recoveryHeight;


            CharacterController cc =
                characterController;


            if (cc != null)
            {
                cc.enabled =
                    false;
            }


            transform.position =
                safePosition;


            if (cc != null)
            {
                cc.enabled =
                    true;
            }


            verticalVelocity =
                0f;

            currentVelocity =
                Vector3.zero;


            Debug.Log(
                $"[GROUND SPAWN] " +
                $"Player positioned at " +
                $"{transform.position}"
            );
        }
        else
        {
            Debug.LogWarning(
                "[GROUND SPAWN CHECK] " +
                "NO GROUND FOUND BELOW PLAYER!"
            );
        }
    }


    // =========================================================
    // FALL RECOVERY
    // =========================================================

    private void RecoverFromFall()
    {
        if (!isLocal)
            return;


        Debug.LogWarning(
            $"[FALL RECOVERY] " +
            $"Player fell to Y = " +
            $"{transform.position.y:F2}"
        );


        Vector3 recoveryPosition =
            FindNearestSpawnPoint();


        CharacterController cc =
            characterController;


        if (cc != null)
        {
            cc.enabled =
                false;
        }


        transform.SetPositionAndRotation(
            recoveryPosition,
            Quaternion.Euler(
                0f,
                yaw,
                0f
            )
        );


        if (cc != null)
        {
            cc.enabled =
                true;
        }


        currentVelocity =
            Vector3.zero;

        verticalVelocity =
            0f;

        jumpRequested =
            false;


        Debug.Log(
            $"[FALL RECOVERY] " +
            $"Player returned to " +
            $"{recoveryPosition}"
        );
    }


    // =========================================================
    // FIND SPAWN POINT FOR RECOVERY
    // =========================================================

    private Vector3 FindNearestSpawnPoint()
    {
        GameObject[] spawnPoints =
            GameObject.FindGameObjectsWithTag(
                "PlayerSpawn"
            );


        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning(
                "[FALL RECOVERY] " +
                "No PlayerSpawn found."
            );


            return Vector3.up * 5f;
        }


        GameObject closest =
            null;

        float closestDistance =
            Mathf.Infinity;


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
                closestDistance =
                    distance;

                closest =
                    spawnPoint;
            }
        }


        // -----------------------------------------------------
        // Search for actual ground.
        // -----------------------------------------------------

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
            Vector3 position =
                hit.point +
                Vector3.up *
                recoveryHeight;


            Debug.Log(
                $"[FALL RECOVERY] " +
                $"Ground found at {hit.point}. " +
                $"Returning player to {position}"
            );


            return position;
        }


        Debug.LogWarning(
            "[FALL RECOVERY] " +
            "Could not find ground."
        );


        return closest.transform.position +
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


        characterController.enabled =
            false;


        transform.SetPositionAndRotation(
            position,
            rotation
        );


        characterController.enabled =
            true;


        yaw =
            rotation.eulerAngles.y;

        pitch =
            0f;


        currentVelocity =
            Vector3.zero;

        verticalVelocity =
            0f;

        jumpRequested =
            false;


        // Reset animation after teleport.
        if (isLocal)
        {
            NetworkAnimationSpeed =
                0f;
        }
    }
}