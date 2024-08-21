using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    public class ADTUtility
    {
        public static bool IsAdtObj(string path)
        {
            return Regex.IsMatch(Path.GetFileName(path), @"^adt_\d+_\d+(_.+)?\.obj$");
        }

        public static bool IsAdtAny(string path)
        {
            return Regex.IsMatch(Path.GetFileName(path), @"^adt_\d+_\d+(_.+)?\.(prefab|obj)$");
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
                        continue;

                    var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var renderers = imported.GetComponentsInChildren<Renderer>();
                    total += renderers.Count() + 1;
                }

                var mainDataPath = Application.dataPath.Replace("Assets", "");
                foreach (var path in paths)
                {
                    if (M2Utility.FindPrefab(path) != null)
                        continue;

                    var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    var dirName = Path.GetDirectoryName(path);

                    var renderers = imported.GetComponentsInChildren<Renderer>();

                    foreach (var renderer in renderers)
                    {
                        var match = Regex.Match(renderer.name, @".*?(\d+_\d+_\d+)$");
                        var name = match.Groups[1].Value;
                        var pathToMetadata = $"{dirName}/tex_{name}.json";

                        if (EditorUtility.DisplayCancelableProgressBar("Creating terrain materials.", pathToMetadata, itemsProcessed / total))
                            return;

                        var sr = new StreamReader(mainDataPath + pathToMetadata);
                        var jsonData = sr.ReadToEnd();
                        sr.Close();

                        var metadata = JsonConvert.DeserializeObject<Tex>(jsonData);
                        if (metadata.layers.Count == 0)
                            continue;

                        for (var idx = 0; idx < metadata.layers.Count; idx++)
                        {
                            var texture = metadata.layers[idx];
                            texture.assetPath = Path.GetRelativePath(mainDataPath, Path.GetFullPath(Path.Join(dirName, texture.file)));
                            metadata.layers[idx] = texture;
                        }

                        renderer.material = MaterialUtility.GetTerrainMaterial(dirName, name, metadata);
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
                    M2Utility.FindOrCreatePrefab(path);
                    itemsProcessed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void PlaceFoliageSpawner(FoliageSettings foliageSettings, GameObject chunk)
        {
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(chunk);
            if (path == null)
            {
                Debug.LogWarning("Could not get prefab path.");
                return;
            }

            var match = Regex.Match(chunk.name, @".*?(\d+_\d+_\d+)$");
            var chunkName = match.Groups[1].Value;

            var dirName = Path.GetDirectoryName(path);
            var mainDataPath = Application.dataPath.Replace("Assets", "");

            var pathToMetadata = Path.Join(mainDataPath, dirName, $"tex_{chunkName}.json");

            if (!File.Exists(pathToMetadata))
            {
                Debug.LogWarning($"{pathToMetadata} not found.");
                return;
            }

            var sr = new StreamReader(pathToMetadata);
            var jsonData = sr.ReadToEnd();
            sr.Close();

            var metadata = JsonConvert.DeserializeObject<Tex>(jsonData);
            if (metadata.layers.Count == 0)
                return;

            var chunkMesh = chunk.GetComponent<MeshFilter>().sharedMesh;
            var layersInfo = new List<Foliage.LayerInfo>();

            for (var idx = 0; idx < metadata.layers.Count; idx++)
            {
                var texture = metadata.layers[idx];

                var foliageDir = Path.Join(dirName, "foliage");

                string jsonDataL;
                var foliageJson = Path.Join(mainDataPath, foliageDir, $"{texture.effectID}.json");

                try
                {
                    var srl = new StreamReader(foliageJson);
                    jsonDataL = srl.ReadToEnd();
                    sr.Close();
                } catch
                {
                    Debug.LogWarning($"Foliage data {foliageJson} not found for chunk {chunkName}");
                    continue;
                }

                var effectData = JsonConvert.DeserializeObject<Effect>(jsonDataL);

                var layerDoodads = new List<GameObject>();
                var layerDoodadWeights = new List<float>();

                for (var j = 0; j < effectData.DoodadID.Count; j++)
                {
                    var doodadId = effectData.DoodadID[j];
                    if (doodadId == 0)
                        continue;

                    var effectModel = effectData.DoodadModelIDs[doodadId.ToString()];

                    var foliagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path.Join(foliageDir, effectModel.fileName.Replace(".obj", ".prefab")));
                    if (foliagePrefab == null)
                    {
                        Debug.LogWarning($"{effectModel.fileName}: prefab not found.");
                        continue;
                    }

                    foreach (var renderer in foliagePrefab.GetComponentsInChildren<Renderer>())
                    {
                        renderer.sharedMaterial.enableInstancing = true;
                    }

                    layerDoodads.Add(foliagePrefab);
                    layerDoodadWeights.Add(effectData.DoodadWeight[j]);
                }

                layersInfo.Add(new Foliage.LayerInfo()
                {
                    density = effectData.Density,
                    doodads = layerDoodads,
                    doodadWeights = layerDoodadWeights
                });
            }

            if (!chunk.TryGetComponent<Foliage.FoliageSpawner>(out var spawner))
                spawner = chunk.AddComponent<Foliage.FoliageSpawner>();

            spawner.SetupSpawner(
                AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Join(dirName, $"tex_{chunkName}.png")),
                layersInfo,
                chunkMesh.bounds,
                (int)(Random.value * 0xffffff),
                foliageSettings
            );
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
            public uint Density;
            public int Sound;
            public List<int> DoodadID;
            public List<float> DoodadWeight;
            public Dictionary<string, EffectModel> DoodadModelIDs;
        }
    }
}
