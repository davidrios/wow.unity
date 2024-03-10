using Newtonsoft.Json;
using System.Collections;
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
    }
}
