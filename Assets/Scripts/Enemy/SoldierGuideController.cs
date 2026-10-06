using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls the Soldier Guide's NavMesh movement and animation.
///
/// IMPORTANT:
/// This component is the ONLY component that should directly move
/// the SoldierGuideModel while the soldier is walking.
///
/// NetworkGameManager should control the GAME FLOW, not the transform.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SoldierGuideController : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField]
    private Transform dungeonDestination;

    [SerializeField]
    private float stoppingDistance = 1f;

    [Header("Movement")]
    [SerializeField]
    private float movementSmoothTime = 0.1f;

    [Header("Animation")]
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private string speedParameter = "Speed";

    [SerializeField]
    private float animationSpeedMultiplier = 1f;

    [Header("Startup")]
    [SerializeField]
    private bool startAutomatically = false;

    private NavMeshAgent agent;

    private bool walking;

    private bool destinationReached;

    private float smoothedAnimationSpeed;

    private bool hasSpeedParameter;


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        CacheAnimatorParameters();
    }


    private void Start()
    {
        if (startAutomatically)
        {
            StartWalking();
        }
    }


    private void Update()
    {
        UpdateAnimation();

        CheckDestination();
    }


    // =========================================================
    // START WALKING
    // =========================================================

    public void StartWalking()
    {
        if (agent == null)
        {
            Debug.LogError(
                "[SOLDIER] NavMeshAgent is missing."
            );

            return;
        }

        if (dungeonDestination == null)
        {
            Debug.LogError(
                "[SOLDIER] Dungeon Destination is not assigned."
            );

            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                "[SOLDIER] Soldier is not on the NavMesh."
            );

            return;
        }

        agent.stoppingDistance =
            stoppingDistance;

        agent.isStopped = false;

        destinationReached = false;

        bool pathStarted =
            agent.SetDestination(
                dungeonDestination.position
            );

        if (!pathStarted)
        {
            Debug.LogError(
                "[SOLDIER] Could not calculate " +
                "NavMesh path to dungeon."
            );

            walking = false;
            return;
        }

        walking = true;

        Debug.Log(
            "[SOLDIER] Started NavMesh movement."
        );
    }


    // =========================================================
    // STOP
    // =========================================================

    public void StopWalking()
    {
        walking = false;

        if (agent != null)
        {
            agent.isStopped = true;
        }

        SmoothAnimationTo(0f);
    }


    // =========================================================
    // DESTINATION
    // =========================================================

    private void CheckDestination()
    {
        if (!walking ||
            agent == null ||
            destinationReached)
        {
            return;
        }

        if (agent.pathPending)
            return;

        if (agent.remainingDistance <=
            agent.stoppingDistance + 0.05f)
        {
            if (!agent.hasPath ||
                agent.velocity.sqrMagnitude < 0.01f)
            {
                destinationReached = true;

                walking = false;

                agent.isStopped = true;

                SmoothAnimationTo(0f);

                Debug.Log(
                    "[SOLDIER] Reached dungeon destination."
                );
            }
        }
    }


    // =========================================================
    // ANIMATION
    // =========================================================

    private void UpdateAnimation()
    {
        if (animator == null ||
            !hasSpeedParameter)
        {
            return;
        }

        float targetSpeed = 0f;

        if (walking &&
            agent != null &&
            !agent.isStopped)
        {
            float velocity =
                agent.velocity.magnitude;

            targetSpeed =
                Mathf.Clamp01(
                    velocity /
                    Mathf.Max(
                        agent.speed,
                        0.01f
                    )
                );

            targetSpeed *=
                animationSpeedMultiplier;
        }

        SmoothAnimationTo(targetSpeed);
    }


    private void SmoothAnimationTo(
        float target)
    {
        smoothedAnimationSpeed =
            Mathf.Lerp(
                smoothedAnimationSpeed,
                target,
                1f -
                Mathf.Exp(
                    -12f *
                    Time.deltaTime
                )
            );

        animator.SetFloat(
            speedParameter,
            smoothedAnimationSpeed
        );
    }


    // =========================================================
    // ANIMATOR
    // =========================================================

    private void CacheAnimatorParameters()
    {
        hasSpeedParameter = false;

        if (animator == null)
            return;

        foreach (
            AnimatorControllerParameter parameter
            in animator.parameters)
        {
            if (
                parameter.name ==
                speedParameter &&
                parameter.type ==
                AnimatorControllerParameterType.Float
            )
            {
                hasSpeedParameter = true;
                break;
            }
        }

        if (!hasSpeedParameter)
        {
            Debug.LogWarning(
                "[SOLDIER] Animator does not contain " +
                $"Float parameter '{speedParameter}'."
            );
        }
    }


    // =========================================================
    // PUBLIC STATUS
    // =========================================================

    public bool IsWalking()
    {
        return walking;
    }


    public bool HasReachedDestination()
    {
        return destinationReached;
    }


    public NavMeshAgent GetAgent()
    {
        return agent;
    }


    // =========================================================
    // DEBUG
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (dungeonDestination == null)
            return;

        Gizmos.DrawWireSphere(
            dungeonDestination.position,
            stoppingDistance
        );

        Gizmos.DrawLine(
            transform.position,
            dungeonDestination.position
        );
    }
}