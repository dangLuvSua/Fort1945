using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SoldierPatrol : NetworkBehaviour
{
    private enum State
    {
        Patrol,
        Chase,
        Search
    }

    [Header("Patrol")]
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float turnSpeed = 240f;
    [SerializeField] private float acceleration = 8f;

    [Tooltip("Gaano kalayo ang pwedeng piliing lugar mula sa kasalukuyang pwesto.")]
    [SerializeField] private float patrolRadius = 12f;

    [Tooltip("Hindi pipili ng lugar na mas malapit pa rito.")]
    [SerializeField] private float minPatrolDistance = 4f;

    [Tooltip("Checked = hindi lalayo ang soldier sa pinag-spawn-an niya.")]
    [SerializeField] private bool stayNearSpawn = false;
    [SerializeField] private float leashRadius = 25f;

    [Tooltip("Gaano katagal titigil pagdating sa pupuntahan (random sa pagitan).")]
    [SerializeField] private float waitMin = 2f;
    [SerializeField] private float waitMax = 5f;

    [SerializeField] private float arriveDistance = 0.4f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 10f;

    [Tooltip("Kabuuang anggulo ng paningin (degrees). 120 = 60 sa kaliwa at 60 sa kanan.")]
    [SerializeField] private float viewAngle = 120f;

    [Tooltip("Kapag mas malapit ang player dito, mapapansin kahit nasa likod.")]
    [SerializeField] private float closeDetectRange = 2.5f;

    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private float targetHeight = 1.0f;

    [Tooltip("Mga layer na humaharang sa paningin (pader, sahig, furniture). " +
             "Huwag isama ang Interactable at RoomBounds.")]
    [SerializeField] private LayerMask obstructionLayers = ~0;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 2.6f;
    [SerializeField] private float chaseStopDistance = 1.2f;
    [SerializeField] private float repathInterval = 0.25f;

    [Tooltip("Ilang segundo bago sumuko kapag hindi na nakikita ang player.")]
    [SerializeField] private float loseSightTime = 4f;

    [Tooltip("Kapag mas malayo ang player dito, titigil ang habol.")]
    [SerializeField] private float loseRange = 20f;

    [SerializeField] private float searchTimeout = 10f;

    [Header("Prison Capture")]
    [Tooltip("A player within this horizontal distance of the soldier is captured and sent to the prison cell.")]
    [SerializeField, Min(0.1f)] private float captureRange = 2.5f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private float animSmoothing = 10f;

    private NavMeshAgent agent;

    private State state = State.Patrol;

    private Vector3 spawnPosition;

    // patrol
    private bool waiting;
    private bool hasDestination;
    private float waitTimer;
    private float stuckTimer;

    // chase / search
    private PlayerController target;
    private Vector3 lastKnownPosition;
    private float timeSinceSeen;
    private float scanTimer;
    private float repathTimer;
    private float searchTimer;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly Dictionary<NetworkId, float> captureCooldowns = new Dictionary<NetworkId, float>();

    // animation (lahat ng players)
    private Vector3 lastPosition;
    private float animSpeed;

    private void OnTriggerEnter(Collider other)
    {
        TryCapturePlayer(other != null ? other.GetComponentInParent<PlayerController>() : null);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryCapturePlayer(collision != null && collision.collider != null
            ? collision.collider.GetComponentInParent<PlayerController>()
            : null);
    }

    private bool TryCapturePlayer(PlayerController player)
    {
        if (player == null || Object == null || !Object.HasStateAuthority ||
            player.Object == null)
            return false;

        NetworkId playerNetworkId = player.Object.Id;
        if (captureCooldowns.TryGetValue(playerNetworkId, out float nextAllowedTime) &&
            Time.time < nextAllowedTime)
            return false;

        captureCooldowns[playerNetworkId] = Time.time + 2f;
        // Resolve the point on the player's own state authority. In Shared
        // mode this can differ from the soldier's authority, and each peer's
        // generated dungeon may have different world-space room coordinates.
        player.RpcTeleportPlayerToPrisonCell();
        Debug.Log($"[SOLDIER] Requested prison-cell teleport for player {playerNetworkId}; that player's authority will resolve its local CellTeleportPoint.", this);
        return true;
    }

    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        agent = GetComponent<NavMeshAgent>();

        spawnPosition = transform.position;
        lastPosition = transform.position;

        if (agent == null)
        {
            Debug.LogError("[SOLDIER] Walang NavMeshAgent sa prefab.", this);
            return;
        }

        if (Object.HasStateAuthority)
        {
            agent.enabled = true;
            agent.speed = walkSpeed;
            agent.angularSpeed = turnSpeed;
            agent.acceleration = acceleration;
            agent.stoppingDistance = 0.1f;
            agent.Warp(transform.position);

            state = State.Patrol;
            BeginWait();
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

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        float dt = Runner.DeltaTime;

        // Maghanap ng player ilang beses kada segundo
        scanTimer -= dt;

        if (scanTimer <= 0f)
        {
            scanTimer = 0.2f;
            ScanForTarget();
        }

        switch (state)
        {
            case State.Patrol:
                UpdatePatrol(dt);
                break;

            case State.Chase:
                UpdateChase(dt);
                break;

            case State.Search:
                UpdateSearch(dt);
                break;
        }
    }

    // =========================================================
    // DETECTION
    // =========================================================

    private void ScanForTarget()
    {
        PlayerController best = null;
        float bestDistance = float.MaxValue;

        float range = state == State.Chase ? loseRange : detectionRange;

        foreach (PlayerController p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, p.transform.position);

            Vector3 flatOffset = p.transform.position - transform.position;
            flatOffset.y = 0f;
            if (flatOffset.sqrMagnitude <= captureRange * captureRange &&
                TryCapturePlayer(p))
            {
                return;
            }

            if (d > range)
                continue;

            // Habang humahabol, hindi na kailangan ng view angle
            bool useFov = state != State.Chase;

            if (!CanSee(p, useFov))
                continue;

            if (d < bestDistance)
            {
                bestDistance = d;
                best = p;
            }
        }

        if (best == null)
            return;

        target = best;
        lastKnownPosition = best.transform.position;
        timeSinceSeen = 0f;

        if (state != State.Chase)
            StartChase();
    }

    private bool CanSee(PlayerController player, bool useFov)
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 point = player.transform.position + Vector3.up * targetHeight;

        Vector3 toTarget = point - eye;
        float distance = toTarget.magnitude;

        if (distance < 0.01f)
            return true;

        // View angle (hindi na kailangan kapag sobrang lapit)
        if (useFov && distance > closeDetectRange)
        {
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);

            if (flat.sqrMagnitude > 0.0001f &&
                Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f)
            {
                return false;
            }
        }

        // Line of sight: ang unang tatamaan (maliban sa sarili) ay dapat ang player
        int count = Physics.RaycastNonAlloc(
            eye,
            toTarget / distance,
            hitBuffer,
            distance,
            obstructionLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearest = float.MaxValue;
        Transform nearestTransform = null;

        for (int i = 0; i < count; i++)
        {
            Transform t = hitBuffer[i].transform;

            if (t.IsChildOf(transform))
                continue;

            if (hitBuffer[i].distance < nearest)
            {
                nearest = hitBuffer[i].distance;
                nearestTransform = t;
            }
        }

        if (nearestTransform == null)
            return true;

        return nearestTransform.GetComponentInParent<PlayerController>() == player;
    }

    // =========================================================
    // CHASE
    // =========================================================

    private void StartChase()
    {
        state = State.Chase;

        waiting = false;
        hasDestination = false;
        repathTimer = 0f;

        agent.speed = chaseSpeed;
        agent.stoppingDistance = chaseStopDistance;
    }

    private void UpdateChase(float dt)
    {
        if (target == null)
        {
            EndChase();
            return;
        }

        timeSinceSeen += dt;

        if (timeSinceSeen >= loseSightTime)
        {
            EndChase();
            return;
        }

        repathTimer -= dt;

        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;

            // Kung nakikita pa, sundan ang player. Kung hindi na, pumunta sa huling nakita.
            Vector3 destination =
                timeSinceSeen < 0.6f
                    ? target.transform.position
                    : lastKnownPosition;

            agent.SetDestination(destination);
        }
    }

    private void EndChase()
    {
        target = null;

        state = State.Search;
        searchTimer = searchTimeout;

        agent.speed = walkSpeed;
        agent.stoppingDistance = 0.1f;
        agent.SetDestination(lastKnownPosition);
    }

    // =========================================================
    // SEARCH
    // =========================================================

    private void UpdateSearch(float dt)
    {
        searchTimer -= dt;

        bool arrived =
            !agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + arriveDistance;

        if (arrived || searchTimer <= 0f)
        {
            state = State.Patrol;
            BeginWait();
        }
    }

    // =========================================================
    // PATROL
    // =========================================================

    private void UpdatePatrol(float dt)
    {
        // Naghihintay
        if (waiting)
        {
            waitTimer -= dt;

            if (waitTimer <= 0f)
            {
                waiting = false;
                StartNewPatrol();
            }

            return;
        }

        if (!hasDestination)
        {
            StartNewPatrol();
            return;
        }

        // Dumating na ba?
        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + arriveDistance)
        {
            BeginWait();
            return;
        }

        // Na-stuck ba? Kung hindi gumagalaw nang matagal, pumili ng bago
        if (agent.velocity.sqrMagnitude < 0.0025f)
        {
            stuckTimer += dt;

            if (stuckTimer >= 2f)
            {
                stuckTimer = 0f;
                StartNewPatrol();
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void StartNewPatrol()
    {
        stuckTimer = 0f;

        if (TryPickPatrolPoint(out Vector3 destination))
        {
            agent.SetDestination(destination);
            hasDestination = true;
        }
        else
        {
            BeginWait();
        }
    }

    private void BeginWait()
    {
        waiting = true;
        hasDestination = false;
        stuckTimer = 0f;
        waitTimer = Random.Range(waitMin, waitMax);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
    }

    private bool TryPickPatrolPoint(out Vector3 result)
    {
        Vector3 center =
            stayNearSpawn
                ? spawnPosition
                : transform.position;

        float radius =
            stayNearSpawn
                ? Mathf.Min(patrolRadius, leashRadius)
                : patrolRadius;

        NavMeshPath path = new NavMeshPath();

        for (int i = 0; i < 12; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;

            // Siguraduhing hindi masyadong malapit
            if (offset.magnitude < minPatrolDistance)
            {
                if (offset.sqrMagnitude < 0.0001f)
                    offset = Vector2.right;

                offset = offset.normalized * minPatrolDistance;
            }

            Vector3 candidate =
                center + new Vector3(offset.x, 0f, offset.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                continue;

            // Dapat maaabot talaga (hindi nakahiwalay na lugar)
            if (agent.CalculatePath(hit.position, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                result = hit.position;
                return true;
            }
        }

        result = transform.position;
        return false;
    }

    // =========================================================
    // ANIMATION (LAHAT NG PLAYERS)
    // =========================================================

    public override void Render()
    {
        if (animator == null || Time.deltaTime <= 0f)
            return;

        float speed =
            (transform.position - lastPosition).magnitude / Time.deltaTime;

        lastPosition = transform.position;

        animSpeed = Mathf.Lerp(animSpeed, speed, animSmoothing * Time.deltaTime);

        animator.SetFloat(speedParameter, animSpeed);
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position;

        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, detectionRange);

        // Close detect range
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(pos, closeDetectRange);

        // View angle
        Gizmos.color = Color.red;

        float half = viewAngle * 0.5f;
        Vector3 left = Quaternion.Euler(0f, -half, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, half, 0f) * transform.forward;

        Gizmos.DrawLine(pos, pos + left * detectionRange);
        Gizmos.DrawLine(pos, pos + right * detectionRange);

        // Patrol radius
        Gizmos.color = Color.cyan;

        Vector3 center =
            Application.isPlaying && stayNearSpawn
                ? spawnPosition
                : pos;

        Gizmos.DrawWireSphere(center, patrolRadius);
    }
}
