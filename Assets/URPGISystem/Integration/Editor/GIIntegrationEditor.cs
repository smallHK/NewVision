using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace NewVision.Integration.Editor
{
    [CustomEditor(typeof(GIIntegration))]
    public class GIIntegrationEditor : UnityEditor.Editor
    {
        private SerializedProperty _settings;
        private SerializedProperty _enablePCSS;
        private SerializedProperty _pcssSettings;
        private SerializedProperty _enableLTC;
        private SerializedProperty _ltcSettings;
        private SerializedProperty _enableIBL;
        private SerializedProperty _enablePRT;
        private SerializedProperty _enableVXGI;
        private SerializedProperty _enableSSAO;
        private SerializedProperty _enableSSR;
        private SerializedProperty _ssrSettings;

        private void OnEnable()
        {
            _settings = serializedObject.FindProperty("settings");
            _enablePCSS = _settings.FindPropertyRelative("enablePCSS");
            _pcssSettings = _settings.FindPropertyRelative("pcssSettings");
            _enableLTC = _settings.FindPropertyRelative("enableLTC");
            _ltcSettings = _settings.FindPropertyRelative("ltcSettings");
            _enableIBL = _settings.FindPropertyRelative("enableIBL");
            _enablePRT = _settings.FindPropertyRelative("enablePRT");
            _enableVXGI = _settings.FindPropertyRelative("enableVXGI");
            _enableSSAO = _settings.FindPropertyRelative("enableSSAO");
            _enableSSR = _settings.FindPropertyRelative("enableSSR");
            _ssrSettings = _settings.FindPropertyRelative("ssrSettings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("GI 集成设置", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 直接光照
            EditorGUILayout.LabelField("直接光照", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_enablePCSS, new GUIContent("启用 PCSS 软阴影"));
            if (_enablePCSS.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_pcssSettings, new GUIContent("PCSS 设置"), true);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(_enableLTC, new GUIContent("启用 LTC 面光源"));
            if (_enableLTC.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_ltcSettings, new GUIContent("LTC 设置"), true);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();

            // 环境光
            EditorGUILayout.LabelField("环境光", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_enableIBL, new GUIContent("启用 IBL 环境光"));
            EditorGUILayout.Space();

            // 间接光照
            EditorGUILayout.LabelField("间接光照", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_enablePRT, new GUIContent("启用 PRT 间接光"));
            EditorGUILayout.PropertyField(_enableVXGI, new GUIContent("启用 VXGI 间接光"));
            EditorGUILayout.Space();

            // 后处理
            EditorGUILayout.LabelField("后处理", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(_enableSSAO, new GUIContent("启用 SSAO (GTAO)"));
            EditorGUILayout.PropertyField(_enableSSR, new GUIContent("启用 SSR 屏幕空间反射"));
            if (_enableSSR.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_ssrSettings, new GUIContent("SSR 设置"), true);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();

            // 说明
            EditorGUILayout.LabelField("集成说明", EditorStyles.helpBox);
            EditorGUILayout.LabelField("- IBL: 环境光处理");
            EditorGUILayout.LabelField("- LTC: 面光源处理");
            EditorGUILayout.LabelField("- PRT: 间接光计算");
            EditorGUILayout.LabelField("- VXGI: 间接光计算（基于体素）");
            EditorGUILayout.LabelField("- PCSS: 直接光照软阴影");
            EditorGUILayout.LabelField("- SSAO: GTAO 环境光遮蔽");
            EditorGUILayout.LabelField("- SSR: 屏幕空间反射");
            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
