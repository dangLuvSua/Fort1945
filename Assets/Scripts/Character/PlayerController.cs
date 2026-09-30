using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 3.0f;
    public float runSpeed = 6.0f;
    public float turnSpeed = 10.0f;

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Check for 'K' key press to trigger nervous look around
        if (Input.GetKeyDown(KeyCode.K))
        {
            animator.SetTrigger("LookAround");
        }

        // Movement input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

        bool isRunning = Input.GetKey(KeyCode.LeftShift) && moveDirection.magnitude > 0.1f;
        float currentMoveSpeed = isRunning ? runSpeed : walkSpeed;

        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            transform.Translate(moveDirection * currentMoveSpeed * Time.deltaTime, Space.World);

            float animSpeed = isRunning ? 0.8f : 0.3f;
            animator.SetFloat("Speed", animSpeed);
        }
        else
        {
            animator.SetFloat("Speed", 0f);
        }
    }
}