using UnityEngine;
using Fusion;

public class PlayerController : NetworkBehaviour
{
    // =========================================================
    // MOVEMENT
    // =========================================================

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;

    [Tooltip("How quickly the player reaches movement speed.")]
    [SerializeField] private float acceleration = 12f;

    [Tooltip("How quickly the player stops.")]
    [SerializeField] private float deceleration = 14f;

    [Tooltip("How quickly the character turns toward movement.")]
    [SerializeField] private float rotationSpeed = 12f;

    [SerializeField] private float gravity = -20f;


    // =========================================================
    // MOUSE LOOK
    // =========================================================

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;

    [Tooltip("Smoothness of character body rotation.")]
    [SerializeField] private float bodySmoothSpeed = 18f;

    [Tooltip("Smoothness of camera rotation.")]
    [SerializeField] private float cameraSmoothSpeed = 18f;


    // =========================================================
    // CAMERA LIMITS
    // =========================================================

    [Header("Camera Look Limits")]
    [SerializeField] private float maxLookUp = 80f;

    [SerializeField] private float maxLookDown = 80f;

    [SerializeField] private float maxLookLeft = 90f;

    [SerializeField] private float maxLookRight = 90f;


    // =========================================================
    // CAMERA
    // =========================================================

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;

    [SerializeField] private AudioListener audioListener;

    [SerializeField] private Transform cameraHolder;


    // =========================================================
    // PLAYER BODY
    // =========================================================

    [Header("Player Body")]
    [SerializeField] private GameObject playerBody;


    // =========================================================
    // ANIMATOR
    // =========================================================

    [Header("Animator")]
    [SerializeField] private string speedParameter = "Speed";


    // =========================================================
    // INTERNAL VARIABLES
    // =========================================================

    private Animator animator;

    private CharacterController characterController;


    // Gravity
    private float verticalVelocity;


    // Current movement velocity
    private Vector3 currentVelocity;


    // =========================================================
    // PLAYER ROTATION
    // =========================================================

    private float targetPlayerYaw;

    private float currentPlayerYaw;


    // =========================================================
    // CAMERA ROTATION
    // =========================================================

    private float targetCameraYaw;

    private float currentCameraYaw;


    private float targetCameraPitch;

    private float currentCameraPitch;


    // =========================================================
    // INPUT
    // =========================================================

    private Vector2 moveInput;


    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        Debug.Log(
            "PLAYER SPAWNED: " +
            Object.InputAuthority
        );


        animator =
            GetComponentInChildren<Animator>();


        characterController =
            GetComponent<CharacterController>();


        if (characterController == null)
        {
            Debug.LogError(
                "NO CHARACTER CONTROLLER FOUND ON PLAYER1!"
            );
        }


        if (animator == null)
        {
            Debug.LogError(
                "NO ANIMATOR FOUND IN BOY1!"
            );
        }


        // =====================================================
        // INITIAL ROTATION
        // =====================================================

        currentPlayerYaw =
            transform.eulerAngles.y;

        targetPlayerYaw =
            currentPlayerYaw;


        currentCameraYaw = 0f;
        targetCameraYaw = 0f;


        currentCameraPitch = 0f;
        targetCameraPitch = 0f;


