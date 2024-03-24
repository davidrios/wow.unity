using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        Selection.selectionChanged += OnSelectionChange;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChange;
    }

    void OnSelectionChange()
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

        foreach (var selectedAsset in selectedAssets.Values)
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedAsset);

            GameObject prefab = M2Utility.FindPrefab(Path.ChangeExtension(path, "prefab"));

            TextAsset placementData = AssetDatabase.LoadAssetAtPath<TextAsset>(Path.ChangeExtension(path, "obj").Replace(".obj", "_ModelPlacementInformation.csv"));
            if (placementData == null)
            {
                EditorUtility.DisplayDialog("Error", "ModelPlacementInformation.csv not found.", "Ok");
                return;
            }

            Debug.Log("Placing Doodads...");
            ItemCollectionUtility.PlaceModels(prefab, placementData);
        }

        Debug.Log("Done placing doodads.");
    }
}
