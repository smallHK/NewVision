using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditorInternal;

namespace NewVision.IBL.Editor
{
    /// <summary>
    /// IBL贴图生成器
    /// 负责从HDR环境贴图生成Irradiance Map、Prefilter Map和BRDF LUT
    /// 
    /// 工作流程：
    /// 1. HDR纹理 → Cubemap转换
    /// 2. Cubemap → Irradiance Map（漫反射辐照度卷积）
    /// 3. Cubemap → Prefilter Map（镜面反射预过滤）
    /// 4. BRDF LUT生成（独立于HDR）
    /// </summary>
    public static class IBLGenerator
    {
        #region 常量配置
        
        /// <summary>Irradiance Map分辨率 - 32x32足够，漫反射是低频信号</summary>
        private const int IRRADIANCE_MAP_SIZE = 32;
        
        /// <summary>Prefilter Map基础分辨率 - 每级mipmap减半</summary>
        private const int PREFILTER_MAP_SIZE = 128;
        
        /// <summary>BRDF LUT分辨率</summary>
        private const int BRDF_LUT_SIZE = 512;
        
        /// <summary>Prefilter Map的mipmap级别数 - 对应不同粗糙度</summary>
        private const int PREFILTER_MIP_LEVELS = 5;
        
        /// <summary>环境Cubemap分辨率</summary>
        private const int ENVCUBE_MAP_SIZE = 512;
        
        #endregion

        #region Shader引用
        
        private static Shader irradianceShader;
        private static Shader prefilterShader;
        private static Shader brdfShader;
        
        #endregion

        #region RenderDoc支持
        
        /// <summary>是否启用RenderDoc捕获</summary>
        public static bool enableRenderDocCapture = false;

        /// <summary>
        /// 获取GameView窗口
        /// RenderDoc捕获需要一个可见的窗口
        /// 不对窗口做任何更改
        /// </summary>
        private static EditorWindow GetGameView()
        {
            System.Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            return gameView;
        }
        
        #endregion

        #region 初始化
        
        /// <summary>
        /// 初始化所有需要的着色器引用
        /// 在每次生成操作前调用
        /// </summary>
        private static void InitShaders()
        {
            irradianceShader = Shader.Find("Hidden/NewVision/IBL/IrradianceConvolution");
            prefilterShader = Shader.Find("Hidden/NewVision/IBL/PrefilterConvolution");
            brdfShader = Shader.Find("Hidden/NewVision/IBL/BRDFIntegration");

            if (irradianceShader == null)
                Debug.LogError("IrradianceConvolution shader not found!");
            if (prefilterShader == null)
                Debug.LogError("PrefilterConvolution shader not found!");
            if (brdfShader == null)
                Debug.LogError("BRDFIntegration shader not found!");
        }
        
        #endregion

        #region HDR转Cubemap
        
