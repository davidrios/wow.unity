using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    class WMOUtility
    {
        public static void PostProcessImport(string path, string jsonData)
        {
            var metadata = JsonConvert.DeserializeObject<WMO>(jsonData);
            if (metadata.fileType != "wmo") {
                return;
            }

            Debug.Log($"{path}: processing wmo");

            if (M2Utility.FindPrefab(path) != null)
            {
                return;
            }

            M2Utility.ProcessTextures(metadata.textures, Path.GetDirectoryName(path));

            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Renderer[] renderers = imported.GetComponentsInChildren<Renderer>();

            var materials = MaterialUtility.GetWMOMaterials(metadata);

            for (uint rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                renderer.material = materials[renderer.name];
            }
            AssetDatabase.Refresh();

            GameObject prefab = M2Utility.FindOrCreatePrefab(path);

            if (metadata.doodadSets.Count > 0) {
                var rootDoodadSetsObj = new GameObject("DoodadSets") { isStatic = true };
                foreach (var doodadSet in metadata.doodadSets) {
                    var setObj = new GameObject(doodadSet.name) { isStatic = true };
                    setObj.transform.parent = rootDoodadSetsObj.transform;
                    setObj.SetActive(false);
                }

                GameObject prefabInst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                rootDoodadSetsObj.transform.parent = prefabInst.transform;
                PrefabUtility.ApplyPrefabInstance(prefabInst, InteractionMode.AutomatedAction);
                PrefabUtility.SavePrefabAsset(prefab);
                Object.DestroyImmediate(rootDoodadSetsObj);
                Object.DestroyImmediate(prefabInst);
                AssetDatabase.Refresh();
            }
        }

        public static bool AssignVertexColors(Group group, List<GameObject> gameObjects)
        {
            if (gameObjects.Count != group.renderBatches.Count)
            {
                Debug.LogError("Attempted to assign vertex colors to WMO, but group size did not match object stack!");
                return false;
            }

            for (int i = 0; i < gameObjects.Count; i++)
            {
                GameObject gameObject = gameObjects[i];
                WMOUtility.RenderBatch renderBatch = group.renderBatches[i];
                MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                Mesh mesh = meshFilter.sharedMesh;
                
                if (mesh == null)
                {
                    Debug.LogError("Attempted to assign vertex colors to WMO, but mesh was missing.");
                    return false;
                }

                mesh.colors = GetVertexColorsInRange(group, renderBatch.firstVertex, renderBatch.lastVertex);
            }

            return true;
        }

        static Color[] GetVertexColorsInRange(Group group, int start, int end)
        {
            List<byte[]> vertexColors = group.vertexColors.GetRange(start, end - start);
            List<Color> parsedColors = new List<Color>();

            for (int i = 0; i < vertexColors.Count; i++)
            {
                Color newColor = new Color();
                byte[] colorData = vertexColors[i];
                newColor.a = (float)colorData[0] / 255f;
                newColor.r = (float)colorData[1] / 255f;
                newColor.b = (float)colorData[2] / 255f;
                newColor.g = (float)colorData[3] / 255f;
            }

            return parsedColors.ToArray();
        }

        public class WMO
        {
            public string fileType;
            public uint fileDataID;
            public string fileName;
            public uint version;
            public uint ambientColor;
            public uint areaTableID;
            public short flags;
            public List<Group> groups;
            public List<string> groupNames;
            public List<M2Utility.Texture> textures;
            public List<Material> materials;
            public List<DoodadSet> doodadSets;
        }

        public class Group
        {
            public string groupName;
            public bool enabled;
            public uint version;
            public uint groupID;
            public List<RenderBatch> renderBatches;
            public List<byte[]> vertexColors;
        }

        public class RenderBatch
        {
            public ushort firstVertex;
            public ushort lastVertex;
            public short flags;
            public uint materialID;
        }

        public class Material {
            public short flags;
            public int shader;
            public uint blendMode;
            public uint texture1;
			public uint color1;
			public uint color1b;
			public uint texture2;
			public uint color2;
			public uint groupType;
			public uint texture3;
			public uint color3;
			public uint flags3;
        }

        public class DoodadSet {
            public string name;
			public uint firstInstanceIndex;
			public uint doodadCount;
        }
    }
}
