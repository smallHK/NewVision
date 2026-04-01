using UnityEngine;
using UnityEditor;
using System.IO;

namespace NewVision.IBL.Editor
{
    /// <summary>
    /// IBL右键菜单扩展
    /// 在Project窗口中为HDR纹理添加IBL生成选项
    /// </summary>
    public static class IBLContextMenu
    {
        private const string MENU_PATH = "Assets/IBL";
        private const string IRRADIANCE_MENU = "Assets/IBL/Generate Irradiance Map";
        private const string PREFILTER_MENU = "Assets/IBL/Generate Prefilter Map";
        private const string ALL_MENU = "Assets/IBL/Generate All IBL Maps";

        /// <summary>
        /// 验证是否为HDR纹理
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

        /// <summary>
        /// 生成Irradiance Map菜单项
        /// 直接在HDR所在目录生成
        /// </summary>
        [MenuItem(IRRADIANCE_MENU, false, 1)]
        private static void GenerateIrradianceMap()
        {
            Texture hdrTexture = Selection.activeObject as Texture;
            if (hdrTexture == null) return;

            string sourcePath = AssetDatabase.GetAssetPath(hdrTexture);
            string directory = Path.GetDirectoryName(sourcePath);
            string outputPath = Path.Combine(directory, hdrTexture.name + "_Irradiance.asset");

            EditorUtility.DisplayProgressBar("IBL Generator", "Generating Irradiance Map...", 0.5f);

            try
            {
                Cubemap envCubemap = CreateTempCubemapFromHDR(hdrTexture);
                IBLGenerator.GenerateIrradianceMap(envCubemap, outputPath);

                if (envCubemap != null)
                {
                    Object.DestroyImmediate(envCubemap);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 生成Prefilter Map菜单项
        /// 直接在HDR所在目录生成
        /// </summary>
        [MenuItem(PREFILTER_MENU, false, 2)]
        private static void GeneratePrefilterMap()
        {
            Texture hdrTexture = Selection.activeObject as Texture;
            if (hdrTexture == null) return;

            string sourcePath = AssetDatabase.GetAssetPath(hdrTexture);
            string directory = Path.GetDirectoryName(sourcePath);
            string outputPath = Path.Combine(directory, hdrTexture.name + "_Prefilter.asset");

            EditorUtility.DisplayProgressBar("IBL Generator", "Generating Prefilter Map...", 0.5f);

            try
            {
                Cubemap envCubemap = CreateTempCubemapFromHDR(hdrTexture);
                IBLGenerator.GeneratePrefilterMap(envCubemap, outputPath);

                if (envCubemap != null)
                {
                    Object.DestroyImmediate(envCubemap);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 生成所有IBL贴图菜单项
        /// 直接在HDR所在目录生成
        /// </summary>
        [MenuItem(ALL_MENU, false, 3)]
        private static void GenerateAllIBLMaps()
        {
            Texture hdrTexture = Selection.activeObject as Texture;
            if (hdrTexture == null) return;

            string sourcePath = AssetDatabase.GetAssetPath(hdrTexture);
            string directory = Path.GetDirectoryName(sourcePath);

            EditorUtility.DisplayProgressBar("IBL Generator", "Generating all IBL maps...", 0.3f);

            try
            {
                IBLGenerator.GenerateAllFromHDR(hdrTexture, directory);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 从HDR纹理创建临时Cubemap
        /// </summary>
        private static Cubemap CreateTempCubemapFromHDR(Texture hdrTexture)
        {
            int size = 512;
            Cubemap cubemap = new Cubemap(size, TextureFormat.RGBAFloat, true);

            Shader equiToCubeShader = Shader.Find("Hidden/NewVision/IBL/EquirectangularToCubemap");
            if (equiToCubeShader == null)
            {
                Debug.LogError("EquirectangularToCubemap shader not found!");
                return null;
            }

            Material convertMaterial = new Material(equiToCubeShader);
            convertMaterial.SetTexture("_MainTex", hdrTexture);

            RenderTexture tempRT = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBFloat);
            RenderTexture previousRT = RenderTexture.active;

            Texture2D tempTex = new Texture2D(size, size, TextureFormat.RGBAFloat, false);

            for (int face = 0; face < 6; face++)
            {
                convertMaterial.SetInt("_FaceIndex", face);
                Graphics.Blit(null, tempRT, convertMaterial);

                RenderTexture.active = tempRT;
                tempTex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tempTex.Apply();

                Color[] pixels = tempTex.GetPixels();
                cubemap.SetPixels(pixels, (CubemapFace)face, 0);
            }

            Object.DestroyImmediate(tempTex);
            RenderTexture.active = previousRT;
            RenderTexture.ReleaseTemporary(tempRT);

            cubemap.Apply();
            return cubemap;
        }
    }
}
