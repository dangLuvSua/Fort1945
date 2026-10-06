using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

public enum GameDifficulty
{
    Easy,
    Normal,
    Difficult
}
public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Networked]
    public GameDifficulty Difficulty { get; private set; }
           = GameDifficulty.Normal;

    public void SetDifficulty(GameDifficulty difficulty)
    {
        if (!networkStateReady)
        {
            Debug.LogWarning(
                "[DIFFICULTY] Cannot set difficulty yet. " +
                "NetworkGameManager has not been spawned."
            );

            return;
        }

        if (!IsHost)
        {
            Debug.LogWarning(
                "[DIFFICULTY] Only the host can change the difficulty."
            );

            return;
        }

        if (GameStarted)
        {
            Debug.LogWarning(
                "[DIFFICULTY] Cannot change difficulty because the game has already started."
            );

            return;
        }

        Difficulty = difficulty;

        Debug.Log(
            $"[DIFFICULTY] Difficulty has been set to: {Difficulty}"
        );
    }
    public string GetDifficultyName()
    {
        return Difficulty.ToString();
    }
    [Networked]
    public NetworkBool GameStarted { get; private set; }

    [Networked]
    public NetworkBool LobbyLocked { get; private set; }

    [Header("Soldier Navigation")]
    [SerializeField] private Transform soldierVisual;
    [SerializeField] private NavMeshAgent soldierAgent;
    private bool networkStateReady;

    public bool IsNetworkStateReady => networkStateReady;

    // =========================================================
    // INTRO FLOW STATE
    // =========================================================
    // The "intro flow" is the ceremonial start of a run:
    //   all ready -> host starts -> gate opens -> everyone enters
    //   -> PING! -> soldier guide walks the ghost path through the
    //   dungeon door -> players step into the RED zone in front of
    //   the door -> it turns GREEN -> countdown -> teleport to the
    //   procedurally generated dungeon scene.
    //
    // The HOST (State Authority) is the source of truth for every
    // transition below. Clients read the replicated state and render
    // the world-space visuals locally.

    [Networked]
    public NetworkBool AllPlayersEnteredGate { get; private set; }

    [Networked]
    public NetworkBool SoldierStarted { get; private set; }

    [Networked]
    public NetworkBool SoldierReachedDoor { get; private set; }

    [Networked]
    public NetworkBool ZoneActive { get; private set; }

    [Networked]
    public NetworkBool ZoneGreen { get; private set; }

    [Networked]
    public NetworkBool TeleportStarted { get; private set; }

    [Networked]
    public Vector3 SoldierPosition { get; private set; }

    [Networked]
    public float SoldierProgress { get; private set; }

    [Networked]
    public float TeleportCountdown { get; private set; }

    [Networked]
    public int DungeonSeed { get; private set; }


    // =========================================================
    // INTRO FLOW CONFIG
    // =========================================================

    [Header("Intro Flow - Zones")]

    [Tooltip(
        "Scene object (BoxCollider trigger) that marks the gate entry zone."
    )]
    [SerializeField]
    private string gateTriggerName = "GateTrigger";

    [Tooltip(
        "Optional. Add an empty GameObject named " +
        "(default) DungeonEntryZone in Fort2 and move it in front " +
        "of the dungeon door. The flow will use its position as the " +
        "zone centre. If missing, zoneFallbackOffset from the gate is used."
    )]
    [SerializeField]
    private string dungeonEntryZoneName = "DungeonEntryZone";

    [Tooltip(
        "Used when there is no DungeonEntryZone object in the scene yet."
    )]
    [SerializeField]
    private Vector3 zoneFallbackOffset =
        new Vector3(30f, 0f, 0f);

    [Tooltip(
        "Half extents of the gate check box (world units)."
    )]
    [SerializeField]
    private Vector3 gateHalfExtents =
        new Vector3(4f, 3.5f, 4f);

    [Tooltip(
        "Half extents of the red/green dungeon zone box (world units)."
    )]
    [SerializeField]
    private Vector3 zoneHalfExtents =
        new Vector3(3.5f, 2.5f, 3.5f);


    // =========================================================
    // INTRO FLOW - SOLDIER
    // =========================================================

    [Header("Intro Flow - Soldier")]

    [Tooltip(
        "Optional. Name of a scene object that should visually " +
        "represent the guide soldier. Example: SoldierGuideModel."
    )]
    [SerializeField]
    private string soldierModelObjectName =
        "SoldierGuideModel";

    [Tooltip(
        "How fast the soldier guide walks the route (world units / second)."
    )]
    [SerializeField]
    private float soldierWalkSpeed = 2.8f;

    [Tooltip(
        "Spacing of the glowing ghost-path dots (world units)."
    )]
    [SerializeField]
    private float ghostPathSpacing = 1.4f;

    [Tooltip(
        "Start the walk route exactly where the SoldierGuideModel " +
        "object sits in the scene instead of the derived gate offset."
    )]
    [SerializeField]
    private bool startFromModelPosition = true;

    [Tooltip(
        "Degrees added to the model's yaw so it faces the travel " +
        "direction. 0 = model's forward is +Z."
    )]
    [SerializeField]
    private float soldierModelForwardYawOffset = 0f;

    [Tooltip(
        "Optional scene object used as the exact NavMesh destination. " +
        "Create an empty GameObject named SoldierDungeonDestination and " +
        "place it just before the dungeon entrance. If empty, the manager " +
        "uses a point in front of DungeonEntryZone."
    )]
    [SerializeField]
    private string soldierDestinationObjectName =
        "SoldierDungeonDestination";

    [Tooltip(
        "NavMesh movement speed. This replaces the old straight-line route movement."
    )]
    [SerializeField]
    private float soldierNavSpeed = 1.8f;

    [Tooltip("NavMesh acceleration used for smooth starts and stops.")]
    [SerializeField]
    private float soldierNavAcceleration = 6f;

    [Tooltip("How close the soldier must get to the destination.")]
    [SerializeField]
    private float soldierNavStoppingDistance = 1f;

    [Tooltip("How quickly the soldier turns while following the NavMesh path.")]
    [SerializeField]
    private float soldierNavAngularSpeed = 360f;

    [Tooltip("How quickly remote clients interpolate toward the replicated soldier position.")]
    [SerializeField]
    private float soldierNetworkLerpSpeed = 12f;

    [Tooltip("Automatically rebuild the visible ghost dots from the NavMesh path.")]
    [SerializeField]
    private bool useNavMeshPathDots = true;


    // =========================================================
    // SOLDIER ANIMATION
    // =========================================================

    [Header("Soldier Animation")]

    [Tooltip(
        "Float Animator parameter used for walking. " +
        "Recommended: Speed."
    )]
    [SerializeField]
    private string soldierSpeedParameter = "Speed";

    [Tooltip(
        "Optional Bool Animator parameter. " +
        "Used if IsWalking exists on the Animator."
    )]
    [SerializeField]
    private string soldierWalkingParameter = "IsWalking";

    [Tooltip(
        "Value sent to the Speed Animator parameter while walking."
    )]
    [SerializeField]
    private float soldierAnimationSpeed = 1f;

    [Tooltip(
        "Artificial vertical bob. Set to 0 when using a real walking animation."
    )]
    [SerializeField]
    private float soldierBobAmount = 0f;


    // =========================================================
    // INTRO FLOW - ZONE TRANSITION
    // =========================================================

    [Header("Intro Flow - Zone Transition")]

    [Tooltip(
        "Seconds the zone stays GREEN before the squad is teleported."
    )]
    [SerializeField]
    private float teleportDelaySeconds = 2.5f;


    // =========================================================
    // INTRO FLOW LOCAL VISUALS
    // =========================================================

    private GameObject gateTag;


    private readonly List<GameObject> pathDots =
        new List<GameObject>();

    private GameObject zoneVisual;

    private GameObject zoneTag;

    private bool soldierVisualBuilt;

    private bool pathDotsBuilt;

    private bool zoneVisualBuilt;

    private GameObject cachedGateObject;

    private GameObject cachedZoneObject;

    private GameObject cachedSoldierModel;

    private Vector3 soldierModelStartPosition =
        Vector3.zero;

    private bool soldierModelStartPositionSet;



    private NavMeshPath soldierNavPath;

    private GameObject cachedSoldierDestination;

    private Vector3 soldierDestinationPosition;

    private bool soldierDestinationSet;

    private bool soldierNavStarted;

    private float soldierInitialPathDistance;

    private Vector3 soldierLastHostPosition;


    // =========================================================
    // SOLDIER ANIMATION INTERNALS
    // =========================================================

    private Animator soldierAnimator;

    private bool soldierAnimatorInitialized;

    private bool soldierAnimatorWarningShown;


    // =========================================================
    // HOST
    // =========================================================

    public bool IsHost
    {
        get
        {
            return Runner != null &&
                   Runner.IsSharedModeMasterClient;
        }
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[GAME MANAGER] Duplicate NetworkGameManager found. " +
                "Destroying duplicate.",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;

        Debug.Log(
            "[GAME MANAGER] Instance assigned."
        );
    }


    private void OnDestroy()
    {
        networkStateReady = false;

        if (Instance == this)
        {
            Instance = null;
        }
    }


    // =========================================================
    // FUSION
    // =========================================================

    public override void Spawned()
    {
        Debug.Log("[GAME MANAGER] Spawned.");
        Debug.Log($"[GAME MANAGER] State Authority: {Object.HasStateAuthority}");
        Debug.Log($"[GAME MANAGER] Is Shared Master: {IsHost}");

        networkStateReady = true;

        if (Object.HasStateAuthority)
        {
            GameStarted = false;
            LobbyLocked = false;

            AllPlayersEnteredGate = false;
            SoldierStarted = false;
            SoldierReachedDoor = false;
            ZoneActive = false;
            ZoneGreen = false;
            TeleportStarted = false;

            SoldierPosition = Vector3.zero;
            SoldierProgress = 0f;
            TeleportCountdown = 0f;
            DungeonSeed = 0;

            Debug.Log("[GAME MANAGER] Network state initialized.");
            Debug.Log($"[DIFFICULTY] Current difficulty: {Difficulty}");
        }
    }

    // =========================================================
    // HOST CHECK
    // =========================================================

    public bool IsPlayerHost(PlayerRef player)
    {
        if (Runner == null)
            return false;

        if (!Runner.IsSharedModeMasterClient)
            return false;

        return player == Runner.LocalPlayer;
    }


    // =========================================================
    // START GAME
    // =========================================================

    public void RequestStartGame()
    {
        if (Runner == null)
        {
            Debug.LogWarning(
                "[GAME MANAGER] Cannot start game. Runner is null."
            );

            return;
        }

        if (!IsHost)
        {
            Debug.LogWarning(
                "[GAME MANAGER] Only the host can start the game."
            );

            return;
        }

        if (GameStarted)
        {
            Debug.LogWarning(
                "[GAME MANAGER] Game is already started."
            );

            return;
        }

        Debug.Log(
            "[GAME MANAGER] Host requested game start."
        );

        RPC_RequestStartGame();
    }


    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    private void RPC_RequestStartGame(
        RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning(
                "[GAME MANAGER] RPC reached non-StateAuthority object."
            );

            return;
        }

        if (GameStarted)
        {
            return;
        }

        Debug.Log(
            "[GAME MANAGER] Checking whether all players are ready..."
        );

        if (!AreAllPlayersReady())
        {
            Debug.LogWarning(
                "[GAME MANAGER] Cannot start. Not all players are ready."
            );

            return;
        }

        GameStarted = true;
        LobbyLocked = true;

        Debug.Log($"[GAME] GAME STARTED.");
        Debug.Log($"[DIFFICULTY] Game starting with difficulty: {Difficulty}");
        Debug.Log("[GAME] Lobby locked.");



    }


    // =========================================================
    // INTRO FLOW
    // =========================================================

    private void Update()
    {
        if (Runner == null)
            return;

        if (!GameStarted)
        {
            return;
        }

        if (TeleportStarted)
            return;

        UpdateGatePhase();
        UpdateSoldierPhase();
        UpdateZonePhase();
    }


    // =========================================================
    // GATE PHASE
    // =========================================================

    private void UpdateGatePhase()
    {
        if (AllPlayersEnteredGate)
            return;

        Vector3 gateCenter =
            GetGateCenter();

        // ------------------------------------------------
        // Show / refresh the gate tag.
        // ------------------------------------------------

        int totalPlayers = 0;
        int playersInsideGate = 0;

        CountPlayersInside(
            gateCenter,
            gateHalfExtents,
            out totalPlayers,
            out playersInsideGate
        );

        if (gateTag == null)
        {
            gateTag =
                CreateWorldTag(
                    gateCenter + Vector3.up * 3.5f,
                    "",
                    new Color(1f, 1f, 0.4f, 1f),
                    40f,
                    0f
                );
        }

        if (gateTag != null)
        {
            WorldTag tagScript =
                gateTag.GetComponent<WorldTag>();

            if (tagScript != null)
            {
                tagScript.SetMessage(
                    "ENTER THE GATE   (" +
                    playersInsideGate +
                    "/" +
                    totalPlayers +
                    ")"
                );
            }
        }

        // ------------------------------------------------
        // Host: decide when everyone is inside the gate.
        // ------------------------------------------------

        if (!Object.HasStateAuthority)
            return;

        if (totalPlayers < 1 ||
            playersInsideGate < totalPlayers)
        {
            return;
        }

        AllPlayersEnteredGate = true;
        SoldierStarted = true;

        // Reset local NavMesh state. The State Authority will start the
        // agent on the next UpdateSoldierPhase call.
        soldierNavStarted = false;
        soldierDestinationSet = false;
        soldierInitialPathDistance = 0f;

        // Anchor the replicated guide position at the scene model start.
        SoldierPosition =
            soldierModelStartPositionSet
                ? soldierModelStartPosition
                : PointAlongRoute(
                    GetSoldierRoute(),
                    0f
                );

        if (NetworkRunnerHandler.Instance != null)
        {
            DungeonSeed =
                NetworkRunnerHandler.Instance.GetSessionSeed();
        }

        Debug.Log(
            "[GAME MANAGER] All players entered the gate. " +
            "Soldier guide starting."
        );

        RPC_OnAllPlayersEntered();
    }


    // =========================================================
    // SOLDIER PHASE
    // =========================================================

    private void UpdateSoldierPhase()
    {
        if (!AllPlayersEnteredGate)
            return;

        BuildSoldierVisual();
        BuildPathDots();

        if (soldierVisual == null)
            return;

        if (!soldierVisual.gameObject.activeSelf)
        {
            soldierVisual.gameObject.SetActive(true);
        }


        // ------------------------------------------------
        // HOST / STATE AUTHORITY
        // ------------------------------------------------
        // The NavMeshAgent is the ONLY thing allowed to move the
        // soldier on the State Authority. This prevents the old
        // route movement and the NavMeshAgent from fighting each other.
        if (Object.HasStateAuthority)
        {
            if (SoldierStarted && !SoldierReachedDoor)
            {
                StartSoldierNavMeshIfNeeded();

                if (soldierAgent != null &&
                    soldierAgent.enabled &&
                    soldierAgent.isOnNavMesh)
                {
                    // Read the position AFTER NavMeshAgent has moved.
                    SoldierPosition =
                        soldierAgent.transform.position;

                    UpdateSoldierProgressFromAgent();

                    if (ReachedSoldierDestination())
                    {
                        soldierAgent.isStopped = true;
                        SoldierReachedDoor = true;
                        ZoneActive = true;
                        SoldierProgress = 1f;

                        Debug.Log(
                            "[GAME MANAGER] Soldier guide reached the dungeon door. " +
                            "Zone active."
                        );
                    }
                }
            }
        }
        else
        {
            // ------------------------------------------------
            // REMOTE CLIENTS
            // ------------------------------------------------
            // Remote clients do NOT run their own NavMeshAgent.
            // They smoothly follow the replicated host position.
            if (soldierAgent != null && soldierAgent.enabled)
            {
                soldierAgent.enabled = false;
            }

            Transform soldierRoot =
                soldierAgent != null
                    ? soldierAgent.transform
                    : soldierVisual.transform.parent != null
                        ? soldierVisual.transform.parent
                        : soldierVisual.transform;

            soldierRoot.position =
                Vector3.Lerp(
                    soldierRoot.position,
                    SoldierPosition,
                    1f - Mathf.Exp(
                        -soldierNetworkLerpSpeed *
                        Time.deltaTime
                    )
                );
        }

        // The NavMeshAgent handles rotation on the host.
        // Remote clients smoothly face the replicated travel direction.
        if (!Object.HasStateAuthority)
        {
            FaceSoldierAlongRoute(soldierRoot: soldierVisual.transform.parent != null
                ? soldierVisual.transform.parent
                : soldierVisual.transform);
        }

        UpdateSoldierAnimation();
    }


    // =========================================================
    // SOLDIER NAVMESH
    // =========================================================

    // =========================================================
    // SOLDIER NAVMESH AGENT INITIALIZATION
    // =========================================================

    // =========================================================
    // SOLDIER NAVMESH AGENT INITIALIZATION
    // =========================================================

    private void InitializeSoldierAgent()
    {
        // =====================================================
        // 1. Already assigned in Inspector
        // =====================================================
        if (soldierAgent != null)
        {
            ConfigureSoldierAgent();
            return;
        }

        // =====================================================
        // 2. Find SoldierGuideController, including inactive objects
        // =====================================================
        SoldierGuideController controller =
            FindFirstObjectByType<SoldierGuideController>(
                FindObjectsInactive.Include
            );

        if (controller != null)
        {
            // Agent may be on the same GameObject
            soldierAgent = controller.GetComponent<NavMeshAgent>();

            // Or somewhere in the parent hierarchy
            if (soldierAgent == null)
            {
                soldierAgent = controller.GetComponentInParent<NavMeshAgent>(true);
            }

            // Or somewhere in the children
            if (soldierAgent == null)
            {
                soldierAgent = controller.GetComponentInChildren<NavMeshAgent>(true);
            }

            if (soldierAgent != null)
            {
                Debug.Log(
                    $"[GAME MANAGER] Found SoldierGuide through SoldierGuideController: " +
                    $"{soldierAgent.gameObject.name}"
                );

                ConfigureSoldierAgent();
                return;
            }
        }

        // =====================================================
        // 3. Find SoldierGuide root, including inactive objects
        // =====================================================
        GameObject soldierRoot = null;

        GameObject[] allObjects =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (GameObject obj in allObjects)
        {
            if (obj.name == "SoldierGuide")
            {
                soldierRoot = obj;
                break;
            }
        }

        if (soldierRoot != null)
        {
            soldierAgent = soldierRoot.GetComponent<NavMeshAgent>();

            if (soldierAgent == null)
            {
                soldierAgent =
                    soldierRoot.GetComponentInChildren<NavMeshAgent>(true);
            }

            if (soldierAgent == null)
            {
                soldierAgent =
                    soldierRoot.GetComponentInParent<NavMeshAgent>(true);
            }

            if (soldierAgent != null)
            {
                Debug.Log(
                    $"[GAME MANAGER] Found SoldierGuide NavMeshAgent on: " +
                    $"{soldierAgent.gameObject.name}"
                );

                ConfigureSoldierAgent();
                return;
            }

            Debug.LogWarning(
                "[GAME MANAGER] SoldierGuide exists, but no NavMeshAgent was found."
            );

            return;
        }

        // =====================================================
        // 4. Nothing found yet
        // =====================================================
        Debug.LogWarning(
            "[GAME MANAGER] SoldierGuide root not found yet. " +
            "Waiting for SoldierGuide to spawn."
        );
    }
    // =========================================================
    // CONFIGURE SOLDIER NAVMESH AGENT
    // =========================================================

    private void ConfigureSoldierAgent()
    {
        if (soldierAgent == null)
            return;

        // ---------------------------------------------------------
        // MOVEMENT
        // ---------------------------------------------------------

        soldierAgent.speed =
            soldierNavSpeed;

        soldierAgent.acceleration =
            soldierNavAcceleration;

        soldierAgent.angularSpeed =
            soldierNavAngularSpeed;

        soldierAgent.stoppingDistance =
            soldierNavStoppingDistance;

        soldierAgent.autoBraking =
            true;

        // ---------------------------------------------------------
        // TRANSFORM CONTROL
        // ---------------------------------------------------------

        soldierAgent.updatePosition =
            true;

        soldierAgent.updateRotation =
            true;

        // Don't force isStopped = false if the manager hasn't
        // actually started the soldier yet.
        //
        // StartSoldierNavMeshIfNeeded() should control this.
        // ---------------------------------------------------------

        Debug.Log(
            "[GAME MANAGER] Soldier NavMeshAgent ready: " +
            soldierAgent.gameObject.name
        );
    }
    private void StartSoldierNavMeshIfNeeded()
    {
        if (soldierNavStarted)
            return;

        if (soldierAgent == null)
            InitializeSoldierAgent();

        if (soldierAgent == null)
            return;

        if (!soldierAgent.enabled)
        {
            Debug.LogWarning(
                "[GAME MANAGER] Soldier NavMeshAgent is disabled. " +
                "Enabling it on the State Authority."
            );

            soldierAgent.enabled = true;
        }

        // ---------------------------------------------------------
        // The NavMeshAgent is on SoldierGuide. Never use the child
        // SoldierGuideModel transform as the movement root.
        // ---------------------------------------------------------
        Vector3 agentStartPosition =
            soldierAgent.transform.position;

        Vector3 navStart;
        Vector3 navDestination;

        if (!TryGetNearestNavMeshPoint(
                agentStartPosition,
                out navStart))
        {
            Debug.LogError(
                "[GAME MANAGER] SoldierGuide could not find a NavMesh " +
                "near its starting position. Make sure the blue NavMesh " +
                "covers the SoldierGuide root. " +
                $"Start: {agentStartPosition}"
            );

            return;
        }

        // ---------------------------------------------------------
        // Attach the agent to the NavMesh.
        //
        // We deliberately avoid Warp here because the previous setup
        // was returning false even though SamplePosition could find a
        // NavMesh. Disabling the agent, moving the movement root, and
        // re-enabling it lets Unity initialize the agent at the sampled
        // NavMesh position.
        // ---------------------------------------------------------
        if (!soldierAgent.isOnNavMesh)
        {
            Debug.Log(
                "[GAME MANAGER] SoldierGuide is off the NavMesh. " +
                $"Moving root from {agentStartPosition} to {navStart}."
            );

            soldierAgent.enabled = false;
            soldierAgent.transform.position = navStart;
            soldierAgent.enabled = true;

            if (!soldierAgent.isOnNavMesh)
            {
                Debug.LogError(
                    "[GAME MANAGER] SoldierGuide could not attach to the " +
                    "NavMesh after repositioning. Check the NavMeshSurface " +
                    "Agent Type, the SoldierGuide Agent Type, and the blue " +
                    "NavMesh under the SoldierGuide root."
                );

                return;
            }
        }

        // ---------------------------------------------------------
        // Resolve destination.
        // ---------------------------------------------------------
        Vector3 destination =
            GetSoldierDestination();

        if (!TryGetNearestNavMeshPoint(
                destination,
                out navDestination))
        {
            Debug.LogError(
                "[GAME MANAGER] Soldier destination is not on or near " +
                "the NavMesh. Make sure SoldierDungeonDestination is on " +
                "the blue NavMesh."
            );

            return;
        }

        soldierDestinationPosition = navDestination;
        soldierDestinationSet = true;

        // Capture the actual movement-root position once.
        if (!soldierModelStartPositionSet)
        {
            soldierModelStartPosition =
                soldierAgent.transform.position;

            soldierModelStartPositionSet = true;
        }

        SoldierPosition =
            soldierAgent.transform.position;

        // ---------------------------------------------------------
        // Configure and start path.
        // ---------------------------------------------------------
        soldierAgent.speed = soldierNavSpeed;
        soldierAgent.acceleration = soldierNavAcceleration;
        soldierAgent.angularSpeed = soldierNavAngularSpeed;
        soldierAgent.stoppingDistance = soldierNavStoppingDistance;
        soldierAgent.autoBraking = true;
        soldierAgent.updatePosition = true;
        soldierAgent.updateRotation = true;
        soldierAgent.isStopped = false;

        bool pathStarted =
            soldierAgent.SetDestination(
                soldierDestinationPosition
            );

        if (!pathStarted)
        {
            Debug.LogError(
                "[GAME MANAGER] NavMeshAgent failed to set the soldier destination."
            );

            return;
        }

        if (soldierAgent.pathStatus ==
            NavMeshPathStatus.PathInvalid)
        {
            Debug.LogError(
                "[GAME MANAGER] Soldier NavMesh path is invalid. " +
                "Check that the blue NavMesh is continuous from SoldierGuide " +
                "to SoldierDungeonDestination and that both use the Humanoid " +
                "agent type."
            );

            soldierAgent.isStopped = true;
            return;
        }

        soldierNavStarted = true;
        soldierLastHostPosition = soldierAgent.transform.position;

        soldierInitialPathDistance =
            Mathf.Max(
                0.1f,
                soldierAgent.remainingDistance
            );

        Debug.Log(
            "[GAME MANAGER] Soldier NavMesh path started. " +
            $"Agent: {soldierAgent.gameObject.name}, " +
            $"Start: {soldierAgent.transform.position}, " +
            $"Destination: {soldierDestinationPosition}, " +
            $"Remaining: {soldierAgent.remainingDistance:F2}"
        );
    }


    private bool TryGetNearestNavMeshPoint(
        Vector3 position,
        out Vector3 result)
    {
        if (NavMesh.SamplePosition(
                position,
                out NavMeshHit hit,
                5f,
                NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = position;
        return false;
    }


    private Vector3 GetSoldierDestination()
    {
        if (cachedSoldierDestination == null)
        {
            cachedSoldierDestination =
                GameObject.Find(
                    soldierDestinationObjectName
                );
        }

        if (cachedSoldierDestination != null)
        {
            return cachedSoldierDestination.transform.position;
        }

        // Fallback: stop a little before the dungeon-entry zone.
        Vector3 zone = GetZoneCenter();
        Vector3 start =
            soldierModelStartPositionSet
                ? soldierModelStartPosition
                : soldierAgent != null
                    ? soldierAgent.transform.position
                    : GetGateCenter();

        Vector3 direction = zone - start;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector3.forward;

        direction.Normalize();

        return zone - direction * 2.2f;
    }


    private bool ReachedSoldierDestination()
    {
        if (soldierAgent == null ||
            !soldierDestinationSet)
        {
            return false;
        }

        if (soldierAgent.pathPending)
            return false;

        if (soldierAgent.remainingDistance >
            soldierAgent.stoppingDistance + 0.05f)
        {
            return false;
        }

        return
            !soldierAgent.hasPath ||
            soldierAgent.velocity.sqrMagnitude < 0.01f;
    }


    private void UpdateSoldierProgressFromAgent()
    {
        if (soldierAgent == null ||
            !soldierDestinationSet)
        {
            return;
        }

        float remaining =
            soldierAgent.remainingDistance;

        float total =
            Mathf.Max(
                0.1f,
                soldierInitialPathDistance
            );

        SoldierProgress =
            Mathf.Clamp01(
                1f - remaining / total
            );

        soldierLastHostPosition =
            soldierAgent.transform.position;
    }


    private bool BuildNavMeshPath(
        Vector3 start,
        Vector3 destination,
        out NavMeshPath path)
    {
        path = new NavMeshPath();

        if (!NavMesh.SamplePosition(
                start,
                out NavMeshHit startHit,
                3f,
                NavMesh.AllAreas))
        {
            return false;
        }

        if (!NavMesh.SamplePosition(
                destination,
                out NavMeshHit destinationHit,
                3f,
                NavMesh.AllAreas))
        {
            return false;
        }

        return NavMesh.CalculatePath(
            startHit.position,
            destinationHit.position,
            NavMesh.AllAreas,
            path
        );
    }


    // =========================================================
    // ZONE PHASE
    // =========================================================

    private void UpdateZonePhase()
    {
        if (ZoneActive && !ZoneGreen)
        {
            if (zoneVisual == null)
            {
                BuildZoneVisual();
            }

            int totalPlayers = 0;
            int playersInsideZone = 0;

            CountPlayersInside(
                GetZoneCenter(),
                zoneHalfExtents,
                out totalPlayers,
                out playersInsideZone
            );

            UpdateZoneTag(
                playersInsideZone,
                totalPlayers,
                false
            );

            if (!Object.HasStateAuthority)
                return;

            if (totalPlayers < 1 ||
                playersInsideZone < totalPlayers)
            {
                return;
            }

            ZoneGreen = true;

            TeleportCountdown =
                Mathf.Max(
                    0f,
                    teleportDelaySeconds
                );

            Debug.Log(
                "[GAME MANAGER] All players inside the zone. " +
                "Zone turned GREEN."
            );

            RPC_OnZoneGreen();

            return;
        }

        if (!ZoneGreen || TeleportStarted)
            return;

        // Every client refreshes the green tag + countdown text.
        UpdateZoneTag(
            0,
            0,
            true
        );

        // ------------------------------------------------
        // Host: countdown then teleport.
        // ------------------------------------------------

        if (Object.HasStateAuthority)
        {
            if (TeleportCountdown > 0f)
            {
                TeleportCountdown =
                    Mathf.Max(
                        0f,
                        TeleportCountdown - Time.deltaTime
                    );
            }

            if (TeleportCountdown > 0f)
                return;

            TeleportStarted = true;

            Debug.Log(
                "[GAME MANAGER] Teleporting the squad to the dungeon!"
            );

            if (NetworkRunnerHandler.Instance != null)
            {
                NetworkRunnerHandler.Instance.LoadDungeonScene();
            }
        }
    }


    // =========================================================
    // INTRO FLOW RPCS
    // =========================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_OnAllPlayersEntered(
        RpcInfo info = default)
    {
        Debug.Log(
            "[GAME MANAGER] PING: all players entered the gate!"
        );

        if (gateTag != null)
        {
            Destroy(gateTag);
            gateTag = null;
        }

        // PING ring around the local player.
        TriggerLocalPing();

        // Big callout above the gate.
        CreateWorldTag(
            GetGateCenter() + Vector3.up * 4f,
            "ALL PLAYERS ENTERED!",
            new Color(1f, 1f, 1f, 1f),
            72f,
            2.4f
        );
    }


    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_OnZoneGreen(
        RpcInfo info = default)
    {
        Debug.Log(
            "[GAME MANAGER] Zone is GREEN!"
        );

        CreateWorldTag(
            GetZoneCenter() + Vector3.up * 5f,
            "TO THE DUNGEON!",
            new Color(0.2f, 1f, 0.4f, 1f),
            64f,
            teleportDelaySeconds
        );
    }


    // =========================================================
    // ROUTE / SOLDIER MOVEMENT
    // =========================================================

    private void AdvanceSoldier()
    {
        // Kept for compatibility with the previous intro-flow structure.
        // Soldier movement is now owned exclusively by NavMeshAgent.
        StartSoldierNavMeshIfNeeded();
    }


    private Vector3[] GetSoldierRoute()
    {
        Vector3 zone =
            GetZoneCenter();

        bool useModelStart =
            startFromModelPosition &&
            soldierModelStartPositionSet;

        Vector3 start =
            useModelStart
                ? soldierModelStartPosition
                : GetGateCenter();

        Vector3 toZone =
            zone - start;

        toZone.y = 0f;

        if (toZone.sqrMagnitude < 0.01f)
        {
            toZone = Vector3.forward;
        }

        toZone.Normalize();

        Vector3 stopInFront =
            zone - toZone * 2.2f;

        if (useModelStart)
        {
            Vector3 midpointToStop =
                Vector3.Lerp(
                    start,
                    stopInFront,
                    0.5f
                );

            return new Vector3[]
            {
                start,
                midpointToStop,
                stopInFront
            };
        }

        Vector3 startBehind =
            start - toZone * 4f;

        Vector3 halfwayToZone =
            Vector3.Lerp(
                start,
                zone,
                0.5f
            );

        return new Vector3[]
        {
            startBehind,
            start,
            halfwayToZone,
            stopInFront
        };
    }


    private float GetRouteLength(
        Vector3[] route)
    {
        if (route == null ||
            route.Length < 2)
        {
            return 0f;
        }

        float length = 0f;

        for (int i = 1; i < route.Length; i++)
        {
            length +=
                Vector3.Distance(
                    route[i - 1],
                    route[i]
                );
        }

        return length;
    }


    private Vector3 PointAlongRoute(
        Vector3[] route,
        float progress)
    {
        if (route == null ||
            route.Length == 0)
        {
            return Vector3.zero;
        }

        if (route.Length == 1)
            return route[0];

        float totalLength =
            GetRouteLength(route);

        if (totalLength <= 0f)
            return route[0];

        float targetDistance =
            Mathf.Clamp01(progress) *
            totalLength;

        float walked = 0f;

        for (int i = 1; i < route.Length; i++)
        {
            Vector3 from =
                route[i - 1];

            Vector3 to =
                route[i];

            float segmentLength =
                Vector3.Distance(
                    from,
                    to
                );

            if (walked + segmentLength >= targetDistance)
            {
                float local =
                    segmentLength > 0f
                        ? (targetDistance - walked) /
                          segmentLength
                        : 0f;

                return Vector3.Lerp(
                    from,
                    to,
                    local
                );
            }

            walked += segmentLength;
        }

        return route[route.Length - 1];
    }


    // =========================================================
    // ZONES
    // =========================================================

    private Vector3 GetGateCenter()
    {
        if (cachedGateObject == null)
        {
            cachedGateObject =
                GameObject.Find(
                    gateTriggerName
                );
        }

        if (cachedGateObject != null)
        {
            return cachedGateObject.transform.position;
        }

        return Vector3.zero;
    }


    private Vector3 GetZoneCenter()
    {
        if (cachedZoneObject == null)
        {
            cachedZoneObject =
                GameObject.Find(
                    dungeonEntryZoneName
                );
        }

        if (cachedZoneObject != null)
        {
            return cachedZoneObject.transform.position;
        }

        return GetGateCenter() +
               zoneFallbackOffset;
    }


    private void CountPlayersInside(
        Vector3 center,
        Vector3 halfExtents,
        out int totalOut,
        out int insideOut)
    {
        totalOut = 0;
        insideOut = 0;

        if (Runner == null)
            return;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            NetworkObject playerObject =
                Runner.GetPlayerObject(player);

            if (playerObject == null)
                continue;

            totalOut++;

            Vector3 delta =
                playerObject.transform.position -
                center;

            if (Mathf.Abs(delta.x) <= halfExtents.x &&
                Mathf.Abs(delta.y) <= halfExtents.y &&
                Mathf.Abs(delta.z) <= halfExtents.z)
            {
                insideOut++;
            }
        }
    }


    // =========================================================
    // VISUALS - PATH
    // =========================================================

    private void BuildPathDots()
    {
        if (pathDotsBuilt)
            return;

        pathDotsBuilt = true;


        // =========================================================
        // START POSITION
        // =========================================================

        Vector3 start =
            soldierModelStartPositionSet
                ? soldierModelStartPosition
                : soldierAgent != null
                    ? soldierAgent.transform.position
                    : GetGateCenter();


        // =========================================================
        // DESTINATION
        // =========================================================

        Vector3 destination =
            GetSoldierDestination();


        // =========================================================
        // BUILD NAVMESH PATH
        // =========================================================

        if (!useNavMeshPathDots ||
            !BuildNavMeshPath(
                start,
                destination,
                out NavMeshPath navPath))
        {
            // Keep the existing fallback behavior.
            BuildFallbackPathDots();

            return;
        }


        // =========================================================
        // VALIDATE PATH
        // =========================================================

        if (navPath.corners == null ||
            navPath.corners.Length < 2)
        {
            BuildFallbackPathDots();

            return;
        }


        // =========================================================
        // DOT SPACING
        // =========================================================

        float spacing =
            Mathf.Max(
                0.5f,
                ghostPathSpacing
            );


        // =========================================================
        // SOLDIER REFERENCE
        // =========================================================

        Transform soldierTransform = null;

        if (soldierAgent != null)
        {
            soldierTransform =
                soldierAgent.transform;
        }
        else if (soldierVisual != null)
        {
            soldierTransform =
                soldierVisual.root;
        }


        // =========================================================
        // BUILD DOTS ALONG NAVMESH ROUTE
        // =========================================================

        for (int i = 1;
             i < navPath.corners.Length;
             i++)
        {
            Vector3 from =
                navPath.corners[i - 1];

            Vector3 to =
                navPath.corners[i];


            float length =
                Vector3.Distance(
                    from,
                    to
                );


            int steps =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        length /
                        spacing
                    )
                );


            for (int step = 1;
                 step <= steps;
                 step++)
            {
                float t =
                    step /
                    (float)steps;


                Vector3 position =
                    Vector3.Lerp(
                        from,
                        to,
                        t
                    );


                // =================================================
                // CREATE DOT
                // =================================================
                //
                // The +1.6 Y offset makes the marker float above
                // the ground.
                //

                GameObject dot =
                    CreateDot(
                        position +
                        Vector3.up * 1.6f,

                        0.22f,

                        2.2f +
                        (i % 3) * 0.9f,

                        0.5f,

                        0f,

                        Vector3.zero
                    );


                if (dot == null)
                    continue;


                // =================================================
                // CONFIGURE SOLDIER PATH DOT
                // =================================================

                GhostDot ghostDot =
                    dot.GetComponent<GhostDot>();


                if (ghostDot != null)
                {
                    // Green soldier path marker.
                    ghostDot.dotColor =
                        new Color(
                            0f,
                            1f,
                            0.15f,
                            1f
                        );


                    // Enable soldier-pass destruction.
                    ghostDot.destroyWhenSoldierPasses =
                        true;


                    // Horizontal distance at which the
                    // dot disappears.
                    ghostDot.soldierPassDistance =
                        0.8f;


                    // Give the GhostDot the actual soldier
                    // reference instead of relying on a search.
                    if (soldierTransform != null)
                    {
                        ghostDot.ConfigureSoldierPass(
                            soldierTransform,
                            0.8f
                        );
                    }
                }


                // =================================================
                // STORE DOT
                // =================================================

                pathDots.Add(dot);
            }
        }
    }
    private void BuildFallbackPathDots()
    {
        Vector3[] route =
            GetSoldierRoute();

        if (route == null ||
            route.Length < 2)
        {
            return;
        }

        float spacing =
            Mathf.Max(0.5f, ghostPathSpacing);

        for (int i = 1; i < route.Length; i++)
        {
            Vector3 from = route[i - 1];
            Vector3 to = route[i];

            float length = Vector3.Distance(from, to);

            int steps =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(length / spacing)
                );

            for (int step = 1; step <= steps; step++)
            {
                float t = step / (float)steps;

                GameObject dot =
                    CreateDot(
                        Vector3.Lerp(from, to, t) + Vector3.up * 1.6f,
                        0.22f,
                        2.2f + (i % 3) * 0.9f,
                        0.5f,
                        0f,
                        Vector3.zero
                    );

                pathDots.Add(dot);
            }
        }
    }


    // =========================================================
    // SOLDIER VISUAL
    // =========================================================
    private void BuildSoldierVisual()
    {
        // =========================================================
        // ALREADY BUILT
        // =========================================================

        if (soldierVisualBuilt &&
            soldierVisual != null)
        {
            return;
        }

        // =========================================================
        // FIND / INITIALIZE SOLDIER ROOT
        // =========================================================

        InitializeSoldierAgent();

        // =========================================================
        // FIND SOLDIER MODEL
        // =========================================================

        if (soldierVisual == null)
        {
            // First look under SoldierGuide.
            if (soldierAgent != null)
            {
                Transform modelTransform =
                    soldierAgent.transform.Find(
                        soldierModelObjectName
                    );

                if (modelTransform != null)
                {
                    soldierVisual =
                        modelTransform;
                }
            }
        }

        // =========================================================
        // FALLBACK SCENE SEARCH
        // =========================================================

        if (soldierVisual == null)
        {
            GameObject modelObject =
                GameObject.Find(
                    soldierModelObjectName
                );

            if (modelObject != null)
            {
                soldierVisual =
                    modelObject.transform;
            }
        }

        // =========================================================
        // STILL NOT FOUND
        // =========================================================

        if (soldierVisual == null)
        {
            Debug.LogWarning(
                "[GAME MANAGER] SoldierGuideModel is not available yet. " +
                "Waiting for SoldierGuide."
            );

            soldierVisualBuilt = false;
            return;
        }

        // =========================================================
        // FIND ANIMATOR
        // =========================================================

        soldierAnimator =
            soldierVisual.GetComponentInChildren<Animator>(
                true
            );

        soldierAnimatorInitialized =
            true;

        if (soldierAnimator != null)
        {
            Debug.Log(
                "[GAME MANAGER] Soldier Animator found on: " +
                soldierAnimator.gameObject.name
            );

            LogSoldierAnimatorParameters();
        }
        else
        {
            Debug.LogError(
                "[GAME MANAGER] SoldierGuideModel was found, " +
                "but no Animator exists on it or its children."
            );
        }

        // =========================================================
        // SAVE MOVEMENT ROOT POSITION
        // =========================================================

        if (soldierAgent != null &&
            !soldierModelStartPositionSet)
        {
            soldierModelStartPosition =
                soldierAgent.transform.position;

            soldierModelStartPositionSet =
                true;

            Debug.Log(
                "[GAME MANAGER] Soldier start position saved: " +
                soldierModelStartPosition
            );
        }

        // =========================================================
        // DONE
        // =========================================================

        soldierVisualBuilt = true;

        Debug.Log(
            "[GAME MANAGER] Soldier visual successfully initialized."
        );
    }
    // =========================================================
    // SOLDIER ANIMATION
    // =========================================================

    private void UpdateSoldierAnimation()
    {
        if (soldierAnimator == null)
            return;

        float speed = 0f;

        if (Runner != null && Runner.IsServer)
        {
            if (soldierAgent != null &&
                soldierAgent.enabled &&
                soldierAgent.isOnNavMesh)
            {
                speed = soldierAgent.velocity.magnitude;
            }
        }
        else
        {
            // Client-side approximation from network progress
            speed = SoldierStarted && !SoldierReachedDoor
                ? soldierNavSpeed
                : 0f;
        }

        if (HasAnimatorParameter("Speed", AnimatorControllerParameterType.Float))
        {
            soldierAnimator.SetFloat(
                "Speed",
                speed,
                0.1f,
                Time.deltaTime
            );
        }

        if (HasAnimatorParameter("IsWalking", AnimatorControllerParameterType.Bool))
        {
            soldierAnimator.SetBool(
                "IsWalking",
                speed > 0.1f
            );
        }
    }

    private bool HasAnimatorParameter(
    string parameterName,
    AnimatorControllerParameterType type)
    {
        if (soldierAnimator == null)
            return false;

        foreach (AnimatorControllerParameter parameter
                 in soldierAnimator.parameters)
        {
            if (parameter.name == parameterName &&
                parameter.type == type)
            {
                return true;
            }
        }

        return false;
    }

    private void LogSoldierAnimatorParameters()
    {
        if (soldierAnimator == null)
            return;

        AnimatorControllerParameter[] parameters =
            soldierAnimator.parameters;

        if (parameters == null ||
            parameters.Length == 0)
        {
            Debug.LogWarning(
                "[GAME MANAGER] Soldier Animator has no parameters."
            );

            return;
        }

        Debug.Log(
            "[GAME MANAGER] Soldier Animator parameters:"
        );

        for (int i = 0; i < parameters.Length; i++)
        {
            Debug.Log(
                $"[GAME MANAGER] - " +
                $"{parameters[i].name} " +
                $"({parameters[i].type})"
            );
        }
    }


    // =========================================================
    // ZONE VISUAL
    // =========================================================

    // =========================================================
    // ZONE VISUAL
    // =========================================================

    private void BuildZoneVisual()
    {
        if (zoneVisualBuilt)
            return;

        zoneVisualBuilt = true;


        Vector3 center =
            GetZoneCenter();

        Vector3 half =
            zoneHalfExtents;


        // =========================================================
        // 8 CORNER POSTS
        // =========================================================

        for (int i = 0; i < 8; i++)
        {
            float sx =
                (i & 1) == 0
                    ? -1f
                    : 1f;

            float sy =
                (i & 2) == 0
                    ? -1f
                    : 1f;

            float sz =
                (i & 4) == 0
                    ? -1f
                    : 1f;


            CreateDot(
                center +
                new Vector3(
                    sx * half.x,
                    sy * half.y,
                    sz * half.z
                ),

                0.4f,

                2f,

                0.4f,

                0f,

                Vector3.zero
            );
        }


        // =========================================================
        // FLOOR FRAME - LEFT
        // =========================================================

        CreateDot(
            center +
            new Vector3(
                -half.x,
                0f,
                0f
            ),

            0.3f,

            4f,

            0.5f,

            0f,

            Vector3.zero
        );


        // =========================================================
        // FLOOR FRAME - RIGHT
        // =========================================================

        CreateDot(
            center +
            new Vector3(
                half.x,
                0f,
                0f
            ),

            0.3f,

            4f,

            0.5f,

            0f,

            Vector3.zero
        );


        // =========================================================
        // FLOOR FRAME - BACK
        // =========================================================

        CreateDot(
            center +
            new Vector3(
                0f,
                0f,
                -half.z
            ),

            0.3f,

            4f,

            0.5f,

            0f,

            Vector3.zero
        );


        // =========================================================
        // FLOOR FRAME - FRONT
        // =========================================================

        CreateDot(
            center +
            new Vector3(
                0f,
                0f,
                half.z
            ),

            0.3f,

            4f,

            0.5f,

            0f,

            Vector3.zero
        );


        // =========================================================
        // ZONE WORLD TAG
        // =========================================================

        zoneTag =
            CreateWorldTag(
                center +
                Vector3.up *
                (half.y + 2.5f),

                "DUNGEON ENTRY",

                new Color(
                    1f,
                    0.15f,
                    0.15f,
                    1f
                ),

                40f,

                0f
            );
    }
    private void UpdateZoneTag(
      int inside,
      int total,
      bool zoneIsGreen)
    {
        if (zoneTag == null)
            return;


        WorldTag tagScript =
            zoneTag.GetComponent<WorldTag>();


        if (tagScript == null)
            return;


        // =========================================================
        // GREEN / READY STATE
        // =========================================================

        if (zoneIsGreen)
        {
            int secondsLeft =
                (int)Mathf.Ceil(
                    Mathf.Max(
                        0f,
                        TeleportCountdown
                    )
                );


            tagScript.SetMessage(
                "TO THE DUNGEON!  (" +
                secondsLeft +
                ")"
            );


            tagScript.SetColor(
                new Color(
                    0.2f,
                    1f,
                    0.4f,
                    1f
                )
            );


            return;
        }


        // =========================================================
        // NORMAL STATE
        // =========================================================

        tagScript.SetMessage(
            "ENTER THE ZONE  (" +
            inside +
            "/" +
            total +
            ")"
        );


        tagScript.SetColor(
            new Color(
                1f,
                0.15f,
                0.15f,
                1f
            )
        );
    }
    // =========================================================
    // WORLD TAG
    // =========================================================

    // =========================================================
    // WORLD TAG
    // =========================================================

    private GameObject CreateWorldTag(
        Vector3 worldPosition,
        string message,
        Color color,
        float fontSize,
        float lifeSeconds)
    {
        if (NetworkRunnerHandler.Instance == null)
            return null;


        GameObject tagPrefab =
            NetworkRunnerHandler.Instance.worldTagPrefab;


        if (tagPrefab == null)
            return null;


        GameObject tag =
            Instantiate(
                tagPrefab
            );


        tag.transform.position =
            worldPosition;


        WorldTag tagScript =
            tag.GetComponent<WorldTag>();


        if (tagScript != null)
        {
            tagScript.Configure(
                message,
                color,
                fontSize,
                lifeSeconds
            );
        }


        return tag;
    }

    // =========================================================
    // DOT
    // =========================================================

    // =========================================================
    // DOT
    // =========================================================

    private GameObject CreateDot(
        Vector3 worldPosition,
        float scale,
        float pulseCyclesPerSecond,
        float swell,
        float lifeSeconds,
        Vector3 velocity)
    {
        GameObject dot =
            new GameObject(
                "GhostDot"
            );


        dot.transform.position =
            worldPosition;


        GhostDot dotScript =
            dot.AddComponent<GhostDot>();


        dotScript.Configure(
            velocity,
            lifeSeconds,
            pulseCyclesPerSecond,
            swell,
            scale
        );


        return dot;
    }

    private GameObject CreateAttachedDot(
        Transform parent,
        Vector3 localPosition,
        float scale,
        float pulseCyclesPerSecond,
        float swell,
        float lifeSeconds,
        Vector3 velocity)
    {
        GameObject dot =
            new GameObject(
                "GhostDot"
            );


        dot.transform.SetParent(
            parent,
            false
        );


        dot.transform.localPosition =
            localPosition;


        GhostDot dotScript =
            dot.AddComponent<GhostDot>();


        dotScript.Configure(
            velocity,
            lifeSeconds,
            pulseCyclesPerSecond,
            swell,
            scale
        );


        return dot;
    }
    // =========================================================
    // PING
    // =========================================================

    // =========================================================
    // PING
    // =========================================================

    private void TriggerLocalPing()
    {
        Vector3 origin =
            GetLocalPlayerPosition();


        const int dotCount = 20;


        for (int i = 0;
             i < dotCount;
             i++)
        {
            float angle =
                (i / (float)dotCount) *
                Mathf.PI *
                2f;


            Vector3 direction =
                new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle)
                );


            CreateDot(
                origin +
                direction *
                (
                    0.6f +
                    (i % 3) *
                    0.25f
                ),

                0.28f,

                6f,

                0.35f,

                1.1f,

                direction * 16f
            );
        }


        // =========================================================
        // PING WORLD TAG
        // =========================================================

        CreateWorldTag(
            origin +
            Vector3.up * 3f,

            "PING!",

            new Color(
                1f,
                1f,
                1f,
                1f
            ),

            56f,

            1.2f
        );
    }
    // =========================================================
    // SOLDIER MODEL
    // =========================================================

    private GameObject GetSoldierModel()
    {
        // Already cached
        if (cachedSoldierModel != null)
            return cachedSoldierModel;

        // Inspector-assigned visual
        if (soldierVisual != null)
        {
            cachedSoldierModel = soldierVisual.gameObject;
            return cachedSoldierModel;
        }

        // Search through the SoldierGuideController
        SoldierGuideController controller =
            FindFirstObjectByType<SoldierGuideController>(
                FindObjectsInactive.Include
            );

        if (controller != null)
        {
            Transform model =
                FindChildRecursive(
                    controller.transform,
                    soldierModelObjectName
                );

            if (model != null)
            {
                cachedSoldierModel = model.gameObject;
                return cachedSoldierModel;
            }
        }

        // Search through the NavMeshAgent root
        if (soldierAgent != null)
        {
            Transform model =
                FindChildRecursive(
                    soldierAgent.transform,
                    soldierModelObjectName
                );

            if (model != null)
            {
                cachedSoldierModel = model.gameObject;
                return cachedSoldierModel;
            }
        }

        return null;
    }

    private Transform FindChildRecursive(
        Transform parent,
        string targetName
    )
    {
        if (parent.name == targetName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform result =
                FindChildRecursive(child, targetName);

            if (result != null)
                return result;
        }

        return null;
    }

    // Keeps the guide model hidden until the walk begins.
    private void UpdateGuideModelVisibility()
    {
        if (soldierVisual == null)
            return;

        bool shouldBeVisible =
            SoldierStarted;

        if (soldierVisual.gameObject.activeSelf != shouldBeVisible)
        {
            soldierVisual.gameObject.SetActive(
                shouldBeVisible
            );
        }
    }

    private Vector3 lastSoldierNetworkPosition;
    private bool hasLastSoldierNetworkPosition;
    // =========================================================
    // SOLDIER ROTATION
    // =========================================================

    private void FaceSoldierAlongRoute(Transform soldierRoot)
    {
        if (soldierRoot == null)
            return;

        // Host uses the NavMeshAgent's own rotation.
        if (Runner != null && Runner.IsServer)
        {
            if (soldierAgent != null &&
                soldierAgent.enabled &&
                soldierAgent.isOnNavMesh)
            {
                soldierRoot.rotation = soldierAgent.transform.rotation;
            }

            return;
        }

        // =====================================================
        // CLIENT: determine direction from network movement
        // =====================================================

        Vector3 currentPosition = SoldierPosition;

        if (!hasLastSoldierNetworkPosition)
        {
            lastSoldierNetworkPosition = currentPosition;
            hasLastSoldierNetworkPosition = true;
            return;
        }

        Vector3 direction =
            currentPosition - lastSoldierNetworkPosition;

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction.normalized, Vector3.up);

            soldierRoot.rotation = Quaternion.Slerp(
                soldierRoot.rotation,
                targetRotation,
                10f * Time.deltaTime
            );
        }

        lastSoldierNetworkPosition = currentPosition;
    }
    // =========================================================
    // SOLDIER BOB
    // =========================================================

    private float GetSoldierBob()
    {
        if (soldierBobAmount <= 0f)
            return 0f;

        if (!SoldierStarted ||
            SoldierReachedDoor)
        {
            return 0f;
        }

        return
            Mathf.Sin(
                Time.time * 10f
            ) *
            soldierBobAmount;
    }


    // =========================================================
    // LOCAL PLAYER
    // =========================================================

    private Vector3 GetLocalPlayerPosition()
    {
        if (Runner == null ||
            !Runner.LocalPlayer.IsValid)
        {
            return Vector3.zero;
        }

        NetworkObject playerObject =
            Runner.GetPlayerObject(
                Runner.LocalPlayer
            );

        if (playerObject == null)
            return Vector3.zero;

        return playerObject.transform.position;
    }


    // =========================================================
    // READY CHECK
    // =========================================================

    public bool AreAllPlayersReady()
    {
        if (Runner == null)
            return false;

        int playerCount = 0;
        int readyCount = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            playerCount++;

            NetworkObject playerObject =
                Runner.GetPlayerObject(player);

            if (playerObject == null)
            {
                Debug.LogWarning(
                    $"[GAME MANAGER] No player object found for {player}."
                );

                return false;
            }

            LobbyPlayerState playerState =
                playerObject.GetComponent<LobbyPlayerState>();

            if (playerState == null)
            {
                Debug.LogWarning(
                    $"[GAME MANAGER] LobbyPlayerState missing on {player}."
                );

                return false;
            }

            if (playerState.IsReady)
            {
                readyCount++;
            }
        }

        if (playerCount == 0)
        {
            Debug.LogWarning(
                "[GAME MANAGER] No active players."
            );

            return false;
        }

        Debug.Log(
            $"[GAME MANAGER] Ready check: " +
            $"{readyCount}/{playerCount}"
        );

        return readyCount == playerCount;
    }


    // =========================================================
    // PLAYER COUNT
    // =========================================================

    public int GetPlayerCount()
    {
        if (Runner == null)
            return 0;

        int count = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            count++;
        }

        return count;
    }


    // =========================================================
    // READY COUNT
    // =========================================================

    public int GetReadyPlayerCount()
    {
        if (Runner == null)
            return 0;

        int readyCount = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            NetworkObject playerObject =
                Runner.GetPlayerObject(player);

            if (playerObject == null)
                continue;

            LobbyPlayerState playerState =
                playerObject.GetComponent<LobbyPlayerState>();

            if (playerState != null &&
                playerState.IsReady)
            {
                readyCount++;
            }
        }

        return readyCount;
    }
}