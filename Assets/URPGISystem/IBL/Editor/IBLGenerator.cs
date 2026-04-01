using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.IO;

namespace NewVision.IBL.Editor
{
    /// <summary>
    /// IBL贴图生成器
    /// 负责从HDR环境贴图生成Irradiance Map、Prefilter Map和BRDF LUT
    /// </summary>
    public static class IBLGenerator
    {
        private const int IRRADIANCE_MAP_SIZE = 32;
        private const int PREFILTER_MAP_SIZE = 128;
        private const int BRDF_LUT_SIZE = 512;
        private const int PREFILTER_MIP_LEVELS = 5;

        private static Shader irradianceShader;
        private static Shader prefilterShader;
        private static Shader brdfShader;

        /// <summary>
        /// 初始化着色器引用
        /// </summary>
        private static void InitShaders()
        {
            irradianceShader = Shader.Find("Hidden/NewVision/IBL/IrradianceConvolution");
            prefilterShader = Shader.Find("Hidden/NewVision/IBL/PrefilterConvolution");
            brdfShader = Shader.Find("Hidden/NewVision/IBL/BRDFIntegration");

            if (irradianceShader == null || prefilterShader == null || brdfShader == null)
            {
                Debug.LogError("IBL Shaders not found! Make sure IrradianceConvolution, PrefilterConvolution and BRDFIntegration shaders exist.");
            }
        }

        /// <summary>
        /// 从HDR纹理生成Cubemap
        /// </summary>
        /// <param name="hdrTexture">HDR环境贴图</param>
        /// <returns>生成的Cubemap</returns>
        private static Cubemap ConvertHDRToCubemap(Texture hdrTexture)
        {
            int size = 512;
            Cubemap cubemap = new Cubemap(size, TextureFormat.RGBAFloat, false);
            cubemap.name = hdrTexture.name + "_Cubemap";

            Shader equiShader = Shader.Find("Hidden/NewVision/IBL/EquirectangularToCubemap");
            if (equiShader == null)
            {
                Debug.LogError("EquirectangularToCubemap shader not found!");
                return null;
            }

            Material convertMaterial = new Material(equiShader);
            convertMaterial.SetTexture("_MainTex", hdrTexture);

            RenderTexture tempRT = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBFloat);
            RenderTexture activeRT = RenderTexture.active;

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

            RenderTexture.active = activeRT;
            RenderTexture.ReleaseTemporary(tempRT);
            Object.DestroyImmediate(tempTex);

            cubemap.Apply();
            return cubemap;
        }