        /// <summary>
        /// 将HDR纹理转换为Cubemap
        /// 
        /// 处理两种情况：
        /// 1. 输入已经是Cubemap类型 - 直接复制像素
        /// 2. 输入是等距圆柱投影HDR - 使用shader转换
        /// 
        /// 步骤：
        /// 1. 检测输入纹理类型
        /// 2. 创建目标Cubemap
        /// 3. 开始RenderDoc捕获（如果启用）
        /// 4. 使用EquirectangularToCubemap shader渲染6个面
        /// 5. 结束RenderDoc捕获
        /// 6. 读取像素写入Cubemap
        /// </summary>
        /// <param name="hdrTexture">输入的HDR纹理（可以是Cubemap或2D纹理）</param>
        /// <returns>转换后的Cubemap</returns>
        public static Cubemap ConvertHDRToCubemap(Texture hdrTexture)
        {
            // Step 1: 创建目标Cubemap
            Cubemap cubemap = new Cubemap(ENVCUBE_MAP_SIZE, TextureFormat.RGBAFloat, false);
            cubemap.name = hdrTexture.name + "_Cubemap";

            // Step 2: 如果输入已经是Cubemap，直接复制像素
            if (hdrTexture is Cubemap existingCubemap)
            {
                Debug.Log("[IBL] Input is already a Cubemap, copying pixels directly...");
                for (int face = 0; face < 6; face++)
                {
                    Color[] pixels = existingCubemap.GetPixels((CubemapFace)face, 0);
                    cubemap.SetPixels(pixels, (CubemapFace)face, 0);
                }
                cubemap.Apply();
                return cubemap;
            }

            // Step 3: 获取GameView用于RenderDoc捕获
            EditorWindow gameView = null;
            if (enableRenderDocCapture)
            {
                gameView = GetGameView();
            }

            // Step 4: 查找转换shader
            Shader equiShader = Shader.Find("Hidden/NewVision/IBL/EquirectangularToCubemap");
            if (equiShader == null)
            {
                Debug.LogError("EquirectangularToCubemap shader not found!");
                return null;
            }

            // Step 5: 创建材质并设置HDR纹理
            Material convertMaterial = new Material(equiShader);
            convertMaterial.SetTexture("_MainTex", hdrTexture);

            // Step 6: 创建临时渲染目标
            RenderTexture tempRT = RenderTexture.GetTemporary(ENVCUBE_MAP_SIZE, ENVCUBE_MAP_SIZE, 0, RenderTextureFormat.ARGBFloat);
            RenderTexture activeRT = RenderTexture.active;

            // Step 7: 创建临时Texture2D用于读取像素
            Texture2D tempTex = new Texture2D(ENVCUBE_MAP_SIZE, ENVCUBE_MAP_SIZE, TextureFormat.RGBAFloat, false);

            // Step 8: 开始RenderDoc捕获
            if (enableRenderDocCapture && gameView != null)
            {
                Debug.Log("=== [IBL] HDR to Cubemap Conversion START ===");
                RenderDoc.BeginCaptureRenderDoc(gameView);
            }

            // Step 9: 渲染Cubemap的6个面
            string[] faceNames = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };
            for (int face = 0; face < 6; face++)
            {
                // 9.1 RenderDoc事件标记 - 当前渲染的面
                if (enableRenderDocCapture)
                {
                    Debug.Log($"--- [HDR->Cubemap] Rendering Face {face}: {faceNames[face]} ---");
                }

                // 9.2 设置当前渲染的面索引
                convertMaterial.SetInt("_FaceIndex", face);

                // 9.3 执行Blit渲染当前面
                Graphics.Blit(null, tempRT, convertMaterial);

                // 9.4 从RenderTexture读取像素
                RenderTexture.active = tempRT;
                tempTex.ReadPixels(new Rect(0, 0, ENVCUBE_MAP_SIZE, ENVCUBE_MAP_SIZE), 0, 0);
                tempTex.Apply();

                // 9.5 将像素写入Cubemap对应面
                Color[] pixels = tempTex.GetPixels();
                cubemap.SetPixels(pixels, (CubemapFace)face, 0);
            }

            // Step 10: 结束RenderDoc捕获
            if (enableRenderDocCapture && gameView != null)
            {
                Debug.Log("=== [IBL] HDR to Cubemap Conversion END ===");
                RenderDoc.EndCaptureRenderDoc(gameView);
            }

            // Step 11: 清理临时资源
            RenderTexture.active = activeRT;
            RenderTexture.ReleaseTemporary(tempRT);
            Object.DestroyImmediate(tempTex);

            // Step 12: 应用Cubemap更改
            cubemap.Apply();
            Debug.Log($"[IBL] Cubemap converted: {cubemap.name}");
            return cubemap;
        }
        
        #endregion

        #region Irradiance Map生成
        
