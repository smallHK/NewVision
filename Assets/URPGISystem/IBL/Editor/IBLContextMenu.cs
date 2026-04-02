using UnityEngine;
using UnityEditor;
using System.IO;

namespace NewVision.IBL.Editor
{
    /// <summary>
    /// IBL右键菜单扩展
    /// 在Project窗口中为HDR纹理添加IBL生成选项
    /// 
    /// 功能：
    /// - 检测选中的HDR文件（.hdr, .exr, .hdri）
    /// - 提供三个菜单选项：生成Irradiance Map、Prefilter Map、全部生成
    /// - 提供RenderDoc捕获开关
    /// - 自动在HDR文件所在目录生成对应的IBL贴图
    /// </summary>
    public static class IBLContextMenu
    {
        #region 菜单路径常量
        
        private const string MENU_PATH = "Assets/IBL";
        private const string IRRADIANCE_MENU = "Assets/IBL/Generate Irradiance Map";
        private const string PREFILTER_MENU = "Assets/IBL/Generate Prefilter Map";
        private const string ALL_MENU = "Assets/IBL/Generate All IBL Maps";
        private const string RENDERDOC_TOGGLE_MENU = "Assets/IBL/Enable RenderDoc Capture";
        
        #endregion

        #region RenderDoc控制菜单
        
        /// <summary>
        /// 切换RenderDoc捕获状态
        /// 用于在生成IBL纹理时捕获shader执行过程
        /// </summary>
        [MenuItem(RENDERDOC_TOGGLE_MENU, false, 0)]
        private static void ToggleRenderDocCapture()
        {
            IBLGenerator.enableRenderDocCapture = !IBLGenerator.enableRenderDocCapture;
            Menu.SetChecked(RENDERDOC_TOGGLE_MENU, IBLGenerator.enableRenderDocCapture);
            
            if (IBLGenerator.enableRenderDocCapture)
            {
                Debug.Log("[IBL] RenderDoc capture enabled. Capture will be triggered when generating IBL textures.");
            }
            else
            {
                Debug.Log("[IBL] RenderDoc capture disabled.");
            }
        }
        
        /// <summary>
        /// 验证RenderDoc菜单状态
        /// 确保菜单显示的勾选状态与实际值同步
        /// </summary>
        [MenuItem(RENDERDOC_TOGGLE_MENU, true)]
        private static bool ValidateRenderDocToggle()
        {
            Menu.SetChecked(RENDERDOC_TOGGLE_MENU, IBLGenerator.enableRenderDocCapture);
            return true;
        }
        
        #endregion

        #region 菜单验证
        
        /// <summary>
        /// 验证是否为HDR纹理
        /// 用于控制菜单项的启用/禁用状态
        /// 
        /// 验证条件：
        /// 1. 选中对象不为空
        /// 2. 选中对象有有效的Asset路径
        /// 3. 文件扩展名为 .hdr、.exr 或 .hdri
        /// 4. 选中对象是Texture类型
        /// </summary>
        [MenuItem(IRRADIANCE_MENU, true)]
        [MenuItem(PREFILTER_MENU, true)]
        [MenuItem(ALL_MENU, true)]
        private static bool ValidateHDRTexture()
        {
            if (Selection.activeObject == null) return false;

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path)) return false;

            string extension = Path.GetExtension(path).ToLower();
            bool isHDR = extension == ".hdr" || extension == ".exr" || extension == ".hdri";

            if (!isHDR) return false;

