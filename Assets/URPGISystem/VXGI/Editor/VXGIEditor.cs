using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace NewVision.VXGI.Editor
{
    [CustomEditor(typeof(MyVXGI))]
    public class VXGIEditor : UnityEditor.Editor
    {
        private SerializedProperty voxelizationShader;
        private SerializedProperty lightingShader;
        private SerializedProperty voxelMipmapCS;
        private SerializedProperty voxelBound;
        private SerializedProperty voxelResolution;
        private SerializedProperty followCamera;
        private SerializedProperty indirectDiffuseIntensity;
        private SerializedProperty indirectSpecularIntensity;
        private SerializedProperty coneTraceSteps;
        private SerializedProperty coneAperture;

        private void OnEnable()
        {
            voxelizationShader = serializedObject.FindProperty("voxelizationShader");
            lightingShader = serializedObject.FindProperty("lightingShader");
            voxelMipmapCS = serializedObject.FindProperty("voxelMipmapCS");
            voxelBound = serializedObject.FindProperty("voxelBound");
            voxelResolution = serializedObject.FindProperty("voxelResolution");
            followCamera = serializedObject.FindProperty("followCamera");
            indirectDiffuseIntensity = serializedObject.FindProperty("indirectDiffuseIntensity");
            indirectSpecularIntensity = serializedObject.FindProperty("indirectSpecularIntensity");
            coneTraceSteps = serializedObject.FindProperty("coneTraceSteps");
            coneAperture = serializedObject.FindProperty("coneAperture");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shader & Compute References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(voxelizationShader);
            EditorGUILayout.PropertyField(lightingShader);
            EditorGUILayout.PropertyField(voxelMipmapCS);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Voxel Volume Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(voxelBound);
            EditorGUILayout.PropertyField(voxelResolution);
            EditorGUILayout.PropertyField(followCamera);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VXGI Rendering Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(indirectDiffuseIntensity);
            EditorGUILayout.PropertyField(indirectSpecularIntensity);
            EditorGUILayout.PropertyField(coneTraceSteps);
            EditorGUILayout.PropertyField(coneAperture);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
