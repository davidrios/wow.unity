using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    public enum RenderingPipeline
    {
        URP,
        //BiRP
    }

    public class Settings : ScriptableObject
    {
        public static Settings getSettings ()
        {
            var settingsPath = "Assets/Settings/WoWUnitySettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<Settings>(settingsPath);
            if (settings == null)
            {
                settings = CreateInstance<Settings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        public RenderingPipeline renderingPipeline = RenderingPipeline.URP;

        [Tooltip("Create mesh collisions for all M2 that don't have an exported `.phys.obj`.")]
        public bool createCollisionForAllM2 = false;
    }
}