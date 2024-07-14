using System.IO;
using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    public class RuntimeSettings : ScriptableObject
    {
        const string SettingsPath = "Assets/Settings/WoWUnityRuntimeSettings.asset";

        private static RuntimeSettings instance;

        public static RuntimeSettings GetSettings()
        {
            if (instance != null)
                return instance;

            instance = AssetDatabase.LoadAssetAtPath<RuntimeSettings>(SettingsPath);
            if (instance != null)
                return instance;

            var settingsDir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);

            instance = CreateInstance<RuntimeSettings>();
            AssetDatabase.CreateAsset(instance, SettingsPath);
            AssetDatabase.SaveAssets();

            return instance;
        }

        public int foliageRayLayer = 6;
    }
}