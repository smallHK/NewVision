using UnityEngine;
using UnityEditor;

namespace NewVision.IBL.Editor
{
    /// <summary>
    /// IBL设置窗口
    /// 提供IBL相关设置和快捷操作
    /// </summary>
    public class IBLSettingsWindow : EditorWindow
    {
        [MenuItem("Tools/IBL/Settings", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<IBLSettingsWindow>("IBL Settings");
            window.minSize = new Vector2(300, 200);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("IBL Generator Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("RenderDoc Capture", EditorStyles.boldLabel);
            
            IBLGenerator.enableRenderDocCapture = EditorGUILayout.Toggle(
                "Enable RenderDoc Capture", 
                IBLGenerator.enableRenderDocCapture
            );

            EditorGUILayout.HelpBox(
                "To capture shader execution in RenderDoc:\n" +
                "1. Launch Unity through RenderDoc\n" +
                "2. Press F12 or use RenderDoc UI to capture\n" +
                "3. Generate IBL textures via right-click menu\n" +
                "4. Check RenderDoc for captured frames",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Open IBL Directory"))
            {
                string iblPath = System.IO.Path.Combine(Application.dataPath, "URPGISystem/IBL");
                if (System.IO.Directory.Exists(iblPath))
                {
                    EditorUtility.RevealInFinder(iblPath);
                }
                else
                {
                    Debug.LogWarning("IBL directory not found.");
                }
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Usage Instructions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Select HDR file (.hdr, .exr, .hdri) in Project window\n" +
                "2. Right-click → IBL → Generate All IBL Maps\n" +
                "3. Generated textures will be in same directory as HDR file\n" +
                "4. Assign textures to IBL material",
                MessageType.Info
            );
        }
    }
}
