using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class PrisonerCrawler : NetworkBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 8f;

    [Header("Crawling")]
    [SerializeField] private float crawlSpeed = 1.2f;
    [Tooltip("Degrees per second. Mas mataas = mas mabilis lumiko.")]
    [SerializeField] private float turnSpeed = 240f;
    [Tooltip("Hihinto ang enemy sa ganitong layo sa player.")]
    [SerializeField] private float stopDistance = 0.8f;
    [Tooltip("Gaano kadalas nag-u-update ng path papunta sa player.")]
    [SerializeField] private float repathInterval = 0.25f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private string awakeParameter = "IsAwake";

    // Naka-network para makita ng lahat ng players ang animation
    [Networked]
    public NetworkBool IsAwake { get; private set; }

    private NavMeshAgent agent;
    private float repathTimer;
    private bool lastAnimValue;
    private bool animInitialized;

    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        agent = GetComponent<NavMeshAgent>();

        if (agent == null)
        {
            Debug.LogError("[PRISONER] Walang NavMeshAgent sa prefab.", this);
            return;
        }

        if (Object.HasStateAuthority)
        {
            agent.enabled = true;
            agent.speed = crawlSpeed;
            agent.angularSpeed = turnSpeed;
            agent.stoppingDistance = stopDistance;
            agent.Warp(transform.position);
        }
        else
        {
            // Ang host lang ang nagpapagalaw, susunod lang ang iba sa NetworkTransform
            agent.enabled = false;
        }
    }

    // =========================================================
    // AI (HOST LANG)
    // =========================================================

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (agent == null || !agent.enabled)
        {
            Log("Agent is null or disabled.");
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Log("Agent is NOT on the NavMesh.");
            return;
        }

        scanTimer -= Runner.DeltaTime;

        if (scanTimer <= 0f)
        {
            scanTimer = 0.25f;
            cachedTarget = FindNearestPlayer(out cachedDistance);
        }

        if (cachedTarget == null)
        {
            Log("Walang player na nahanap.");
            return;
        }

        if (!IsAwake)
        {
            if (cachedDistance > detectionRange)
            {
                Log($"Masyadong malayo ang player: {cachedDistance:0.0} / {detectionRange}");
                return;
            }

            WakeUp();
        }

        repathTimer -= Runner.DeltaTime;

        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            agent.SetDestination(cachedTarget.position);
        }
    }

    // =========================================================
    // WAKE UP
    // =========================================================

    public void WakeUp()
    {
        if (!Object.HasStateAuthority)
            return;

        IsAwake = true;
    }

    // =========================================================
    // FIND NEAREST PLAYER
    // =========================================================

    private Transform FindNearestPlayer(out float nearestDistance)
    {
        Transform nearest = null;
        nearestDistance = float.MaxValue;

        foreach (PlayerController p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, p.transform.position);

            if (d < nearestDistance)
            {
                nearestDistance = d;
                nearest = p.transform;
            }
        }

        return nearest;
    }

    // =========================================================
    // ANIMATION (LAHAT NG PLAYERS)
    // =========================================================

    public override void Render()
    {
        if (animator == null)
            return;

        bool awake = IsAwake;

        if (!animInitialized || awake != lastAnimValue)
        {
            animInitialized = true;
            lastAnimValue = awake;

            animator.SetBool(awakeParameter, awake);
        }
    }

    private float scanTimer;
    private Transform cachedTarget;
    private float cachedDistance;
    private float nextLogTime;

    private void Log(string msg)
    {
        if (Time.time < nextLogTime) return;
        nextLogTime = Time.time + 2f;
        Debug.Log("[PRISONER] " + msg, this);
    }
}