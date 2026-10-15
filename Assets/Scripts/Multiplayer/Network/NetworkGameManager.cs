using System.Collections.Generic;
using System.Collections;
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

    [Header("Fort Mission")]
    [Tooltip("Assign the existing FortMissionItemSpawner component from its separate scene GameObject. The manager will never add a spawner component at runtime.")]
    [SerializeField] private FortMissionItemSpawner fortMissionItemSpawner;

    // =========================================================
    // DIFFICULTY
    // =========================================================

    [Networked]
    public GameDifficulty Difficulty { get; private set; }
        = GameDifficulty.Normal;
    [Networked]
    public PlayerRef HostPlayer { get; set; }
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
        if (!networkStateReady)
            return GameDifficulty.Normal.ToString();

        return Difficulty.ToString();
    }


    // =========================================================
    // NETWORK GAME STATE
    // =========================================================

    [Networked]
    public NetworkBool GameStarted { get; private set; }

    [Networked]
    public NetworkBool LobbyLocked { get; private set; }


    // =========================================================
    // SOLDIER NAVIGATION REFERENCES
    // =========================================================

    [Header("Soldier Navigation")]
    [SerializeField]
    private Transform soldierVisual;

    [SerializeField]
    private NavMeshAgent soldierAgent;

    private bool networkStateReady;

    public bool IsNetworkStateReady =>
        networkStateReady;


    // =========================================================
    // INTRO FLOW STATE
    // =========================================================

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
    public NetworkBool MissionItemsSpawned { get; private set; }

    [Networked]
    public NetworkBool DungeonGateUnlocked { get; private set; }

    [Networked]
    public NetworkBool PrisonCell1Unlocked { get; private set; }

    [Networked]
    public NetworkBool PrisonCell2Unlocked { get; private set; }

    [Networked]
    public NetworkBool PrisonCell1GateOpen { get; private set; }

    [Networked]
    public NetworkBool PrisonCell2GateOpen { get; private set; }


    // =========================================================
    // SOLDIER INTERACTION / DIALOGUE
    // =========================================================

    [Networked]
    public NetworkBool SoldierInteractionStarted { get; private set; }

    [Networked]
    public NetworkBool SoldierDialogueActive { get; private set; }

    [Header("Soldier Dialogue")]
    [SerializeField]
    private float soldierDialogueDuration = 3f;

    private float soldierDialogueTimer;


    // =========================================================
    // REPLICATED SOLDIER STATE
    // =========================================================

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
        "of the dungeon door."
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
        "represent the guide soldier."
    )]
    [SerializeField]
    private string soldierModelObjectName =
        "SoldierGuideModel";

    [Tooltip(
        "How fast the soldier guide walks the route."
    )]
    [SerializeField]
    private float soldierWalkSpeed = 2.8f;

    [Tooltip(
        "Start the walk route exactly where the SoldierGuideModel " +
        "object sits in the scene."
    )]
    [SerializeField]
    private bool startFromModelPosition = true;

    [Tooltip(
        "Degrees added to the model's yaw."
    )]
    [SerializeField]
    private float soldierModelForwardYawOffset = 0f;

    [Tooltip(
        "Optional scene object used as the exact NavMesh destination."
    )]
    [SerializeField]
    private string soldierDestinationObjectName =
        "SoldierDungeonDestination";

    [Tooltip(
        "NavMesh movement speed."
    )]
    [SerializeField]
    private float soldierNavSpeed = 1.8f;

    [Tooltip(
        "NavMesh acceleration."
    )]
    [SerializeField]
    private float soldierNavAcceleration = 6f;

    [Tooltip(
        "How close the soldier must get to the destination."
    )]
    [SerializeField]
    private float soldierNavStoppingDistance = 1f;

    [Tooltip(
        "How quickly the soldier turns."
    )]
    [SerializeField]
    private float soldierNavAngularSpeed = 360f;

    [Tooltip(
        "How quickly remote clients interpolate."
    )]
    [SerializeField]
    private float soldierNetworkLerpSpeed = 12f;


    // =========================================================
    // SOLDIER ANIMATION
    // =========================================================

    [Header("Soldier Animation")]

    [Tooltip(
        "Float Animator parameter used for walking."
    )]
    [SerializeField]
    private string soldierSpeedParameter = "Speed";

    [Tooltip(
        "Optional Bool Animator parameter."
    )]
    [SerializeField]
    private string soldierWalkingParameter = "IsWalking";

    [Tooltip(
        "Value sent to the Speed Animator parameter while walking."
    )]
    [SerializeField]
    private float soldierAnimationSpeed = 1f;

    [Tooltip(
        "Artificial vertical bob."
    )]
    [SerializeField]
    private float soldierBobAmount = 0f;


    // =========================================================
    // INTRO FLOW - ZONE TRANSITION
    // =========================================================

    [Header("Intro Flow - Zone Transition")]

    [Tooltip(
        "Seconds the zone stays GREEN before teleport."
    )]
    [SerializeField]
    private float teleportDelaySeconds = 5f;

    [SerializeField] private Transform worldTagPoint;
    [SerializeField] private Transform gateTagPoint;


    // =========================================================
    // FORT MISSION - PRE-DUNGEON ITEMS
    // =========================================================

    [Header("Fort Mission - Pre-Dungeon Items")]

    [Tooltip(
        "Candle ItemData asset players must collect. " +
        "If empty, fallbackCandleItemId is used."
    )]
    [SerializeField]
    private ItemData candleItem;

    [Tooltip(
        "Key ItemData asset required to unlock the dungeon gate. " +
        "If empty, fallbackKeyItemId is used."
    )]
    [SerializeField]
    private ItemData keyItem;

    [Tooltip(
        "Fallback ItemData id used when the candle asset is not assigned " +
        "(203 = Candle)."
    )]
    [SerializeField, Min(1)]
    private int fallbackCandleItemId = 203;

    [Tooltip(
        "Fallback ItemData id used when the key asset is not assigned " +
        "(103 = Dungeon Key)."
    )]
    [SerializeField, Min(1)]
    private int fallbackKeyItemId = 103;

    [Tooltip(
        "Total team candles required before the gate can be unlocked. " +
        "This is also how many candles are spawned."
    )]
    [SerializeField, Min(1)]
    private int candlesRequiredPerPlayer = 2;

    [Tooltip(
        "Keys spawned for the whole squad (must be 1 for the intended design)."
    )]
    [SerializeField, Min(1)]
    private int keysToSpawn = 1;

    [Tooltip(
        "Optional exact ground Y values to snap spawned items to " +
        "(e.g. the Y of existing visible items like -2.21 / 4.71). " +
        "When empty, the Y of existing scene items and markers is used."
    )]
    [SerializeField]
    private float[] manualReferenceHeights;

    [Header("Fort Mission - Mission Panel")]

    [Tooltip(
        "MissionData asset shown when the fort mission starts. " +
        "If empty, a default message is used."
    )]
    [SerializeField]
    private MissionData fortMission;


    // =========================================================
    // LOCAL VISUALS
    // =========================================================

    private GameObject gateTag;

    private GameObject zoneVisual;
    private GameObject zoneTag;

    private DungeonZoneVisual dungeonZoneVisual;

    private bool soldierVisualBuilt;
    private bool zoneVisualBuilt;

    private GameObject cachedGateObject;
    private GameObject cachedZoneObject;
    private GameObject cachedSoldierModel;

    private Vector3 soldierModelStartPosition =
        Vector3.zero;

    private bool soldierModelStartPositionSet;


    // =========================================================
    // NAVMESH INTERNAL STATE
    // =========================================================

    private NavMeshPath soldierNavPath;

    private GameObject cachedSoldierDestination;

    private Vector3 soldierDestinationPosition;

    private bool soldierDestinationSet;
    private bool soldierNavStarted;

    private bool missionComponentsEnsured;

    private float soldierInitialPathDistance;

    private Vector3 soldierLastHostPosition;


    // =========================================================
    // ANIMATION INTERNALS
    // =========================================================

    private Animator soldierAnimator;

    private bool soldierAnimatorInitialized;
    private bool soldierAnimatorWarningShown;


    // =========================================================
    // REMOTE SOLDIER ROTATION
    // =========================================================

    private Vector3 lastSoldierNetworkPosition;
    private bool hasLastSoldierNetworkPosition;


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
    // UNITY - AWAKE
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

        // The mission progress panel stays hidden until the fort mission starts.
        GameObject missionProgressPanel =
            GameObject.Find("MissionProgressPanel");

        if (missionProgressPanel != null)
        {
            missionProgressPanel.SetActive(false);
        }

        Debug.Log(
            "[GAME MANAGER] Instance assigned."
        );
    }


    // =========================================================
    // UNITY - DESTROY
    // =========================================================

    private void OnDestroy()
    {
        networkStateReady = false;

        if (Instance == this)
        {
            Instance = null;
        }
    }


    // =========================================================
    // FUSION - SPAWNED
    // =========================================================

    public override void Spawned()
    {
        Debug.Log(
            "[GAME MANAGER] Spawned."
        );

        Debug.Log(
            $"[GAME MANAGER] State Authority: " +
            $"{Object.HasStateAuthority}"
        );

        Debug.Log(
            $"[GAME MANAGER] Is Shared Master: {IsHost}"
        );

        networkStateReady = true;

        if (Object.HasStateAuthority)
        {
            HostPlayer = Runner.LocalPlayer;
            GameStarted = false;
            LobbyLocked = false;
            DungeonGateUnlocked = false;
            PrisonCell1Unlocked = false;
            PrisonCell2Unlocked = false;
            PrisonCell1GateOpen = false;
            PrisonCell2GateOpen = false;
            MissionItemsSpawned = false;

            AllPlayersEnteredGate = false;

            // -------------------------------------------------
            // SOLDIER FLOW
            // -------------------------------------------------

            SoldierInteractionStarted = false;
            SoldierDialogueActive = false;
            SoldierStarted = false;
            SoldierReachedDoor = false;

            // -------------------------------------------------
            // ZONE FLOW
            // -------------------------------------------------

            ZoneActive = false;
            ZoneGreen = false;
            TeleportStarted = false;

            // -------------------------------------------------
            // REPLICATED VALUES
            // -------------------------------------------------

            SoldierPosition = Vector3.zero;
            SoldierProgress = 0f;
            TeleportCountdown = 0f;
            DungeonSeed = 0;

            Debug.Log(
                "[GAME MANAGER] Network state initialized."
            );

            Debug.Log(
                $"[DIFFICULTY] Current difficulty: {Difficulty}"
            );
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
            return;

        Debug.Log(
            "[GAME MANAGER] Checking whether all players are ready..."
        );

        if (!AreAllPlayersReady())
        {
            Debug.LogWarning(
                "[GAME MANAGER] Cannot start. " +
                "Not all players are ready."
            );

            return;
        }

        GameStarted = true;
        LobbyLocked = true;

        Debug.Log(
            "[GAME] GAME STARTED."
        );

        Debug.Log(
            $"[DIFFICULTY] Game starting with difficulty: {Difficulty}"
        );

        Debug.Log(
            "[GAME] Lobby locked."
        );
    }


    // =========================================================
    // SOLDIER INTERACTION
    // =========================================================

    public void RequestSoldierInteraction()
    {
        if (Runner == null)
        {
            Debug.LogWarning(
                "[SOLDIER] Cannot interact. Runner is null."
            );

            return;
        }

        if (!networkStateReady)
        {
            Debug.LogWarning(
                "[SOLDIER] NetworkGameManager is not spawned yet."
            );

            return;
        }

        if (!AllPlayersEnteredGate)
        {
            Debug.LogWarning(
                "[SOLDIER] Players have not entered the gate yet."
            );

            return;
        }

        if (SoldierInteractionStarted)
        {
            Debug.Log(
                "[SOLDIER] Soldier has already been interacted with."
            );

            return;
        }

        if (!IsHost)
        {
            Debug.Log(
                "[SOLDIER] Only the host can initiate " +
                "the soldier interaction."
            );

            return;
        }

        Debug.Log(
            "[SOLDIER] Host requested soldier interaction."
        );

        RPC_RequestSoldierInteraction();
    }


    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    private void RPC_RequestSoldierInteraction(
        RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        if (SoldierInteractionStarted)
            return;

        if (!AllPlayersEnteredGate)
            return;

        // -------------------------------------------------
        // Only Shared Mode Master Client may trigger.
        // -------------------------------------------------

        if (info.Source != Runner.LocalPlayer)
        {
            Debug.LogWarning(
                "[SOLDIER] Non-host attempted to interact " +
                "with the soldier."
            );

            return;
        }

        SoldierInteractionStarted = true;
        SoldierDialogueActive = true;

        soldierDialogueTimer = 0f;

        Debug.Log(
            "[SOLDIER] HOST INTERACTED WITH SOLDIER."
        );

        Debug.Log(
            "[SOLDIER] Dialogue started for all players."
        );

        RPC_StartSoldierDialogue();
    }


    // =========================================================
    // SOLDIER DIALOGUE RPC
    // =========================================================

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_StartSoldierDialogue(
        RpcInfo info = default)
    {
        Debug.Log(
            "[SOLDIER] Showing dialogue to local player."
        );

        SoldierDialogueController dialogue =
            FindFirstObjectByType<SoldierDialogueController>(
                FindObjectsInactive.Include
            );

        if (dialogue != null)
        {
            dialogue.ShowDialogue(
                "Excuse me sir, how do we get out of this place?"
            );
        }
        else
        {
            Debug.LogWarning(
                "[SOLDIER] SoldierDialogueController was not found."
            );
        }
    }


    // =========================================================
    // SOLDIER DIALOGUE PROGRESSION
    // =========================================================

    private void UpdateSoldierDialogue()
    {
        if (!Object.HasStateAuthority)
            return;

        if (!SoldierDialogueActive)
            return;

        soldierDialogueTimer += Time.deltaTime;

        if (soldierDialogueTimer <
            soldierDialogueDuration)
        {
            return;
        }

        soldierDialogueTimer = 0f;

        SoldierDialogueActive = false;

        SoldierStarted = true;

        // Tell ALL clients to hide their dialogue panel
        RPC_HideSoldierDialogue();

        Debug.Log(
            "[SOLDIER] Dialogue finished."
        );

        Debug.Log(
            "[SOLDIER] Soldier is now starting to walk."
        );
    }

    [Rpc(
    RpcSources.StateAuthority,
    RpcTargets.All
)]
    private void RPC_HideSoldierDialogue(
    RpcInfo info = default)
    {
        SoldierDialogueController dialogue =
            FindFirstObjectByType<SoldierDialogueController>(
                FindObjectsInactive.Include
            );

        if (dialogue != null)
        {
            dialogue.HideDialogue();

            Debug.Log(
                "[SOLDIER] Dialogue panel hidden."
            );
        }
        else
        {
            Debug.LogWarning(
                "[SOLDIER] SoldierDialogueController " +
                "was not found when hiding dialogue."
            );
        }
    }


    // =========================================================
    // MAIN UPDATE
    // =========================================================

    private void Update()
    {
        if (Runner == null)
            return;

        if (!networkStateReady)
            return;

        if (!GameStarted)
            return;

        if (TeleportStarted)
            return;

        if (SoldierReachedDoor || ZoneActive)
        {
            EnsureFortMissionComponents();

            if (Object != null && Object.HasStateAuthority && !MissionItemsSpawned)
            {
                TryStartFortMission();
            }
        }

        UpdateGatePhase();

        UpdateSoldierDialogue();

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

        int totalPlayers = 0;
        int playersInsideGate = 0;

        CountPlayersInside(
            gateCenter,
            gateHalfExtents,
            out totalPlayers,
            out playersInsideGate
        );

        // -------------------------------------------------
        // Gate world tag
        // -------------------------------------------------

        if (gateTag == null)
        {

            gateTag = CreateWorldTag(
                gateTagPoint.position,
                "",
                new Color(1f, 1f, 0.4f, 1f),
                18f,
                0.25f
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

        // -------------------------------------------------
        // Only State Authority decides completion.
        // -------------------------------------------------

        if (!Object.HasStateAuthority)
            return;

        if (totalPlayers < 1 ||
            playersInsideGate < totalPlayers)
        {
            return;
        }

        // -------------------------------------------------
        // Everyone entered.
        // -------------------------------------------------

        AllPlayersEnteredGate = true;

        // IMPORTANT:
        // Soldier does NOT start here.
        //
        // The soldier now waits for:
        //
        // Host → E → Dialogue → SoldierStarted
        //

        SoldierInteractionStarted = false;
        SoldierDialogueActive = false;
        SoldierStarted = false;

        SoldierReachedDoor = false;

        soldierNavStarted = false;
        soldierDestinationSet = false;
        soldierInitialPathDistance = 0f;

        // -------------------------------------------------
        // Set initial replicated position.
        // -------------------------------------------------

        if (!soldierModelStartPositionSet)
        {
            BuildSoldierVisual();
        }

        if (soldierModelStartPositionSet)
        {
            SoldierPosition =
                soldierModelStartPosition;
        }
        else if (soldierAgent != null)
        {
            SoldierPosition =
                soldierAgent.transform.position;
        }
        else
        {
            SoldierPosition =
                PointAlongRoute(
                    GetSoldierRoute(),
                    0f
                );
        }

        // -------------------------------------------------
        // Dungeon seed
        // -------------------------------------------------

        if (NetworkRunnerHandler.Instance != null)
        {
            DungeonSeed =
                NetworkRunnerHandler.Instance.GetSessionSeed();
        }

        Debug.Log(
            "[GAME MANAGER] All players entered the gate."
        );

        Debug.Log(
            "[GAME MANAGER] Mission: Find the Soldier."
        );

        RPC_OnAllPlayersEntered();
    }

    // =========================================================
    // COUNT PLAYERS INSIDE GATE
    // =========================================================

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
    // SOLDIER PHASE
    // =========================================================

    private void UpdateSoldierPhase()
    {
        if (!AllPlayersEnteredGate)
            return;

        // -------------------------------------------------
        // Make sure soldier exists.
        // -------------------------------------------------

        BuildSoldierVisual();

        if (soldierVisual == null)
            return;

        // -------------------------------------------------
        // Soldier becomes visible after gate completion.
        // -------------------------------------------------

        if (!soldierVisual.gameObject.activeSelf)
        {
            soldierVisual.gameObject.SetActive(true);
        }

        // -------------------------------------------------
        // WAIT FOR HOST INTERACTION
        // -------------------------------------------------
        //
        // Soldier is visible but completely stationary.
        //
        // No path dots.
        // No NavMesh movement.
        //

        if (!SoldierInteractionStarted)
        {
            UpdateSoldierAnimation();

            return;
        }

        // -------------------------------------------------
        // WAIT FOR DIALOGUE TO FINISH
        // -------------------------------------------------

        if (!SoldierStarted)
        {
            UpdateSoldierAnimation();

            return;
        }

        // -------------------------------------------------
        // SOLDIER HAS STARTED
        // -------------------------------------------------

        // -------------------------------------------------
        // HOST / STATE AUTHORITY
        // -------------------------------------------------

        if (Object.HasStateAuthority)
        {
            if (SoldierStarted &&
                !SoldierReachedDoor)
            {
                StartSoldierNavMeshIfNeeded();

                if (soldierAgent != null &&
                    soldierAgent.enabled &&
                    soldierAgent.isOnNavMesh)
                {
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
                            "[GAME MANAGER] Soldier guide reached " +
                            "the dungeon door. Zone active."
                        );

                        // Pre-dungeon mission: spawn the candles & key,
                        // show the mission panel, and arm the gate lock.
                        TryStartFortMission();
                    }
                }
            }
        }
        else
        {
            // -------------------------------------------------
            // REMOTE CLIENT
            // -------------------------------------------------

            if (soldierAgent != null &&
                soldierAgent.enabled)
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

        // -------------------------------------------------
        // Remote rotation
        // -------------------------------------------------

        if (!Object.HasStateAuthority)
        {
            Transform soldierRoot =
                soldierVisual.transform.parent != null
                    ? soldierVisual.transform.parent
                    : soldierVisual.transform;

            FaceSoldierAlongRoute(
                soldierRoot
            );
        }

        UpdateSoldierAnimation();
    }


    // =========================================================
    // SOLDIER NAVMESH INITIALIZATION
    // =========================================================

    private void InitializeSoldierAgent()
    {
        // -------------------------------------------------
        // 1. Inspector assignment
        // -------------------------------------------------

        if (soldierAgent != null)
        {
            ConfigureSoldierAgent();
            return;
        }

        // -------------------------------------------------
        // 2. SoldierGuideController
        // -------------------------------------------------

        SoldierGuideController controller =
            FindFirstObjectByType<SoldierGuideController>(
                FindObjectsInactive.Include
            );

        if (controller != null)
        {
            soldierAgent =
                controller.GetComponent<NavMeshAgent>();

            if (soldierAgent == null)
            {
                soldierAgent =
                    controller.GetComponentInParent<NavMeshAgent>(
                        true
                    );
            }

            if (soldierAgent == null)
            {
                soldierAgent =
                    controller.GetComponentInChildren<NavMeshAgent>(
                        true
                    );
            }

            if (soldierAgent != null)
            {
                Debug.Log(
                    "[GAME MANAGER] Found SoldierGuide through " +
                    "SoldierGuideController: " +
                    soldierAgent.gameObject.name
                );

                ConfigureSoldierAgent();
                return;
            }
        }

        // -------------------------------------------------
        // 3. Find SoldierGuide root
        // -------------------------------------------------

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
            soldierAgent =
                soldierRoot.GetComponent<NavMeshAgent>();

            if (soldierAgent == null)
            {
                soldierAgent =
                    soldierRoot.GetComponentInChildren<NavMeshAgent>(
                        true
                    );
            }

            if (soldierAgent == null)
            {
                soldierAgent =
                    soldierRoot.GetComponentInParent<NavMeshAgent>(
                        true
                    );
            }

            if (soldierAgent != null)
            {
                Debug.Log(
                    "[GAME MANAGER] Found SoldierGuide NavMeshAgent on: " +
                    soldierAgent.gameObject.name
                );

                ConfigureSoldierAgent();
                return;
            }

            Debug.LogWarning(
                "[GAME MANAGER] SoldierGuide exists, but no NavMeshAgent was found."
            );

            return;
        }

        Debug.LogWarning(
            "[GAME MANAGER] SoldierGuide root not found yet. " +
            "Waiting for SoldierGuide to spawn."
        );
    }


    // =========================================================
    // CONFIGURE SOLDIER NAVMESH
    // =========================================================

    private void ConfigureSoldierAgent()
    {
        if (soldierAgent == null)
            return;

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

        soldierAgent.updatePosition =
            true;

        soldierAgent.updateRotation =
            true;

        Debug.Log(
            "[GAME MANAGER] Soldier NavMeshAgent ready: " +
            soldierAgent.gameObject.name
        );
    }


    // =========================================================
    // START SOLDIER NAVMESH
    // =========================================================

    private void StartSoldierNavMeshIfNeeded()
    {
        if (soldierNavStarted)
            return;

        // Safety:
        // Never start movement before host interaction.
        if (!SoldierInteractionStarted)
            return;

        if (!SoldierStarted)
            return;

        if (soldierAgent == null)
            InitializeSoldierAgent();

        if (soldierAgent == null)
            return;

        if (!soldierAgent.enabled)
        {
            Debug.LogWarning(
                "[GAME MANAGER] Soldier NavMeshAgent is disabled. " +
                "Enabling it on State Authority."
            );

            soldierAgent.enabled = true;
        }

        // -------------------------------------------------
        // Start position
        // -------------------------------------------------

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
                "near its starting position. " +
                $"Start: {agentStartPosition}"
            );

            return;
        }

        // -------------------------------------------------
        // Attach agent to NavMesh
        // -------------------------------------------------

        if (!soldierAgent.isOnNavMesh)
        {
            Debug.Log(
                "[GAME MANAGER] SoldierGuide is off the NavMesh. " +
                $"Moving root from {agentStartPosition} to {navStart}."
            );

            soldierAgent.enabled = false;

            soldierAgent.transform.position =
                navStart;

            soldierAgent.enabled = true;

            if (!soldierAgent.isOnNavMesh)
            {
                Debug.LogError(
                    "[GAME MANAGER] SoldierGuide could not attach " +
                    "to the NavMesh after repositioning."
                );

                return;
            }
        }

        // -------------------------------------------------
        // Destination
        // -------------------------------------------------

        Vector3 destination =
            GetSoldierDestination();

        if (!TryGetNearestNavMeshPoint(
                destination,
                out navDestination))
        {
            Debug.LogError(
                "[GAME MANAGER] Soldier destination is not on " +
                "or near the NavMesh."
            );

            return;
        }

        soldierDestinationPosition =
            navDestination;

        soldierDestinationSet =
            true;

        // -------------------------------------------------
        // Capture starting position
        // -------------------------------------------------

        if (!soldierModelStartPositionSet)
        {
            soldierModelStartPosition =
                soldierAgent.transform.position;

            soldierModelStartPositionSet =
                true;
        }

        SoldierPosition =
            soldierAgent.transform.position;

        // -------------------------------------------------
        // Configure movement
        // -------------------------------------------------

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

        soldierAgent.updatePosition =
            true;

        soldierAgent.updateRotation =
            true;

        soldierAgent.isStopped =
            false;

        // -------------------------------------------------
        // Set destination
        // -------------------------------------------------

        bool pathStarted =
            soldierAgent.SetDestination(
                soldierDestinationPosition
            );

        if (!pathStarted)
        {
            Debug.LogError(
                "[GAME MANAGER] NavMeshAgent failed to set " +
                "the soldier destination."
            );

            return;
        }

        if (soldierAgent.pathStatus ==
            NavMeshPathStatus.PathInvalid)
        {
            Debug.LogError(
                "[GAME MANAGER] Soldier NavMesh path is invalid."
            );

            soldierAgent.isStopped =
                true;

            return;
        }

        soldierNavStarted =
            true;

        soldierLastHostPosition =
            soldierAgent.transform.position;

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


    // =========================================================
    // SAMPLE NAVMESH
    // =========================================================

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


    // =========================================================
    // SOLDIER DESTINATION
    // =========================================================

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

        // -------------------------------------------------
        // Fallback
        // -------------------------------------------------

        Vector3 zone =
            GetZoneCenter();

        Vector3 start =
            soldierModelStartPositionSet
                ? soldierModelStartPosition
                : soldierAgent != null
                    ? soldierAgent.transform.position
                    : GetGateCenter();

        Vector3 direction =
            zone - start;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            direction =
                Vector3.forward;
        }

        direction.Normalize();

        return zone -
               direction * 2.2f;
    }


    // =========================================================
    // DESTINATION CHECK
    // =========================================================

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


    // =========================================================
    // SOLDIER PROGRESS
    // =========================================================

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
                1f -
                remaining /
                total
            );

        soldierLastHostPosition =
            soldierAgent.transform.position;
    }


    // =========================================================
    // ZONE PHASE
    // =========================================================

    private void UpdateZonePhase()
    {
        if (!ZoneActive)
        {
            return;
        }

        EnsureFortMissionComponents();

        // ---------------------------------------------------------
        // FIND ZONE VISUAL
        // ---------------------------------------------------------

        FindDungeonZoneVisual();

        if (dungeonZoneVisual != null)
        {
            dungeonZoneVisual.SetActive(true);
        }

        // ---------------------------------------------------------
        // LEGACY / EXISTING ZONE VISUAL
        // ---------------------------------------------------------

        if (zoneVisual == null)
        {
            BuildZoneVisual();
        }

        // =========================================================
        // GATE UNLOCKED -> ENTER THE DUNGEON
        // =========================================================

        if (DungeonGateUnlocked)
        {
            if (dungeonZoneVisual != null)
            {
                dungeonZoneVisual.SetGreen();
            }
            return;
        }

        // =========================================================
        // GATE IS STILL LOCKED - WAIT FOR THE KEY
        // =========================================================

        if (dungeonZoneVisual != null)
        {
            dungeonZoneVisual.SetRed();
        }

        UpdateGateStatusTag();
    }

    // =========================================================
    // FORT MISSION COMPONENTS
    // =========================================================

    private void EnsureFortMissionComponents()
    {
        // Resolve the existing scene component only. Never add a spawner to
        // NetworkGameManager, because the scene spawner owns the configured
        // ItemData assets and spawn points.
        if (fortMissionItemSpawner == null)
            fortMissionItemSpawner = FindFortMissionSpawner();

        if (fortMissionItemSpawner == null)
        {
            Debug.LogError(
                "[GAME MANAGER] FortMissionItemSpawner is not assigned and " +
                "could not be found in the scene. Assign the existing scene " +
                "spawner in the Inspector. No spawner was added automatically.",
                this);
        }

        if (missionComponentsEnsured)
            return;

        missionComponentsEnsured = true;

        // Hold-to-unlock gate interaction on the dungeon entrance.
        EnsureDungeonGateUnlock();
    }

    private FortMissionItemSpawner FindFortMissionSpawner()
    {
        // The explicit Inspector reference always takes priority.
        if (fortMissionItemSpawner != null)
            return fortMissionItemSpawner;

        // Find the existing scene component, including inactive scene objects.
        // This does not create or attach any component.
        fortMissionItemSpawner = FindFirstObjectByType<FortMissionItemSpawner>(
            FindObjectsInactive.Include);

        return fortMissionItemSpawner;
    }

    private FortMissionItemSpawner GetFortMissionSpawner()
    {
        FortMissionItemSpawner spawner = FindFortMissionSpawner();

        if (spawner == null)
        {
            Debug.LogError(
                "[GAME MANAGER] No FortMissionItemSpawner reference is available. " +
                "Assign the existing separate scene GameObject in the Inspector. " +
                "The manager will not create a replacement component.",
                this);
        }

        return spawner;
    }

    private void EnsureDungeonGateUnlock()
    {
        GameObject zoneObject =
            GetDungeonEntryZoneObject();

        if (zoneObject == null)
        {
            Debug.LogWarning(
                "[GAME MANAGER] DungeonEntryZone was not found. " +
                "The gate unlock interaction cannot be created."
            );

            return;
        }

        // The interaction component belongs on Locktrigger. DungeonEntryZone
        // remains the larger team-ready zone checked by the gate authority.
        GameObject lockTriggerObject = GameObject.Find("Locktrigger");
        GameObject interactionObject = lockTriggerObject != null
            ? lockTriggerObject
            : zoneObject;

        DungeonGateUnlock gate =
            interactionObject.GetComponent<DungeonGateUnlock>();

        // Keep compatibility with scenes that still have the component on
        // DungeonEntryZone, but do not create a second gate component.
        if (gate == null)
            gate = zoneObject.GetComponent<DungeonGateUnlock>();

        if (gate == null)
        {
            gate =
                interactionObject.AddComponent<DungeonGateUnlock>();
        }

        DungeonGateUnlock[] allGates = FindObjectsByType<DungeonGateUnlock>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        foreach (DungeonGateUnlock otherGate in allGates)
        {
            if (otherGate != null && otherGate != gate &&
                (otherGate.gameObject == zoneObject ||
                 otherGate.gameObject.name == "ZoneFloor"))
                otherGate.enabled = false;
        }

        // Keep the required key in sync with the manager.
        gate.SetKeyItemData(keyItem);
    }

    // =========================================================
    // FORT MISSION - SPAWN + MISSION PANEL
    // =========================================================




    private void TryStartFortMission()
    {
        // Shared Mode host only. Runner.IsServer is not the right authority check
        // for a Shared Mode session.
        if (Runner == null)
        {
            Debug.LogWarning("[GAME MANAGER] Cannot start Fort mission: Runner is null.", this);
            return;
        }

        if (!IsHost)
            return;

        // The manager must be a valid Fusion NetworkBehaviour.
        if (Object == null || !Object.HasStateAuthority)
        {
            Debug.LogError(
                "[GAME MANAGER] Host cannot start the Fort mission: " +
                "NetworkGameManager has no State Authority. " +
                "Check its NetworkObject and Fusion scene setup."
            );
            return;
        }

        if (MissionItemsSpawned)
            return;

        FortMissionItemSpawner spawner = GetFortMissionSpawner();
        if (spawner == null)
            return;

        PlayerInventory localInventory = GetLocalPlayerInventory();
        ItemData candle = ResolveCandleItem(localInventory);
        ItemData key = ResolveKeyItem(localInventory);

        bool itemsSpawned = spawner.SpawnMissionItems(
            Runner,
            candle,
            key,
            candlesRequiredPerPlayer,
            keysToSpawn,
            manualReferenceHeights
        );

        Debug.Log(
            $"[GAME MANAGER] Fort mission items spawned: {itemsSpawned}."
        );

        if (!itemsSpawned)
        {
            Debug.LogError(
                "[GAME MANAGER] Mission item spawning failed. " +
                "Check the [FORT SPAWNER] logs."
            );
            return;
        }

        MissionItemsSpawned = true;
        RPC_StartFortMission();
    }

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_StartFortMission()
    {
        Debug.Log("[FORT MISSION] Start RPC received; configuring local mission panel.", this);
        FortMissionItemSpawner spawner = GetFortMissionSpawner();
        if (spawner == null)
            return;

        PlayerInventory localInventory =
            GetLocalPlayerInventory();

        spawner.ConfigureFortMission(
            fortMission,
            ResolveCandleItem(localInventory),
            ResolveKeyItem(localInventory),
            candlesRequiredPerPlayer,
            keysToSpawn,
            manualReferenceHeights
        );

        // The briefing is shown first. MissionPanel's OK button starts the
        // progress HUD locally after the player acknowledges it.
        if (fortMission != null)
            MissionManager.Show(fortMission);
        else
            MissionManager.ShowRaw("FORT MISSION", "Collect the required items and unlock the dungeon gate.");
        Debug.Log("[FORT MISSION] Local briefing panel shown.", this);
    }

    public ItemData CandleItem => candleItem;
    public ItemData KeyItem => keyItem;
    public MissionData FortMission => fortMission;

    // =========================================================
    // GATE UNLOCK REQUESTS
    // =========================================================

    public void RequestPrisonCellUnlock(int cellNumber, int keyItemId)
    {
        if (Runner == null || !Runner.IsRunning)
            return;

        RPC_RequestPrisonCellUnlock(cellNumber, keyItemId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPrisonCellUnlock(int cellNumber, int keyItemId, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || info.Source == PlayerRef.None ||
            Runner == null || !Runner.TryGetPlayerObject(info.Source, out NetworkObject playerObject) ||
            playerObject == null)
            return;

        int requiredKeyId = cellNumber == 1 ? 102 : cellNumber == 2 ? 104 : 0;
        if (requiredKeyId == 0 || keyItemId != requiredKeyId)
            return;

        PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
        if (inventory == null || !inventory.HasItemId(requiredKeyId))
        {
            Debug.LogWarning($"[PRISON GATE] Player {info.Source} requested cell {cellNumber} without its prison key.", this);
            return;
        }

        if (cellNumber == 1)
        {
            PrisonCell1Unlocked = true;
            PrisonCell1GateOpen = true;
        }
        else
        {
            PrisonCell2Unlocked = true;
            PrisonCell2GateOpen = true;
        }

        Debug.Log($"[PRISON GATE] Cell {cellNumber} unlocked by player {info.Source}.", this);
    }

    public void RequestPrisonCellGateState(int cellNumber, bool open)
    {
        if (Runner == null || !Runner.IsRunning)
            return;

        RPC_RequestPrisonCellGateState(cellNumber, open);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestPrisonCellGateState(
        int cellNumber,
        bool open,
        RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || info.Source == PlayerRef.None ||
            Runner == null || !Runner.TryGetPlayerObject(info.Source, out NetworkObject playerObject) ||
            playerObject == null)
            return;

        if (cellNumber == 1 && PrisonCell1Unlocked)
            PrisonCell1GateOpen = open;
        else if (cellNumber == 2 && PrisonCell2Unlocked)
            PrisonCell2GateOpen = open;
    }

    public void RequestDungeonGateUnlock()
    {
        if (Runner == null)
        {
            return;
        }

        RPC_RequestGateUnlock();
    }

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    private void RPC_RequestGateUnlock(
        RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (!MissionItemsSpawned)
        {
            Debug.LogWarning(
                "[GATE] Unlock requested before the mission started."
            );

            return;
        }

        if (DungeonGateUnlocked)
        {
            return;
        }

        if (info.Source == PlayerRef.None)
        {
            return;
        }

        if (!AreAllActivePlayersInDungeonEntryZone())
        {
            Debug.LogWarning("[GATE] Unlock blocked: every active player must stand inside the red entrance zone. See the per-player zone checks above.");
            return;
        }

        if (!CanPlayerUnlockGate(info.Source))
        {
            Debug.LogWarning(
                $"[GATE] Player {info.Source} cannot unlock the gate " +
                "yet (missing key or candles)."
            );

            return;
        }

        DungeonGateUnlocked = true;
        TeleportCountdown = 0f;

        Debug.Log("[GAME MANAGER] DUNGEON GATE UNLOCKED! Players can walk through to the teleport trigger.");

        RPC_OnGateUnlocked();
    }

    /// <summary>Called by a separate scene trigger after the party walks through the open gate.</summary>
    public bool TryLoadDungeonFromTeleportTrigger(Collider teleportTrigger)
    {
        if (teleportTrigger == null || Runner == null || Object == null ||
            !Object.HasStateAuthority || !DungeonGateUnlocked || TeleportStarted)
            return false;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) ||
                playerObject == null ||
                !PlayerOverlapsTrigger(playerObject.transform, teleportTrigger))
                return false;
        }

        TeleportStarted = true;
        Debug.Log("[GAME MANAGER] All players reached the teleport trigger. Loading Dungeon.");

        if (NetworkRunnerHandler.Instance == null)
        {
            TeleportStarted = false;
            Debug.LogError("[GAME MANAGER] NetworkRunnerHandler is missing; cannot load Dungeon.", this);
            return false;
        }

        StartCoroutine(LoadDungeonAfterTeleportEffect());
        return true;
    }

    private IEnumerator LoadDungeonAfterTeleportEffect()
    {
        // Give each client time to play its local fade and teleport sound.
        yield return new WaitForSecondsRealtime(1f);
        if (NetworkRunnerHandler.Instance != null)
            NetworkRunnerHandler.Instance.LoadDungeonScene();
    }

    private static bool PlayerOverlapsTrigger(Transform playerRoot, Collider trigger)
    {
        Bounds bounds = trigger.bounds;
        Collider[] colliders = playerRoot.GetComponentsInChildren<Collider>();
        foreach (Collider playerCollider in colliders)
        {
            if (playerCollider != null && playerCollider.enabled &&
                bounds.Intersects(playerCollider.bounds))
                return true;
        }

        return bounds.Contains(playerRoot.position);
    }

    private bool AreAllActivePlayersInDungeonEntryZone()
    {
        if (Runner == null)
            return false;

        GameObject zone = GetDungeonEntryZoneObject();
        BoxCollider box = zone != null ? zone.GetComponent<BoxCollider>() : null;
        if (box == null)
            return false;

        int activeCount = 0;
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) || playerObject == null)
            {
                Debug.LogWarning($"[GATE] Player {player} has no registered NetworkObject; cannot validate the entry zone.");
                return false;
            }

            activeCount++;
            Vector3 local = box.transform.InverseTransformPoint(playerObject.transform.position) - box.center;
            Vector3 half = box.size * 0.5f;
            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z)
            {
                Debug.LogWarning($"[GATE] Player {player} is outside DungeonEntryZone. Player position: {playerObject.transform.position}; zone local position: {local}.");
                return false;
            }
        }

        return activeCount > 0;
    }

    [Rpc(
        RpcSources.StateAuthority,
        RpcTargets.All
    )]
    private void RPC_OnGateUnlocked()
    {
        Debug.Log(
            "[GAME MANAGER] Gate unlocked. Enter the dungeon!"
        );

        FindDungeonZoneVisual();

        if (dungeonZoneVisual != null)
        {
            dungeonZoneVisual.SetGreen();
        }

        UpdateGateStatusTag();

        MissionManager.ShowRaw(
            "GATE UNLOCKED",
            "The dungeon gate is open. Move into the darkness!",
            0f,
            DestroyDungeonLockTriggerAfterAcknowledgement
        );
    }

    /// <summary>Remove the local lock interaction after this client acknowledges the unlocked notice.</summary>
    private void DestroyDungeonLockTriggerAfterAcknowledgement()
    {
        GameObject lockTrigger = GameObject.Find("Locktrigger");
        if (lockTrigger != null)
        {
            Destroy(lockTrigger);
            Debug.Log("[GAME MANAGER] Locktrigger destroyed after gate-unlocked acknowledgement.");
        }
    }

    private void UpdateGateStatusTag()
    {
        if (zoneTag == null)
        {
            return;
        }

        WorldTag tagScript =
            zoneTag.GetComponent<WorldTag>();

        if (tagScript == null)
        {
            return;
        }

        if (DungeonGateUnlocked)
        {
            tagScript.SetMessage(
                "GATE UNLOCKED - ENTER THE DUNGEON"
            );

            tagScript.SetColor(
                new Color(
                    0.2f,
                    1f,
                    0.4f,
                    1f
                )
            );
        }
        else
        {
            tagScript.SetMessage(
                "GATE LOCKED - FIND THE KEY & CANDLES"
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
    }

    // =========================================================
    // GATE UNLOCK VALIDATION
    // =========================================================

    private bool CanPlayerUnlockGate(PlayerRef player)
    {
        if (!Runner.TryGetPlayerObject(
                player,
                out NetworkObject playerObject) ||
            playerObject == null)
        {
            return false;
        }

        PlayerInventory inventory =
            playerObject.GetComponent<PlayerInventory>();

        if (inventory == null)
        {
            return false;
        }

        // -------------------------------------------------
        // The interacting player must have the key equipped.
        // -------------------------------------------------

        int keyId = GetKeyItemId();

        if (!inventory.HasItemId(keyId))
        {
            Debug.LogWarning($"[GATE] Player {player} does not carry key item ID {keyId}.");
            return false;
        }

        int selectedSlot = inventory.SelectedSlot;
        if (selectedSlot < 0 ||
            inventory.GetQuickBarItemIdAt(selectedSlot) != keyId)
        {
            Debug.LogWarning($"[GATE] Player {player} has key ID {keyId} but it is not equipped. Selected slot: {selectedSlot}.");
            return false;
        }

        // -------------------------------------------------
        // Candles are a team objective. Any player can collect them; only
        // their combined inventory total matters for unlocking the gate.
        int teamCandles = GetTeamItemCount(GetCandleItemId());
        if (teamCandles < candlesRequiredPerPlayer)
        {
            Debug.LogWarning($"[GATE] Team has {teamCandles}/{candlesRequiredPerPlayer} required candles.");
            return false;
        }

        return true;
    }

    /// <summary>Returns the total quantity held by all active players.</summary>
    public int GetTeamItemCount(int itemId)
    {
        if (Runner == null || itemId <= 0)
            return 0;

        int total = 0;
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) ||
                playerObject == null)
                continue;

            PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
            if (inventory != null)
                total += inventory.GetQuantityForItemId(itemId);
        }

        return total;
    }

    // =========================================================
    // FORT MISSION - ITEM RESOLUTION
    // =========================================================

    private ItemData ResolveCandleItem(PlayerInventory inventory)
    {
        if (candleItem != null)
        {
            return candleItem;
        }

        return inventory != null
            ? inventory.GetItemData(fallbackCandleItemId)
            : null;
    }

    private ItemData ResolveKeyItem(PlayerInventory inventory)
    {
        if (keyItem != null)
        {
            return keyItem;
        }

        return inventory != null
            ? inventory.GetItemData(fallbackKeyItemId)
            : null;
    }

    private int GetCandleItemId()
    {
        return candleItem != null
            ? candleItem.itemId
            : fallbackCandleItemId;
    }

    private int GetKeyItemId()
    {
        return keyItem != null
            ? keyItem.itemId
            : fallbackKeyItemId;
    }

    private PlayerInventory GetLocalPlayerInventory()
    {
        if (Runner == null)
        {
            return null;
        }

        PlayerRef localPlayer =
            Runner.LocalPlayer;

        if (!localPlayer.IsValid)
        {
            return null;
        }

        if (!Runner.TryGetPlayerObject(
                localPlayer,
                out NetworkObject playerObject) ||
            playerObject == null)
        {
            return null;
        }

        return playerObject.GetComponent<PlayerInventory>();
    }

    [Rpc(
    RpcSources.StateAuthority,
    RpcTargets.All
)]
    private void RPC_OnZoneReset(
    RpcInfo info = default)
    {
        Debug.Log(
            "[DUNGEON ZONE] Zone reset to RED."
        );

        if (zoneTag == null)
        {
            return;
        }

        WorldTag tagScript =
            zoneTag.GetComponent<WorldTag>();

        if (tagScript == null)
        {
            return;
        }

        tagScript.SetColor(
            new Color(
                1f,
                0.15f,
                0.15f,
                1f
            )
        );

        tagScript.SetMessage(
            "ENTER THE ZONE"
        );
    }


    // =========================================================
    // ALL PLAYERS ENTERED RPC
    // =========================================================

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnAllPlayersEntered()
    {
        Debug.Log("[GAME] All players entered the gate.");

        // Existing world tag / ping
        TriggerLocalPing();

        // Existing Mission System
        MissionManager.ShowRaw(
            "FIND THE SOLDIER",
            "Find the soldier and ask him how to get out of Fort Santiago.",
            0f
        );
    }

    // =========================================================
    // ZONE GREEN RPC
    // =========================================================

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

        UpdateZoneTag(
            GetPlayerCountInsideZone(),
            GetPlayerCount(),
            true
        );
    }
    private int GetPlayerCountInsideZone()
    {
        int totalPlayers;
        int playersInsideZone;

        CountPlayersInsideZone(
            out totalPlayers,
            out playersInsideZone
        );

        return playersInsideZone;
    }

    // =========================================================
    // ROUTE
    // =========================================================

    private void AdvanceSoldier()
    {
        // Kept for compatibility.
        //
        // Soldier movement is now controlled exclusively
        // by the NavMeshAgent.

        if (!SoldierStarted)
            return;

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
            toZone =
                Vector3.forward;
        }

        toZone.Normalize();

        Vector3 stopInFront =
            zone -
            toZone * 2.2f;

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
            start -
            toZone * 4f;

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

        for (int i = 1;
             i < route.Length;
             i++)
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

        for (int i = 1;
             i < route.Length;
             i++)
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

            if (walked + segmentLength >=
                targetDistance)
            {
                float local =
                    segmentLength > 0f
                        ? (
                            targetDistance -
                            walked
                          ) /
                          segmentLength
                        : 0f;

                return Vector3.Lerp(
                    from,
                    to,
                    local
                );
            }

            walked +=
                segmentLength;
        }

        return route[
            route.Length - 1
        ];
    }


    // =========================================================
    // GATE / ZONE CENTERS
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


    // =========================================================
    // COUNT PLAYERS INSIDE AREA
    // =========================================================

    private void CountPlayersInsideZone(
     out int totalOut,
     out int insideOut)
    {
        totalOut = 0;
        insideOut = 0;

        if (Runner == null)
            return;

        GameObject zoneObject = GetDungeonEntryZoneObject();

        if (zoneObject == null)
        {
            Debug.LogWarning(
                "[DUNGEON ZONE] DungeonEntryZone was not found."
            );

            return;
        }

        BoxCollider boxCollider =
            zoneObject.GetComponent<BoxCollider>();

        if (boxCollider == null)
        {
            Debug.LogError(
                "[DUNGEON ZONE] DungeonEntryZone does not have a BoxCollider."
            );

            return;
        }

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            NetworkObject playerObject =
                Runner.GetPlayerObject(player);

            if (playerObject == null)
                continue;

            totalOut++;

            // Convert player position into the collider's local space.
            Vector3 localPosition =
                boxCollider.transform.InverseTransformPoint(
                    playerObject.transform.position
                );

            // Account for the BoxCollider's center offset.
            localPosition -= boxCollider.center;

            Vector3 halfSize =
                boxCollider.size * 0.5f;

            bool inside =
                Mathf.Abs(localPosition.x) <= halfSize.x &&
                Mathf.Abs(localPosition.y) <= halfSize.y &&
                Mathf.Abs(localPosition.z) <= halfSize.z;

            if (inside)
            {
                insideOut++;
            }
        }
    }

    private GameObject GetDungeonEntryZoneObject()
    {
        if (cachedZoneObject == null)
        {
            cachedZoneObject =
                GameObject.Find(dungeonEntryZoneName);
        }

        return cachedZoneObject;
    }

    // =========================================================
    // SOLDIER VISUAL
    // =========================================================

    private void BuildSoldierVisual()
    {
        if (soldierVisualBuilt &&
            soldierVisual != null)
        {
            return;
        }

        // -------------------------------------------------
        // Find / initialize NavMeshAgent
        // -------------------------------------------------

        InitializeSoldierAgent();

        // -------------------------------------------------
        // Find model under agent
        // -------------------------------------------------

        if (soldierVisual == null)
        {
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

        // -------------------------------------------------
        // Fallback scene search
        // -------------------------------------------------

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

        // -------------------------------------------------
        // Still not found
        // -------------------------------------------------

        if (soldierVisual == null)
        {
            Debug.LogWarning(
                "[GAME MANAGER] SoldierGuideModel is not available yet."
            );

            soldierVisualBuilt =
                false;

            return;
        }

        // -------------------------------------------------
        // Animator
        // -------------------------------------------------

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

        // -------------------------------------------------
        // Save movement-root position
        // -------------------------------------------------

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

        soldierVisualBuilt =
            true;

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

        // -------------------------------------------------
        // State Authority
        // -------------------------------------------------

        if (Object.HasStateAuthority)
        {
            if (soldierAgent != null &&
                soldierAgent.enabled &&
                soldierAgent.isOnNavMesh &&
                SoldierStarted)
            {
                speed =
                    soldierAgent.velocity.magnitude;
            }
        }
        else
        {
            // Client approximation
            speed =
                SoldierStarted &&
                !SoldierReachedDoor
                    ? soldierNavSpeed
                    : 0f;
        }

        // -------------------------------------------------
        // Speed
        // -------------------------------------------------

        if (HasAnimatorParameter(
                soldierSpeedParameter,
                AnimatorControllerParameterType.Float))
        {
            soldierAnimator.SetFloat(
                soldierSpeedParameter,
                speed,
                0.1f,
                Time.deltaTime
            );
        }

        // -------------------------------------------------
        // IsWalking
        // -------------------------------------------------

        if (HasAnimatorParameter(
                soldierWalkingParameter,
                AnimatorControllerParameterType.Bool))
        {
            soldierAnimator.SetBool(
                soldierWalkingParameter,
                speed > 0.1f
            );
        }
    }


    // =========================================================
    // ANIMATOR PARAMETER CHECK
    // =========================================================

    private bool HasAnimatorParameter(
        string parameterName,
        AnimatorControllerParameterType type)
    {
        if (soldierAnimator == null)
            return false;

        foreach (
            AnimatorControllerParameter parameter
            in soldierAnimator.parameters)
        {
            if (parameter.name ==
                    parameterName &&
                parameter.type ==
                    type)
            {
                return true;
            }
        }

        return false;
    }


    // =========================================================
    // LOG ANIMATOR PARAMETERS
    // =========================================================

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

        for (int i = 0;
             i < parameters.Length;
             i++)
        {
            Debug.Log(
                "[GAME MANAGER] - " +
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
        // ---------------------------------------------------------
        // Already built
        // ---------------------------------------------------------

        if (zoneVisualBuilt)
            return;

        // ---------------------------------------------------------
        // Find actual DungeonEntryZone
        // ---------------------------------------------------------

        GameObject zoneObject =
            GetDungeonEntryZoneObject();

        if (zoneObject == null)
        {
            Debug.LogWarning(
                "[DUNGEON ZONE] Cannot build zone visual. " +
                "DungeonEntryZone was not found."
            );

            return;
        }

        // ---------------------------------------------------------
        // Get actual BoxCollider
        // ---------------------------------------------------------

        BoxCollider boxCollider =
            zoneObject.GetComponent<BoxCollider>();

        if (boxCollider == null)
        {
            Debug.LogError(
                "[DUNGEON ZONE] Cannot build zone visual. " +
                "DungeonEntryZone does not have a BoxCollider."
            );

            return;
        }

        // ---------------------------------------------------------
        // Get collider center in WORLD SPACE
        // ---------------------------------------------------------

        Vector3 center =
            zoneObject.transform.TransformPoint(
                boxCollider.center
            );

        // ---------------------------------------------------------
        // Get collider size in WORLD SPACE
        // ---------------------------------------------------------
        //
        // This accounts for the GameObject's scale.
        //
        // The visual is intended for an axis-aligned zone.
        // If the DungeonEntryZone is rotated, see the note below.
        // ---------------------------------------------------------

        Vector3 worldSize =
            Vector3.Scale(
                boxCollider.size,
                zoneObject.transform.lossyScale
            );

        Vector3 half =
            worldSize * 0.5f;

        // ---------------------------------------------------------
        // Mark as successfully built
        // ---------------------------------------------------------

        zoneVisualBuilt = true;

        Debug.Log(
            "[DUNGEON ZONE] Building zone visual from actual BoxCollider."
        );

        Debug.Log(
            $"[DUNGEON ZONE] Center: {center}"
        );

        Debug.Log(
            $"[DUNGEON ZONE] Size: {worldSize}"
        );

        // =========================================================
        // 8 CORNER POSTS
        // =========================================================

        // =========================================================
        // ZONE TAG
        // =========================================================
        if (zoneTag == null && worldTagPoint != null)
        {
            zoneTag = CreateWorldTag(
                worldTagPoint.position,
                "DUNGEON ENTRY",
                new Color(1f, 0.15f, 0.15f, 1f),
                18f,
                0.25f
            );
        }

        Debug.Log(
            "[DUNGEON ZONE] Zone visual successfully created."
        );
    }


    private void FindDungeonZoneVisual()
    {
        if (dungeonZoneVisual != null)
            return;

        GameObject zoneObject =
            GetDungeonEntryZoneObject();

        if (zoneObject == null)
            return;

        dungeonZoneVisual =
            zoneObject.GetComponent<DungeonZoneVisual>();

        if (dungeonZoneVisual == null)
        {
            Debug.LogWarning(
                "[DUNGEON ZONE] DungeonZoneVisual component was not found."
            );

            return;
        }

        Debug.Log(
            "[DUNGEON ZONE] DungeonZoneVisual found."
        );
    }

    // =========================================================
    // ZONE TAG
    // =========================================================
    private void UpdateZoneTag(
        int inside,
        int total,
        bool zoneIsGreen)
    {
        if (zoneTag == null)
        {
            return;
        }

        WorldTag tagScript =
            zoneTag.GetComponent<WorldTag>();

        if (tagScript == null)
        {
            return;
        }

        // =========================================================
        // GREEN
        // =========================================================

        if (zoneIsGreen)
        {
            int secondsLeft =
                Mathf.CeilToInt(
                    Mathf.Max(
                        0f,
                        TeleportCountdown
                    )
                );

            tagScript.SetMessage(
                "TO THE DUNGEON!  " +
                "(" +
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
        // RED
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
    private GameObject CreateWorldTag(
     Vector3 position,
     string message,
     Color color,
     float fontSize,
     float scale,
     Transform parent = null)
    {
        NetworkRunnerHandler handler =
            NetworkRunnerHandler.Instance;

        if (handler == null)
        {
            handler =
                FindFirstObjectByType<NetworkRunnerHandler>();

            if (handler == null)
            {
                Debug.LogError(
                    "[WORLD TAG] NetworkRunnerHandler could not be found."
                );

                return null;
            }
        }

        if (handler.worldTagPrefab == null)
        {
            Debug.LogError(
                "[WORLD TAG] WorldTag Prefab is not assigned."
            );

            return null;
        }

        GameObject tagObject;

        if (parent != null)
        {
            tagObject = Instantiate(
                handler.worldTagPrefab,
                parent
            );

            tagObject.transform.localPosition = position;
            tagObject.transform.localRotation = Quaternion.identity;
        }
        else
        {
            tagObject = Instantiate(
                handler.worldTagPrefab,
                position,
                Quaternion.identity
            );
        }

        WorldTag tag =
            tagObject.GetComponent<WorldTag>();

        if (tag == null)
        {
            tag =
                tagObject.GetComponentInChildren<WorldTag>();
        }

        if (tag == null)
        {
            Debug.LogError(
                "[WORLD TAG] WorldTag component not found."
            );

            Destroy(tagObject);
            return null;
        }

        tag.Configure(
            message,
            color,
            fontSize,
            0f
        );

        tag.SetScale(scale);

        return tagObject;
    }


    // =========================================================
    // DOT
    // =========================================================



    // =========================================================
    // LOCAL PING
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

            
        }

        CreateWorldTag(
            origin +
            Vector3.up * 3f,

            "",

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
    // GET SOLDIER MODEL
    // =========================================================

    private GameObject GetSoldierModel()
    {
        if (cachedSoldierModel != null)
            return cachedSoldierModel;

        if (soldierVisual != null)
        {
            cachedSoldierModel =
                soldierVisual.gameObject;

            return cachedSoldierModel;
        }

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
                cachedSoldierModel =
                    model.gameObject;

                return cachedSoldierModel;
            }
        }

        if (soldierAgent != null)
        {
            Transform model =
                FindChildRecursive(
                    soldierAgent.transform,
                    soldierModelObjectName
                );

            if (model != null)
            {
                cachedSoldierModel =
                    model.gameObject;

                return cachedSoldierModel;
            }
        }

        return null;
    }


    // =========================================================
    // FIND CHILD RECURSIVELY
    // =========================================================

    private Transform FindChildRecursive(
        Transform parent,
        string targetName)
    {
        if (parent.name == targetName)
            return parent;

        foreach (Transform child
                 in parent)
        {
            Transform result =
                FindChildRecursive(
                    child,
                    targetName
                );

            if (result != null)
                return result;
        }

        return null;
    }


    // =========================================================
    // GUIDE MODEL VISIBILITY
    // =========================================================
    //
    // Kept for compatibility with the previous structure.
    //
    // The new flow intentionally keeps the soldier visible after
    // everyone enters the gate, because the mission is:
    //
    // "Find the Soldier"
    //
    // Therefore this is NOT called automatically to hide him.
    //

    private void UpdateGuideModelVisibility()
    {
        if (soldierVisual == null)
            return;

        bool shouldBeVisible =
            AllPlayersEnteredGate;

        if (soldierVisual.gameObject.activeSelf !=
            shouldBeVisible)
        {
            soldierVisual.gameObject.SetActive(
                shouldBeVisible
            );
        }
    }


    // =========================================================
    // SOLDIER ROTATION
    // =========================================================

    private void FaceSoldierAlongRoute(
        Transform soldierRoot)
    {
        if (soldierRoot == null)
            return;

        // -------------------------------------------------
        // State Authority
        // -------------------------------------------------

        if (Object.HasStateAuthority)
        {
            if (soldierAgent != null &&
                soldierAgent.enabled &&
                soldierAgent.isOnNavMesh)
            {
                soldierRoot.rotation =
                    soldierAgent.transform.rotation;
            }

            return;
        }

        // -------------------------------------------------
        // Client
        // -------------------------------------------------

        Vector3 currentPosition =
            SoldierPosition;

        if (!hasLastSoldierNetworkPosition)
        {
            lastSoldierNetworkPosition =
                currentPosition;

            hasLastSoldierNetworkPosition =
                true;

            return;
        }

        Vector3 direction =
            currentPosition -
            lastSoldierNetworkPosition;

        direction.y = 0f;

        if (direction.sqrMagnitude >
            0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up
                );

            soldierRoot.rotation =
                Quaternion.Slerp(
                    soldierRoot.rotation,
                    targetRotation,
                    10f *
                    Time.deltaTime
                );
        }

        lastSoldierNetworkPosition =
            currentPosition;
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

        foreach (PlayerRef player
                 in Runner.ActivePlayers)
        {
            playerCount++;

            NetworkObject playerObject =
                Runner.GetPlayerObject(
                    player
                );

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
            "[GAME MANAGER] Ready check: " +
            $"{readyCount}/{playerCount}"
        );

        return readyCount ==
               playerCount;
    }


    // =========================================================
    // PLAYER COUNT
    // =========================================================

    public int GetPlayerCount()
    {
        if (Runner == null)
            return 0;

        int count = 0;

        foreach (PlayerRef player
                 in Runner.ActivePlayers)
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

        foreach (PlayerRef player
                 in Runner.ActivePlayers)
        {
            NetworkObject playerObject =
                Runner.GetPlayerObject(
                    player
                );

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

