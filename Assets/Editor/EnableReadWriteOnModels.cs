using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EnableReadWriteOnModels
{
    [MenuItem("Tools/Mesh/Enable Read-Write In Selected Folders")]
    private static void Run()
    {
        List<string> folders = new List<string>();

        foreach (Object obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(obj);

            if (AssetDatabase.IsValidFolder(path))
                folders.Add(path);
        }

        if (folders.Count == 0)
        {
            Debug.LogWarning("Pumili muna ng folder sa Project window.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Model", folders.ToArray());

        int changed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null || importer.isReadable)
                continue;

            importer.isReadable = true;
            importer.SaveAndReimport();
            changed++;
        }

        Debug.Log($"Read/Write enabled on {changed} model(s).");
    }
}