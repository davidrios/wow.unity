using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    class AssetConversionManager
    {
        private static ConcurrentQueue<string> importedModelPathQueue = new();

        public static void QueueMetadata(string filePath)
        {
            importedModelPathQueue.Enqueue(filePath);
        }

        public static void PostProcessImports()
        {
            while (importedModelPathQueue.TryDequeue(out string path))
            {
                if (Regex.IsMatch(Path.GetFileName(path), @"^adt_\d+_\d+.obj$")) {
                    ADTUtility.PostProcessImport(path);
                } else if (path.EndsWith("_invn.obj")) {
                    M2Utility.PostProcessDoubleSidedImport(path);
                } else {
                    var dirName = Path.GetDirectoryName(path);
                    string pathToMetadata = dirName + "/" + Path.GetFileNameWithoutExtension(path) + ".json";
                    string mainDataPath = Application.dataPath.Replace("Assets", "");

                    var sr = new StreamReader(mainDataPath + pathToMetadata);
                    var jsonData = sr.ReadToEnd();
                    sr.Close();

                    M2Utility.PostProcessImport(path, jsonData);
                    WMOUtility.PostProcessImport(path, jsonData);
                }
            }
        }

        public static void ProcessAssets()
        {
            EditorApplication.update -= ProcessAssets;

            PostProcessImports();
            ItemCollectionUtility.BeginQueue();
        }
    }
}
