using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace NewVision.PCSS
{
    [CustomEditor(typeof(MyPCSS))]
    public class PCSSLightInspector : Editor
    {
        private SerializedProperty _settings;
        private SerializedProperty _blockerSampleCount;
        private SerializedProperty _pcfSampleCount;
        private SerializedProperty _softness;
        private SerializedProperty _softnessFalloff;
        private SerializedProperty _maxStaticGradientBias;
        private SerializedProperty _blockerGradientBias;
        private SerializedProperty _pcfGradientBias;
        private SerializedProperty _cascadeBlendDistance;
        private SerializedProperty _supportOrthographic;
        private SerializedProperty _noiseTexture;
        private SerializedProperty _pcssShader;

        private void OnEnable()
        {
            _settings = serializedObject.FindProperty("settings");
            _blockerSampleCount = _settings.FindPropertyRelative("BlockerSampleCount");
            _pcfSampleCount = _settings.FindPropertyRelative("PCFSampleCount");
            _softness = _settings.FindPropertyRelative("Softness");
            _softnessFalloff = _settings.FindPropertyRelative("SoftnessFalloff");
            _maxStaticGradientBias = _settings.FindPropertyRelative("MaxStaticGradientBias");
            _blockerGradientBias = _settings.FindPropertyRelative("BlockerGradientBias");
            _pcfGradientBias = _settings.FindPropertyRelative("PCFGradientBias");
            _cascadeBlendDistance = _settings.FindPropertyRelative("CascadeBlendDistance");
            _supportOrthographic = _settings.FindPropertyRelative("SupportOrthographic");
            _noiseTexture = _settings.FindPropertyRelative("NoiseTexture");
            _pcssShader = _settings.FindPropertyRelative("PCSSShader");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PCSS Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 采样设置
            EditorGUILayout.LabelField("采样设置", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_blockerSampleCount, new GUIContent("Blocker 采样数"));
            EditorGUILayout.PropertyField(_pcfSampleCount, new GUIContent("PCF 采样数"));
            EditorGUILayout.Space();

            // 柔和度
            EditorGUILayout.LabelField("柔和度", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_softness, new GUIContent("柔和度"));
            EditorGUILayout.PropertyField(_softnessFalloff, new GUIContent("柔和度衰减"));
            EditorGUILayout.Space();

            // 阴影偏移
            EditorGUILayout.LabelField("阴影偏移 (抗 Acne)", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_maxStaticGradientBias, new GUIContent("最大静态梯度偏移"));
            EditorGUILayout.PropertyField(_blockerGradientBias, new GUIContent("Blocker 梯度偏移"));
            EditorGUILayout.PropertyField(_pcfGradientBias, new GUIContent("PCF 梯度偏移"));
            EditorGUILayout.Space();

            // 级联混合
            EditorGUILayout.LabelField("级联混合", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_cascadeBlendDistance, new GUIContent("级联混合距离"));
            EditorGUILayout.PropertyField(_supportOrthographic, new GUIContent("支持正交投影"));
            EditorGUILayout.Space();

            // 资源
            EditorGUILayout.LabelField("资源", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_noiseTexture, new GUIContent("噪声纹理"));
            EditorGUILayout.PropertyField(_pcssShader, new GUIContent("PCSS 着色器"));
            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
