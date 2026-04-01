using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;


namespace NewVision.SSR
{


    public static class GlobalMySSRSettings
    {
        const string GlobalScaleShaderString = "_LimSSRGlobalScale";
        const string GlobalInverseScaleShaderString = "_LimSSRGlobalInvScale";
        private static float mGlobalScale = 1.0f;
        public static float GlobalResolutionScale
        {
            get
            {
                return mGlobalScale;
            }
            internal set
            {
                value = Mathf.Clamp(value, 0.1f, 2.0f);
                mGlobalScale = value;
                Shader.SetGlobalFloat(GlobalScaleShaderString, mGlobalScale);
                Shader.SetGlobalFloat(GlobalInverseScaleShaderString, 1.0f / mGlobalScale);
            }
        }
    }

    public struct SreenSpaceReflectionsSettings
    {


    }
    public enum DitherMode
    {
        Dither8x8,
        InterleavedGradient,
    }

    public enum RaytraceModes
    {
        LinearTracing = 0,
        HiZTracing = 1,
    }

    [System.Serializable]
    public class SSRSettings
    {
        [HideInInspector] public Material SSR_Instance;
        [HideInInspector] public Shader SSRShader;

        public float stepStrideLength = .03f;
        public float maxSteps = 128;
        [Range(0, 1)]
        public uint downSample = 0;
        public float minSmoothness = 0.5f;
        public bool reflectSky = true;

        public DitherMode ditherType = DitherMode.InterleavedGradient;
        public RaytraceModes tracingMode = RaytraceModes.LinearTracing;

    }

    [ExecuteAlways]
    public class MySSR : ScriptableRendererFeature
    {
        internal static MySSR ssrFeatureInstance;
        internal MySSRPass renderPass = null;
        [SerializeField] SSRSettings Settings = new SSRSettings();
        public static bool Enabled { get; set; } = true;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // 检查是否启用SSR
            if (!renderingData.cameraData.postProcessEnabled || !Enabled)
            {
                return;
            }
            
            // 检查是否找到SSR shader
            if (!GetMaterial())
            {
                Debug.LogError("Cannot find ssr shader!");
                return;
            }

            // 设置材质属性
            SetMaterialProperties(in renderingData);
            
            // 设置渲染目标
            renderPass.Source = renderer.cameraColorTarget;
            
            // 传递相机方向向量
            Settings.SSR_Instance.SetVector("_WorldSpaceViewDir", -renderingData.cameraData.camera.transform.forward);
            
            // 确保深度纹理可用（SSR依赖深度信息）
            renderingData.cameraData.camera.depthTextureMode |= (DepthTextureMode.MotionVectors | DepthTextureMode.Depth | DepthTextureMode.DepthNormals);
            
            // 设置渲染缩放
            float renderscale = renderingData.cameraData.isSceneViewCamera ? 1 : renderingData.cameraData.renderScale;
            renderPass.RenderScale = renderscale;

            // 传递SSR参数到shader
            Settings.SSR_Instance.SetFloat("stride", Settings.stepStrideLength); // 光线步进步长
            Settings.SSR_Instance.SetFloat("numSteps", Settings.maxSteps); // 最大光线追踪步数
            Settings.SSR_Instance.SetFloat("minSmoothness", Settings.minSmoothness); // 光滑度阈值
            Settings.SSR_Instance.SetInt("reflectSky", Settings.reflectSky ? 1 : 0); // 是否反射天空

            // 将SSR pass添加到渲染管线
            renderer.EnqueuePass(renderPass);
        }

        void SetMaterialProperties(in RenderingData renderingData)
        {
            var projectionMatrix = renderingData.cameraData.GetGPUProjectionMatrix();
            var viewMatrix = renderingData.cameraData.GetViewMatrix();
#if UNITY_EDITOR
            if (renderingData.cameraData.isSceneViewCamera)
            {
                Settings.SSR_Instance.SetFloat("_RenderScale", 1);
            }
            else
            {
                Settings.SSR_Instance.SetFloat("_RenderScale", renderingData.cameraData.renderScale);
            }
#else
            Settings.SSR_Instance.SetFloat("_RenderScale", renderingData.cameraData.renderScale);
#endif
            Settings.SSR_Instance.SetMatrix("_MyInverseProjectionMatrix", projectionMatrix.inverse);
            Settings.SSR_Instance.SetMatrix("_MyProjectionMatrix", projectionMatrix);
            Settings.SSR_Instance.SetMatrix("_MyInverseViewMatrix", viewMatrix.inverse);
            Settings.SSR_Instance.SetMatrix("_MyViewMatrix", viewMatrix);
        }

        public override void Create()
        {
            ssrFeatureInstance = this;
            renderPass = new MySSRPass()
            {
                // 设置渲染时机为不透明物体渲染之后，透明物体渲染之前
                // 这样可以确保SSR能够正确获取场景的深度和颜色信息
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents,
                Settings = this.Settings
            };
            
            // 确保获取SSR材质
            GetMaterial();
        }

