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
                serializedObject.FindProperty("spawnLayer"),
                new GUIContent("Layer to spawn foliage in", "Set to -1 to leave it unset."));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("playerTag"),
                new GUIContent("Player object tag", "The tag that the players object have. Will be used for distance calculation."));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("spawnDistance"),
                new GUIContent("Spawn distance (from player)"));
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