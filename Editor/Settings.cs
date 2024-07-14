using System.IO;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    public enum RenderingPipeline
    {
        URP,
        BiRP
    }

    public class Settings : ScriptableObject
    {
        const string SettingsPath = "Assets/Settings/WoWUnitySettings.asset";

        private static Settings instance;

        public static Settings GetSettings()
        {
            if (instance != null)
                return instance;

            instance = AssetDatabase.LoadAssetAtPath<Settings>(SettingsPath);
            if (instance != null)
                return instance;

            var settingsDir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);

            instance = CreateInstance<Settings>();
            AssetDatabase.CreateAsset(instance, SettingsPath);
            AssetDatabase.SaveAssets();

            return instance;
        }

        public RenderingPipeline renderingPipeline = RenderingPipeline.URP;

        [Tooltip("Create mesh collisions for all M2 that don't have an exported `.phys.obj`.")]
        public bool createCollisionForAllM2 = false;
    }
}