using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    // =========================================================
    // CORRIDOR PIECES
    // =========================================================

    [Header("Corridor Pieces")]
    [Tooltip("Add Dungeon Straight, Dungeon Left and Dungeon Right here.")]
    public GameObject[] corridorPieces;


    // =========================================================
    // INTERSECTION
    // =========================================================

    [Header("Intersection")]
    [Tooltip("Add Dungeon Intersection here.")]
    public GameObject intersectionPrefab;


    // =========================================================
    // NORMAL TERMINAL ROOMS
    // =========================================================

    [Header("Terminal Rooms")]
    [Tooltip("Normal terminal rooms. Each needs ONE Entrance connector.")]
    public GameObject[] roomPrefabs;


    // =========================================================
    // UNIQUE ROOM
    // =========================================================

    [Header("Unique Room")]
    [Tooltip("This room can spawn ONLY ONCE per dungeon layout.")]
    public GameObject uniqueRoomPrefab;


    // =========================================================
    // DUNGEON SETTINGS
    // =========================================================

    [Header("Dungeon Settings")]

    [Min(2)]
    [Tooltip("Total number of terminal rooms, including the Unique Room.")]
    public int numberOfRooms = 10;

    [Min(0)]
    [Tooltip("Maximum number of normal corridors before a terminal room.")]
    public int maxCorridorsBeforeRoom = 2;

    [Min(1)]
    [Tooltip("Number of placement attempts for each piece.")]
    public int placementAttempts = 15;


    // =========================================================
    // LEVEL
    // =========================================================

    [Header("Level")]

    [Tooltip("Keep all dungeon pieces on the same Y level.")]
    public bool keepDungeonLevel = true;

    public float dungeonY = 0f;


    // =========================================================
    // GENERATION
    // =========================================================

    [Header("Generation")]

    [Tooltip("Generate automatically when the scene starts.")]
    public bool generateOnStart = true;

    [Tooltip("Show generation information in the Console.")]
    public bool debugGeneration = true;


    // =========================================================
    // GENERATION RETRY
    // =========================================================

    [Header("Generation Retry")]

    [Tooltip("How many completely new layouts to try if generation fails.")]
    [Min(1)]
    public int maxGenerationRetries = 20;


    // =========================================================
    // RUNTIME DATA
    // =========================================================

    private readonly List<GameObject> generatedPieces =
        new List<GameObject>();

    private readonly List<DungeonConnector> availableExits =
        new List<DungeonConnector>();

    private int roomsCreated = 0;

    private bool uniqueRoomCreated = false;


    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateDungeonWithRetries();
        }
    }


    // =========================================================
    // GENERATE WITH RETRIES
    // =========================================================

    private void GenerateDungeonWithRetries()
    {
        for (
            int attempt = 1;
            attempt <= maxGenerationRetries;
            attempt++
        )
        {
            if (debugGeneration)
            {
                Debug.Log(
                    "Dungeon generation attempt " +
                    attempt +
                    "/" +
                    maxGenerationRetries
                );
            }

            bool success = GenerateDungeon();

            if (success)
            {
                if (debugGeneration)
                {
                    Debug.Log(
                        "Valid dungeon found on attempt " +
                        attempt +
                        "."
                    );
                }

                return;
            }

            if (debugGeneration)
            {
                Debug.LogWarning(
                    "Generation attempt " +
                    attempt +
                    " failed. Rooms: " +
                    roomsCreated +
                    "/" +
                    numberOfRooms +
                    " | Unique Room: " +
                    uniqueRoomCreated
                );
            }
        }

        Debug.LogError(
            "Could not generate a complete dungeon after " +
            maxGenerationRetries +
            " attempts."
        );
    }


    // =========================================================
    // MAIN GENERATION
    // =========================================================

    private bool GenerateDungeon()
    {
        ClearDungeon();

        if (!ValidateSetup())
        {
            return false;
        }


        // -----------------------------------------------------
        // STARTING CORRIDOR
        // -----------------------------------------------------

        GameObject firstCorridor =
            CreateCorridorAt(
                Vector3.zero,
                Quaternion.identity
            );

        if (firstCorridor == null)
        {
            Debug.LogError(
                "Could not create starting corridor."
            );

            return false;
        }


        DungeonConnector firstEntrance =
            FindEntrance(firstCorridor);

        if (firstEntrance == null)
        {
            Debug.LogError(
                "Starting corridor has no Entrance connector."
            );

            return false;
        }


        // -----------------------------------------------------
        // FIRST TERMINAL ROOM
        // -----------------------------------------------------

        if (!TryPlaceRoom(firstEntrance))
        {
            Debug.LogError(
                "Could not place starting terminal room."
            );

            return false;
        }

        firstEntrance.used = true;


        // Add the starting corridor's Exit.
        AddAvailableExits(firstCorridor);


        // -----------------------------------------------------
        // INTERSECTION COUNT
        // -----------------------------------------------------

        if (numberOfRooms % 2 != 0)
        {
            Debug.LogWarning(
                "This branching topology works cleanly with " +
                "an EVEN number of terminal rooms. " +
                "Odd room counts may require the future loop system."
            );
        }


        int intersectionsNeeded;

        if (numberOfRooms % 2 == 0)
        {
            intersectionsNeeded =
                (numberOfRooms - 2) / 2;
        }
        else
        {
            intersectionsNeeded =
                Mathf.Max(
                    0,
                    (numberOfRooms - 3) / 2
                );
        }


        // -----------------------------------------------------
        // CREATE INTERSECTIONS
        // -----------------------------------------------------

        for (
            int i = 0;
            i < intersectionsNeeded;
            i++
        )
        {
            bool intersectionPlaced =
                TryAddIntersectionToDungeon();

            if (!intersectionPlaced)
            {
                Debug.LogWarning(
                    "Could not place Intersection #" +
                    (i + 1) +
                    "."
                );

                break;
            }

            if (debugGeneration)
            {
                Debug.Log(
                    "Intersection " +
                    (i + 1) +
                    "/" +
                    intersectionsNeeded +
                    " created. Open exits: " +
                    availableExits.Count
                );
            }
        }


        // -----------------------------------------------------
        // CLOSE ALL OPEN EXITS
        // -----------------------------------------------------

        bool finished =
            FinishAllOpenExits();

        if (!finished)
        {
            return false;
        }


        // -----------------------------------------------------
        // FINAL VALIDATION
        // -----------------------------------------------------

        if (
            roomsCreated == numberOfRooms &&
            uniqueRoomCreated
        )
        {
            if (debugGeneration)
            {
                Debug.Log(
                    "Dungeon generated successfully! " +
                    "Terminal Rooms: " +
                    roomsCreated +
                    "/" +
                    numberOfRooms +
                    " | Unique Room: YES" +
                    " | Total Pieces: " +
                    generatedPieces.Count
                );
            }

            return true;
        }


        return false;
    }


    // =========================================================
    // ADD INTERSECTION
    // =========================================================

    private bool TryAddIntersectionToDungeon()
    {
        if (
            intersectionPrefab == null ||
            availableExits.Count == 0
        )
        {
            return false;
        }


        List<int> exitIndices =
            new List<int>();


        for (
            int i = 0;
            i < availableExits.Count;
            i++
        )
        {
            exitIndices.Add(i);
        }


        // Randomize which Exit we try first.
        ShuffleList(exitIndices);


        foreach (int exitIndex in exitIndices)
        {
            if (
                exitIndex < 0 ||
                exitIndex >= availableExits.Count
            )
            {
                continue;
            }


            DungeonConnector targetExit =
                availableExits[exitIndex];


            if (
                targetExit == null ||
                targetExit.used
            )
            {
                continue;
            }


            GameObject intersection =
                TryPlaceIntersection(
                    targetExit
                );


            if (intersection == null)
            {
                continue;
            }


            // The old Exit is now connected.
            targetExit.used = true;


            // Remove the old Exit.
            availableExits.RemoveAt(
                exitIndex
            );


            // Add the Intersection's Exits.
            AddAvailableExits(
                intersection
            );


            return true;
        }


        return false;
    }


    // =========================================================
    // FINISH OPEN EXITS
    // =========================================================

    private bool FinishAllOpenExits()
    {
        if (availableExits.Count == 0)
        {
            return false;
        }


        // -----------------------------------------------------
        // RANDOMIZE EXIT ORDER
        // -----------------------------------------------------

        List<DungeonConnector> exitsToFinish =
            new List<DungeonConnector>(
                availableExits
            );


        ShuffleList(exitsToFinish);


        // -----------------------------------------------------
        // PLACE UNIQUE ROOM FIRST
        // -----------------------------------------------------

        if (uniqueRoomPrefab != null)
        {
            bool uniquePlaced = false;


            foreach (
                DungeonConnector targetExit
                in exitsToFinish
            )
            {
                if (targetExit == null)
                {
                    continue;
                }


                if (targetExit.used)
                {
                    continue;
                }


                // Try to place the Prison here.
                if (
                    TryPlaceUniqueRoom(
                        targetExit
                    )
                )
                {
                    targetExit.used = true;


                    availableExits.Remove(
                        targetExit
                    );


                    uniquePlaced = true;


                    if (debugGeneration)
                    {
                        Debug.Log(
                            "Dungeon Prison successfully placed."
                        );
                    }


                    break;
                }
            }


            // Prison must exist.
            // If it cannot fit anywhere, regenerate
            // the entire layout.
            if (!uniquePlaced)
            {
                Debug.LogWarning(
                    "Dungeon Prison could not be placed anywhere. " +
                    "Generating a completely new layout."
                );

                return false;
            }
        }


        // -----------------------------------------------------
        // NORMAL TERMINAL ROOMS
        // -----------------------------------------------------

        exitsToFinish =
            new List<DungeonConnector>(
                availableExits
            );


        ShuffleList(exitsToFinish);


        foreach (
            DungeonConnector targetExit
            in exitsToFinish
        )
        {
            if (roomsCreated >= numberOfRooms)
            {
                break;
            }


            if (targetExit == null)
            {
                continue;
            }


            if (targetExit.used)
            {
                continue;
            }


            bool roomPlaced =
                TryCreateBranchToRoom(
                    targetExit
                );


            if (roomPlaced)
            {
                targetExit.used = true;


                availableExits.Remove(
                    targetExit
                );
            }
            else
            {
                targetExit.used = true;


                availableExits.Remove(
                    targetExit
                );


                Debug.LogWarning(
                    "Could not close an Exit with a terminal room."
                );
            }
        }


        // -----------------------------------------------------
        // FINAL CHECK
        // -----------------------------------------------------

        if (
            roomsCreated == numberOfRooms &&
            uniqueRoomCreated
        )
        {
            return true;
        }


        return false;
    }


    // =========================================================
    // CREATE NORMAL BRANCH TO ROOM
    // =========================================================

    private bool TryCreateBranchToRoom(
        DungeonConnector startingExit
    )
    {
        if (startingExit == null)
        {
            return false;
        }


        /*
         * Possible branch lengths:
         *
         * 0:
         * Exit -> Room
         *
         * 1:
         * Exit -> Corridor -> Room
         *
         * 2:
         * Exit -> Corridor -> Corridor -> Room
         */


        List<int> lengths =
            new List<int>();


        for (
            int i = 0;
            i <= maxCorridorsBeforeRoom;
            i++
        )
        {
            lengths.Add(i);
        }


        ShuffleList(lengths);


        foreach (int corridorCount in lengths)
        {
            DungeonConnector currentExit =
                startingExit;


            List<GameObject> branchPieces =
                new List<GameObject>();


            bool branchFailed = false;


            // -------------------------------------------------
            // CREATE CORRIDORS
            // -------------------------------------------------

            for (
                int c = 0;
                c < corridorCount;
                c++
            )
            {
                GameObject corridor =
                    TryPlaceCorridor(
                        currentExit
                    );


                if (corridor == null)
                {
                    branchFailed = true;
                    break;
                }


                branchPieces.Add(
                    corridor
                );


                DungeonConnector nextExit =
                    FindUnusedExit(
                        corridor
                    );


                if (nextExit == null)
                {
                    branchFailed = true;
                    break;
                }


                currentExit =
                    nextExit;
            }


            // -------------------------------------------------
            // BRANCH FAILED
            // -------------------------------------------------

            if (branchFailed)
            {
                DestroyBranchPieces(
                    branchPieces
                );

                continue;
            }


            // -------------------------------------------------
            // PLACE NORMAL ROOM
            // -------------------------------------------------

            if (
                TryPlaceRoom(
                    currentExit
                )
            )
            {
                currentExit.used = true;

                return true;
            }


            // Room failed.
            // Delete the temporary corridor chain.

            DestroyBranchPieces(
                branchPieces
            );
        }


        return false;
    }


    // =========================================================
    // PLACE CORRIDOR
    // =========================================================

    private GameObject TryPlaceCorridor(
        DungeonConnector targetExit
    )
    {
        if (
            targetExit == null ||
            corridorPieces == null ||
            corridorPieces.Length == 0
        )
        {
            return null;
        }


        for (
            int attempt = 0;
            attempt < placementAttempts;
            attempt++
        )
        {
            GameObject prefab =
                GetRandomValidPrefab(
                    corridorPieces
                );


            if (prefab == null)
            {
                continue;
            }


            GameObject corridor =
                Instantiate(
                    prefab,
                    Vector3.zero,
                    Quaternion.identity,
                    transform
                );


            DungeonConnector entrance =
                FindEntrance(
                    corridor
                );


            // -------------------------------------------------
            // CHECK ENTRANCE
            // -------------------------------------------------

            if (entrance == null)
            {
                Debug.LogWarning(
                    corridor.name +
                    " has no Entrance connector."
                );


                Destroy(corridor);

                continue;
            }


            // -------------------------------------------------
            // CHECK ROOM BOUNDS
            // -------------------------------------------------

            if (
                FindRoomBounds(
                    corridor
                ) == null
            )
            {
                Debug.LogWarning(
                    corridor.name +
                    " has no RoomBounds."
                );


                Destroy(corridor);

                continue;
            }


            // -------------------------------------------------
            // ALIGN
            // -------------------------------------------------

            AlignPiece(
                corridor,
                entrance,
                targetExit
            );


            SnapToDungeonLevel(
                corridor
            );


            AlignEntrancePosition(
                corridor,
                targetExit
            );


            // -------------------------------------------------
            // OVERLAP CHECK
            // -------------------------------------------------

            if (
                RoomOverlaps(
                    corridor
                )
            )
            {
                Destroy(corridor);

                continue;
            }


            // -------------------------------------------------
            // SUCCESS
            // -------------------------------------------------

            generatedPieces.Add(
                corridor
            );


            entrance.used = true;


            return corridor;
        }


        return null;
    }


    // =========================================================
    // PLACE INTERSECTION
    // =========================================================

    private GameObject TryPlaceIntersection(
        DungeonConnector targetExit
    )
    {
        if (
            intersectionPrefab == null ||
            targetExit == null
        )
        {
            return null;
        }


        for (
            int attempt = 0;
            attempt < placementAttempts;
            attempt++
        )
        {
            GameObject intersection =
                Instantiate(
                    intersectionPrefab,
                    Vector3.zero,
                    Quaternion.identity,
                    transform
                );


            DungeonConnector entrance =
                FindEntrance(
                    intersection
                );


            // -------------------------------------------------
            // CHECK ENTRANCE
            // -------------------------------------------------

            if (entrance == null)
            {
                Debug.LogWarning(
                    "Dungeon Intersection has no Entrance."
                );


                Destroy(intersection);

                continue;
            }


            // -------------------------------------------------
            // CHECK ROOM BOUNDS
            // -------------------------------------------------

            if (
                FindRoomBounds(
                    intersection
                ) == null
            )
            {
                Debug.LogWarning(
                    "Dungeon Intersection has no RoomBounds."
                );


                Destroy(intersection);

                continue;
            }


            // -------------------------------------------------
            // ALIGN
            // -------------------------------------------------

            AlignPiece(
                intersection,
                entrance,
                targetExit
            );


            SnapToDungeonLevel(
                intersection
            );


            AlignEntrancePosition(
                intersection,
                targetExit
            );


            // -------------------------------------------------
            // OVERLAP CHECK
            // -------------------------------------------------

            if (
                RoomOverlaps(
                    intersection
                )
            )
            {
                Destroy(intersection);

                continue;
            }


            // -------------------------------------------------
            // SUCCESS
            // -------------------------------------------------

            generatedPieces.Add(
                intersection
            );


            entrance.used = true;


            return intersection;
        }


        return null;
    }


    // =========================================================
    // PLACE UNIQUE ROOM
    // =========================================================

    private bool TryPlaceUniqueRoom(
        DungeonConnector targetExit
    )
    {
        if (
            targetExit == null ||
            uniqueRoomPrefab == null ||
            uniqueRoomCreated ||
            roomsCreated >= numberOfRooms
        )
        {
            return false;
        }


        for (
            int attempt = 0;
            attempt < placementAttempts;
            attempt++
        )
        {
            GameObject room =
                Instantiate(
                    uniqueRoomPrefab,
                    Vector3.zero,
                    Quaternion.identity,
                    transform
                );


            DungeonConnector entrance =
                FindEntrance(
                    room
                );


            // -------------------------------------------------
            // CHECK ENTRANCE
            // -------------------------------------------------

            if (entrance == null)
            {
                Debug.LogWarning(
                    room.name +
                    " has no Entrance connector."
                );


                Destroy(room);

                continue;
            }


            // -------------------------------------------------
            // CHECK ROOM BOUNDS
            // -------------------------------------------------

            if (
                FindRoomBounds(
                    room
                ) == null
            )
            {
                Debug.LogWarning(
                    room.name +
                    " has no RoomBounds."
                );


                Destroy(room);

                continue;
            }


            // -------------------------------------------------
            // ALIGN PRISON
            // -------------------------------------------------

            AlignPiece(
                room,
                entrance,
                targetExit
            );


            SnapToDungeonLevel(
                room
            );


            AlignEntrancePosition(
                room,
                targetExit
            );


            // -------------------------------------------------
            // OVERLAP CHECK
            // -------------------------------------------------

            if (
                RoomOverlaps(
                    room
                )
            )
            {
                Destroy(room);

                continue;
            }


            // -------------------------------------------------
            // SUCCESS
            // -------------------------------------------------

            generatedPieces.Add(
                room
            );


            entrance.used = true;


            roomsCreated++;


            uniqueRoomCreated = true;


            if (debugGeneration)
            {
                Debug.Log(
                    "UNIQUE ROOM CREATED: " +
                    room.name +
                    " | Terminal Rooms: " +
                    roomsCreated +
                    "/" +
                    numberOfRooms
                );
            }


            return true;
        }


        return false;
    }


    // =========================================================
    // PLACE NORMAL TERMINAL ROOM
    // =========================================================

    private bool TryPlaceRoom(
        DungeonConnector targetExit
    )
    {
        if (
            targetExit == null ||
            roomPrefabs == null ||
            roomPrefabs.Length == 0 ||
            roomsCreated >= numberOfRooms
        )
        {
            return false;
        }


        for (
            int attempt = 0;
            attempt < placementAttempts;
            attempt++
        )
        {
            GameObject prefab =
                GetRandomValidPrefab(
                    roomPrefabs
                );


            if (prefab == null)
            {
                continue;
            }


            GameObject room =
                Instantiate(
                    prefab,
                    Vector3.zero,
                    Quaternion.identity,
                    transform
                );


            DungeonConnector entrance =
                FindEntrance(
                    room
                );


            // -------------------------------------------------
            // CHECK ENTRANCE
            // -------------------------------------------------

            if (entrance == null)
            {
                Debug.LogWarning(
                    room.name +
                    " has no Entrance connector."
                );


                Destroy(room);

                continue;
            }


            // -------------------------------------------------
            // CHECK ROOM BOUNDS
            // -------------------------------------------------

            if (
                FindRoomBounds(
                    room
                ) == null
            )
            {
                Debug.LogWarning(
                    room.name +
                    " has no RoomBounds."
                );


                Destroy(room);

                continue;
            }


            // -------------------------------------------------
            // ALIGN ROOM
            // -------------------------------------------------

            AlignPiece(
                room,
                entrance,
                targetExit
            );


            SnapToDungeonLevel(
                room
            );


            AlignEntrancePosition(
                room,
                targetExit
            );


            // -------------------------------------------------
            // OVERLAP CHECK
            // -------------------------------------------------

            if (
                RoomOverlaps(
                    room
                )
            )
            {
                Destroy(room);

                continue;
            }


            // -------------------------------------------------
            // SUCCESS
            // -------------------------------------------------

            generatedPieces.Add(
                room
            );


            entrance.used = true;


            roomsCreated++;


            if (debugGeneration)
            {
                Debug.Log(
                    "Terminal Room created: " +
                    roomsCreated +
                    "/" +
                    numberOfRooms
                );
            }


            return true;
        }


        return false;
    }


    // =========================================================
    // ALIGN PIECE
    // =========================================================

    private void AlignPiece(
        GameObject piece,
        DungeonConnector entrance,
        DungeonConnector targetExit
    )
    {
        if (
            piece == null ||
            entrance == null ||
            targetExit == null
        )
        {
            return;
        }


        Vector3 entranceForward =
            entrance.transform.forward;


        Vector3 targetForward =
            targetExit.transform.forward;


        // Ignore Y.
        // This keeps the dungeon flat.

        entranceForward.y = 0f;
        targetForward.y = 0f;


        if (
            entranceForward.sqrMagnitude <
            0.0001f ||

            targetForward.sqrMagnitude <
            0.0001f
        )
        {
            return;
        }


        entranceForward.Normalize();
        targetForward.Normalize();


        // Entrance must face the opposite
        // direction of the Exit.

        Quaternion correction =
            Quaternion.FromToRotation(
                entranceForward,
                -targetForward
            );


        piece.transform.rotation =
            correction *
            piece.transform.rotation;


        // Exact position alignment.

        AlignEntrancePosition(
            piece,
            targetExit
        );
    }


    // =========================================================
    // EXACT CONNECTOR POSITION
    // =========================================================

    private void AlignEntrancePosition(
        GameObject piece,
        DungeonConnector targetExit
    )
    {
        if (
            piece == null ||
            targetExit == null
        )
        {
            return;
        }


        DungeonConnector entrance =
            FindEntrance(
                piece
            );


        if (entrance == null)
        {
            return;
        }


        Vector3 difference =
            targetExit.transform.position -
            entrance.transform.position;


        piece.transform.position +=
            difference;
    }


    // =========================================================
    // SNAP TO DUNGEON LEVEL
    // =========================================================

    private void SnapToDungeonLevel(
        GameObject piece
    )
    {
        if (
            piece == null ||
            !keepDungeonLevel
        )
        {
            return;
        }


        Vector3 position =
            piece.transform.position;


        position.y =
            dungeonY;


        piece.transform.position =
            position;
    }


    // =========================================================
    // ADD AVAILABLE EXITS
    // =========================================================

    private void AddAvailableExits(
        GameObject piece
    )
    {
        if (piece == null)
        {
            return;
        }


        DungeonConnector[] connectors =
            piece.GetComponentsInChildren<
                DungeonConnector
            >();


        foreach (
            DungeonConnector connector
            in connectors
        )
        {
            if (
                connector.connectorType ==
                DungeonConnector.ConnectorType.Exit &&

                !connector.used &&

                !availableExits.Contains(
                    connector
                )
            )
            {
                availableExits.Add(
                    connector
                );
            }
        }
    }


    // =========================================================
    // FIND UNUSED EXIT
    // =========================================================

    private DungeonConnector FindUnusedExit(
        GameObject piece
    )
    {
        if (piece == null)
        {
            return null;
        }


        DungeonConnector[] connectors =
            piece.GetComponentsInChildren<
                DungeonConnector
            >();


        foreach (
            DungeonConnector connector
            in connectors
        )
        {
            if (
                connector.connectorType ==
                DungeonConnector.ConnectorType.Exit &&

                !connector.used
            )
            {
                return connector;
            }
        }


        return null;
    }


    // =========================================================
    // FIND ENTRANCE
    // =========================================================

    private DungeonConnector FindEntrance(
        GameObject piece
    )
    {
        if (piece == null)
        {
            return null;
        }


        DungeonConnector[] connectors =
            piece.GetComponentsInChildren<
                DungeonConnector
            >();


        foreach (
            DungeonConnector connector
            in connectors
        )
        {
            if (
                connector.connectorType ==
                DungeonConnector.ConnectorType.Entrance
            )
            {
                return connector;
            }
        }


        return null;
    }


    // =========================================================
    // FIND ROOM BOUNDS
    // =========================================================

    private BoxCollider FindRoomBounds(
        GameObject piece
    )
    {
        if (piece == null)
        {
            return null;
        }


        // First check direct child.

        Transform bounds =
            piece.transform.Find(
                "RoomBounds"
            );


        if (bounds != null)
        {
            BoxCollider collider =
                bounds.GetComponent<
                    BoxCollider
                >();


            if (collider != null)
            {
                return collider;
            }
        }


        // Then search all children.

        BoxCollider[] colliders =
            piece.GetComponentsInChildren<
                BoxCollider
            >();


        foreach (
            BoxCollider collider
            in colliders
        )
        {
            if (
                collider.gameObject.name ==
                "RoomBounds"
            )
            {
                return collider;
            }
        }


        return null;
    }


    // =========================================================
    // OVERLAP CHECK
    // =========================================================

    private bool RoomOverlaps(
        GameObject newPiece
    )
    {
        BoxCollider newBounds =
            FindRoomBounds(
                newPiece
            );


        if (newBounds == null)
        {
            Debug.LogWarning(
                newPiece.name +
                " has no RoomBounds."
            );


            return true;
        }


        foreach (
            GameObject existingPiece
            in generatedPieces
        )
        {
            if (existingPiece == null)
            {
                continue;
            }


            BoxCollider existingBounds =
                FindRoomBounds(
                    existingPiece
                );


            if (existingBounds == null)
            {
                continue;
            }


            bool overlapping =
                Physics.ComputePenetration(
                    newBounds,
                    newBounds.transform.position,
                    newBounds.transform.rotation,

                    existingBounds,
                    existingBounds.transform.position,
                    existingBounds.transform.rotation,

                    out Vector3 direction,
                    out float distance
                );


            if (overlapping)
            {
                return true;
            }
        }


        return false;
    }


    // =========================================================
    // CREATE STARTING CORRIDOR
    // =========================================================

    private GameObject CreateCorridorAt(
        Vector3 position,
        Quaternion rotation
    )
    {
        if (
            corridorPieces == null ||
            corridorPieces.Length == 0
        )
        {
            return null;
        }


        GameObject selected =
            GetRandomValidPrefab(
                corridorPieces
            );


        if (selected == null)
        {
            return null;
        }


        GameObject corridor =
            Instantiate(
                selected,
                position,
                rotation,
                transform
            );


        if (
            FindEntrance(corridor) == null ||
            FindRoomBounds(corridor) == null
        )
        {
            Debug.LogError(
                corridor.name +
                " needs an Entrance and RoomBounds."
            );


            Destroy(corridor);


            return null;
        }


        generatedPieces.Add(
            corridor
        );


        return corridor;
    }


    // =========================================================
    // GET RANDOM VALID PREFAB
    // =========================================================

    private GameObject GetRandomValidPrefab(
        GameObject[] prefabs
    )
    {
        if (
            prefabs == null ||
            prefabs.Length == 0
        )
        {
            return null;
        }


        List<GameObject> validPrefabs =
            new List<GameObject>();


        foreach (
            GameObject prefab
            in prefabs
        )
        {
            if (prefab != null)
            {
                validPrefabs.Add(
                    prefab
                );
            }
        }


        if (validPrefabs.Count == 0)
        {
            return null;
        }


        return validPrefabs[
            Random.Range(
                0,
                validPrefabs.Count
            )
        ];
    }


    // =========================================================
    // SHUFFLE LIST
    // =========================================================

    private void ShuffleList<T>(
        List<T> list
    )
    {
        for (
            int i = list.Count - 1;
            i > 0;
            i--
        )
        {
            int randomIndex =
                Random.Range(
                    0,
                    i + 1
                );


            T temporary =
                list[i];


            list[i] =
                list[randomIndex];


            list[randomIndex] =
                temporary;
        }
    }


    // =========================================================
    // DESTROY TEMPORARY BRANCH
    // =========================================================

    private void DestroyBranchPieces(
        List<GameObject> branchPieces
    )
    {
        if (branchPieces == null)
        {
            return;
        }


        foreach (
            GameObject piece
            in branchPieces
        )
        {
            if (piece == null)
            {
                continue;
            }


            generatedPieces.Remove(
                piece
            );


            Destroy(piece);
        }
    }


    // =========================================================
    // CLEAR DUNGEON
    // =========================================================

    private void ClearDungeon()
    {
        availableExits.Clear();

        generatedPieces.Clear();

        roomsCreated = 0;

        uniqueRoomCreated = false;


        for (
            int i = transform.childCount - 1;
            i >= 0;
            i--
        )
        {
            Destroy(
                transform.GetChild(i).gameObject
            );
        }
    }


    // =========================================================
    // VALIDATE SETUP
    // =========================================================

    private bool ValidateSetup()
    {
        if (
            corridorPieces == null ||
            corridorPieces.Length == 0
        )
        {
            Debug.LogError(
                "No corridor pieces assigned."
            );

            return false;
        }


        if (intersectionPrefab == null)
        {
            Debug.LogError(
                "Dungeon Intersection is not assigned."
            );

            return false;
        }


        if (
            roomPrefabs == null ||
            roomPrefabs.Length == 0
        )
        {
            Debug.LogError(
                "No normal terminal rooms assigned."
            );

            return false;
        }


        if (uniqueRoomPrefab == null)
        {
            Debug.LogError(
                "Unique Room is not assigned. " +
                "Assign Dungeon Prison."
            );

            return false;
        }


        if (numberOfRooms < 2)
        {
            Debug.LogError(
                "Number Of Rooms must be at least 2."
            );

            return false;
        }


        if (placementAttempts < 1)
        {
            Debug.LogError(
                "Placement Attempts must be at least 1."
            );

            return false;
        }


        if (maxCorridorsBeforeRoom < 0)
        {
            Debug.LogError(
                "Max Corridors Before Room cannot be negative."
            );

            return false;
        }


        if (maxGenerationRetries < 1)
        {
            Debug.LogError(
                "Max Generation Retries must be at least 1."
            );

            return false;
        }


        return true;
    }
}