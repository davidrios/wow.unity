using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WowUnity;

public class WoWUnityWindow : EditorWindow
{
    private Dictionary<string, GameObject> selectedMapTiles;
    private GameObject selectedForDoodads;
    private TextAsset modelPlacementInfo;

    [MenuItem("Window/wow.unity")]
    public static void ShowWindow()
    {
        GetWindow<WoWUnityWindow>("wow.unity");
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChangeForMap;
        Selection.selectionChanged += OnSelectionChangeForDoodadPlacement;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChangeForMap;
        Selection.selectionChanged -= OnSelectionChangeForDoodadPlacement;
    }

    void OnSelectionChangeForMap()
    {
        selectedMapTiles = new();
        foreach (var obj in Selection.objects)
        {
            if (obj.GetType() != typeof(GameObject))
                continue;

            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
            if (!ADTUtility.IsAdtAny(path))
                continue;

            selectedMapTiles[obj.name] = obj as GameObject;
        }
        Repaint();
    }

    void OnSelectionChangeForDoodadPlacement()
    {
        var selected = Selection.GetFiltered<GameObject>(SelectionMode.Editable | SelectionMode.ExcludePrefab);
        if (selected.Length > 0)
            selectedForDoodads = selected[0];
        else
            selectedForDoodads = null;

        Repaint();
    }

