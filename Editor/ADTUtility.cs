using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    class ADTUtility
    {
        public static bool IsAdtObj(string path)
        {
            return Regex.IsMatch(Path.GetFileName(path), @"^adt_\d+_\d+\.obj$");
        }

        public static bool IsAdtAny(string path)
        {
            return Regex.IsMatch(Path.GetFileName(path), @"^adt_\d+_\d+\.(prefab|obj)$");
        }

        public static void PostProcessImport(string path)
        {
            Debug.Log($"{path}: processing adt");

            if (M2Utility.FindPrefab(path) != null)
            {
                return;
            }

            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            var dirName = Path.GetDirectoryName(path);
            string mainDataPath = Application.dataPath.Replace("Assets", "");

            Renderer[] renderers = imported.GetComponentsInChildren<Renderer>();
            AssetDatabase.StartAssetEditing();
            try
            {
                var total = (float)renderers.Count();

                for (var i = 0; i < total; i++) {
                    var renderer = renderers[i];
                    string pathToMetadata = $"{dirName}/tex_{renderer.name}.json";

                    EditorUtility.DisplayProgressBar("Creating terrain materials.", pathToMetadata, i / total);

                    var sr = new StreamReader(mainDataPath + pathToMetadata);
                    var jsonData = sr.ReadToEnd();
                    sr.Close();

                    var metadata = JsonConvert.DeserializeObject<Tex>(jsonData);
                    if (metadata.layers.Count == 0)
                    {
                        continue;
                    }

                    for (var idx = 0; idx < metadata.layers.Count; idx++)
                    {
                        var texture = metadata.layers[idx];
                        texture.assetPath = Path.GetRelativePath(mainDataPath, Path.GetFullPath(Path.Join(dirName, texture.file)));
                        metadata.layers[idx] = texture;
                    }

                    renderer.material = MaterialUtility.GetTerrainMaterial(dirName, renderer.name, metadata);
                }
            } catch (System.Exception e)
            {
                Debug.LogError($"{path}: failed processing terrain");
                throw;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                EditorUtility.ClearProgressBar();
            }

            GameObject prefab = M2Utility.FindOrCreatePrefab(path);

            var rootDoodadSetsObj = new GameObject("EnvironmentSet") { isStatic = true };

            GameObject prefabInst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rootDoodadSetsObj.transform.parent = prefabInst.transform;
            PrefabUtility.ApplyPrefabInstance(prefabInst, InteractionMode.AutomatedAction);
            PrefabUtility.SavePrefabAsset(prefab);
            Object.DestroyImmediate(rootDoodadSetsObj);
            Object.DestroyImmediate(prefabInst);
            AssetDatabase.Refresh();
        }

        public class Tex
        {
            public List<Layer> layers;
        }

        public class Layer
        {
            public uint index;
            public uint effectID;
            public float scale;
            public uint fileDataID;
            public string file;
            public string assetPath;
        }
    }
}
