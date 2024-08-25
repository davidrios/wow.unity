using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    public class M2Utility
    {
        public const string DOUBLE_SIDED_INVERSE_SUFFIX = "__dsinv.obj";

        public static void PostProcessImport(string path, string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<M2>(jsonData);
            if (metadata.fileType != "m2") {
                return;
            }

            if (FindPrefab(path) != null)
            {
                return;
            }

            Debug.Log($"{path}: processing m2");

            ProcessTextures(metadata.textures, Path.GetDirectoryName(path));

            var importedInstance = InstantiateImported(path);

            var renderers = importedInstance.GetComponentsInChildren<Renderer>();

            var skinMaterials = MaterialUtility.GetSkinMaterials(metadata);

            var isDoubleSided = false;
            var nonDoubleSided = new List<string>();

            // Configure materials
            for (uint rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                var (material, isMatDoubleSided) = skinMaterials[rendererIndex];
                renderer.material = material;

                isDoubleSided = isDoubleSided || isMatDoubleSided;
                if (!isMatDoubleSided)
                    nonDoubleSided.Add(renderer.name);
            }
            AssetDatabase.Refresh();

            SaveAsPrefab(importedInstance, path);

            if (isDoubleSided && Settings.GetSettings().renderingPipeline == RenderingPipeline.BiRP)
                AssetConversionManager.QueueCreateDoublesided((path, nonDoubleSided));

            if (metadata.textureTransforms.Count > 0 && metadata.textureTransforms[0].translation.timestamps.Count > 0)
            {
                for (int i = 0; i < metadata.textureTransforms.Count; i++)
                {
                    AnimationClip newClip = AnimationUtility.CreateAnimationClip(metadata.textureTransforms[i]);
                    AssetDatabase.CreateAsset(newClip, Path.GetDirectoryName(path) + "/" + Path.GetFileNameWithoutExtension(path) + "[" + i +  "]" + ".anim");
                }
            }
        }

        public static void ProcessDoubleSided(string origPath, List<string> nonDoubleSided)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(origPath);
            var dinst = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            foreach (var name in nonDoubleSided)
            {
                var nonDouble = dinst.transform.Find(name);
                if (nonDouble != null)
                    UnityEngine.Object.DestroyImmediate(nonDouble.gameObject);
            }

            foreach (var meshFilter in dinst.GetComponentsInChildren<MeshFilter>())
            {
                meshFilter.sharedMesh = DuplicateAndReverseMesh(meshFilter.sharedMesh);
            }

            var invPath = origPath.Replace(".obj", DOUBLE_SIDED_INVERSE_SUFFIX);
            ObjExporter.ExportObj(dinst, invPath);
            UnityEngine.Object.DestroyImmediate(dinst);

            AssetDatabase.ImportAsset(invPath);

            var origPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(invPath.Replace(DOUBLE_SIDED_INVERSE_SUFFIX, ".prefab"));
            if (origPrefab == null)
            {
                Debug.LogWarning($"{invPath}: could not find original prefab");
                return;
            }

            var origPrefabInst = PrefabUtility.InstantiatePrefab(origPrefab) as GameObject;

            if (origPrefabInst.transform.Find(Path.GetFileNameWithoutExtension(invPath)) != null)
            {
                UnityEngine.Object.DestroyImmediate(origPrefabInst);
                return;
            }

            SetupDoubleSided(origPrefabInst, invPath);

            PrefabUtility.ApplyPrefabInstance(origPrefabInst, InteractionMode.AutomatedAction);
            PrefabUtility.SavePrefabAsset(origPrefab);

            UnityEngine.Object.DestroyImmediate(origPrefabInst);

            Debug.Log($"{origPath}: processed double sided");
        }

        public static GameObject SetupDoubleSided(GameObject gameObject, string invPath)
        {
            var texturesByRenderer = gameObject.GetComponentsInChildren<Renderer>()
                .Select((item) => (item.name, item.sharedMaterial))
                .ToDictionary((item) => item.Item1, (item) => item.Item2);

            var invPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(invPath);
            var invPrefabInst = PrefabUtility.InstantiatePrefab(invPrefab, gameObject.transform) as GameObject;
            invPrefabInst.isStatic = gameObject.isStatic;
            foreach (Transform childTransform in invPrefabInst.transform)
            {
                childTransform.gameObject.isStatic = gameObject.isStatic;
            }
            var renderers = invPrefabInst.GetComponentsInChildren<Renderer>();
            for (var idx = 0; idx < renderers.Length; idx++)
            {
                renderers[idx].sharedMaterial = texturesByRenderer[renderers[idx].name];
            }

            return invPrefabInst;
        }

        public static GameObject InstantiateImported(string path)
        {
            var importedModelObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (importedModelObject == null)
            {
                Debug.LogWarning("Tried to create prefab, but could not find imported model: " + path);
                return null;
            }

            var rootObj = new GameObject() { isStatic = true };
            var rootModelInstance = PrefabUtility.InstantiatePrefab(importedModelObject, rootObj.transform) as GameObject;

            //Set the object as static, and all it's child objects
            rootModelInstance.isStatic = true;
            foreach (Transform childTransform in rootModelInstance.transform)
            {
                childTransform.gameObject.isStatic = true;
            }

            return rootObj;
        }

        public static void SaveAsPrefab(GameObject gameObject, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(gameObject, Path.ChangeExtension(path, ".prefab"));
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        public static GameObject FindPrefab(string path)
        {
            string prefabPath = Path.ChangeExtension(path, "prefab");
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
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
            var invertedMesh = new Mesh()
            {
                vertices = mesh.vertices,
                triangles = mesh.triangles,
                uv = mesh.uv, // Copy UVs if necessary
                uv2 = mesh.uv2 // Copy UVs if necessary
            };

            // Invert the normals
            var normals = mesh.normals;
            for (var i = 0; i < normals.Length; i++)
            {
                normals[i] = -normals[i];
            }
            invertedMesh.normals = normals;

            // Invert the triangle order to keep the mesh visible from the other side
            for (var i = 0; i < invertedMesh.subMeshCount; i++)
            {
                var triangles = invertedMesh.GetTriangles(i);
                for (var j = 0; j < triangles.Length; j += 3)
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