        private bool GetMaterial()
        {
            if (Settings.SSR_Instance != null)
            {
                return true;
            }


            if (Settings.SSRShader == null)
            {
                Settings.SSRShader = Shader.Find("Hidden/SSR_Shader");
                if (Settings.SSRShader == null)
                {
                    return false;
                }
            }

            Settings.SSR_Instance = CoreUtils.CreateEngineMaterial(Settings.SSRShader);
            return Settings.SSR_Instance != null;
        }
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(Settings.SSR_Instance);
        }
    }


    public class MySSRPass : ScriptableRenderPass
    {
        static int frame = 0;
        int reflectionMapID;
        int tempPaddedSourceID;
        public RenderTargetIdentifier Source { get; internal set; }
        internal SSRSettings Settings { get; set; }
        internal float RenderScale { get; set; }
        //private float Scale => Settings.downSample + 1;

        private float PaddedScreenHeight;
        private float PaddedScreenWidth;
        private float ScreenHeight;
        private float ScreenWidth;
        private Vector2 PaddedScale;
        bool IsPadded => Settings.tracingMode == RaytraceModes.HiZTracing;
        private float Scale => Settings.downSample + 1;

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);
            Settings.SSR_Instance.SetInt("_Frame", frame);
            if (Settings.ditherType == DitherMode.InterleavedGradient)
            {
                Settings.SSR_Instance.SetInt("_DitherMode", 1);
            }
            else
            {
                Settings.SSR_Instance.SetInt("_DitherMode", 0);
            }

            GlobalMySSRSettings.GlobalResolutionScale = 1.0f / Scale;
            if (IsPadded)
            {
                ScreenHeight = cameraTextureDescriptor.height * GlobalMySSRSettings.GlobalResolutionScale;
                ScreenWidth = cameraTextureDescriptor.width * GlobalMySSRSettings.GlobalResolutionScale;
                PaddedScreenWidth = Mathf.NextPowerOfTwo((int)ScreenWidth);
                PaddedScreenHeight = Mathf.NextPowerOfTwo((int)ScreenHeight);
            }
            else
            {
                ScreenHeight = cameraTextureDescriptor.height;
                ScreenWidth = cameraTextureDescriptor.width;
                PaddedScreenWidth = ScreenWidth / Scale;
                PaddedScreenHeight = ScreenHeight / Scale;
            }

            cameraTextureDescriptor.colorFormat = RenderTextureFormat.DefaultHDR;
            cameraTextureDescriptor.mipCount = 8;
            cameraTextureDescriptor.autoGenerateMips = true;
            cameraTextureDescriptor.useMipMap = true;

            reflectionMapID = Shader.PropertyToID("_ReflectedColorMap");
            Vector2 screenResolution = new Vector2(ScreenWidth, ScreenHeight);
            Settings.SSR_Instance.SetVector("_ScreenResolution", screenResolution);
            if (IsPadded)
            {
                Vector2 paddedResolution = new Vector2(PaddedScreenWidth, PaddedScreenHeight);
                PaddedScale = paddedResolution / screenResolution;
                Settings.SSR_Instance.SetVector("_PaddedResolution", paddedResolution);
                Settings.SSR_Instance.SetVector("_PaddedScale", PaddedScale);

                float cx = 1.0f / (512.0f * paddedResolution.x);
                float cy = 1.0f / (512.0f * paddedResolution.y);

                Settings.SSR_Instance.SetVector("crossEpsilon", new Vector2(cx, cy));
            }
            else
            {
                PaddedScale = Vector2.one;
                Settings.SSR_Instance.SetVector("_PaddedScale", Vector2.one);
            }

            cmd.GetTemporaryRT(reflectionMapID, Mathf.CeilToInt(PaddedScreenWidth), Mathf.CeilToInt(PaddedScreenHeight), 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Default, 1, false);
            tempPaddedSourceID = Shader.PropertyToID("_TempPaddedSource");
            int tx = (int)(IsPadded ? Mathf.NextPowerOfTwo((int)cameraTextureDescriptor.width) : cameraTextureDescriptor.width);
            int ty = (int)(IsPadded ? Mathf.NextPowerOfTwo((int)cameraTextureDescriptor.height) : cameraTextureDescriptor.height);
            cameraTextureDescriptor.width = tx;
            cameraTextureDescriptor.height = ty;
            cmd.GetTemporaryRT(tempPaddedSourceID, cameraTextureDescriptor, FilterMode.Trilinear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 定义pass索引
            const int hiZPass = 2;      // HiZ光线追踪pass
            const int linearPass = 0;   // 线性光线追踪pass
            const int compPass = 1;     // 合成pass

            // 创建命令缓冲区
            CommandBuffer commandBuffer = CommandBufferPool.Get("Screen space reflections");
            
            // 1. 步骤1：将原始画面复制到临时纹理
            // 这是为了在合成时使用原始画面作为基础
            commandBuffer.Blit(Source, tempPaddedSourceID, PaddedScale, Vector2.zero);

            // 2. 步骤2：执行光线追踪
            // 根据设置选择不同的光线追踪模式
            if (Settings.tracingMode == RaytraceModes.HiZTracing)
            {
                // 使用HiZ（深度金字塔）加速的光线追踪
                commandBuffer.Blit(null, reflectionMapID, Settings.SSR_Instance, hiZPass);
            }
            else
            {
                // 使用传统的线性光线追踪
                commandBuffer.Blit(null, reflectionMapID, Settings.SSR_Instance, linearPass);
            }

            // 3. 步骤3：合成反射结果
            // 将反射效果与原始画面合成
            commandBuffer.Blit(tempPaddedSourceID, Source, Settings.SSR_Instance, compPass);

            // 4. 步骤4：清理临时资源
            commandBuffer.ReleaseTemporaryRT(reflectionMapID);
            commandBuffer.ReleaseTemporaryRT(tempPaddedSourceID);
            
            // 5. 步骤5：执行命令缓冲区
            context.ExecuteCommandBuffer(commandBuffer);
            
            // 6. 步骤6：释放命令缓冲区
            CommandBufferPool.Release(commandBuffer);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(reflectionMapID);
            cmd.ReleaseTemporaryRT(tempPaddedSourceID);
            frame++;
        }
    }

}


