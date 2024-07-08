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

        public static void PostProcessImports(List<string> paths)
        {
            var total = 0f;
            var itemsProcessed = 0f;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in paths)
                {
                    if (M2Utility.FindPrefab(path) != null)
                    {
                        continue;
                    }

                    var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Renderer[] renderers = imported.GetComponentsInChildren<Renderer>();
                    total += renderers.Count() + 1;
                }

                foreach (var path in paths)
                {
                    if (M2Utility.FindPrefab(path) != null)
                    {
                        continue;
                    }

                    var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    var dirName = Path.GetDirectoryName(path);
                    var mainDataPath = Application.dataPath.Replace("Assets", "");

                    Renderer[] renderers = imported.GetComponentsInChildren<Renderer>();

                    foreach (var renderer in renderers)
                    {
                        var pathToMetadata = $"{dirName}/tex_{renderer.name}.json";

                        if (EditorUtility.DisplayCancelableProgressBar("Creating terrain materials.", pathToMetadata, itemsProcessed / total))
                        {
                            return;
                        }

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
                        itemsProcessed++;
                    }
                }
            }
            catch (System.Exception)
            {
                Debug.LogError($"failed processing terrain materials");
                throw;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                EditorUtility.ClearProgressBar();
            }

            try
            {
                foreach (var path in paths)
                {
                    if (M2Utility.FindPrefab(path) != null)
                    {
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar("Creating terrain materials.", path, itemsProcessed / total))
                    {
                        return;
                    }

                    //ADT Liquid Volume Queue
                    // LiquidUtility.QueueLiquidData(xxx);

                    GameObject prefab = M2Utility.FindOrCreatePrefab(path);

                    var rootDoodadSetsObj = new GameObject("EnvironmentSet") { isStatic = true };

                    GameObject prefabInst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    rootDoodadSetsObj.transform.parent = prefabInst.transform;
                    PrefabUtility.ApplyPrefabInstance(prefabInst, InteractionMode.AutomatedAction);
                    PrefabUtility.SavePrefabAsset(prefab);
                    Object.DestroyImmediate(rootDoodadSetsObj);
                    Object.DestroyImmediate(prefabInst);
                    AssetDatabase.Refresh();

                    itemsProcessed++;
                }
            } finally
            {
                EditorUtility.ClearProgressBar();
            }
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

        public class EffectModel
        {
            public int fileDataID;
            public string fileName;
        }

        public class Effect
        {
            public int ID;
            public int Density;
            public int Sound;
            public List<int> DoodadID;
            public List<float> DoodadWeight;
            public Dictionary<string, EffectModel> DoodadModelIDs;
        }
    }
}
