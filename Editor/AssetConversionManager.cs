using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    class AssetConversionManager
    {
        private static List<string> importedModelPathQueue = new();

        public static void QueueMetadata(string filePath)
        {
            importedModelPathQueue.Add(filePath);
        }

        public static void PostProcessImports()
        {
            if (importedModelPathQueue.Count == 0)
            {
                return;
            }

            if (!Directory.Exists("Assets/Materials/wow"))
            {
                Directory.CreateDirectory("Assets/Materials/wow").Create();
            }

            foreach (string path in importedModelPathQueue)
            {
                if (Regex.IsMatch(Path.GetFileName(path), @"^adt_\d+_\d+.obj$")) {
                    ADTUtility.PostProcessImport(path);
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

            //Processing done: remove all paths from the queue
            importedModelPathQueue.Clear();
        }

        public static void ProcessAssets()
        {
            EditorApplication.update -= ProcessAssets;

            PostProcessImports();
            ItemCollectionUtility.BeginQueue();
        }
    }
}