            Texture texture = Selection.activeObject as Texture;
            return texture != null;
        }
        
        #endregion

        #region Irradiance Map生成菜单
        
        /// <summary>
        /// 生成Irradiance Map菜单项
        /// 直接在HDR所在目录生成
        /// 
        /// 步骤：
        /// 1. 获取选中的HDR纹理
        /// 2. 转换HDR为Cubemap（使用IBLGenerator.ConvertHDRToCubemap）
        /// 3. 调用IBLGenerator生成Irradiance Map
        /// 4. 清理临时Cubemap
        /// </summary>
        [MenuItem(IRRADIANCE_MENU, false, 1)]
        private static void GenerateIrradianceMap()
        {
            // Step 1: 获取选中的HDR纹理
            Texture hdrTexture = Selection.activeObject as Texture;
            if (hdrTexture == null) return;

            // Step 2: 构建输出路径
            string sourcePath = AssetDatabase.GetAssetPath(hdrTexture);
            string directory = Path.GetDirectoryName(sourcePath);
            string outputPath = Path.Combine(directory, hdrTexture.name + "_Irradiance.asset");

            EditorUtility.DisplayProgressBar("IBL Generator", "Generating Irradiance Map...", 0.5f);

            try
            {
                // Step 3: 转换HDR为Cubemap（关键修复：使用IBLGenerator的方法）
                Cubemap envCubemap = IBLGenerator.ConvertHDRToCubemap(hdrTexture);
                if (envCubemap != null)
                {
                    // Step 4: 生成Irradiance Map
                    IBLGenerator.GenerateIrradianceMap(envCubemap, outputPath);
                    
                    // Step 5: 清理临时Cubemap
                    Object.DestroyImmediate(envCubemap);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
        }
        
        #endregion

        #region Prefilter Map生成菜单
        
        /// <summary>
        /// 生成Prefilter Map菜单项
        /// 直接在HDR所在目录生成
        /// 
        /// 步骤：
        /// 1. 获取选中的HDR纹理
        /// 2. 转换HDR为Cubemap（使用IBLGenerator.ConvertHDRToCubemap）
        /// 3. 调用IBLGenerator生成Prefilter Map
        /// 4. 清理临时Cubemap
        /// </summary>
        [MenuItem(PREFILTER_MENU, false, 2)]
        private static void GeneratePrefilterMap()
        {
            // Step 1: 获取选中的HDR纹理
            Texture hdrTexture = Selection.activeObject as Texture;
            if (hdrTexture == null) return;

            // Step 2: 构建输出路径
            string sourcePath = AssetDatabase.GetAssetPath(hdrTexture);
            string directory = Path.GetDirectoryName(sourcePath);
            string outputPath = Path.Combine(directory, hdrTexture.name + "_Prefilter.asset");

            EditorUtility.DisplayProgressBar("IBL Generator", "Generating Prefilter Map...", 0.5f);

            try
            {
                // Step 3: 转换HDR为Cubemap（关键修复：使用IBLGenerator的方法）
                Cubemap envCubemap = IBLGenerator.ConvertHDRToCubemap(hdrTexture);
                if (envCubemap != null)
                {
                    // Step 4: 生成Prefilter Map
                    IBLGenerator.GeneratePrefilterMap(envCubemap, outputPath);
                    
                    // Step 5: 清理临时Cubemap
                    Object.DestroyImmediate(envCubemap);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
        }
        
        #endregion

        #region 全部生成菜单
        
        /// <summary>
        /// 生成所有IBL贴图菜单项
        /// 直接在HDR所在目录生成
        /// 
        /// 步骤：
        /// 1. 获取选中的HDR纹理
        /// 2. 构建输出目录
        /// 3. 调用IBLGenerator.GenerateAllFromHDR生成所有贴图
        /// </summary>
        [MenuItem(ALL_MENU, false, 3)]
        private static void GenerateAllIBLMaps()
        {
            // Step 1: 获取选中的HDR纹理
            Texture hdrTexture = Selection.activeObject as Texture;
            if (hdrTexture == null) return;

            // Step 2: 构建输出目录
            string sourcePath = AssetDatabase.GetAssetPath(hdrTexture);
            string directory = Path.GetDirectoryName(sourcePath);

            EditorUtility.DisplayProgressBar("IBL Generator", "Generating all IBL maps...", 0.3f);

            try
            {
                // Step 3: 生成所有IBL贴图
                IBLGenerator.GenerateAllFromHDR(hdrTexture, directory);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
        }
        
        #endregion
    }
}