        /// <summary>
        /// 生成Irradiance Map（漫反射辐照度贴图）
        /// 
        /// 原理：
        /// - 对环境贴图进行半球卷积积分
        /// - 计算每个法线方向接收到的总辐照度
        /// - 用于PBR的漫反射间接光照
        /// 
        /// 步骤：
        /// 1. 初始化shader
        /// 2. 创建目标Cubemap（32x32）
        /// 3. 开始RenderDoc捕获（如果启用）
        /// 4. 渲染6个面，每个面执行半球卷积
        /// 5. 结束RenderDoc捕获
        /// 6. 保存为Asset文件
        /// </summary>
        /// <param name="sourceCubemap">源环境Cubemap</param>
        /// <param name="outputPath">输出Asset路径</param>
        /// <returns>生成的Irradiance Cubemap</returns>
        public static Cubemap GenerateIrradianceMap(Cubemap sourceCubemap, string outputPath)
        {
            // Step 1: 获取GameView用于RenderDoc捕获
            EditorWindow gameView = null;
            if (enableRenderDocCapture)
            {
                gameView = GetGameView();
            }

            // Step 2: 初始化shader
            InitShaders();
            if (irradianceShader == null) return null;

            // Step 3: 创建目标Irradiance Map（32x32，无mipmap）
            Cubemap irradianceMap = new Cubemap(IRRADIANCE_MAP_SIZE, TextureFormat.RGBAFloat, false);
            irradianceMap.name = sourceCubemap.name + "_Irradiance";

            // Step 4: 创建材质并设置源Cubemap
            Material irradianceMaterial = new Material(irradianceShader);
            irradianceMaterial.SetTexture("_MainTex", sourceCubemap);

            // Step 5: 创建临时渲染目标
            RenderTexture tempRT = RenderTexture.GetTemporary(IRRADIANCE_MAP_SIZE, IRRADIANCE_MAP_SIZE, 0, RenderTextureFormat.ARGBFloat);
            RenderTexture previousRT = RenderTexture.active;

            // Step 6: 创建临时Texture2D用于读取像素
            Texture2D tempTex = new Texture2D(IRRADIANCE_MAP_SIZE, IRRADIANCE_MAP_SIZE, TextureFormat.RGBAFloat, false);

            // Step 7: 开始RenderDoc捕获
            if (enableRenderDocCapture && gameView != null)
            {
                Debug.Log("=== [IBL] Irradiance Map Generation START ===");
                RenderDoc.BeginCaptureRenderDoc(gameView);
            }

            // Step 8: 渲染Cubemap的6个面
            string[] faceNames = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };
            for (int face = 0; face < 6; face++)
            {
                // 8.1 RenderDoc事件标记 - 当前渲染的面
                if (enableRenderDocCapture)
                {
                    Debug.Log($"--- [Irradiance] Rendering Face {face}: {faceNames[face]} ---");
                }

                // 8.2 设置当前渲染的面索引
                irradianceMaterial.SetInt("_FaceIndex", face);

                // 8.3 执行Blit - shader执行半球卷积
                Graphics.Blit(null, tempRT, irradianceMaterial);

                // 8.4 从RenderTexture读取像素
                RenderTexture.active = tempRT;
                tempTex.ReadPixels(new Rect(0, 0, IRRADIANCE_MAP_SIZE, IRRADIANCE_MAP_SIZE), 0, 0);
                tempTex.Apply();

                // 8.5 将像素写入Irradiance Map
                Color[] pixels = tempTex.GetPixels();
                irradianceMap.SetPixels(pixels, (CubemapFace)face, 0);
            }

            // Step 9: 结束RenderDoc捕获
            if (enableRenderDocCapture && gameView != null)
            {
                Debug.Log("=== [IBL] Irradiance Map Generation END ===");
                RenderDoc.EndCaptureRenderDoc(gameView);
            }

            // Step 10: 清理临时资源
            Object.DestroyImmediate(tempTex);
            RenderTexture.active = previousRT;
            RenderTexture.ReleaseTemporary(tempRT);

            // Step 11: 应用Irradiance Map更改
            irradianceMap.Apply();

            // Step 12: 保存为Asset文件
            AssetDatabase.CreateAsset(irradianceMap, outputPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[IBL] Irradiance Map generated: {outputPath}");
            return irradianceMap;
        }
        
        #endregion

        #region Prefilter Map生成
        
