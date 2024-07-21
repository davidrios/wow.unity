using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    public class ModelUtility
    {
        public static void SetupLODGroup(string path)
        {
            var prefab = M2Utility.FindPrefab(path);
            var prefabInst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (SetupLODGroup(prefabInst))
            {
                PrefabUtility.ApplyPrefabInstance(prefabInst, InteractionMode.AutomatedAction);
                PrefabUtility.SavePrefabAsset(prefab);
            }

            Object.DestroyImmediate(prefabInst);
        }

        public static bool SetupLODGroup(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<LODGroup>(out var _))
                return false;

            var renderers = gameObject.GetComponentsInChildren<Renderer>();
            var lodGroup = gameObject.AddComponent<LODGroup>();
            var lods = lodGroup.GetLODs();
            lods[0].renderers = renderers;
            lods[1].renderers = lods[0].renderers;
            lods[2].renderers = lods[0].renderers;
            lodGroup.SetLODs(lods);

            return true;
        }
    }
}
