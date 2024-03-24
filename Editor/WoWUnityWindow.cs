using System.IO;
using UnityEditor;
using UnityEngine;
using WowUnity;

public class WoWUnityWindow : EditorWindow
{
    private GameObject selectedAsset;

    [MenuItem("Window/wow.unity")]
    public static void ShowWindow()
    {
        GetWindow<WoWUnityWindow>("wow.unity");
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

        selectedAsset = EditorGUILayout.ObjectField("Select map tile:", selectedAsset, typeof(GameObject), false) as GameObject;

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

    void ProcessAssets()
    {
        AssetConversionManager.JobPostprocessAllAssets();
    }

    void SetupTerrain()
    {
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedAsset);
        if (!ADTUtility.IsAdtObj(path))
        {
            EditorUtility.DisplayDialog("Error", "Please select a valid map tile.", "Ok");
            return;
        }
        ADTUtility.PostProcessImport(path);
    }

    void PlaceDoodads()
    {
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedAsset);
        if (!ADTUtility.IsAdtAny(path))
        {
            EditorUtility.DisplayDialog("Error", "Please select a valid map tile.", "Ok");
            return;
        }

        SetupTerrain();
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
}
