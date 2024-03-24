using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WowUnity;

public class WoWUnityWindow : EditorWindow
{
    private Dictionary<string, GameObject> selectedAssets;

    [MenuItem("Window/wow.unity")]
    public static void ShowWindow()
    {
        GetWindow<WoWUnityWindow>("wow.unity");
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChangeForMap;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChangeForMap;
    }

    void OnSelectionChangeForMap()
    {
        selectedAssets = new();
        foreach (var obj in Selection.objects)
        {
            if (obj.GetType() != typeof(GameObject))
            {
                continue;
            }

            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
            if (!ADTUtility.IsAdtAny(path))
            {
                continue;
            }

            selectedAssets[obj.name] = obj as GameObject;
        }
        Repaint();
    }

    private void OnGUI()
    {
        GUILayout.Label("All Assets", EditorStyles.boldLabel);
        if (GUILayout.Button("Process"))
        {
            ProcessAssets();
        }

        GUILayout.Space(10);

        GUILayout.Label("Map", EditorStyles.boldLabel);

        if (selectedAssets == null || selectedAssets.Count == 0)
        {
            GUILayout.Label("No tiles selected. Select some in the project window.");
        } else
        {
            GUILayout.Label("Selected tiles:");

            foreach (var asset in selectedAssets.Values)
            {
                EditorGUILayout.ObjectField(asset, typeof(GameObject), false);
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Setup Terrain"))
            {
                SetupTerrain();
            }

            if (GUILayout.Button("Place Doodads"))
            {
                PlaceDoodads();
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        GUILayout.Label("Double-sided util", EditorStyles.boldLabel);
        if (GUILayout.Button("Create for selected objects"))
        {
            CreateDoubleSided();
        }
    }

    void ProcessAssets()
    {
        AssetConversionManager.JobPostprocessAllAssets();
    }

    void SetupTerrain()
    {
        foreach (var selectedAsset in selectedAssets.Values)
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedAsset);
            ADTUtility.PostProcessImport(path);
        }

        Debug.Log("Done setting up terrain.");
    }

    void PlaceDoodads()
    {
        SetupTerrain();

        if (AssetConversionManager.HasQueue())
        {
            AssetConversionManager.JobPostprocessAllAssets();
        }

        foreach (var selectedAsset in selectedAssets.Values)
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedAsset);

            GameObject prefab = M2Utility.FindPrefab(Path.ChangeExtension(path, "prefab"));

            TextAsset placementData = AssetDatabase.LoadAssetAtPath<TextAsset>(Path.ChangeExtension(path, "obj").Replace(".obj", "_ModelPlacementInformation.csv"));
            if (placementData == null)
            {
                Debug.LogWarning($"{path}: ModelPlacementInformation.csv not found.");
                continue;
            }

            Debug.Log("Placing Doodads...");
            ItemCollectionUtility.PlaceModels(prefab, placementData);
        }

        Debug.Log("Done placing doodads.");
    }

    void CreateDoubleSided()
    {
        GameObject parent = null;
        HashSet<string> doubleSidedList = new();
        foreach (var selected in Selection.gameObjects)
        {
            if (!selected.GetComponent<MeshFilter>())
            {
                Debug.LogWarning("Invalid object selected.");
                return;
            }

            if (parent == null)
            {
                parent = selected.transform.parent.gameObject;
            } else
            {
                if (parent != selected.transform.parent.gameObject)
                {
                    Debug.LogWarning("Selected objects must be children of the same parent.");
                    return;
                }
            }

            doubleSidedList.Add(selected.name);
        }

        List<string> nonDoubleSided = new();
        for (var i = 0; i < parent.transform.childCount; i++)
        {
            var child = parent.transform.GetChild(i);
            if (!doubleSidedList.Contains(child.name))
            {
                nonDoubleSided.Add(child.name);
            }
        }

        var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(parent);
        if (path == null || !path.EndsWith(".obj"))
        {
            Debug.LogWarning("Invalid object selected.");
            return;
        }

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        M2Utility.ExportDoubleSided(path, asset, nonDoubleSided);

        Debug.Log($"{path}: exported double sided.");

        var invPath = path.Replace(".obj", "_invn.obj");
        var newAsset = AssetImporter.GetAtPath(invPath);
        if (newAsset == null)
        {
            AssetDatabase.ImportAsset(invPath);
        }
        else {
            newAsset.SaveAndReimport();
        }
    }
}