        SetupLocalPlayer();
    }


    // =========================================================
    // LOCAL PLAYER / CAMERA SETUP
    // =========================================================

    private void SetupLocalPlayer()
    {
        bool isLocalPlayer =
            Object.HasStateAuthority;


        if (isLocalPlayer)
        {
            // Enable ONLY our camera
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
            }


            // Enable ONLY our audio listener
            if (audioListener != null)
            {
                audioListener.enabled = true;
            }


            Cursor.lockState =
                CursorLockMode.Locked;

            Cursor.visible = false;
        }
        else
        {
            // Disable cameras belonging to other players
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
            }


            if (audioListener != null)
            {
                audioListener.enabled = false;
            }
        }
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (Object == null)
            return;


        // Only local player reads mouse/input
        if (!Object.HasStateAuthority)
            return;


        ReadInput();

        HandleMouseLook();
    }


    // =========================================================
    // READ INPUT
    // =========================================================

    private void ReadInput()
    {
        float horizontal =
            Input.GetAxisRaw("Horizontal");

        float vertical =
            Input.GetAxisRaw("Vertical");


        moveInput =
            new Vector2(
                horizontal,
                vertical
            );


        moveInput =
            Vector2.ClampMagnitude(
                moveInput,
                1f
            );
    }


    // =========================================================
    // MOUSE LOOK
    // =========================================================

    private void HandleMouseLook()
    {
        float mouseX =
            Input.GetAxis("Mouse X");


        float mouseY =
            Input.GetAxis("Mouse Y");


        // =====================================================
        // HORIZONTAL CAMERA
        // =====================================================

        targetCameraYaw +=
            mouseX *
            mouseSensitivity;


        // Camera exceeds right boundary
        if (targetCameraYaw > maxLookRight)
        {
            float overflow =
                targetCameraYaw -
                maxLookRight;


            targetPlayerYaw += overflow;


            targetCameraYaw =
                maxLookRight;
        }


        // Camera exceeds left boundary
        if (targetCameraYaw < -maxLookLeft)
        {
            float overflow =
                targetCameraYaw +
                maxLookLeft;


            targetPlayerYaw += overflow;


            targetCameraYaw =
                -maxLookLeft;
        }


        // =====================================================
        // VERTICAL CAMERA
        // =====================================================

        targetCameraPitch -=
            mouseY *
            mouseSensitivity;


        targetCameraPitch =
            Mathf.Clamp(
                targetCameraPitch,
                -maxLookDown,
                maxLookUp
            );
    }


    // =========================================================
    // FIXED NETWORK UPDATE
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;


        HandleMovement();

        HandleSmoothRotation();
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    private void HandleMovement()
    {
        // =====================================================
        // GET MOVEMENT DIRECTION
        // =====================================================

        Vector3 forward =
            transform.forward;

        Vector3 right =
            transform.right;


        Vector3 desiredDirection =
            forward * moveInput.y +
            right * moveInput.x;


        desiredDirection =
            Vector3.ClampMagnitude(
                desiredDirection,
                1f
            );


        // =====================================================
        // SPEED
        // =====================================================

        bool isRunning =
            Input.GetKey(KeyCode.LeftShift) &&
            moveInput.magnitude > 0.01f;


        float targetSpeed =
            isRunning
                ? runSpeed
                : walkSpeed;


        Vector3 targetVelocity =
            desiredDirection *
            targetSpeed;


        // =====================================================
        // SMOOTH ACCELERATION / DECELERATION
        // =====================================================

        float smoothRate =
            desiredDirection.magnitude > 0.01f
                ? acceleration
                : deceleration;


        currentVelocity =
            Vector3.MoveTowards(
                currentVelocity,
                targetVelocity,
                smoothRate *
                Runner.DeltaTime
            );


        // =====================================================
        // GRAVITY
        // =====================================================

        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
        }
        else
        {
            verticalVelocity +=
                gravity *
                Runner.DeltaTime;
        }


        // =====================================================
        // APPLY MOVEMENT
        // =====================================================

        Vector3 finalMovement =
            currentVelocity;


        finalMovement.y =
            verticalVelocity;


        characterController.Move(
            finalMovement *
            Runner.DeltaTime
        );


        // =====================================================
        // ANIMATION
        // =====================================================

        UpdateAnimator(
            currentVelocity,
            isRunning
        );
    }


    // =========================================================
    // CHARACTER ROTATION
    // =========================================================

    private void HandleSmoothRotation()
    {
        // =====================================================
        // MOVEMENT ROTATION
        // =====================================================

        Vector3 horizontalVelocity =
            new Vector3(
                currentVelocity.x,
                0f,
                currentVelocity.z
            );


        if (horizontalVelocity.magnitude > 0.05f)
        {
            float movementYaw =
                Mathf.Atan2(
                    horizontalVelocity.x,
                    horizontalVelocity.z
                ) *
                Mathf.Rad2Deg;


            /*
             * Only rotate toward movement when
             * the player is actually moving.
             */

            targetPlayerYaw =
                Mathf.LerpAngle(
                    targetPlayerYaw,
                    movementYaw,
                    rotationSpeed *
                    Runner.DeltaTime
                );


            /*
             * If movement turns the body,
             * keep camera relative to the body.
             */
            targetCameraYaw = 0f;
        }


        // =====================================================
        // SMOOTH BODY ROTATION
        // =====================================================

        currentPlayerYaw =
            Mathf.LerpAngle(
                currentPlayerYaw,
                targetPlayerYaw,
                bodySmoothSpeed *
                Runner.DeltaTime
            );


        transform.rotation =
            Quaternion.Euler(
                0f,
                currentPlayerYaw,
                0f
            );


        // =====================================================
        // SMOOTH CAMERA
        // =====================================================

        currentCameraYaw =
            Mathf.Lerp(
                currentCameraYaw,
                targetCameraYaw,
                cameraSmoothSpeed *
                Runner.DeltaTime
            );


        currentCameraPitch =
            Mathf.Lerp(
                currentCameraPitch,
                targetCameraPitch,
                cameraSmoothSpeed *
                Runner.DeltaTime
            );


        if (cameraHolder != null)
        {
            cameraHolder.localRotation =
                Quaternion.Euler(
                    currentCameraPitch,
                    currentCameraYaw,
                    0f
                );
        }
    }


    // =========================================================
    // ANIMATOR
    // =========================================================

    private void UpdateAnimator(
        Vector3 velocity,
        bool isRunning
    )
    {
        if (animator == null)
            return;


        Vector3 horizontalVelocity =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            );


        float normalizedSpeed =
            horizontalVelocity.magnitude /
            runSpeed;


        if (normalizedSpeed < 0.01f)
        {
            animator.SetFloat(
                speedParameter,
                0f
            );
        }
        else if (isRunning)
        {
            animator.SetFloat(
                speedParameter,
                0.8f
            );
        }
        else
        {
            animator.SetFloat(
                speedParameter,
                0.3f
            );
        }
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDisable()
    {
        if (Object != null &&
            Object.HasStateAuthority)
        {
            Cursor.lockState =
                CursorLockMode.None;

            Cursor.visible = true;
        }
    }
}