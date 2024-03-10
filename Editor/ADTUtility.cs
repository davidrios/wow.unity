using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    class ADTUtility
    {
        public static void PostProcessImport(string path)
        {
            Debug.Log($"{path}: processing adt");

            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            var dirName = Path.GetDirectoryName(path);
            string mainDataPath = Application.dataPath.Replace("Assets", "");

            Renderer[] renderers = imported.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                string pathToMetadata = $"{dirName}/tex_{renderer.name}.json";
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
