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
                        var pathToMetadata = $"{dirName}/tex_{renderer.name}.json";

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
                    M2Utility.FindOrCreatePrefab(path);
                    itemsProcessed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void PlaceChunkFoliage(Transform adtRoot, GameObject chunk, float userDensity)
        {
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(adtRoot.GetChild(0));
            if (path == null)
            {
                Debug.LogWarning("Could not get prefab path.");
                return;
            }

            var sectionName = chunk.name;

            var dirName = Path.GetDirectoryName(path);
            var mainDataPath = Application.dataPath.Replace("Assets", "");

            var pathToMetadata = Path.Join(mainDataPath, dirName, $"tex_{sectionName}.json");

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

            var texPath = Path.Join(mainDataPath, dirName, $"tex_{sectionName}.png");
            var texFileData = File.ReadAllBytes(texPath);
            var texImage = new Texture2D(64, 64);
            if (!texImage.LoadImage(texFileData)) // Load the image data into the Texture2D.
            {
                Debug.LogError("Failed to load texture from " + texPath);
                return;
            }

            texImage = RotateTexture180(texImage);

            var pixels = texImage.GetPixels();
            var pixelsBase = new Dictionary<int, float>();
            var pixelsR = new Dictionary<int, float>();
            var pixelsG = new Dictionary<int, float>();
            var pixelsB = new Dictionary<int, float>();
            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                if (pixel.r == 0 && pixel.g == 0 && pixel.b == 0)
                    pixelsBase.Add(i, 1);

                if (pixel.r > 0)
                    pixelsR.Add(i, pixel.r);

                if (pixel.g > 0)
                    pixelsG.Add(i, pixel.g);

                if (pixel.b > 0)
                    pixelsB.Add(i, pixel.b);
            }

            Object.DestroyImmediate(texImage);

            var layerPixels = new List<Dictionary<int, float>>() { pixelsBase, pixelsR, pixelsG, pixelsB };

            var foliageSetsTransform = adtRoot.Find("FoliageSets");
            if (foliageSetsTransform == null)
            {
                var foliageSets = new GameObject("FoliageSets");
                foliageSets.transform.parent = adtRoot;
                foliageSetsTransform = foliageSets.transform;
            }

            var foliageSectionTransform = foliageSetsTransform.Find(sectionName);
            if (foliageSectionTransform == null)
            {
                var foliageSection = new GameObject(sectionName);
                foliageSection.transform.parent = foliageSetsTransform;
                foliageSectionTransform = foliageSection.transform;
            }

            foliageSectionTransform.gameObject.AddComponent<FixFoliagePosition>();

            var sectionMesh = chunk.GetComponent<MeshFilter>().sharedMesh;

            for (var idx = 0; idx < metadata.layers.Count; idx++)
            {
                var layerTransform = foliageSectionTransform.Find($"layer{idx}");
                if (layerTransform != null)
                    continue;

                var layer = new GameObject($"layer{idx}");
                layer.transform.parent = foliageSectionTransform;
                layerTransform = layer.transform;

                var texture = metadata.layers[idx];

                var foliageDir = Path.Join(dirName, "foliage");

                var srl = new StreamReader(Path.Join(mainDataPath, foliageDir, $"{texture.effectID}.json"));
                var jsonDataL = srl.ReadToEnd();
                sr.Close();

                var effectData = JsonConvert.DeserializeObject<ADTUtility.Effect>(jsonDataL);

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

                    var weight = effectData.DoodadWeight[j];
                    var density = effectData.Density * weight;
                    var prob = (density * 100 * userDensity) / (64 * 64);

                    foreach (var (pixelIdx, pixelVal) in layerPixels[idx])
                    {
                        if (Random.value > prob * pixelVal)
                            continue;

                        var x = pixelIdx % 64;
                        var z = pixelIdx / 64;

                        var posX = (x / 64f) * (sectionMesh.bounds.size.x - 64f / sectionMesh.bounds.size.x) + Random.Range(0, 64f / sectionMesh.bounds.size.x);
                        var posZ = (z / 64f) * (sectionMesh.bounds.size.z - 64f / sectionMesh.bounds.size.z) + Random.Range(0, 64f / sectionMesh.bounds.size.z);

                        if (true)
                        {
                            var pos = new Vector3(posX, -sectionMesh.bounds.size.y * 2, posZ);

                            var foliage = PrefabUtility.InstantiatePrefab(foliagePrefab) as GameObject;
                            foliage.transform.position = pos;
                            foliage.transform.RotateAround(pos, Vector3.up, Random.Range(0, 180));
                            foliage.transform.parent = layerTransform;
                            foliage.isStatic = false;
                            foreach (Transform transform in foliage.GetComponentInChildren<Transform>())
                            {
                                transform.gameObject.isStatic = false;
                                foreach (Transform subtransform in transform.GetComponentInChildren<Transform>())
                                {
                                    subtransform.gameObject.isStatic = false;
                                }
                            }
                        }
                    }
                }
            }

            foliageSectionTransform.localPosition = new Vector3(
                sectionMesh.bounds.center.x - sectionMesh.bounds.extents.x,
                sectionMesh.bounds.center.y + sectionMesh.bounds.extents.y,
                sectionMesh.bounds.center.z - sectionMesh.bounds.extents.z
            );

            foliageSetsTransform.localPosition = Vector3.zero;
        }

        public static Texture2D RotateTexture180(Texture2D originalTexture)
        {
            var rotatedTexture = new Texture2D(originalTexture.width, originalTexture.height);

            // Get the original pixels from the texture
            var originalPixels = originalTexture.GetPixels();
            var rotatedPixels = new Color[originalPixels.Length];

            int width = originalTexture.width;
            var height = originalTexture.height;

            // Loop through each pixel and set it in the new position
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    // Calculate the index for the original and rotated positions
                    var originalIndex = y * width + x;
                    var rotatedIndex = (height - 1 - y) * width + (width - 1 - x);

                    // Set the rotated pixel
                    rotatedPixels[rotatedIndex] = originalPixels[originalIndex];
                }
            }

            // Apply the rotated pixels to the new texture
            rotatedTexture.SetPixels(rotatedPixels);
            rotatedTexture.Apply();

            return rotatedTexture;
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
