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
        public int foliageRayPreventLayerMask = 0;
        public float foliageSpawnDistance = 70f;
        public float foliagePoolDistance = 71f;
        public float foliageDensityFactor = 0.6f;
        public bool foliageSetupLODs = true;
        public float foliageCullWidth = 7;
        public bool foliageCastsShadows = true;
    }

    [CustomEditor(typeof(RuntimeSettings))]
    public class SettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = RuntimeSettings.GetSettings();

            GUILayout.Label("Foliage", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("foliageRayLayer"),
                new GUIContent("Raycast layer"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("foliageRayPreventLayerMask"),
                new GUIContent("Prevent raycast layer mask"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("foliageSpawnDistance"),
                new GUIContent("Spawn distance (from player)"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("foliagePoolDistance"),
                new GUIContent("Pool reclaim distance"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("foliageDensityFactor"),
                new GUIContent("Density factor", "Density factor when placing the foliage"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("foliageSetupLODs"),
                new GUIContent("Set up LOD levels", "Attach LOD Group components to placed foliage"));
            if (settings.foliageSetupLODs)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("foliageCullWidth"), new GUIContent("Cull Transition (% Screen Size)"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("foliageCastsShadows"),
                new GUIContent("Casts shadows"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}