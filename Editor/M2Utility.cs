using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    class M2Utility
    {
        public static void PostProcessImport(string path, string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<M2>(jsonData);
            if (metadata.fileType != "m2") {
                return;
            }

            Debug.Log($"{path}: processing m2");

            if (FindPrefab(path) != null)
            {
                return;
            }

            ProcessTextures(metadata.textures, Path.GetDirectoryName(path));

            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Renderer[] renderers = imported.GetComponentsInChildren<Renderer>();

            var skinMaterials = MaterialUtility.GetSkinMaterials(metadata);

            var isDoubleSided = false;
            List<string> nonDoubleSided = new();

            for (uint rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                var (material, isMatDoubleSided) = skinMaterials[rendererIndex];
                renderer.material = material;

                isDoubleSided = isDoubleSided || isMatDoubleSided;
                if (!isMatDoubleSided)
                {
                    nonDoubleSided.Add(renderer.name);
                }
            }
            AssetDatabase.Refresh();

            GameObject prefab = FindOrCreatePrefab(path);

            if (isDoubleSided)
            {
                ExportDoubleSided(path, imported, nonDoubleSided);
            }

            if (metadata.textureTransforms.Count > 0 && metadata.textureTransforms[0].translation.timestamps.Count > 0)
            {
                for (int i = 0; i < metadata.textureTransforms.Count; i++)
                {
                    AnimationClip newClip = AnimationUtility.CreateAnimationClip(metadata.textureTransforms[i]);
                    AssetDatabase.CreateAsset(newClip, Path.GetDirectoryName(path) + "/" + Path.GetFileNameWithoutExtension(path) + "[" + i +  "]" + ".anim");
                }
            }
        }

        public static void ExportDoubleSided(string origPath, GameObject asset, List<string> nonDoubleSided)
        {
            GameObject dinst = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            foreach (var name in nonDoubleSided)
            {
                var nonDouble = dinst.transform.Find(name);
                if (nonDouble != null)
                {
                    UnityEngine.Object.DestroyImmediate(nonDouble.gameObject);
                }
            }

            foreach (var meshFilter in dinst.GetComponentsInChildren<MeshFilter>())
            {
                meshFilter.sharedMesh = DuplicateAndReverseMesh(meshFilter.sharedMesh);
            }

            string invPath = origPath.Replace(".obj", "_invn.obj");
            ObjExporter.ExportObj(dinst, invPath);
            UnityEngine.Object.DestroyImmediate(dinst);
        }

        public static void PostProcessDoubleSidedImport(string path)
        {
            Debug.Log($"{path}: processing double sided");

            var origPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace("_invn.obj", ".prefab"));
            if (origPrefab == null)
            {
                Debug.LogWarning($"{path}: could not find original prefab");
                return;
            }

            GameObject origPrefabInst = PrefabUtility.InstantiatePrefab(origPrefab) as GameObject;

            if (origPrefabInst.transform.Find(Path.GetFileNameWithoutExtension(path)) != null)
            {
                UnityEngine.Object.DestroyImmediate(origPrefabInst);
                return;
            }

            var texturesByRenderer = origPrefabInst.GetComponentsInChildren<Renderer>()
                .Select((item) => (item.name, item.sharedMaterial))
                .ToDictionary((item) => item.Item1, (item) => item.Item2);

            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var renderers = imported.GetComponentsInChildren<Renderer>();
            for (var idx = 0; idx < renderers.Length; idx++)
            {
                renderers[idx].sharedMaterial = texturesByRenderer[renderers[idx].name];
            }
            var importedInst = PrefabUtility.InstantiatePrefab(imported, origPrefabInst.transform) as GameObject;

            PrefabUtility.ApplyPrefabInstance(origPrefabInst, InteractionMode.AutomatedAction);
            PrefabUtility.SavePrefabAsset(origPrefab);

            UnityEngine.Object.DestroyImmediate(origPrefabInst);
        }

        public static GameObject FindOrCreatePrefab(string path)
        {
            GameObject existingPrefab = FindPrefab(path);

            if (existingPrefab == null)
            {
                return GeneratePrefab(path);
            }

            return existingPrefab;
        }

        public static GameObject FindPrefab(string path)
        {
            string prefabPath = Path.ChangeExtension(path, "prefab");
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        public static GameObject GeneratePrefab(string path)
        {
            string prefabPath = Path.ChangeExtension(path, "prefab");
            GameObject importedModelObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (importedModelObject == null)
            {
                Debug.LogWarning("Tried to create prefab, but could not find imported model: " + path);
                return null;
            }

            var rootObj = new GameObject() { isStatic = true };
            GameObject rootModelInstance = PrefabUtility.InstantiatePrefab(importedModelObject, rootObj.transform) as GameObject;

            //Set the object as static, and all it's child objects
            rootModelInstance.isStatic = true;
            foreach (Transform childTransform in rootModelInstance.transform)
            {
                childTransform.gameObject.isStatic = true;
            }

            GameObject newPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(rootObj, prefabPath, InteractionMode.AutomatedAction);
            AssetDatabase.Refresh();
            UnityEngine.Object.DestroyImmediate(rootObj);

            return newPrefab;
        }

        public static void ProcessTextures(List<Texture> textures, string dirName) {
            string mainDataPath = Application.dataPath.Replace("Assets", "");

            for (var idx = 0; idx < textures.Count; idx++) {
                var texture = textures[idx];
                texture.assetPath = Path.GetRelativePath(mainDataPath, Path.GetFullPath(Path.Join(dirName, texture.fileNameExternal)));
                textures[idx] = texture;
            }
        }

        public static Mesh DuplicateAndReverseMesh(Mesh mesh)
        {
            Mesh invertedMesh = new()
            {
                vertices = mesh.vertices,
                triangles = mesh.triangles,
                uv = mesh.uv, // Copy UVs if necessary
                uv2 = mesh.uv2 // Copy UVs if necessary
            };

            // Invert the normals
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = -normals[i];
            }
            invertedMesh.normals = normals;

            // Invert the triangle order to keep the mesh visible from the other side
            for (int i = 0; i < invertedMesh.subMeshCount; i++)
            {
                int[] triangles = invertedMesh.GetTriangles(i);
                for (int j = 0; j < triangles.Length; j += 3)
                {
                    // Swap order of triangles
                    (triangles[j + 1], triangles[j]) = (triangles[j], triangles[j + 1]);
                }
                invertedMesh.SetTriangles(triangles, i);
            }

            return invertedMesh;
        }

        [Serializable]
        public class M2
        {
            public string fileType;
            public uint fileDataID;
            public string fileName;
            public string internalName;
            public Skin skin;
            public List<Texture> textures = new List<Texture>();
            public List<short> textureTypes = new List<short>();
            public List<Material> materials = new List<Material>();
            public List<short> textureCombos = new List<short>();
            public List<ColorData> colors = new List<ColorData>();
            public List<TextureTransform> textureTransforms = new List<TextureTransform>();
            public List<uint> textureTransformsLookup = new List<uint>();
        }

        [Serializable]
        public class Skin
        {
            public List<SubMesh> subMeshes = new List<SubMesh>();
            public List<TextureUnit> textureUnits = new List<TextureUnit>();
        }

        [Serializable]
        public struct SubMesh
        {
            public bool enabled;
        }

        [Serializable]
        public struct TextureUnit
        {
            public uint skinSectionIndex;
            public uint geosetIndex;
            public uint materialIndex;
            public uint colorIndex;
            public uint textureComboIndex;
        }

        [Serializable]
        public struct Texture
        {
            public string fileNameInternal;
            public string fileNameExternal;
            public string assetPath;
            public string mtlName;
            public string uniqMtlName;
            public short flag;
            public uint fileDataID;
        }

        [Serializable]
        public struct Material
        {
            public short flags;
            public uint blendingMode;
        }

        [Serializable]
        public struct ColorData
        {
            public MultiValueAnimationInformation color;
            public SingleValueAnimationInformation alpha;
        }

        [Serializable]
        public struct TextureTransform
        {
            public MultiValueAnimationInformation translation;
            public MultiValueAnimationInformation rotation;
            public MultiValueAnimationInformation scaling;
        }

        [Serializable]
        public struct SingleValueAnimationInformation
        {
            public uint globalSeq;
            public int interpolation;
            public List<List<uint>> timestamps;
            public List<List<float>> values;
        }

        [Serializable]
        public struct MultiValueAnimationInformation
        {
            public uint globalSeq;
            public int interpolation;
            public List<List<uint>> timestamps;
            public List<List<List<float>>> values;
        }
    }
}
