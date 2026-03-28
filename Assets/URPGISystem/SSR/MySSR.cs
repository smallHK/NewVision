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
    internal class SSRSettings
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
            if (!renderingData.cameraData.postProcessEnabled || !Enabled)
            {
                return;
            }
            if (!GetMaterial())
            {
                Debug.LogError("Cannot find ssr shader!");
                return;
            }

            SetMaterialProperties(in renderingData);
            renderPass.Source = renderer.cameraColorTarget;
            Settings.SSR_Instance.SetVector("_WorldSpaceViewDir", renderingData.cameraData.camera.transform.forward);
            renderingData.cameraData.camera.depthTextureMode |= (DepthTextureMode.MotionVectors | DepthTextureMode.Depth | DepthTextureMode.DepthNormals);
            float renderscale = renderingData.cameraData.isSceneViewCamera ? 1 : renderingData.cameraData.renderScale;
            renderPass.RenderScale = renderscale;

            Settings.SSR_Instance.SetFloat("stride", Settings.stepStrideLength);
            Settings.SSR_Instance.SetFloat("numSteps", Settings.maxSteps);
            Settings.SSR_Instance.SetFloat("minSmoothness", Settings.minSmoothness);
            Settings.SSR_Instance.SetInt("reflectSky", Settings.reflectSky ? 1 : 0);

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
            Settings.SSR_Instance.SetMatrix("_InverseProjectionMatrix", projectionMatrix.inverse);
            Settings.SSR_Instance.SetMatrix("_ProjectionMatrix", projectionMatrix);
            Settings.SSR_Instance.SetMatrix("_InverseViewMatrix", viewMatrix.inverse);
            Settings.SSR_Instance.SetMatrix("_ViewMatrix", viewMatrix);
        }

        public override void Create()
        {
            ssrFeatureInstance = this;
            renderPass = new MySSRPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents,
                Settings = this.Settings
            };
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
                Settings.SSRShader = Shader.Find("Hidden/Universal Render Pipeline/MyExtension/SSR");
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

            const int hiZPass = 2;
            const int linearPass = 0;
            const int compPass = 1;

            CommandBuffer commandBuffer = CommandBufferPool.Get("Screen space reflections");
            commandBuffer.Blit(Source, tempPaddedSourceID, PaddedScale, Vector2.zero);

            if (Settings.tracingMode == RaytraceModes.HiZTracing)
            {
                commandBuffer.Blit(null, reflectionMapID, Settings.SSR_Instance, hiZPass);
            }
            else
            {
                commandBuffer.Blit(null, reflectionMapID, Settings.SSR_Instance, linearPass);
            }

            //compose reflection with main texture
            commandBuffer.Blit(tempPaddedSourceID, Source, Settings.SSR_Instance, compPass);

            commandBuffer.ReleaseTemporaryRT(reflectionMapID);
            commandBuffer.ReleaseTemporaryRT(tempPaddedSourceID);
            context.ExecuteCommandBuffer(commandBuffer);
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


