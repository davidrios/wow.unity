using UnityEditor;
using UnityEngine;

namespace WowUnity
{
    [CustomEditor(typeof(FoliageSettings))]
    public class FoliageSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("rayLayer"),
                new GUIContent("Raycast layer"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("rayPreventLayerMask"),
                new GUIContent("Prevent raycast layer mask"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("spawnDistance"),
                new GUIContent("Spawn distance (from player)"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("poolDistance"),
                new GUIContent("Pool reclaim distance"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("densityFactor"),
                new GUIContent("Density factor", "Density factor when placing the foliage"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("castsShadows"),
                new GUIContent("Casts shadows"));

            if (GUILayout.Button("Apply and Respawn"))
                Foliage.FoliageSpawner.RespawnAll();

            serializedObject.ApplyModifiedProperties();
        }
    }
}