using UnityEngine.SceneManagement;
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

    [Header("Capture")]
    [SerializeField] private float captureLookDuration = 0.5f;

    [Tooltip("Dagdag na tingala (degrees) habang nakatingin sa soldier. 0 = diretso sa ulo niya.")]
    [Range(0f, 60f)]
    [SerializeField] private float captureLookUpAngle = 15f;

    private bool isBeingCaptured;
    private float captureTimer;
    private float captureStartYaw;
    private float captureStartPitch;
    private float captureTargetYaw;
    private float captureTargetPitch;

    [Header("Low Sanity Movement / Camera")]
    [SerializeField, Range(0.1f, 1f)] private float lowestSanitySpeedMultiplier = 0.55f;
    [SerializeField, Range(0f, 8f)] private float maximumDizzyCameraRoll = 2.5f;
    [SerializeField, Min(0f)] private float dizzyCameraRollSpeed = 1.5f;
    [SerializeField, Range(0.1f, 1f)] private float lowestSanityLookMultiplier = 0.4f;
    [SerializeField, Range(0f, 1f)] private float disableSprintAtSanityPercent = 0.25f;
    [SerializeField, Min(0f)] private float jumpscareDizzinessRecoveryTime = 3f;
    [SerializeField, Range(0f, 20f)] private float jumpscareDizzinessRoll = 7f;

    private float jumpscareDizzinessUntil;
    private float jumpscareDizzinessDuration;
    private float jumpscareDizzinessStrength;


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
    // SCENE-SPECIFIC SIZING
    // =========================================================

    [Header("Scene-Specific Sizing")]
    [Tooltip("If true, adjusts CharacterController height depending on the active scene.")]
    [SerializeField] private bool enableSceneHeightAdjustment = true;

    [Tooltip("Name of the scene that requires a taller character.")]
    [SerializeField] private string tallerSceneName = "Dungeon";

    [Tooltip("CharacterController height in the taller scene.")]
    [SerializeField] private float dungeonHeight = 1.5f;

    [Tooltip("CharacterController center Y in the taller scene (usually Height / 2).")]
    [SerializeField] private float dungeonCenterY = 0.75f;

    [Tooltip("Camera local Y position in the taller scene.")]
    [SerializeField] private float dungeonCameraY = 1.3f;

    [Header("Default Sizing (Other Scenes)")]
    [SerializeField] private float defaultHeight = 1.5f;
    [SerializeField] private float defaultCenterY = 0.75f;
    [SerializeField] private float defaultCameraY = 1.3f;


    // =========================================================
    // PLAYER BODY & SCALING
    // =========================================================

    [Header("Player Body")]
    [SerializeField] private GameObject playerBody;

    [Header("Scene-Specific Model Scaling")]
    [Tooltip("If true, scales the player model and adjusts collider depending on the active scene.")]
    [SerializeField] private bool enableSceneScaleAdjustment = true;

    [Tooltip("Player scale in the dungeon scene.")]
    [SerializeField] private float dungeonPlayerScale = 1.25f;

    [Tooltip("Extra collision radius in the Dungeon to keep the player slightly clear of walls.")]
    [SerializeField, Range(0f, 0.2f)] private float dungeonWallClearance = 0.05f;

    [Tooltip("Player scale in default scenes.")]
    [SerializeField] private float defaultPlayerScale = 1.0f;

    private float baseColliderRadius = -1f;


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
    private NetworkTransform networkTransform;   // NEW
    private PlayerSanity playerSanity;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private Vector2 moveInput;

    private bool runInput;
    private float movementLockedUntil;
    private bool skipMovementAfterTeleport;
    private bool verifyTeleportOnNextNetworkTick;
    private bool verifyTeleportAfterMovement;
    private Vector3 expectedTeleportPosition;
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
        characterController = GetComponent<CharacterController>();
        networkTransform = GetComponent<NetworkTransform>();   // NEW
        playerSanity = GetComponent<PlayerSanity>();

        if (characterController == null)
            return;

        characterController.enabled = true;

        // Apply scene-specific character controller height & camera position
        ApplySceneHeight();
        ApplySceneScale();

        // The game uses Fusion Shared mode: the local player owns their
        // object through State Authority.
        isLocal = Object.HasStateAuthority;

        TryFindAnimator();

        yaw = transform.eulerAngles.y;
        pitch = 0f;

        SetupCamera();

        if (isLocal)
        {
            SetupInput();
        }

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
                animatorSearchTimer = AnimatorSearchInterval;
                TryFindAnimator();
            }
        }

        if (animator != null)
        {
            animator.SetFloat(speedParameter, NetworkAnimationSpeed);
        }
    }


    // =========================================================
    // FIND ANIMATOR
    // =========================================================

    private void TryFindAnimator()
    {
        if (animator != null)
            return;

        animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            animatorSearchTimer = 0f;
        }
    }


    // =========================================================
    // CAMERA SETUP
    // =========================================================

    private void SetupCamera()
    {
        if (playerCamera != null)
            playerCamera.enabled = isLocal;

        if (audioListener != null)
            audioListener.enabled = isLocal;
    }


    // =========================================================
    // INPUT SETUP
    // =========================================================

    private void SetupInput()
    {
        if (inputActions == null)
            return;

        // Clone the asset so every player has its own input state.
        InputActionAsset actions = Instantiate(inputActions);

        InputActionMap map = actions.FindActionMap("Player", true);

        moveAction = map.FindAction("Move", true);
        lookAction = map.FindAction("Look", true);
        sprintAction = map.FindAction("Sprint", true);
        jumpAction = map.FindAction("Jump", true);

        map.Enable();

        inputActions = actions;
        inputInitialized = true;

        LockCursor(true);
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (Object == null)
            return;

        if (!Object.HasStateAuthority)
            return;

        bool optionsOwnCursor = InGameOptionsController.Instance != null &&
            InGameOptionsController.Instance.isActiveAndEnabled;

        if (!optionsOwnCursor && Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // The options canvas is inactive after Back to Game, so its Update
            // cannot handle Escape until a HUD button opens it again.
            if (InGameOptionsController.Instance != null)
            {
                InGameOptionsController.Instance.ToggleSettingsRoot();
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        if ((!optionsOwnCursor || !InGameOptionsController.Instance.IsAnyPanelOpen) &&
            Mouse.current != null &&
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

        if (isBeingCaptured)
        {
            UpdateCaptureLook();
            return;
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
            return;

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

        runInput =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpRequested = true;
        }
    }


    // =========================================================
    // MOUSE LOOK
    // =========================================================

    private void HandleLook()
    {
        float lookMultiplier = 1f;
        if (playerSanity != null)
        {
            lookMultiplier = Mathf.Lerp(
                1f,
                lowestSanityLookMultiplier,
                playerSanity.LowSanitySeverity
            );
        }

        Vector2 look = lookAction != null
            ? lookAction.ReadValue<Vector2>() * mouseSensitivity * lookMultiplier
            : Vector2.zero;

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
            float sanitySeverity = playerSanity != null
                ? playerSanity.LowSanitySeverity
                : 0f;
            float jumpscareSeverity = GetJumpscareDizzinessSeverity();
            float dizzyRoll = Mathf.Sin(Time.time * dizzyCameraRollSpeed) *
                              (maximumDizzyCameraRoll * sanitySeverity +
                               jumpscareDizzinessRoll * jumpscareSeverity);
            cameraHolder.localRotation =
                Quaternion.Euler(pitch, 0f, dizzyRoll);
        }
    }

    /// <summary>Applies a temporary camera sway after a jumpscare.</summary>
    public void ApplyJumpscareDizziness(float strength = 1f, float duration = -1f)
    {
        jumpscareDizzinessStrength = Mathf.Clamp01(strength);
        float effectDuration = duration > 0f ? duration : jumpscareDizzinessRecoveryTime;
        jumpscareDizzinessDuration = Mathf.Max(0.01f, effectDuration);
        jumpscareDizzinessUntil = Time.unscaledTime + effectDuration;
    }

    private float GetJumpscareDizzinessSeverity()
    {
        if (jumpscareDizzinessStrength <= 0f || jumpscareDizzinessUntil <= Time.unscaledTime)
        {
            jumpscareDizzinessStrength = 0f;
            return 0f;
        }

        float remaining = jumpscareDizzinessUntil - Time.unscaledTime;
        return jumpscareDizzinessStrength * Mathf.Clamp01(
            remaining / jumpscareDizzinessDuration);
    }


    // =========================================================
    // FIXED NETWORK UPDATE
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (verifyTeleportOnNextNetworkTick)
        {
            verifyTeleportOnNextNetworkTick = false;
            float error = Vector3.Distance(transform.position, expectedTeleportPosition);
            Debug.Log(
                $"[PRISON TELEPORT CHECK] Player {Object.Id} next state tick: " +
                $"expected={expectedTeleportPosition}, actual={transform.position}, " +
                $"error={error:F3}m, moveInput={moveInput}, locked={Time.time < movementLockedUntil}.", this);
        }

        // Apply horizontal player rotation
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (skipMovementAfterTeleport)
        {
            // CharacterController can retain its pre-teleport physics position
            // until a simulation step has synchronized the new transform. Do
            // not let Move() restore that stale position on the first tick.
            skipMovementAfterTeleport = false;
        }
        else
        {
            HandleMovement();
        }

        if (verifyTeleportAfterMovement)
        {
            verifyTeleportAfterMovement = false;
            float errorAfterMove = Vector3.Distance(transform.position, expectedTeleportPosition);
            Debug.Log(
                $"[PRISON TELEPORT AFTER MOVE] Player {Object.Id}: " +
                $"expected={expectedTeleportPosition}, actual={transform.position}, " +
                $"error={errorAfterMove:F3}m, grounded={characterController != null && characterController.isGrounded}, " +
                $"controllerBottom={(characterController != null ? characterController.bounds.min.y : float.NaN):F3}, " +
                $"verticalVelocity={verticalVelocity:F3}.", this);
        }
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    private void HandleMovement()
    {
        Vector2 effectiveMoveInput = Time.time < movementLockedUntil ? Vector2.zero : moveInput;
        Vector3 desiredDirection =
            transform.forward * effectiveMoveInput.y +
            transform.right * effectiveMoveInput.x;

        desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);

        bool wantsRun =
            runInput &&
            effectiveMoveInput.magnitude > 0.01f &&
            (playerSanity == null ||
             playerSanity.SanityPercent > disableSprintAtSanityPercent);

        bool isRunning =
            stamina != null
                ? stamina.Tick(wantsRun, Runner.DeltaTime)
                : wantsRun;

        float targetSpeed = isRunning ? runSpeed : walkSpeed;

        if (playerSanity != null)
        {
            float sanitySlow = Mathf.Lerp(
                1f,
                lowestSanitySpeedMultiplier,
                playerSanity.LowSanitySeverity
            );
            targetSpeed *= sanitySlow;
        }

        Vector3 targetVelocity = desiredDirection * targetSpeed;

        float rate =
            desiredDirection.magnitude > 0.01f
                ? acceleration
                : deceleration;

        currentVelocity =
            Vector3.MoveTowards(
                currentVelocity,
                targetVelocity,
                rate * Runner.DeltaTime
            );

        // GROUND
        bool isGrounded = characterController.isGrounded;

        // JUMP
        if (isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (jumpRequested)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        jumpRequested = false;

        // GRAVITY
        verticalVelocity += gravity * Runner.DeltaTime;

        // FINAL MOVEMENT
        Vector3 finalMovement = currentVelocity;
        finalMovement.y = verticalVelocity;

        characterController.Move(finalMovement * Runner.DeltaTime);

        // ANIMATION
        UpdateNetworkAnimation(currentVelocity, isRunning);
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
            new Vector3(velocity.x, 0f, velocity.z).magnitude;

        horizontalSpeed /= Mathf.Max(runSpeed, 0.01f);

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

        Vector3 rayOrigin = transform.position + Vector3.up * 2f;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            10f,
            groundLayers,
            QueryTriggerInteraction.Ignore))
        {
            Vector3 safePosition = hit.point + Vector3.up * recoveryHeight;

            CharacterController cc = characterController;

            if (cc != null)
                cc.enabled = false;

            transform.position = safePosition;

            if (cc != null)
                cc.enabled = true;

            verticalVelocity = 0f;
            currentVelocity = Vector3.zero;
        }
    }


    // =========================================================
    // FALL RECOVERY
    // =========================================================

    private void RecoverFromFall()
    {
        if (!isLocal)
            return;

        Vector3 recoveryPosition = FindNearestSpawnPoint();

        CharacterController cc = characterController;

        if (cc != null)
            cc.enabled = false;

        transform.SetPositionAndRotation(
            recoveryPosition,
            Quaternion.Euler(0f, yaw, 0f)
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
            GameObject.FindGameObjectsWithTag("PlayerSpawn");

        if (spawnPoints.Length == 0)
            return Vector3.up * 5f;

        GameObject closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (GameObject spawnPoint in spawnPoints)
        {
            float distance = Vector3.Distance(
                transform.position,
                spawnPoint.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = spawnPoint;
            }
        }

        Vector3 rayOrigin = closest.transform.position + Vector3.up * 10f;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            50f,
            groundLayers,
            QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * recoveryHeight;
        }

        return closest.transform.position + Vector3.up * recoveryHeight;
    }


    // =========================================================
    // CURSOR
    // =========================================================

    private void LockCursor(bool locked)
    {
        Cursor.lockState =
            locked ? CursorLockMode.Locked : CursorLockMode.None;

        Cursor.visible = !locked;
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        // PlayerController may have been disabled temporarily
        // by puzzle mode.
        //
        // Spawned() creates/enables the input map only once.
        // If this component is re-enabled later, we must
        // enable that same input map again.
        if (isLocal &&
            inputInitialized &&
            inputActions != null)
        {
            InputActionMap playerMap =
                inputActions.FindActionMap(
                    "Player",
                    false
                );

            if (playerMap != null)
            {
                playerMap.Enable();

                Debug.Log(
                    "[PLAYER CONTROLLER] Player input re-enabled."
                );
            }
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (isLocal &&
            inputInitialized &&
            inputActions != null)
        {
            InputActionMap playerMap =
                inputActions.FindActionMap(
                    "Player",
                    false
                );

            if (playerMap != null)
            {
                playerMap.Disable();
            }

            // Puzzle mode needs the cursor unlocked
            // while the player is interacting with UI/pieces.
            LockCursor(false);
        }
    }


    // =========================================================
    // SCENE HEIGHT ADJUSTMENT
    // =========================================================

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneHeight();
        ApplySceneScale();
    }

    public void ApplySceneHeight()
    {
        if (!enableSceneHeightAdjustment || characterController == null)
            return;

        string currentScene = SceneManager.GetActiveScene().name;
        bool isDungeon = currentScene.Equals(tallerSceneName, System.StringComparison.OrdinalIgnoreCase);

        float targetHeight = isDungeon ? dungeonHeight : defaultHeight;
        float targetCenterY = isDungeon ? dungeonCenterY : defaultCenterY;
        float targetCameraY = isDungeon ? dungeonCameraY : defaultCameraY;

        characterController.enabled = false;
        characterController.height = targetHeight;
        characterController.center = new Vector3(characterController.center.x, targetCenterY, characterController.center.z);
        characterController.enabled = true;

        if (cameraHolder != null)
        {
            Vector3 camPos = cameraHolder.localPosition;
            camPos.y = targetCameraY;
            cameraHolder.localPosition = camPos;
        }

        Debug.Log($"[PlayerController] Scene: '{currentScene}' | Height: {targetHeight} | CenterY: {targetCenterY} | CameraY: {targetCameraY}");
    }

    public void ApplySceneScale()
    {
        if (!enableSceneScaleAdjustment || characterController == null)
            return;

        if (baseColliderRadius <= 0f)
            baseColliderRadius = characterController.radius;

        string currentScene = SceneManager.GetActiveScene().name;
        bool isDungeon = currentScene.Equals(tallerSceneName, System.StringComparison.OrdinalIgnoreCase);

        float targetScale = isDungeon ? dungeonPlayerScale : defaultPlayerScale;

        // Scale player body if assigned, otherwise find "Player" child or use transform
        Transform bodyTransform = playerBody != null 
            ? playerBody.transform 
            : transform.Find("Player");

        if (bodyTransform != null)
        {
            bodyTransform.localScale = Vector3.one * targetScale;
        }

        // Match collider height to the visual scale, but keep the capsule's
        // base width so the enlarged Dungeon model can still pass through
        // the generated corridor clearance.
        characterController.enabled = false;
        characterController.height *= targetScale;
        characterController.center = new Vector3(
            characterController.center.x,
            characterController.center.y * targetScale,
            characterController.center.z
        );
        characterController.radius = baseColliderRadius +
            (isDungeon ? dungeonWallClearance : 0f);
        characterController.enabled = true;

        if (cameraHolder != null)
        {
            Vector3 camPos = cameraHolder.localPosition;
            camPos.y *= targetScale;
            cameraHolder.localPosition = camPos;
        }

        Debug.Log($"[PlayerController] Scene: '{currentScene}' | Applied Player Scale: {targetScale} | Collider Height: {characterController.height}");
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

        transform.SetPositionAndRotation(position, rotation);

        characterController.enabled = true;

        // NEW: tell the NetworkTransform this is a teleport, otherwise it can
        // interpolate or re-apply the old networked position over ours.
        if (networkTransform == null)
            networkTransform = GetComponent<NetworkTransform>();

        if (networkTransform != null && Object != null && Object.HasStateAuthority)
            networkTransform.Teleport(position, rotation);

        // NEW: make physics see the new position immediately.
        Physics.SyncTransforms();

        yaw = rotation.eulerAngles.y;
        pitch = 0f;

        currentVelocity = Vector3.zero;
        verticalVelocity = 0f;
        jumpRequested = false;
        moveInput = Vector2.zero;
        runInput = false;
        movementLockedUntil = Time.time + 0.25f;
        skipMovementAfterTeleport = true;

        if (isLocal)
        {
            NetworkAnimationSpeed = 0f;
        }
    }

    /// <summary>Teleports this player's state authority to the Fort 2 ending marker.</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcTeleportToEnding(Vector3 position, Quaternion rotation)
    {
        if (Object == null || !Object.HasStateAuthority)
            return;

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (characterController == null)
        {
            Debug.LogError($"[ENDING] Player {Object.Id} has no CharacterController; ending teleport aborted.", this);
            return;
        }

        Teleport(position, rotation);
    }

    /// <summary>Resolves and teleports this player to the prison cell on this player's state authority.</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcTeleportPlayerToPrisonCell()
    {
        Vector3 previousPosition = transform.position;
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (characterController == null)
        {
            Debug.LogError($"[PRISON TELEPORT] Player {Object.Id} has no CharacterController; teleport aborted.", this);
            return;
        }

        DungeonGenerator generator = FindFirstObjectByType<DungeonGenerator>();
        if (generator == null ||
            !generator.TryGetPrisonCaptureSpawn(out Vector3 position, out Quaternion rotation))
        {
            Debug.LogError(
                $"[PRISON TELEPORT] Player {Object.Id}'s state authority could not resolve CellTeleportPoint in its local generated dungeon. Teleport aborted.",
                this);
            return;
        }

        Debug.Log(
            $"[PRISON TELEPORT] Player {Object.Id}'s state authority resolved its local CellTeleportPoint to {position}; applying teleport now.", this);

        float bottomOffset = characterController.height * 0.5f - characterController.center.y;
        Vector3 playerRootPosition = position + Vector3.up * bottomOffset;

        Teleport(playerRootPosition, rotation);
        expectedTeleportPosition = playerRootPosition;
        verifyTeleportOnNextNetworkTick = true;
        verifyTeleportAfterMovement = true;
        Debug.Log(
            $"[PRISON TELEPORT] Player {Object.Id} moved from {previousPosition} to {transform.position} " +
            $"on state authority. Floor={position}, CharacterController bottom offset={bottomOffset:F2}, " +
            $"lock=0.25s.", this);
    }

    // Ilagay ang parehong [Rpc(...)] attribute ng RpcTeleportPlayerToPrisonCell
    // Ilagay ang parehong [Rpc(...)] attribute ng RpcTeleportPlayerToPrisonCell
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcCaptureAndTeleport(Vector3 lookTarget)
    {
        if (isBeingCaptured)
            return;

        isBeingCaptured = true;
        captureTimer = 0f;

        captureStartYaw = yaw;
        captureStartPitch = pitch;
        captureTargetYaw = yaw;
        captureTargetPitch = pitch;

        Transform camTransform = cameraHolder != null ? cameraHolder : transform;
        Vector3 dir = lookTarget - camTransform.position;

        if (dir.sqrMagnitude > 0.0001f)
        {
            captureTargetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            float vertical = Mathf.Clamp(dir.y / dir.magnitude, -1f, 1f);

            // Positive pitch = pababa, negative = pataas.
            // Binabawasan natin ng captureLookUpAngle para mas tumingala.
            float pitchToTarget = -Mathf.Asin(vertical) * Mathf.Rad2Deg;

            captureTargetPitch = Mathf.Clamp(
                pitchToTarget - captureLookUpAngle,
                -maxLookUp,
                maxLookDown
            );
        }

        // Huminto agad ang player
        moveInput = Vector2.zero;
        runInput = false;
        jumpRequested = false;
    }
    private void UpdateCaptureLook()
    {
        moveInput = Vector2.zero;
        runInput = false;
        jumpRequested = false;

        captureTimer += Time.deltaTime;

        float t = Mathf.Clamp01(captureTimer / Mathf.Max(0.01f, captureLookDuration));
        float ease = Mathf.SmoothStep(0f, 1f, t);

        yaw = Mathf.LerpAngle(captureStartYaw, captureTargetYaw, ease);
        pitch = Mathf.Lerp(captureStartPitch, captureTargetPitch, ease);

        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        if (t >= 1f)
        {
            RpcTeleportPlayerToPrisonCell();
            isBeingCaptured = false;
        }
    }
}