    private void OnGUI()
    {
        var settings = Settings.GetSettings();

        GUILayout.Label("Settings", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        try
        {
            if (GUILayout.Button("Open Editor Settings"))
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            }

            if (GUILayout.Button("Open Runtime Settings"))
            {
                var rsettings = RuntimeSettings.GetSettings();
                Selection.activeObject = rsettings;
                EditorGUIUtility.PingObject(rsettings);
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            }
        }
        finally
        {
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        GUILayout.Label("All Assets", EditorStyles.boldLabel);
        if (GUILayout.Button($"Process ({settings.renderingPipeline})"))
            ProcessAssets();

        GUILayout.Space(10);

        GUILayout.Label("Map", EditorStyles.boldLabel);

        if (selectedMapTiles == null || selectedMapTiles.Count == 0)
        {
            GUILayout.Label("No tiles selected. Select some in the project window.");
        }
        else
        {
            GUILayout.Label("Selected tiles:");

            foreach (var asset in selectedMapTiles.Values)
            {
                EditorGUILayout.ObjectField(asset, typeof(GameObject), false);
            }

            GUILayout.BeginHorizontal();

            try
            {
                if (GUILayout.Button($"Setup Terrain ({settings.renderingPipeline})"))
                    SetupTerrain();

                if (GUILayout.Button("Place Doodads"))
                    PlaceDoodads();
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();

            try
            {
                if (GUILayout.Button("Setup Foliage Spawner"))
                    SetupFoliage();
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        GUILayout.Space(10);

        GUILayout.Label("Manual doodad placement", EditorStyles.boldLabel);
        modelPlacementInfo = EditorGUILayout.ObjectField("Placement CSV: ", modelPlacementInfo, typeof(TextAsset), false) as TextAsset;
        if (modelPlacementInfo != null)
        {
            if (Path.GetExtension(AssetDatabase.GetAssetPath(modelPlacementInfo)).ToLower() != ".csv")
                modelPlacementInfo = null;
        }

        if (selectedForDoodads == null)
        {
            GUILayout.Label("Select a game object to place the doodads in.");
        }
        else
        {
            GUILayout.Label($"Place in game object: {selectedForDoodads.name}");

            if (modelPlacementInfo != null)
            {
                GUILayout.BeginHorizontal();
                try
                {

                    if (GUILayout.Button("Place M2s"))
                        PlaceM2OnSelected();

                    if (GUILayout.Button("Place WMOs"))
                        PlaceWMOOnSelected();
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }
            }
        }

        if (Settings.GetSettings().renderingPipeline == RenderingPipeline.BiRP)
        {
            GUILayout.Space(10);

            GUILayout.Label("Double-sided util", EditorStyles.boldLabel);
            if (GUILayout.Button("Create for selected"))
                CreateDoubleSided();
        }

        if (GUILayout.Button("Test respawn foliage"))
            FoliageSpawner.RespawnAll();
    }

    void ProcessAssets()
    {
        AssetConversionManager.JobPostprocessAllAssets();
    }

    List<string> GetRootAdtPaths()
    {
        var paths = new List<string>();
        var pathsH = new HashSet<string>();

        foreach (var selectedAsset in selectedMapTiles.Values)
        {
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedAsset);
            if (pathsH.Contains(path))
                continue;

            pathsH.Add(path);
            paths.Add(path);
        }

        return paths;
    }

    void SetupTerrain()
    {
        ADTUtility.PostProcessImports(GetRootAdtPaths());
        Debug.Log("Done setting up terrain.");
    }

    void PlaceDoodads()
    {
        ProcessAssets();
        SetupTerrain();

        foreach (var path in GetRootAdtPaths())
        {
            var prefab = M2Utility.FindPrefab(Path.ChangeExtension(path, "prefab"));

            var placementData = AssetDatabase.LoadAssetAtPath<TextAsset>(Path.ChangeExtension(path, "obj").Replace(".obj", "_ModelPlacementInformation.csv"));
            if (placementData == null)
            {
                Debug.LogWarning($"{path}: ModelPlacementInformation.csv not found.");
                continue;
            }

            Debug.Log($"{path}: placing doodads...");
            ItemCollectionUtility.PlaceModels(prefab, placementData);
        }

        Debug.Log("Done placing doodads.");
    }

    public void SetupFoliage()
    {
        SetupTerrain();
        AssetConversionManager.JobPostprocessAllAssets();

        foreach (var selectedAsset in selectedMapTiles.Values)
        {
            if (selectedAsset.name.StartsWith("adt_"))
            {
                var adtRoot = selectedAsset.transform;

                var parentTransform = selectedAsset.transform.parent;
                if (parentTransform.name == adtRoot.name)
                    adtRoot = parentTransform;

                var chunksContainer = adtRoot.Find(adtRoot.name);
                for (var i = 0; i < chunksContainer.childCount; i++)
                {
                    ADTUtility.PlaceFoliageSpawner(chunksContainer.GetChild(i).gameObject);
                }
            }
            else
            {
                ADTUtility.PlaceFoliageSpawner(selectedAsset);
            }

        }
    }

    void PlaceM2OnSelected()
    {
        ItemCollectionUtility.ParseFileAndSpawnDoodads(selectedForDoodads.transform, modelPlacementInfo, "m2");
    }

    void PlaceWMOOnSelected()
    {
        ItemCollectionUtility.ParseFileAndSpawnDoodads(selectedForDoodads.transform, modelPlacementInfo, "wmo");
    }

    void CreateDoubleSided()
    {
        GameObject parent = null;
        var doubleSidedList = new HashSet<string>();
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
            }
            else
            {
                if (parent != selected.transform.parent.gameObject)
                {
                    Debug.LogWarning("Selected objects must be children of the same parent.");
                    return;
                }
            }

            doubleSidedList.Add(selected.name);
        }

        var nonDoubleSided = new List<string>();
        for (var i = 0; i < parent.transform.childCount; i++)
        {
            var child = parent.transform.GetChild(i);
            if (!doubleSidedList.Contains(child.name))
                nonDoubleSided.Add(child.name);
        }

        var dinst = Instantiate(parent);
        try
        {
            foreach (var name in nonDoubleSided)
            {
                var nonDouble = dinst.transform.Find(name);
                if (nonDouble != null)
                    DestroyImmediate(nonDouble.gameObject);
            }

            var renderers = new List<Renderer>();
            foreach (var meshFilter in dinst.GetComponentsInChildren<MeshFilter>())
            {
                meshFilter.sharedMesh = M2Utility.DuplicateAndReverseMesh(meshFilter.sharedMesh);
                renderers.Add(meshFilter.gameObject.GetComponent<Renderer>());
            }

            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(parent);
            if (path == null || path.Length == 0)
            {
                var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (currentPrefabStage == null)
                {
                    Debug.LogWarning("couldn't get the prefab");
                    return;
                }

                path = currentPrefabStage.assetPath;
            }
            var invPath = path.Replace(Path.GetExtension(path), M2Utility.DOUBLE_SIDED_INVERSE_SUFFIX);
            ObjExporter.ExportObj(dinst, invPath);

            AssetDatabase.ImportAsset(invPath);

            M2Utility.SetupDoubleSided(parent.transform.parent.gameObject, invPath);
        }
        finally
        {
            DestroyImmediate(dinst);
        }
    }
}
