using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class PuzzleAutoSetupWindow : EditorWindow
{
    // =========================================================
    // SCROLL
    // =========================================================

    private Vector2 scrollPosition;

    // =========================================================
    // PUZZLE CONTROLLER AUTO FILL
    // =========================================================

    private PuzzleTableController controller;
    private Transform solvedTargetsRoot;
    private Transform guideRoot;

    private PuzzleTableController.PuzzleDifficulty difficulty =
        PuzzleTableController.PuzzleDifficulty.Hard;

    private string itemPrefix = "HPP";

    // =========================================================
    // HARD WORLD PREFAB GENERATOR
    // =========================================================

    private GameObject templateWorldPrefab;
    private Transform hardMeshRoot;

    private const string HardWorldOutputFolder =
        "Assets/Prefabs/Items/World/Puzzle/Hard";

    // =========================================================
    // PUZZLE PIECE SPAWNER
    // =========================================================

    private PuzzlePieceSpawner puzzlePieceSpawner;

    // =========================================================
    // PLAYER INVENTORY
    // =========================================================

    private GameObject playerPrefab;

    // =========================================================
    // ICON GENERATOR
    // =========================================================

    private int iconSize = 256;

    private const string MediumIconFolder =
        "Assets/Prefabs/Items/World/Puzzle/Medium/Icons";

    private const string HardIconFolder =
        "Assets/Prefabs/Items/World/Puzzle/Hard/Icons";

    // =========================================================
    // OPEN WINDOW
    // =========================================================

    [MenuItem("Tools/Puzzle/Auto Setup Puzzle")]
    public static void ShowWindow()
    {
        GetWindow<PuzzleAutoSetupWindow>(
            "Puzzle Auto Setup"
        );
    }

    // =========================================================
    // WINDOW
    // =========================================================

    private void OnGUI()
    {
        scrollPosition =
            EditorGUILayout.BeginScrollView(
                scrollPosition
            );

        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Puzzle Auto Setup",
            EditorStyles.boldLabel
        );

        GUILayout.Space(10);

        // =====================================================
        // 1. HARD ITEM DATA
        // =====================================================

        EditorGUILayout.LabelField(
            "1. Hard ItemData Setup",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Configures HPP1-HPP24:\n" +
            "IDs = 3001-3024\n" +
            "Names = Hard Map Piece 01-24\n\n" +
            "Existing correct icons and HPP prefab references " +
            "are preserved.",
            MessageType.Info
        );

        if (GUILayout.Button(
                "CONFIGURE HARD ITEM DATA",
                GUILayout.Height(40)))
        {
            ConfigureHardItemData();
        }

        GUILayout.Space(20);

        // =====================================================
        // 2. PUZZLE CONTROLLER
        // =====================================================

        EditorGUILayout.LabelField(
            "2. Puzzle Controller Auto Fill",
            EditorStyles.boldLabel
        );

        controller =
            (PuzzleTableController)
            EditorGUILayout.ObjectField(
                "Puzzle Table Controller",
                controller,
                typeof(PuzzleTableController),
                true
            );

        difficulty =
            (PuzzleTableController.PuzzleDifficulty)
            EditorGUILayout.EnumPopup(
                "Difficulty",
                difficulty
            );

        solvedTargetsRoot =
            (Transform)
            EditorGUILayout.ObjectField(
                "Solved Targets Root",
                solvedTargetsRoot,
                typeof(Transform),
                true
            );

        guideRoot =
            (Transform)
            EditorGUILayout.ObjectField(
                "Guide Root",
                guideRoot,
                typeof(Transform),
                true
            );

        itemPrefix =
            EditorGUILayout.TextField(
                "Item Prefix",
                itemPrefix
            );

        GUILayout.Space(10);

        if (GUILayout.Button(
                "AUTO FILL PUZZLE PIECES",
                GUILayout.Height(40)))
        {
            AutoFillPuzzlePieces();
        }

        GUILayout.Space(25);

        // =====================================================
        // 3. HARD WORLD PREFABS
        // =====================================================

        EditorGUILayout.LabelField(
            "3. Hard World Prefab Generator",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Template World Prefab:\n" +
            "Drag MPP1_World from the PROJECT window.\n\n" +
            "Hard Mesh Root:\n" +
            "Drag HardMapReference from the HIERARCHY.\n\n" +
            "Creates HPP1_World to HPP24_World.",
            MessageType.Info
        );

        templateWorldPrefab =
            (GameObject)
            EditorGUILayout.ObjectField(
                "Template World Prefab",
                templateWorldPrefab,
                typeof(GameObject),
                false
            );

        hardMeshRoot =
            (Transform)
            EditorGUILayout.ObjectField(
                "Hard Mesh Root",
                hardMeshRoot,
                typeof(Transform),
                true
            );

        GUILayout.Space(10);

        if (GUILayout.Button(
                "CREATE HARD WORLD PREFABS",
                GUILayout.Height(45)))
        {
            GenerateHardWorldPrefabs();
        }

        GUILayout.Space(25);

        // =====================================================
        // 4. PUZZLE SPAWNER
        // =====================================================

        EditorGUILayout.LabelField(
            "4. Puzzle Piece Spawner Auto Fill",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Automatically fills PuzzlePieceSpawner:\n\n" +
            "Easy = PP1-PP8\n" +
            "Normal = MPP1-MPP16\n" +
            "Difficult = HPP1-HPP24",
            MessageType.Info
        );

        puzzlePieceSpawner =
            (PuzzlePieceSpawner)
            EditorGUILayout.ObjectField(
                "Puzzle Piece Spawner",
                puzzlePieceSpawner,
                typeof(PuzzlePieceSpawner),
                true
            );

        GUILayout.Space(10);

        if (GUILayout.Button(
                "AUTO FILL PUZZLE SPAWNER",
                GUILayout.Height(45)))
        {
            AutoFillPuzzleSpawner();
        }

        GUILayout.Space(25);

        // =====================================================
        // 5. PLAYER INVENTORY
        // =====================================================

        EditorGUILayout.LabelField(
            "5. Player Inventory Puzzle Catalog",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Adds every puzzle ItemData to PlayerInventory.itemCatalog:\n\n" +
            "Easy = PP1-PP8\n" +
            "Normal = MPP1-MPP16\n" +
            "Difficult = HPP1-HPP24\n\n" +
            "Existing inventory items are preserved.",
            MessageType.Info
        );

        playerPrefab =
            (GameObject)
            EditorGUILayout.ObjectField(
                "Player Prefab",
                playerPrefab,
                typeof(GameObject),
                false
            );

        GUILayout.Space(10);

        if (GUILayout.Button(
                "AUTO ADD PUZZLE ITEMS TO INVENTORY",
                GUILayout.Height(45)))
        {
            AutoAddPuzzleItemsToInventory();
        }

        GUILayout.Space(25);

        // =====================================================
        // 6. ICON GENERATOR
        // =====================================================

        EditorGUILayout.LabelField(
            "6. Medium + Hard Puzzle Icon Generator",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Automatically generates inventory icons from the " +
            "actual MPP and HPP world-piece visuals.\n\n" +
            "Medium = MPP1-MPP16\n" +
            "Hard = HPP1-HPP24\n\n" +
            "The generated PNG files are automatically imported " +
            "as Sprites and assigned to each ItemData.",
            MessageType.Info
        );

        iconSize =
            EditorGUILayout.IntSlider(
                "Icon Resolution",
                iconSize,
                128,
                512
            );

        GUILayout.Space(10);

        if (GUILayout.Button(
                "GENERATE MEDIUM + HARD ICONS",
                GUILayout.Height(50)))
        {
            GenerateAllPuzzleIcons();
        }

        GUILayout.Space(30);

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // 1. CONFIGURE HARD ITEM DATA
    // =========================================================

    private void ConfigureHardItemData()
    {
        int configuredCount = 0;

        for (int i = 1; i <= 24; i++)
        {
            string expectedName =
                $"HPP{i}";

            ItemData item =
                FindItemData(
                    expectedName
                );

            if (item == null)
            {
                Debug.LogWarning(
                    $"[PUZZLE AUTO SETUP] Missing {expectedName}."
                );

                continue;
            }

            Undo.RecordObject(
                item,
                $"Configure {expectedName}"
            );

            item.itemId =
                3000 + i;

            item.itemName =
                $"Hard Map Piece {i:00}";

            item.description =
                "A torn piece of the Fort Santiago map.";

            // Keep generated icons.
            // Do NOT clear item.icon here anymore.

            if (item.heldPrefab != null &&
                item.heldPrefab.name.StartsWith("MPP"))
            {
                item.heldPrefab = null;
            }

            if (item.worldPrefab != null &&
                item.worldPrefab.name.StartsWith("MPP"))
            {
                item.worldPrefab = null;
            }

            EditorUtility.SetDirty(
                item
            );

            configuredCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[PUZZLE AUTO SETUP] HARD ITEM DATA COMPLETE. " +
            $"{configuredCount}/24 configured."
        );
    }

    // =========================================================
    // 2. AUTO FILL PUZZLE CONTROLLER
    // =========================================================

    private void AutoFillPuzzlePieces()
    {
        if (controller == null)
        {
            Debug.LogError(
                "[PUZZLE AUTO SETUP] Puzzle Table Controller is missing."
            );

            return;
        }

        if (solvedTargetsRoot == null)
        {
            Debug.LogError(
                "[PUZZLE AUTO SETUP] Solved Targets Root is missing."
            );

            return;
        }

        if (guideRoot == null)
        {
            Debug.LogError(
                "[PUZZLE AUTO SETUP] Guide Root is missing."
            );

            return;
        }

        int pieceCount =
            solvedTargetsRoot.childCount;

        if (pieceCount <= 0)
        {
            Debug.LogError(
                "[PUZZLE AUTO SETUP] No solved target children found."
            );

            return;
        }

        if (pieceCount >
            PuzzleNetworkState.MaxPieces)
        {
            Debug.LogError(
                $"[PUZZLE AUTO SETUP] {pieceCount} pieces found, " +
                $"but PuzzleNetworkState supports only " +
                $"{PuzzleNetworkState.MaxPieces}."
            );

            return;
        }

        SerializedObject serializedController =
            new SerializedObject(
                controller
            );

        string arrayPropertyName =
            GetArrayPropertyName(
                difficulty
            );

        SerializedProperty piecesArray =
            serializedController.FindProperty(
                arrayPropertyName
            );

        if (piecesArray == null)
        {
            Debug.LogError(
                $"[PUZZLE AUTO SETUP] Could not find " +
                $"{arrayPropertyName}."
            );

            return;
        }

        Undo.RecordObject(
            controller,
            "Auto Setup Puzzle Pieces"
        );

        piecesArray.arraySize =
            pieceCount;

        int successCount =
            0;

        for (int i = 0;
             i < pieceCount;
             i++)
        {
            int pieceNumber =
                i + 1;

            string jigsawName =
                $"JIGSAW.{pieceNumber:000}";

            Transform solvedTarget =
                FindChildRecursive(
                    solvedTargetsRoot,
                    jigsawName
                );

            Transform guideTarget =
                FindChildRecursive(
                    guideRoot,
                    jigsawName
                );

            ItemData item =
                FindItemData(
                    $"{itemPrefix}{pieceNumber}"
                );

            SerializedProperty element =
                piecesArray
                    .GetArrayElementAtIndex(
                        i
                    );

            SerializedProperty itemProperty =
                element.FindPropertyRelative(
                    "item"
                );

            SerializedProperty solvedTargetProperty =
                element.FindPropertyRelative(
                    "solvedTarget"
                );

            SerializedProperty guideRendererProperty =
                element.FindPropertyRelative(
                    "guideRenderer"
                );

            if (itemProperty != null)
            {
                itemProperty.objectReferenceValue =
                    item;
            }

            if (solvedTargetProperty != null)
            {
                solvedTargetProperty.objectReferenceValue =
                    solvedTarget;
            }

            Renderer guideRenderer =
                null;

            if (guideTarget != null)
            {
                guideRenderer =
                    guideTarget.GetComponent<Renderer>();

                if (guideRenderer == null)
                {
                    guideRenderer =
                        guideTarget
                            .GetComponentInChildren<Renderer>(
                                true
                            );
                }
            }

            if (guideRendererProperty != null)
            {
                guideRendererProperty.objectReferenceValue =
                    guideRenderer;
            }

            bool valid =
                item != null &&
                solvedTarget != null &&
                guideRenderer != null;

            if (valid)
            {
                successCount++;
            }
            else
            {
                Debug.LogWarning(
                    $"[PUZZLE AUTO SETUP] Problem with " +
                    $"piece {pieceNumber}: " +
                    $"Item={item != null}, " +
                    $"Target={solvedTarget != null}, " +
                    $"Guide={guideRenderer != null}"
                );
            }
        }

        serializedController
            .ApplyModifiedProperties();

        EditorUtility.SetDirty(
            controller
        );

        if (controller.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                controller.gameObject.scene
            );
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[PUZZLE AUTO SETUP] {difficulty} complete. " +
            $"{successCount}/{pieceCount} pieces assigned."
        );
    }

    // =========================================================
    // 3. GENERATE HARD WORLD PREFABS
    // =========================================================

    private void GenerateHardWorldPrefabs()
    {
        if (templateWorldPrefab == null)
        {
            Debug.LogError(
                "[HARD WORLD GENERATOR] " +
                "Template World Prefab is missing."
            );

            return;
        }

        if (hardMeshRoot == null)
        {
            Debug.LogError(
                "[HARD WORLD GENERATOR] " +
                "Hard Mesh Root is missing."
            );

            return;
        }

        string templatePath =
            AssetDatabase.GetAssetPath(
                templateWorldPrefab
            );

        if (string.IsNullOrEmpty(
                templatePath))
        {
            Debug.LogError(
                "[HARD WORLD GENERATOR] " +
                "Template must come from the Project window."
            );

            return;
        }

        EnsureFolder(
            HardWorldOutputFolder
        );

        int successCount =
            0;

        for (int i = 1;
             i <= 24;
             i++)
        {
            string itemName =
                $"HPP{i}";

            string worldName =
                $"HPP{i}_World";

            string meshName =
                $"JIGSAW.{i:000}";

            ItemData itemData =
                FindItemData(
                    itemName
                );

            if (itemData == null)
            {
                Debug.LogWarning(
                    $"[HARD WORLD GENERATOR] Missing {itemName}."
                );

                continue;
            }

            Transform sourcePiece =
                FindChildRecursive(
                    hardMeshRoot,
                    meshName
                );

            if (sourcePiece == null)
            {
                Debug.LogWarning(
                    $"[HARD WORLD GENERATOR] Missing {meshName}."
                );

                continue;
            }

            MeshFilter sourceMeshFilter =
                sourcePiece.GetComponent<MeshFilter>();

            MeshRenderer sourceMeshRenderer =
                sourcePiece.GetComponent<MeshRenderer>();

            if (sourceMeshFilter == null ||
                sourceMeshFilter.sharedMesh == null)
            {
                Debug.LogWarning(
                    $"[HARD WORLD GENERATOR] " +
                    $"{meshName} has no valid MeshFilter."
                );

                continue;
            }

            string destinationPath =
                $"{HardWorldOutputFolder}/" +
                $"{worldName}.prefab";

            GameObject existing =
                AssetDatabase
                    .LoadAssetAtPath<GameObject>(
                        destinationPath
                    );

            if (existing == null)
            {
                bool copied =
                    AssetDatabase.CopyAsset(
                        templatePath,
                        destinationPath
                    );

                if (!copied)
                {
                    Debug.LogError(
                        $"[HARD WORLD GENERATOR] " +
                        $"Could not create {worldName}."
                    );

                    continue;
                }

                AssetDatabase.ImportAsset(
                    destinationPath
                );
            }

            GameObject prefabRoot =
                PrefabUtility.LoadPrefabContents(
                    destinationPath
                );

            if (prefabRoot == null)
            {
                Debug.LogError(
                    $"[HARD WORLD GENERATOR] " +
                    $"Could not open {worldName}."
                );

                continue;
            }

            prefabRoot.name =
                worldName;

            Transform visual =
                FindChildRecursive(
                    prefabRoot.transform,
                    "Visual"
                );

            if (visual == null)
            {
                Debug.LogError(
                    $"[HARD WORLD GENERATOR] " +
                    $"{worldName} has no Visual child."
                );

                PrefabUtility.UnloadPrefabContents(
                    prefabRoot
                );

                continue;
            }

            // Mesh Filter
            MeshFilter targetMeshFilter =
                visual.GetComponent<MeshFilter>();

            if (targetMeshFilter == null)
            {
                targetMeshFilter =
                    visual.gameObject
                        .AddComponent<MeshFilter>();
            }

            targetMeshFilter.sharedMesh =
                sourceMeshFilter.sharedMesh;

            // Renderer
            MeshRenderer targetRenderer =
                visual.GetComponent<MeshRenderer>();

            if (targetRenderer == null)
            {
                targetRenderer =
                    visual.gameObject
                        .AddComponent<MeshRenderer>();
            }

            if (sourceMeshRenderer != null)
            {
                targetRenderer.sharedMaterials =
                    sourceMeshRenderer.sharedMaterials;
            }

            // Collider
            MeshCollider meshCollider =
                visual.GetComponent<MeshCollider>();

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh =
                    sourceMeshFilter.sharedMesh;
            }
            else
            {
                BoxCollider boxCollider =
                    visual.GetComponent<BoxCollider>();

                if (boxCollider == null)
                {
                    meshCollider =
                        visual.gameObject
                            .AddComponent<MeshCollider>();

                    meshCollider.sharedMesh =
                        sourceMeshFilter.sharedMesh;
                }
            }

            // ItemPickup
            ItemPickup pickup =
                prefabRoot.GetComponent<ItemPickup>();

            if (pickup != null)
            {
                SetItemDataOnPickup(
                    pickup,
                    itemData
                );
            }

            PrefabUtility.SaveAsPrefabAsset(
                prefabRoot,
                destinationPath
            );

            PrefabUtility.UnloadPrefabContents(
                prefabRoot
            );

            AssetDatabase.ImportAsset(
                destinationPath
            );

            GameObject createdPrefab =
                AssetDatabase
                    .LoadAssetAtPath<GameObject>(
                        destinationPath
                    );

            if (createdPrefab == null)
                continue;

            NetworkObject networkObject =
                createdPrefab.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogError(
                    $"[HARD WORLD GENERATOR] " +
                    $"{worldName} has no NetworkObject."
                );

                continue;
            }

            Undo.RecordObject(
                itemData,
                $"Assign {worldName}"
            );

            itemData.heldPrefab =
                createdPrefab;

            itemData.worldPrefab =
                networkObject;

            EditorUtility.SetDirty(
                itemData
            );

            successCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[HARD WORLD GENERATOR] COMPLETE. " +
            $"{successCount}/24 configured."
        );
    }

    // =========================================================
    // 4. AUTO FILL PUZZLE SPAWNER
    // =========================================================

    private void AutoFillPuzzleSpawner()
    {
        if (puzzlePieceSpawner == null)
        {
            Debug.LogError(
                "[PUZZLE SPAWNER AUTO SETUP] " +
                "Puzzle Piece Spawner is missing."
            );

            return;
        }

        SerializedObject serializedSpawner =
            new SerializedObject(
                puzzlePieceSpawner
            );

        SerializedProperty puzzleSets =
            serializedSpawner.FindProperty(
                "puzzleSets"
            );

        if (puzzleSets == null)
        {
            Debug.LogError(
                "[PUZZLE SPAWNER AUTO SETUP] " +
                "Could not find 'puzzleSets'."
            );

            return;
        }

        Undo.RecordObject(
            puzzlePieceSpawner,
            "Auto Fill Puzzle Piece Spawner"
        );

        puzzleSets.arraySize = 3;

        int easyCount =
            ConfigureSpawnerSet(
                puzzleSets.GetArrayElementAtIndex(0),
                "Easy",
                "PP",
                8
            );

        int mediumCount =
            ConfigureSpawnerSet(
                puzzleSets.GetArrayElementAtIndex(1),
                "Normal",
                "MPP",
                16
            );

        int hardCount =
            ConfigureSpawnerSet(
                puzzleSets.GetArrayElementAtIndex(2),
                "Difficult",
                "HPP",
                24
            );

        serializedSpawner
            .ApplyModifiedProperties();

        EditorUtility.SetDirty(
            puzzlePieceSpawner
        );

        if (puzzlePieceSpawner.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                puzzlePieceSpawner.gameObject.scene
            );
        }

        Debug.Log(
            "[PUZZLE SPAWNER AUTO SETUP] COMPLETE. " +
            $"Easy {easyCount}/8, " +
            $"Normal {mediumCount}/16, " +
            $"Difficult {hardCount}/24."
        );
    }

    private int ConfigureSpawnerSet(
        SerializedProperty setProperty,
        string difficultyName,
        string prefix,
        int pieceCount)
    {
        if (setProperty == null)
            return 0;

        SerializedProperty difficultyProperty =
            setProperty.FindPropertyRelative(
                "difficulty"
            );

        SerializedProperty piecesProperty =
            setProperty.FindPropertyRelative(
                "pieces"
            );

        if (difficultyProperty == null ||
            piecesProperty == null)
        {
            Debug.LogError(
                $"[PUZZLE SPAWNER AUTO SETUP] " +
                $"Invalid set: {difficultyName}."
            );

            return 0;
        }

        SetEnumValueByName(
            difficultyProperty,
            difficultyName
        );

        piecesProperty.arraySize =
            pieceCount;

        int assigned = 0;

        for (int i = 0;
             i < pieceCount;
             i++)
        {
            ItemData item =
                FindItemData(
                    $"{prefix}{i + 1}"
                );

            SerializedProperty slot =
                piecesProperty
                    .GetArrayElementAtIndex(
                        i
                    );

            slot.objectReferenceValue =
                item;

            if (item != null)
                assigned++;
        }

        return assigned;
    }

    // =========================================================
    // 5. PLAYER INVENTORY PUZZLE CATALOG
    // =========================================================

    private void AutoAddPuzzleItemsToInventory()
    {
        if (playerPrefab == null)
        {
            Debug.LogError(
                "[INVENTORY AUTO SETUP] Player Prefab is missing."
            );

            return;
        }

        string prefabPath =
            AssetDatabase.GetAssetPath(
                playerPrefab
            );

        if (string.IsNullOrEmpty(prefabPath))
        {
            Debug.LogError(
                "[INVENTORY AUTO SETUP] " +
                "Drag PlayerRoot from the PROJECT window."
            );

            return;
        }

        GameObject prefabRoot =
            PrefabUtility.LoadPrefabContents(
                prefabPath
            );

        if (prefabRoot == null)
            return;

        PlayerInventory inventory =
            prefabRoot.GetComponent<PlayerInventory>();

        if (inventory == null)
        {
            inventory =
                prefabRoot.GetComponentInChildren<PlayerInventory>(
                    true
                );
        }

        if (inventory == null)
        {
            Debug.LogError(
                "[INVENTORY AUTO SETUP] " +
                "PlayerInventory was not found."
            );

            PrefabUtility.UnloadPrefabContents(
                prefabRoot
            );

            return;
        }

        SerializedObject serializedInventory =
            new SerializedObject(
                inventory
            );

        SerializedProperty catalog =
            serializedInventory.FindProperty(
                "itemCatalog"
            );

        if (catalog == null)
        {
            Debug.LogError(
                "[INVENTORY AUTO SETUP] " +
                "Could not find itemCatalog."
            );

            PrefabUtility.UnloadPrefabContents(
                prefabRoot
            );

            return;
        }

        List<ItemData> items =
            new List<ItemData>();

        for (int i = 0;
             i < catalog.arraySize;
             i++)
        {
            ItemData existing =
                catalog
                    .GetArrayElementAtIndex(i)
                    .objectReferenceValue
                as ItemData;

            if (existing != null &&
                !items.Contains(existing))
            {
                items.Add(
                    existing
                );
            }
        }

        int originalCount =
            items.Count;

        int easyAdded =
            AddPuzzleRangeToCatalog(
                items,
                "PP",
                8
            );

        int mediumAdded =
            AddPuzzleRangeToCatalog(
                items,
                "MPP",
                16
            );

        int hardAdded =
            AddPuzzleRangeToCatalog(
                items,
                "HPP",
                24
            );

        catalog.arraySize =
            items.Count;

        for (int i = 0;
             i < items.Count;
             i++)
        {
            catalog
                .GetArrayElementAtIndex(i)
                .objectReferenceValue =
                items[i];
        }

        serializedInventory
            .ApplyModifiedProperties();

        EditorUtility.SetDirty(
            inventory
        );

        PrefabUtility.SaveAsPrefabAsset(
            prefabRoot,
            prefabPath
        );

        PrefabUtility.UnloadPrefabContents(
            prefabRoot
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int totalAdded =
            easyAdded +
            mediumAdded +
            hardAdded;

        Debug.Log(
            "[INVENTORY AUTO SETUP] COMPLETE. " +
            $"Original Catalog: {originalCount}, " +
            $"Easy Added: {easyAdded}, " +
            $"Normal Added: {mediumAdded}, " +
            $"Difficult Added: {hardAdded}, " +
            $"Total Added: {totalAdded}, " +
            $"Final Catalog Size: {items.Count}."
        );
    }

    private int AddPuzzleRangeToCatalog(
        List<ItemData> catalog,
        string prefix,
        int count)
    {
        int added = 0;

        for (int i = 1;
             i <= count;
             i++)
        {
            ItemData item =
                FindItemData(
                    $"{prefix}{i}"
                );

            if (item == null)
                continue;

            bool alreadyExists =
                false;

            foreach (ItemData existing
                     in catalog)
            {
                if (existing == null)
                    continue;

                if (existing == item ||
                    existing.itemId == item.itemId)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (alreadyExists)
                continue;

            catalog.Add(
                item
            );

            added++;
        }

        return added;
    }

    // =========================================================
    // 6. MEDIUM + HARD ICON GENERATOR
    // =========================================================

    private void GenerateAllPuzzleIcons()
    {
        EnsureFolder(
            MediumIconFolder
        );

        EnsureFolder(
            HardIconFolder
        );

        int mediumCreated =
            GeneratePuzzleIconRange(
                "MPP",
                16,
                MediumIconFolder
            );

        int hardCreated =
            GeneratePuzzleIconRange(
                "HPP",
                24,
                HardIconFolder
            );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[PUZZLE ICON GENERATOR] COMPLETE. " +
            $"Medium {mediumCreated}/16, " +
            $"Hard {hardCreated}/24."
        );
    }

    private int GeneratePuzzleIconRange(
        string prefix,
        int count,
        string outputFolder)
    {
        int successCount = 0;

        for (int i = 1;
             i <= count;
             i++)
        {
            string itemName =
                $"{prefix}{i}";

            ItemData item =
                FindItemData(
                    itemName
                );

            if (item == null)
            {
                Debug.LogWarning(
                    $"[PUZZLE ICON GENERATOR] " +
                    $"Missing {itemName}."
                );

                continue;
            }

            GameObject worldPrefab =
                GetWorldPrefabGameObject(
                    item
                );

            if (worldPrefab == null)
            {
                Debug.LogWarning(
                    $"[PUZZLE ICON GENERATOR] " +
                    $"{itemName} has no world prefab."
                );

                continue;
            }

            GameObject sourceVisual =
                FindVisualGameObject(
                    worldPrefab
                );

            if (sourceVisual == null)
            {
                Debug.LogWarning(
                    $"[PUZZLE ICON GENERATOR] " +
                    $"{worldPrefab.name} has no Visual object."
                );

                continue;
            }

            Texture2D renderedIcon =
                RenderPrefabIcon(
                    sourceVisual,
                    iconSize
                );

            if (renderedIcon == null)
            {
                Debug.LogWarning(
                    $"[PUZZLE ICON GENERATOR] " +
                    $"Failed to render {itemName}."
                );

                continue;
            }

            string pngPath =
                $"{outputFolder}/{itemName}_Icon.png";

            byte[] pngBytes =
                renderedIcon.EncodeToPNG();

            File.WriteAllBytes(
                pngPath,
                pngBytes
            );

            DestroyImmediate(
                renderedIcon
            );

            AssetDatabase.ImportAsset(
                pngPath,
                ImportAssetOptions.ForceUpdate
            );

            TextureImporter importer =
                AssetImporter.GetAtPath(
                    pngPath
                )
                as TextureImporter;

            if (importer == null)
            {
                Debug.LogWarning(
                    $"[PUZZLE ICON GENERATOR] " +
                    $"Could not configure {pngPath}."
                );

                continue;
            }

            importer.textureType =
                TextureImporterType.Sprite;

            importer.spriteImportMode =
                SpriteImportMode.Single;

            importer.alphaIsTransparency =
                true;

            importer.mipmapEnabled =
                false;

            importer.wrapMode =
                TextureWrapMode.Clamp;

            importer.filterMode =
                FilterMode.Bilinear;

            importer.textureCompression =
                TextureImporterCompression.Uncompressed;

            importer.SaveAndReimport();

            Sprite iconSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    pngPath
                );

            if (iconSprite == null)
            {
                Debug.LogWarning(
                    $"[PUZZLE ICON GENERATOR] " +
                    $"Sprite import failed for {itemName}."
                );

                continue;
            }

            Undo.RecordObject(
                item,
                $"Assign {itemName} Icon"
            );

            item.icon =
                iconSprite;

            EditorUtility.SetDirty(
                item
            );

            successCount++;

            Debug.Log(
                $"[PUZZLE ICON GENERATOR] " +
                $"{itemName} → {pngPath}"
            );
        }

        return successCount;
    }

    // =========================================================
    // RENDER ICON
    // =========================================================

    private Texture2D RenderPrefabIcon(
        GameObject sourceVisual,
        int resolution)
    {
        PreviewRenderUtility preview =
            new PreviewRenderUtility();

        GameObject previewObject =
            null;

        try
        {
            previewObject =
                Instantiate(
                    sourceVisual
                );

            previewObject.name =
                "PuzzleIconPreview";

            preview.AddSingleGO(
                previewObject
            );

            Renderer[] renderers =
                previewObject.GetComponentsInChildren<Renderer>(
                    true
                );

            if (renderers == null ||
                renderers.Length == 0)
            {
                preview.Cleanup();

                return null;
            }

            bool hasBounds =
                false;

            Bounds bounds =
                new Bounds();

            foreach (Renderer renderer
                     in renderers)
            {
                if (renderer == null ||
                    !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds =
                        renderer.bounds;

                    hasBounds =
                        true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds
                    );
                }
            }

            if (!hasBounds)
            {
                preview.Cleanup();

                return null;
            }

            Camera camera =
                preview.camera;

            camera.clearFlags =
                CameraClearFlags.SolidColor;

            camera.backgroundColor =
                new Color(
                    0f,
                    0f,
                    0f,
                    0f
                );

            camera.orthographic =
                true;

            camera.nearClipPlane =
                0.01f;

            camera.farClipPlane =
                1000f;

            // Puzzle pieces lie flat in the world prefabs,
            // so render them from directly above.
            float width =
                Mathf.Max(
                    0.01f,
                    bounds.extents.x
                );

            float depth =
                Mathf.Max(
                    0.01f,
                    bounds.extents.z
                );

            float size =
                Mathf.Max(
                    width,
                    depth
                );

            camera.orthographicSize =
                size * 1.20f;

            float cameraHeight =
                Mathf.Max(
                    2f,
                    bounds.size.magnitude * 2f
                );

            camera.transform.position =
                bounds.center +
                Vector3.up *
                cameraHeight;

            camera.transform.rotation =
                Quaternion.LookRotation(
                    Vector3.down,
                    Vector3.forward
                );

            // Preview lighting.
            preview.lights[0].intensity =
                1.4f;

            preview.lights[0].transform.rotation =
                Quaternion.Euler(
                    50f,
                    30f,
                    0f
                );

            preview.lights[1].intensity =
                0.8f;

            preview.lights[1].transform.rotation =
                Quaternion.Euler(
                    340f,
                    218f,
                    177f
                );

            Rect previewRect =
                new Rect(
                    0,
                    0,
                    resolution,
                    resolution
                );

            preview.BeginStaticPreview(
                previewRect
            );

            // true = allow SRP/URP rendering.
            preview.Render(
                true
            );

            Texture2D result =
                preview.EndStaticPreview();

            preview.Cleanup();

            return result;
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "[PUZZLE ICON GENERATOR] " +
                $"Render failed: {exception.Message}"
            );

            preview.Cleanup();

            return null;
        }
    }

    // =========================================================
    // WORLD PREFAB / VISUAL
    // =========================================================

    private GameObject GetWorldPrefabGameObject(
        ItemData item)
    {
        if (item == null)
            return null;

        if (item.heldPrefab != null)
        {
            return item.heldPrefab;
        }

        if (item.worldPrefab != null)
        {
            return item.worldPrefab.gameObject;
        }

        return null;
    }

    private GameObject FindVisualGameObject(
        GameObject prefab)
    {
        if (prefab == null)
            return null;

        Transform visual =
            FindChildRecursive(
                prefab.transform,
                "Visual"
            );

        if (visual != null)
            return visual.gameObject;

        // Fallback:
        // If there is no Visual child, use the prefab itself
        // when it contains renderers.
        Renderer renderer =
            prefab.GetComponentInChildren<Renderer>(
                true
            );

        if (renderer != null)
            return prefab;

        return null;
    }

    // =========================================================
    // ITEM PICKUP SERIALIZED FIELD
    // =========================================================

    private void SetItemDataOnPickup(
        ItemPickup pickup,
        ItemData itemData)
    {
        SerializedObject serializedPickup =
            new SerializedObject(
                pickup
            );

        SerializedProperty itemProperty =
            serializedPickup.FindProperty(
                "itemData"
            );

        if (itemProperty == null)
        {
            SerializedProperty iterator =
                serializedPickup.GetIterator();

            bool enterChildren =
                true;

            while (iterator.NextVisible(
                enterChildren))
            {
                enterChildren = false;

                if (iterator.propertyType !=
                    SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                if (iterator.displayName ==
                    "Item Data")
                {
                    itemProperty =
                        iterator.Copy();

                    break;
                }
            }
        }

        if (itemProperty == null)
        {
            Debug.LogWarning(
                $"[HARD WORLD GENERATOR] " +
                $"Could not find Item Data field on " +
                $"{pickup.name}."
            );

            return;
        }

        itemProperty.objectReferenceValue =
            itemData;

        serializedPickup
            .ApplyModifiedProperties();
    }

    // =========================================================
    // PUZZLE ARRAY PROPERTY
    // =========================================================

    private string GetArrayPropertyName(
        PuzzleTableController.PuzzleDifficulty targetDifficulty)
    {
        switch (targetDifficulty)
        {
            case PuzzleTableController
                .PuzzleDifficulty.Medium:

                return "mediumPieces";

            case PuzzleTableController
                .PuzzleDifficulty.Hard:

                return "hardPieces";

            default:

                return "easyPieces";
        }
    }

    // =========================================================
    // ENUM SETTER
    // =========================================================

    private bool SetEnumValueByName(
        SerializedProperty enumProperty,
        string targetName)
    {
        if (enumProperty == null ||
            enumProperty.propertyType !=
            SerializedPropertyType.Enum)
        {
            return false;
        }

        string[] enumNames =
            enumProperty.enumNames;

        for (int i = 0;
             i < enumNames.Length;
             i++)
        {
            if (enumNames[i] ==
                targetName)
            {
                enumProperty.enumValueIndex =
                    i;

                return true;
            }
        }

        Debug.LogWarning(
            $"[PUZZLE AUTO SETUP] " +
            $"Enum value '{targetName}' was not found."
        );

        return false;
    }

    // =========================================================
    // FIND ITEM DATA
    // =========================================================

    private ItemData FindItemData(
        string exactName)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                $"{exactName} t:ItemData"
            );

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid
                );

            ItemData item =
                AssetDatabase
                    .LoadAssetAtPath<ItemData>(
                        path
                    );

            if (item != null &&
                item.name == exactName)
            {
                return item;
            }
        }

        return null;
    }

    // =========================================================
    // FIND CHILD
    // =========================================================

    private Transform FindChildRecursive(
        Transform root,
        string exactName)
    {
        if (root == null)
            return null;

        if (root.name ==
            exactName)
        {
            return root;
        }

        foreach (Transform child
                 in root)
        {
            Transform found =
                FindChildRecursive(
                    child,
                    exactName
                );

            if (found != null)
                return found;
        }

        return null;
    }

    // =========================================================
    // FOLDER CREATION
    // =========================================================

    private void EnsureFolder(
        string folderPath)
    {
        if (AssetDatabase.IsValidFolder(
                folderPath))
        {
            return;
        }

        int lastSlash =
            folderPath.LastIndexOf('/');

        if (lastSlash <= 0)
            return;

        string parent =
            folderPath.Substring(
                0,
                lastSlash
            );

        string folderName =
            folderPath.Substring(
                lastSlash + 1
            );

        if (!AssetDatabase.IsValidFolder(
                parent))
        {
            EnsureFolder(
                parent
            );
        }

        AssetDatabase.CreateFolder(
            parent,
            folderName
        );
    }
}