        /// <summary>
        /// 生成Irradiance Map
        /// 对环境贴图进行漫反射卷积
        /// </summary>
        /// <param name="sourceCubemap">源环境Cubemap</param>
        /// <param name="outputPath">输出路径</param>
        /// <returns>生成的Irradiance Cubemap</returns>
        public static Cubemap GenerateIrradianceMap(Cubemap sourceCubemap, string outputPath)
        {
            InitShaders();
            if (irradianceShader == null) return null;

            Cubemap irradianceMap = new Cubemap(IRRADIANCE_MAP_SIZE, TextureFormat.RGBAFloat, false);
            irradianceMap.name = sourceCubemap.name + "_Irradiance";

            Material irradianceMaterial = new Material(irradianceShader);
            irradianceMaterial.SetTexture("_MainTex", sourceCubemap);

            RenderTexture tempRT = RenderTexture.GetTemporary(IRRADIANCE_MAP_SIZE, IRRADIANCE_MAP_SIZE, 0, RenderTextureFormat.ARGBFloat);
            RenderTexture previousRT = RenderTexture.active;

            Texture2D tempTex = new Texture2D(IRRADIANCE_MAP_SIZE, IRRADIANCE_MAP_SIZE, TextureFormat.RGBAFloat, false);

            for (int face = 0; face < 6; face++)
            {
                RenderTexture.active = tempRT;
                RenderCubemapFace(sourceCubemap, (CubemapFace)face, irradianceMaterial, tempRT);

                tempTex.ReadPixels(new Rect(0, 0, IRRADIANCE_MAP_SIZE, IRRADIANCE_MAP_SIZE), 0, 0);
                tempTex.Apply();

                Color[] pixels = tempTex.GetPixels();
                irradianceMap.SetPixels(pixels, (CubemapFace)face, 0);
            }

            Object.DestroyImmediate(tempTex);
            RenderTexture.active = previousRT;
            RenderTexture.ReleaseTemporary(tempRT);

            irradianceMap.Apply();

            AssetDatabase.CreateAsset(irradianceMap, outputPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Irradiance Map generated: {outputPath}");
            return irradianceMap;
        }

        /// <summary>
        /// 生成Prefilter Map
        /// 对环境贴图进行镜面反射预过滤，生成多级Mipmap
        /// </summary>
        /// <param name="sourceCubemap">源环境Cubemap</param>
        /// <param name="outputPath">输出路径</param>
        /// <returns>生成的Prefilter Cubemap</returns>
        public static Cubemap GeneratePrefilterMap(Cubemap sourceCubemap, string outputPath)
        {
            InitShaders();
            if (prefilterShader == null) return null;

            Cubemap prefilterMap = new Cubemap(PREFILTER_MAP_SIZE, TextureFormat.RGBAFloat, true);
            prefilterMap.name = sourceCubemap.name + "_Prefilter";

            Material prefilterMaterial = new Material(prefilterShader);
            prefilterMaterial.SetTexture("_MainTex", sourceCubemap);

            RenderTexture previousRT = RenderTexture.active;

            for (int mip = 0; mip < PREFILTER_MIP_LEVELS; mip++)
            {
                int mipSize = PREFILTER_MAP_SIZE >> mip;
                float roughness = (float)mip / (float)(PREFILTER_MIP_LEVELS - 1);
                roughness = Mathf.Clamp(roughness, 0.0f, 1.0f);

                prefilterMaterial.SetFloat("_Roughness", roughness);

                RenderTexture tempRT = RenderTexture.GetTemporary(mipSize, mipSize, 0, RenderTextureFormat.ARGBFloat);
                Texture2D tempTex = new Texture2D(mipSize, mipSize, TextureFormat.RGBAFloat, false);

                for (int face = 0; face < 6; face++)
                {
                    RenderTexture.active = tempRT;
                    RenderCubemapFace(sourceCubemap, (CubemapFace)face, prefilterMaterial, tempRT);

                    tempTex.ReadPixels(new Rect(0, 0, mipSize, mipSize), 0, 0);
                    tempTex.Apply();

                    Color[] pixels = tempTex.GetPixels();
                    prefilterMap.SetPixels(pixels, (CubemapFace)face, mip);
                }

                Object.DestroyImmediate(tempTex);
                RenderTexture.ReleaseTemporary(tempRT);
            }

            RenderTexture.active = previousRT;

            prefilterMap.Apply();

            AssetDatabase.CreateAsset(prefilterMap, outputPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Prefilter Map generated: {outputPath}");
            return prefilterMap;
        }

        /// <summary>
        /// 生成BRDF LUT
        /// 预计算BRDF积分查找表
        /// </summary>
        /// <param name="outputPath">输出路径</param>
        /// <returns>生成的BRDF LUT纹理</returns>
        public static Texture2D GenerateBRDFLut(string outputPath)
        {
            InitShaders();
            if (brdfShader == null) return null;

            Texture2D brdfLut = new Texture2D(BRDF_LUT_SIZE, BRDF_LUT_SIZE, TextureFormat.RGBAFloat, false);
            brdfLut.name = "BRDF_LUT";

            Material brdfMaterial = new Material(brdfShader);

            RenderTexture tempRT = RenderTexture.GetTemporary(BRDF_LUT_SIZE, BRDF_LUT_SIZE, 0, RenderTextureFormat.ARGBFloat);
            RenderTexture previousRT = RenderTexture.active;

            Graphics.Blit(null, tempRT, brdfMaterial);

            RenderTexture.active = tempRT;
            brdfLut.ReadPixels(new Rect(0, 0, BRDF_LUT_SIZE, BRDF_LUT_SIZE), 0, 0);

            RenderTexture.active = previousRT;
            RenderTexture.ReleaseTemporary(tempRT);

            brdfLut.Apply();

            AssetDatabase.CreateAsset(brdfLut, outputPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"BRDF LUT generated: {outputPath}");
            return brdfLut;
        }

        /// <summary>
        /// 渲染Cubemap的一个面
        /// </summary>
        private static void RenderCubemapFace(Cubemap source, CubemapFace face, Material material, RenderTexture target)
        {
            Vector3[] directions = new Vector3[]
            {
                Vector3.right,
                Vector3.left,
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.back
            };

            Vector3[] upVectors = new Vector3[]
            {
                Vector3.up,
                Vector3.up,
                Vector3.forward,
                Vector3.back,
                Vector3.up,
                Vector3.up
            };

            RenderTexture.active = target;
            GL.Clear(true, true, Color.black);

            GL.PushMatrix();
            GL.LoadProjectionMatrix(Matrix4x4.Perspective(90f, 1f, 0.1f, 100f));

            material.SetPass(0);

            DrawFullscreenQuad();

            GL.PopMatrix();
        }

        /// <summary>
        /// 绘制全屏四边形
        /// </summary>
        private static void DrawFullscreenQuad()
        {
            GL.Begin(GL.QUADS);
            GL.TexCoord2(0, 0);
            GL.Vertex3(-1, -1, 0);
            GL.TexCoord2(1, 0);
            GL.Vertex3(1, -1, 0);
            GL.TexCoord2(1, 1);
            GL.Vertex3(1, 1, 0);
            GL.TexCoord2(0, 1);
            GL.Vertex3(-1, 1, 0);
            GL.End();
        }

        /// <summary>
        /// 从HDR纹理生成完整的IBL资源
        /// </summary>
        /// <param name="hdrTexture">HDR环境贴图</param>
        /// <param name="outputDirectory">输出目录</param>
        public static void GenerateAllFromHDR(Texture hdrTexture, string outputDirectory)
        {
            string basePath = Path.Combine(outputDirectory, hdrTexture.name);

            Cubemap envCubemap = ConvertHDRToCubemap(hdrTexture);
            if (envCubemap == null)
            {
                Debug.LogError("Failed to convert HDR to Cubemap");
                return;
            }

            string cubemapPath = basePath + "_Cubemap.asset";
            AssetDatabase.CreateAsset(envCubemap, cubemapPath);
            AssetDatabase.SaveAssets();

            string irradiancePath = basePath + "_Irradiance.asset";
            GenerateIrradianceMap(envCubemap, irradiancePath);

            string prefilterPath = basePath + "_Prefilter.asset";
            GeneratePrefilterMap(envCubemap, prefilterPath);

            string brdfPath = Path.Combine(outputDirectory, "BRDF_LUT.asset");
            string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), brdfPath);
            if (!File.Exists(fullPath))
            {
                GenerateBRDFLut(brdfPath);
            }

            AssetDatabase.Refresh();
        }
    }
}