        /// <summary>
        /// 生成Prefilter Map（镜面反射预过滤贴图）
        /// 
        /// 原理：
        /// - 使用GGX重要性采样对环境贴图进行预过滤
        /// - 每级mipmap对应不同的粗糙度（0~1）
        /// - 用于PBR的镜面反射间接光照
        /// 
        /// 步骤：
        /// 1. 初始化shader
        /// 2. 创建目标Cubemap（128x128，5级mipmap）
        /// 3. 开始RenderDoc捕获（如果启用）
        /// 4. 遍历5级mipmap，每级使用不同粗糙度
        /// 5. 渲染6个面
        /// 6. 结束RenderDoc捕获
        /// 7. 保存为Asset文件
        /// </summary>
        /// <param name="sourceCubemap">源环境Cubemap</param>
        /// <param name="outputPath">输出Asset路径</param>
        /// <returns>生成的Prefilter Cubemap</returns>
        public static Cubemap GeneratePrefilterMap(Cubemap sourceCubemap, string outputPath)
        {
            // Step 1: 获取GameView用于RenderDoc捕获
            EditorWindow gameView = null;
            if (enableRenderDocCapture)
            {
                gameView = GetGameView();
            }

            // Step 2: 初始化shader
            InitShaders();
            if (prefilterShader == null) return null;

            // Step 3: 创建目标Prefilter Map（128x128，带mipmap）
            Cubemap prefilterMap = new Cubemap(PREFILTER_MAP_SIZE, TextureFormat.RGBAFloat, true);
            prefilterMap.name = sourceCubemap.name + "_Prefilter";

            // Step 4: 创建材质并设置源Cubemap
            Material prefilterMaterial = new Material(prefilterShader);
            prefilterMaterial.SetTexture("_MainTex", sourceCubemap);

            // Step 5: 保存当前RenderTexture
            RenderTexture previousRT = RenderTexture.active;

            // Step 6: 开始RenderDoc捕获
            if (enableRenderDocCapture && gameView != null)
            {
                Debug.Log("=== [IBL] Prefilter Map Generation START ===");
                RenderDoc.BeginCaptureRenderDoc(gameView);
            }

            // Step 7: 遍历每级mipmap
            string[] faceNames = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" };
            for (int mip = 0; mip < PREFILTER_MIP_LEVELS; mip++)
            {
                // 7.1 计算当前mipmap级别的分辨率和粗糙度
                int mipSize = PREFILTER_MAP_SIZE >> mip;
                float roughness = (float)mip / (float)(PREFILTER_MIP_LEVELS - 1);
                roughness = Mathf.Clamp(roughness, 0.0f, 1.0f);

                // 7.2 RenderDoc事件标记 - 当前mipmap级别
                if (enableRenderDocCapture)
                {
                    Debug.Log($"--- [Prefilter] Mip Level {mip}: Size={mipSize}x{mipSize}, Roughness={roughness:F2} ---");
                }

                // 7.3 设置粗糙度参数
                prefilterMaterial.SetFloat("_Roughness", roughness);

                // 7.4 创建临时渲染目标和Texture2D
                RenderTexture tempRT = RenderTexture.GetTemporary(mipSize, mipSize, 0, RenderTextureFormat.ARGBFloat);
                Texture2D tempTex = new Texture2D(mipSize, mipSize, TextureFormat.RGBAFloat, false);

                // 7.5 渲染Cubemap的6个面
                for (int face = 0; face < 6; face++)
                {
                    // 7.5.1 RenderDoc事件标记 - 当前渲染的面
                    if (enableRenderDocCapture)
                    {
                        Debug.Log($"    [Prefilter] Mip{mip} Face {face}: {faceNames[face]}");
                    }

                    // 7.5.2 设置当前渲染的面索引
                    prefilterMaterial.SetInt("_FaceIndex", face);

                    // 7.5.3 执行Blit - shader执行GGX重要性采样
                    Graphics.Blit(null, tempRT, prefilterMaterial);

                    // 7.5.4 从RenderTexture读取像素
                    RenderTexture.active = tempRT;
                    tempTex.ReadPixels(new Rect(0, 0, mipSize, mipSize), 0, 0);
                    tempTex.Apply();

                    // 7.5.5 将像素写入Prefilter Map对应mipmap级别
                    Color[] pixels = tempTex.GetPixels();
                    prefilterMap.SetPixels(pixels, (CubemapFace)face, mip);
                }

                // 7.6 清理当前mipmap的临时资源
                Object.DestroyImmediate(tempTex);
                RenderTexture.ReleaseTemporary(tempRT);
            }

            // Step 8: 结束RenderDoc捕获
            if (enableRenderDocCapture && gameView != null)
            {
                Debug.Log("=== [IBL] Prefilter Map Generation END ===");
                RenderDoc.EndCaptureRenderDoc(gameView);
            }

            // Step 9: 恢复RenderTexture
            RenderTexture.active = previousRT;

            // Step 10: 应用Prefilter Map更改
            prefilterMap.Apply();

            // Step 11: 保存为Asset文件
            AssetDatabase.CreateAsset(prefilterMap, outputPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[IBL] Prefilter Map generated: {outputPath}");
            return prefilterMap;
        }
        
        #endregion

        #region BRDF LUT生成
        
        /// <summary>
        /// 生成BRDF LUT（BRDF查找表）
        /// 
        /// 原理：
        /// - 预计算Cook-Torrance BRDF的积分结果
        /// - 输入：NdotV（法线与视线夹角余弦）和粗糙度
        /// - 输出：缩放因子(R通道)和偏移因子(G通道)
        /// 
        /// 步骤：
        /// 1. 初始化shader
        /// 2. 创建目标Texture2D（512x512）
        /// 3. 执行Blit渲染
        /// 4. 读取像素
        /// 5. 保存为Asset文件
        /// </summary>
        /// <param name="outputPath">输出Asset路径</param>
        /// <returns>生成的BRDF LUT纹理</returns>
        public static Texture2D GenerateBRDFLut(string outputPath)
        {
            // Step 1: 初始化shader
            InitShaders();
            if (brdfShader == null) return null;

            // Step 2: 创建目标BRDF LUT纹理
            Texture2D brdfLut = new Texture2D(BRDF_LUT_SIZE, BRDF_LUT_SIZE, TextureFormat.RGBAFloat, false);
            brdfLut.name = "BRDF_LUT";

            // Step 3: 创建材质
            Material brdfMaterial = new Material(brdfShader);

            // Step 4: 创建临时渲染目标
            RenderTexture tempRT = RenderTexture.GetTemporary(BRDF_LUT_SIZE, BRDF_LUT_SIZE, 0, RenderTextureFormat.ARGBFloat);
            RenderTexture previousRT = RenderTexture.active;

            // Step 5: 执行Blit - shader执行BRDF积分
            Graphics.Blit(null, tempRT, brdfMaterial);

            // Step 6: 从RenderTexture读取像素
            RenderTexture.active = tempRT;
            brdfLut.ReadPixels(new Rect(0, 0, BRDF_LUT_SIZE, BRDF_LUT_SIZE), 0, 0);

            // Step 7: 恢复RenderTexture并清理
            RenderTexture.active = previousRT;
            RenderTexture.ReleaseTemporary(tempRT);

            // Step 8: 应用BRDF LUT更改
            brdfLut.Apply();

            // Step 9: 保存为Asset文件
            AssetDatabase.CreateAsset(brdfLut, outputPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[IBL] BRDF LUT generated: {outputPath}");
            return brdfLut;
        }
        
        #endregion

        #region 综合生成接口
        
        /// <summary>
        /// 从HDR纹理生成完整的IBL资源
        /// 
        /// 步骤：
        /// 1. 转换HDR为Cubemap
        /// 2. 保存Cubemap
        /// 3. 生成Irradiance Map
        /// 4. 生成Prefilter Map
        /// 5. 生成BRDF LUT（如果不存在）
        /// </summary>
        /// <param name="hdrTexture">输入的HDR环境贴图</param>
        /// <param name="outputDirectory">输出目录</param>
        public static void GenerateAllFromHDR(Texture hdrTexture, string outputDirectory)
        {
            // Step 1: 构建输出路径
            string basePath = Path.Combine(outputDirectory, hdrTexture.name);

            // Step 2: 转换HDR为Cubemap
            Cubemap envCubemap = ConvertHDRToCubemap(hdrTexture);
            if (envCubemap == null)
            {
                Debug.LogError("[IBL] Failed to convert HDR to Cubemap");
                return;
            }

            // Step 3: 保存Cubemap
            string cubemapPath = basePath + "_Cubemap.asset";
            AssetDatabase.CreateAsset(envCubemap, cubemapPath);
            AssetDatabase.SaveAssets();

            // Step 4: 生成Irradiance Map
            string irradiancePath = basePath + "_Irradiance.asset";
            GenerateIrradianceMap(envCubemap, irradiancePath);

            // Step 5: 生成Prefilter Map
            string prefilterPath = basePath + "_Prefilter.asset";
            GeneratePrefilterMap(envCubemap, prefilterPath);

            // Step 6: 生成BRDF LUT（如果不存在）
            string brdfPath = Path.Combine(outputDirectory, "BRDF_LUT.asset");
            string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), brdfPath);
            if (!File.Exists(fullPath))
            {
                GenerateBRDFLut(brdfPath);
            }

            // Step 7: 刷新资源数据库
            AssetDatabase.Refresh();
        }
        
        #endregion
    }
}
