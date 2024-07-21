using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    class AssetConversionManager
    {
        private static readonly ConcurrentQueue<string> importedModelPathQueue = new();
        private static readonly ConcurrentQueue<string> importedWMOPathQueue = new();
        private static readonly ConcurrentQueue<string> physicsQueue = new();
        private static readonly ConcurrentQueue<string> lodGroupsQueue = new();
        private static readonly ConcurrentQueue<(string, List<string>)> doubleSidedCreationQueue = new();
        private static bool isBusy = false;

        public static void QueueCreateDoublesided((string, List<string>) item)
        {
            doubleSidedCreationQueue.Enqueue(item);
        }

        public static void QueuePostprocess(string filePath)
        {
            importedModelPathQueue.Enqueue(filePath);
        }

        public static bool IsBusy()
        {
            return isBusy;
        }

        public static string ReadAssetJson(string path)
        {
            var dirName = Path.GetDirectoryName(path);
            var pathToMetadata = dirName + "/" + Path.GetFileNameWithoutExtension(path) + ".json";
            var mainDataPath = Application.dataPath.Replace("Assets", "");

            var sr = new StreamReader(mainDataPath + pathToMetadata);
            var jsonData = sr.ReadToEnd();
            sr.Close();

            return jsonData;
        }

        public static void RunPostProcessImports()
        {
            var itemsToProcess = importedModelPathQueue.Count;
            var itemsProcessed = 0f;

            var hasPlacement = new List<(string, TextAsset)>();

            AssetDatabase.StartAssetEditing();
            try
            {
                while (importedModelPathQueue.TryDequeue(out string path))
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Postprocessing WoW assets", path, itemsProcessed / itemsToProcess))
                        return;

                    Debug.Log($"{path}: postprocessing");

                    var jsonData = ReadAssetJson(path);

                    if (WMOUtility.IsWMO(jsonData))
                    {
                        // process this separately because of StartAssetEditing issues
                        importedWMOPathQueue.Enqueue(path);
                        continue;
                    }

                    if (M2Utility.FindPrefab(path) == null)
                    {
                        // process this separately because of StartAssetEditing issues
                        physicsQueue.Enqueue(path);
                    }

                    M2Utility.PostProcessImport(path, jsonData);

                    if (Settings.GetSettings().addLODGroups)
                        lodGroupsQueue.Enqueue(path);

                    itemsProcessed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            itemsToProcess = doubleSidedCreationQueue.Count;
            itemsProcessed = 0f;
            while (doubleSidedCreationQueue.TryDequeue(out (string, List<string>) item))
            {
                if (EditorUtility.DisplayCancelableProgressBar("Creating double sided", item.Item1, itemsProcessed / itemsToProcess))
                    return;

                M2Utility.ProcessDoubleSided(item.Item1, item.Item2);
                itemsProcessed++;
            }

            while (importedWMOPathQueue.TryDequeue(out string path))
            {
                Debug.Log($"{path}: postprocessing");

                if (EditorUtility.DisplayCancelableProgressBar("Postprocessing WoW WMOs", path, itemsProcessed / itemsToProcess))
                    return;

                WMOUtility.PostProcessImport(path, ReadAssetJson(path));

                var placementData = AssetDatabase.LoadAssetAtPath<TextAsset>(path.Replace(".obj", "_ModelPlacementInformation.csv"));
                if (placementData != null)
                    hasPlacement.Add((path, placementData));

                itemsProcessed++;
            }

            itemsToProcess = physicsQueue.Count;
            itemsProcessed = 0f;

            var createCollisionForAllM2 = Settings.GetSettings().createCollisionForAllM2;

            while (physicsQueue.TryDequeue(out string path))
            {
                Debug.Log($"{path}: setup physics");

                if (EditorUtility.DisplayCancelableProgressBar("Setting up physics", path, itemsProcessed / itemsToProcess))
                    return;

                M2Utility.SetupPhysics(path, createCollisionForAllM2);

                itemsProcessed++;
            }

            itemsToProcess = lodGroupsQueue.Count;
            itemsProcessed = 0f;
            while (lodGroupsQueue.TryDequeue(out string path))
            {
                Debug.Log($"{path}: setup LODGroup");

                if (EditorUtility.DisplayCancelableProgressBar("Setting up LODGroup", path, itemsProcessed / itemsToProcess))
                    return;

                ModelUtility.SetupLODGroup(path);

                itemsProcessed++;
            }

            itemsToProcess = hasPlacement.Count;
            itemsProcessed = 0f;

            foreach (var (path, placementData) in hasPlacement)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Placing doodads", path, itemsProcessed / itemsToProcess))
                    return;

                Debug.Log($"{path}: placing models");
                ItemCollectionUtility.PlaceModels(M2Utility.FindPrefab(path), placementData);
                itemsProcessed++;
            }
        }

        private static void PostProcessImports()
        {
            isBusy = true;

            try
            {
                RunPostProcessImports();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("PostProcessImports done");
            isBusy = false;
        }

        public static void JobPostprocessAllAssets()
        {
            importedModelPathQueue.Clear();
            importedWMOPathQueue.Clear();
            physicsQueue.Clear();

            EditorUtility.DisplayProgressBar("Postprocessing WoW assets", "Looking for assets.", 0);
            try
            {
                var allAssets = AssetDatabase.GetAllAssetPaths();
                foreach (var path in allAssets)
                {
                    if (WoWExportUnityPostprocessor.ValidAsset(path) && !ADTUtility.IsAdtObj(path) && !path.EndsWith(M2Utility.DOUBLE_SIDED_INVERSE_SUFFIX))
                        QueuePostprocess(path);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            PostProcessImports();
        }
    }
}
