using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    class AssetConversionManager
    {
        private static ConcurrentQueue<string> importedModelPathQueue = new();
        private static bool isBusy = false;

        public static void QueuePostprocess(string filePath)
        {
            importedModelPathQueue.Enqueue(filePath);
        }

        public static bool IsBusy()
        {
            return isBusy;
        }

        public static void RunPostProcessImports()
        {
            var itemsToProcess = importedModelPathQueue.Count;
            var itemsProcessed = 0;

            while (importedModelPathQueue.TryDequeue(out string path))
            {
                Debug.Log("Postprocessing " + path);

                if (EditorUtility.DisplayCancelableProgressBar("Postprocessing WoW assets", path, itemsProcessed / itemsToProcess))
                {
                    EditorUtility.DisplayDialog("Postprocessing WoW assets", "You can resume from where you left off or process everything from the menu Jobs > WoWUnity.", "Ok");
                    break;
                }

                if (Regex.IsMatch(Path.GetFileName(path), @"^adt_\d+_\d+.obj$"))
                {
                    ADTUtility.PostProcessImport(path);
                }
                else if (path.EndsWith("_invn.obj"))
                {
                    M2Utility.PostProcessDoubleSidedImport(path);
                }
                else
                {
                    var dirName = Path.GetDirectoryName(path);
                    string pathToMetadata = dirName + "/" + Path.GetFileNameWithoutExtension(path) + ".json";
                    string mainDataPath = Application.dataPath.Replace("Assets", "");

                    var sr = new StreamReader(mainDataPath + pathToMetadata);
                    var jsonData = sr.ReadToEnd();
                    sr.Close();

                    M2Utility.PostProcessImport(path, jsonData);
                    WMOUtility.PostProcessImport(path, jsonData);
                }

                SetupPhysics(path);

                TextAsset placementData = AssetDatabase.LoadAssetAtPath<TextAsset>(path.Replace(".obj", "_ModelPlacementInformation.csv"));
                if (placementData != null)
                {
                    ItemCollectionUtility.PlaceModels(M2Utility.FindPrefab(path), placementData);
                }

                itemsProcessed++;
            }
        }

        public static void SetupPhysics(string path)
        {
            GameObject physicsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace(".obj", ".phys.obj"));
            if (physicsPrefab == null)
            {
                return;
            }

            var prefab = M2Utility.FindPrefab(path);

            if (prefab.transform.Find("Collision") != null)
            {
                return;
            }

            var collisionMesh = physicsPrefab.GetComponentInChildren<MeshFilter>();
            if (collisionMesh == null)
            {
                return;
            }

            var prefabInst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            prefabInst.GetComponentsInChildren<MeshCollider>().ToList().ForEach(collider => Object.DestroyImmediate(collider));

            GameObject collider = new();
            collider.transform.SetParent(prefabInst.transform);
            collider.name = "Collision";
            MeshCollider parentCollider = collider.AddComponent<MeshCollider>();
            parentCollider.sharedMesh = collisionMesh.sharedMesh;
            PrefabUtility.ApplyPrefabInstance(prefabInst, InteractionMode.AutomatedAction);
            PrefabUtility.SavePrefabAsset(prefab);

            Object.DestroyImmediate(prefabInst);
        }

        public static void PostProcessImports()
        {
            if (importedModelPathQueue.Count == 0)
            {
                return;
            }

            isBusy = true;

            try
            {
                RunPostProcessImports();
            } finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("PostProcessImports done");
            isBusy = false;
        }

        [MenuItem("Jobs/WoWUnity/Postprocess last imported assets")]
        private static void JobPostprocessLastAssets()
        {
            PostProcessImports();
        }

        [MenuItem("Jobs/WoWUnity/Repostprocess all assets")]
        private static void JobPostprocessAllAssets()
        {
            importedModelPathQueue.Clear();
            EditorUtility.DisplayProgressBar("Postprocessing WoW assets", "Looking for assets.", 0);
            string[] allAssets = AssetDatabase.GetAllAssetPaths();
            foreach (string path in allAssets)
            {
                if (WoWExportUnityPostprocessor.ValidAsset(path))
                {
                    QueuePostprocess(path);
                }
            }
            EditorUtility.ClearProgressBar();
            PostProcessImports();
        }
    }
}
