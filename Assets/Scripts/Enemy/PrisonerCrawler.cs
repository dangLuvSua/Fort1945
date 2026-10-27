using Fusion;
using UnityEngine;
using UnityEngine.Audio;
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

    [Header("Prison Limit")]
    [Tooltip("Checked = mag-te-teleport agad pabalik. Unchecked = maglalakad pabalik.")]
    [SerializeField] private bool teleportBack = false;

    [Tooltip("Checked = pagbalik niya, matutulog ulit (nakahiga). Unchecked = gising pa rin.")]
    [SerializeField] private bool sleepWhenHome = true;

    [Tooltip("Ilang segundo siyang hindi papansin ang players pagkatapos bumalik.")]
    [SerializeField] private float ignorePlayersAfterReturn = 5f;

    [SerializeField] private float returnArriveDistance = 0.4f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private string awakeParameter = "IsAwake";

    [Header("Audio")]
    [SerializeField] private AudioClip[] crawlSfx;
    [SerializeField] private AudioClip prisonerCrySfx;
    [SerializeField, Range(0f, 1f)] private float crawlSfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float prisonerCryVolume = 1f;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField, Min(0.1f)] private float crawlSfxInterval = 0.55f;
    [SerializeField, Min(0f)] private float crawlSfxIntervalRandomness = 0.15f;
    [SerializeField, Min(0.1f)] private float cryIntervalMin = 7f;
    [SerializeField, Min(0.1f)] private float cryIntervalMax = 13f;
    [SerializeField, Min(0.1f)] private float audioMaxDistance = 22f;

    private AudioSource audioSource;
    private float nextCrawlSfxTime;
    private float nextCryTime;
    private Vector3 previousAudioPosition;
    private bool audioAwakeInitialized;
    private bool lastAudioAwake;

    // Naka-network para makita ng lahat ng players ang animation
    [Networked]
    public NetworkBool IsAwake { get; private set; }

    private NavMeshAgent agent;
    private float repathTimer;
    private bool lastAnimValue;
    private bool animInitialized;

    // Pinag-spawn-an niya (host lang ang gumagamit nito)
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool isReturning;
    private float ignorePlayersUntil;

    // Salt stun/repel timer (state authority lang)
    private float saltStunUntil;

    // =========================================================
    // SPAWNED
    // =========================================================

    public override void Spawned()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        agent = GetComponent<NavMeshAgent>();

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        SetupAudio();

        if (agent == null)
        {
            Debug.LogError("[PRISONER] Walang NavMeshAgent sa prefab.", this);
            return;
        }

        if (Object.HasStateAuthority)
        {
            int cellArea = NavMesh.GetAreaFromName("CrawlerBlocked");
            if (cellArea >= 0)
                agent.areaMask &= ~(1 << cellArea);
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

    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = Mathf.Max(2f, audioMaxDistance);
        audioSource.dopplerLevel = 0f;
        audioSource.outputAudioMixerGroup = sfxMixerGroup;
        previousAudioPosition = transform.position;
        nextCrawlSfxTime = Time.time + Random.Range(0.1f, crawlSfxInterval);
        ScheduleNextCry();
    }

    private void Update()
    {
        if (audioSource == null)
            return;

        bool awake = IsAwake;
        if (!audioAwakeInitialized)
        {
            audioAwakeInitialized = true;
            lastAudioAwake = awake;
        }
        else if (awake != lastAudioAwake)
        {
            lastAudioAwake = awake;
            if (awake)
            {
                PlayCry();
                ScheduleNextCry();
                nextCrawlSfxTime = Time.time + 0.2f;
            }
        }

        Vector3 currentPosition = transform.position;
        bool moving = awake && (currentPosition - previousAudioPosition).sqrMagnitude > 0.0004f;
        previousAudioPosition = currentPosition;

        if (!awake)
            return;

        if (moving && Time.time >= nextCrawlSfxTime)
        {
            PlayRandomCrawlSfx();
            nextCrawlSfxTime = Time.time + Mathf.Max(
                0.1f,
                crawlSfxInterval + Random.Range(-crawlSfxIntervalRandomness, crawlSfxIntervalRandomness));
        }

        if (Time.time >= nextCryTime)
        {
            PlayCry();
            ScheduleNextCry();
        }
    }

    private void PlayRandomCrawlSfx()
    {
        if (crawlSfx == null || crawlSfx.Length == 0)
            return;

        AudioClip clip = crawlSfx[Random.Range(0, crawlSfx.Length)];
        if (clip != null)
            audioSource.PlayOneShot(clip, crawlSfxVolume);
    }

    private void PlayCry()
    {
        if (prisonerCrySfx != null)
            audioSource.PlayOneShot(prisonerCrySfx, prisonerCryVolume);
    }

    private void ScheduleNextCry()
    {
        float min = Mathf.Max(0.1f, cryIntervalMin);
        float max = Mathf.Max(min, cryIntervalMax);
        nextCryTime = Time.time + Random.Range(min, max);
    }

    // =========================================================
    // PRISON DOOR TRIGGER
    // =========================================================

    private void OnTriggerEnter(Collider other)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
            return;

        if (isReturning)
            return;

        if (other.GetComponentInParent<PrisonDoorTrigger>() == null)
            return;

        // Kung tulog pa siya, walang kailangang gawin
        if (!IsAwake)
            return;

        Debug.Log("[PRISONER] Natamaan ang pintuan ng Prison, babalik sa spawn.", this);

        StartReturn();
    }

    private void StartReturn()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        isReturning = true;
        cachedTarget = null;

        if (teleportBack)
        {
            agent.ResetPath();
            agent.Warp(spawnPosition);
            transform.rotation = spawnRotation;

            FinishReturn();
            return;
        }

        agent.stoppingDistance = 0.1f;
        agent.SetDestination(spawnPosition);
    }

    private void UpdateReturn()
    {
        if (agent.pathPending)
            return;

        if (agent.remainingDistance <= agent.stoppingDistance + returnArriveDistance)
        {
            agent.ResetPath();
            transform.rotation = spawnRotation;

            FinishReturn();
        }
    }

    private void FinishReturn()
    {
        isReturning = false;

        agent.stoppingDistance = stopDistance;

        ignorePlayersUntil = Time.time + ignorePlayersAfterReturn;

        if (sleepWhenHome)
            IsAwake = false;

        repathTimer = 0f;
    }

    // =========================================================
    // SALT STUN / REPEL
    // =========================================================

    public void ApplySaltRepel(
        Vector3 sourcePosition,
        float stunDuration,
        float repelDistance)
    {
        if (Object == null || !Object.IsValid)
            return;

        if (stunDuration <= 0f || repelDistance <= 0f)
            return;

        if (!Object.HasStateAuthority)
        {
            RPC_RequestSaltRepel(
                sourcePosition,
                stunDuration,
                repelDistance
            );

            return;
        }

        ApplySaltRepelStateAuthority(
            sourcePosition,
            stunDuration,
            repelDistance
        );
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSaltRepel(
        Vector3 sourcePosition,
        float stunDuration,
        float repelDistance,
        RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        ApplySaltRepelStateAuthority(
            sourcePosition,
            stunDuration,
            repelDistance
        );
    }

    private void ApplySaltRepelStateAuthority(
        Vector3 sourcePosition,
        float stunDuration,
        float repelDistance)
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh ||
            isReturning ||
            !IsAwake)
        {
            return;
        }

        // Kung paulit-ulit ang spray, pahabain lang ang stun.
        saltStunUntil = Mathf.Max(
            saltStunUntil,
            Time.time + stunDuration
        );

        cachedTarget = null;
        scanTimer = 0f;

        Vector3 away = transform.position - sourcePosition;
        away.y = 0f;

        if (away.sqrMagnitude < 0.0001f)
            away = -transform.forward;

        away.Normalize();

        Vector3 repelTarget =
            transform.position + away * repelDistance;

        if (NavMesh.SamplePosition(
                repelTarget,
                out NavMeshHit hit,
                2f,
                agent.areaMask))
        {
            agent.stoppingDistance = stopDistance;
            agent.SetDestination(hit.position);
        }
        else
        {
            agent.ResetPath();
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

        // Pabalik sa spawn, hindi muna papansin ang players
        if (isReturning)
        {
            UpdateReturn();
            return;
        }

        // Natamaan ng asin: lumayo muna at huwag munang humabol
        if (saltStunUntil > 0f)
        {
            if (Time.time < saltStunUntil)
                return;

            saltStunUntil = 0f;

            agent.ResetPath();
            agent.stoppingDistance = stopDistance;

            scanTimer = 0f;
            repathTimer = 0f;
            cachedTarget = null;
        }

        // Kakabalik lang, magpahinga muna bago pumansin ulit
        if (Time.time < ignorePlayersUntil)
            return;

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
