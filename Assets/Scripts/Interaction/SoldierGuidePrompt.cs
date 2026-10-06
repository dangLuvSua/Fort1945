using UnityEngine;
using Fusion;

/// <summary>
/// Displays a local "[E] Follow the soldier" world-space prompt
/// when the local player is close to the SoldierGuide.
///
/// This component is purely cosmetic.
/// It does NOT control the networked soldier flow.
///
/// Recommended location:
/// SoldierGuide
///     └── SoldierGuideModel
///             └── SoldierGuidePrompt
///
/// The WorldTag prefab is assigned directly through the Inspector,
/// so this script does not depend on NetworkRunnerHandler.
/// </summary>
public class SoldierGuidePrompt : MonoBehaviour
{
    // =========================================================
    // RANGE
    // =========================================================

    [Header("Range")]

    [Tooltip("Maximum distance between the local player and the soldier.")]
    [SerializeField]
    private float range = 3f;

    [Tooltip("Vertical/world offset of the prompt above the soldier.")]
    [SerializeField]
    private Vector3 tagOffset =
        new Vector3(
            0f,
            2.6f,
            0f
        );


    // =========================================================
    // TAG
    // =========================================================

    [Header("Tag")]

    [Tooltip(
        "World-space tag prefab containing the WorldTag component."
    )]
    [SerializeField]
    private GameObject worldTagPrefab;

    [SerializeField]
    private string promptText =
        "[E] Follow the soldier";

    [SerializeField]
    private Color promptColor =
        new Color(
            0.3f,
            1f,
            1f,
            1f
        );

    [SerializeField]
    private float fontSize = 40f;


    // =========================================================
    // INTERNAL
    // =========================================================

    private GameObject tagInstance;

    private PlayerController localPlayer;

    private float checkTimer;


    // =========================================================
    // UNITY
    // =========================================================

    private void Update()
    {
        // -----------------------------------------------------
        // Throttle player/range checks.
        // -----------------------------------------------------

        checkTimer -= Time.deltaTime;

        if (checkTimer <= 0f)
        {
            checkTimer = 0.1f;

            FindLocalPlayer();
            UpdatePrompt();
        }


        // -----------------------------------------------------
        // Keep the tag attached to the moving soldier.
        // -----------------------------------------------------

        if (tagInstance != null)
        {
            tagInstance.transform.position =
                transform.position +
                tagOffset;
        }
    }


    // =========================================================
    // FIND LOCAL PLAYER
    // =========================================================

    private void FindLocalPlayer()
    {
        // Already have the local player.
        if (localPlayer != null)
        {
            if (localPlayer.gameObject.activeInHierarchy)
                return;

            localPlayer = null;
        }


        PlayerController[] players =
            FindObjectsByType<PlayerController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );


        foreach (PlayerController player in players)
        {
            NetworkObject networkObject =
                player.GetComponent<NetworkObject>();

            if (networkObject == null)
                continue;


            // -------------------------------------------------
            // Only use this client's player.
            // -------------------------------------------------

            if (networkObject.HasInputAuthority)
            {
                localPlayer = player;

                Debug.Log(
                    "[SOLDIER PROMPT] Local player found: " +
                    player.gameObject.name
                );

                return;
            }
        }
    }


    // =========================================================
    // UPDATE PROMPT
    // =========================================================

    private void UpdatePrompt()
    {
        bool flowActive =
            IsFlowActive();

        bool playerInRange =
            IsPlayerInRange();

        bool shouldShow =
            flowActive &&
            playerInRange;


        // -----------------------------------------------------
        // SHOW
        // -----------------------------------------------------

        if (shouldShow)
        {
            if (tagInstance == null)
            {
                tagInstance =
                    CreateTag();
            }
        }


        // -----------------------------------------------------
        // HIDE
        // -----------------------------------------------------

        else
        {
            if (tagInstance != null)
            {
                Destroy(tagInstance);

                tagInstance = null;
            }
        }
    }


    // =========================================================
    // FLOW CHECK
    // =========================================================

    private bool IsFlowActive()
    {
        NetworkGameManager manager =
            NetworkGameManager.Instance;


        if (manager == null)
        {
            return false;
        }


        // -----------------------------------------------------
        // Prompt appears only while the soldier-follow phase
        // is active and before teleportation begins.
        // -----------------------------------------------------

        return
            manager.SoldierStarted &&
            !manager.TeleportStarted;
    }


    // =========================================================
    // PLAYER RANGE
    // =========================================================

    private bool IsPlayerInRange()
    {
        if (localPlayer == null)
            return false;


        float distance =
            Vector3.Distance(
                transform.position,
                localPlayer.transform.position
            );


        return distance <= range;
    }


    // =========================================================
    // CREATE TAG
    // =========================================================

    private GameObject CreateTag()
    {
        // -----------------------------------------------------
        // Make sure a prefab was assigned.
        // -----------------------------------------------------

        if (worldTagPrefab == null)
        {
            Debug.LogWarning(
                "[SOLDIER PROMPT] World Tag Prefab is not assigned " +
                "in the SoldierGuidePrompt Inspector."
            );

            return null;
        }


        // -----------------------------------------------------
        // Create the local cosmetic tag.
        // -----------------------------------------------------

        GameObject tag =
            Instantiate(
                worldTagPrefab
            );


        tag.transform.position =
            transform.position +
            tagOffset;


        // -----------------------------------------------------
        // Find WorldTag component.
        // -----------------------------------------------------

        WorldTag tagScript =
            tag.GetComponent<WorldTag>();


        // Also support WorldTag being on a child object.
        if (tagScript == null)
        {
            tagScript =
                tag.GetComponentInChildren<WorldTag>(
                    true
                );
        }


        // -----------------------------------------------------
        // Configure WorldTag.
        // -----------------------------------------------------

        if (tagScript != null)
        {
            tagScript.Configure(
                promptText,
                promptColor,
                fontSize,
                0f
            );
        }
        else
        {
            Debug.LogWarning(
                "[SOLDIER PROMPT] WorldTag component was not found " +
                "on the assigned World Tag Prefab."
            );
        }


        Debug.Log(
            "[SOLDIER PROMPT] Created [E] Follow the soldier tag."
        );


        return tag;
    }


    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDisable()
    {
        if (tagInstance != null)
        {
            Destroy(tagInstance);

            tagInstance = null;
        }
    }


    // =========================================================
    // OPTIONAL DEBUG GIZMO
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            new Color(
                0.3f,
                1f,
                1f,
                0.35f
            );

        Gizmos.DrawWireSphere(
            transform.position,
            range
        );
    }
}