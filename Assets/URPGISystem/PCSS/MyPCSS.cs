using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision.PCSS
{
    public class MyPCSS : ScriptableRendererFeature
    {
        [System.Serializable]
        public class PCSSSettings
        {
            [Header("采样设置")]
            [Range(1, 64)] public int BlockerSampleCount = 16;
            [Range(1, 64)] public int PCFSampleCount = 16;

            [Header("柔和度")]
            [Range(0f, 7.5f)] public float Softness = 1f;
            [Range(0f, 5f)] public float SoftnessFalloff = 4f;

            [Header("阴影偏移 (抗 Acne)")]
            [Range(0f, 0.15f)] public float MaxStaticGradientBias = 0.05f;
            [Range(0f, 1f)] public float BlockerGradientBias = 0f;
            [Range(0f, 1f)] public float PCFGradientBias = 1f;

            [Header("级联混合")]
            [Range(0f, 1f)] public float CascadeBlendDistance = 0.5f;
            public bool SupportOrthographic = true;

            [Header("资源")]
            public Texture2D NoiseTexture;
            public Shader PCSSShader;
        }

        public PCSSSettings settings = new PCSSSettings();
        private MyPCSSPass _pass;
        private MyPCSSPostPass _postPass;
        private Material _material;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.PCSSShader == null) return;
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(settings.PCSSShader);

            bool allowMainLightShadows = renderingData.shadowData.supportsMainLightShadows && renderingData.lightData.mainLightIndex != -1;
            if (!allowMainLightShadows) return;

            _pass.Setup(_material, settings);
            renderer.EnqueuePass(_pass);
            renderer.EnqueuePass(_postPass);
        }

        public override void Create()
        {
            _pass = new MyPCSSPass();
            _postPass = new MyPCSSPostPass();
            _pass.renderPassEvent = RenderPassEvent.AfterRenderingGbuffer;
            _postPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CoreUtils.Destroy(_material);
            }
        }
    }

    public class MyPCSSPass : ScriptableRenderPass
    {
        private const string k_SSShadowsTextureName = "_ScreenSpaceShadowmapTexture";

        private Material _material;
        private MyPCSS.PCSSSettings _settings;
        private RenderTextureDescriptor m_RenderTextureDescriptor;
        private RenderTargetHandle m_RenderTarget;

        private static readonly ProfilingSampler s_ProfilingSampler = new ProfilingSampler("PCSS Shadows");

        private static readonly int BlockerSamplesId = Shader.PropertyToID("Blocker_Samples");
        private static readonly int PCFSamplesId = Shader.PropertyToID("PCF_Samples");
        private static readonly int SoftnessId = Shader.PropertyToID("Softness");
        private static readonly int SoftnessFalloffId = Shader.PropertyToID("SoftnessFalloff");
        private static readonly int MaxStaticBiasId = Shader.PropertyToID("RECEIVER_PLANE_MIN_FRACTIONAL_ERROR");
        private static readonly int BlockerBiasId = Shader.PropertyToID("Blocker_GradientBias");
        private static readonly int PCFBiasId = Shader.PropertyToID("PCF_GradientBias");
        private static readonly int CascadeBlendId = Shader.PropertyToID("CascadeBlendDistance");
        private static readonly int NoiseCoordsId = Shader.PropertyToID("NoiseCoords");
        private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTexture");

        public MyPCSSPass()
        {
            m_RenderTarget.Init(k_SSShadowsTextureName);
        }

        public void Setup(Material material, MyPCSS.PCSSSettings settings)
        {
            _material = material;
            _settings = settings;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            m_RenderTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            m_RenderTextureDescriptor.depthBufferBits = 0;
            m_RenderTextureDescriptor.msaaSamples = 1;
            m_RenderTextureDescriptor.colorFormat = RenderTextureFormat.R8;

            cmd.GetTemporaryRT(m_RenderTarget.id, m_RenderTextureDescriptor, FilterMode.Point);

            ConfigureTarget(m_RenderTarget.Identifier());
            ConfigureClear(ClearFlag.Color, Color.white);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null || _settings == null) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, s_ProfilingSampler))
            {
                SetMaterialParams(cmd, ref renderingData);

                Camera camera = renderingData.cameraData.camera;

                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, _material);
                cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, false);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, false);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowScreen, true);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void SetMaterialParams(CommandBuffer cmd, ref RenderingData renderingData)
        {
            cmd.SetGlobalInt(BlockerSamplesId, _settings.BlockerSampleCount);
            cmd.SetGlobalInt(PCFSamplesId, _settings.PCFSampleCount);

            float shadowDist = renderingData.cameraData.maxShadowDistance;
            if (shadowDist < 0.001f) shadowDist = 1f;
            cmd.SetGlobalFloat(SoftnessId, _settings.Softness / 64f / Mathf.Sqrt(shadowDist));
            cmd.SetGlobalFloat(SoftnessFalloffId, Mathf.Exp(_settings.SoftnessFalloff));

            cmd.SetGlobalFloat(MaxStaticBiasId, _settings.MaxStaticGradientBias);
            cmd.SetGlobalFloat(BlockerBiasId, _settings.BlockerGradientBias);
            cmd.SetGlobalFloat(PCFBiasId, _settings.PCFGradientBias);

            cmd.SetGlobalFloat(CascadeBlendId, _settings.CascadeBlendDistance);

            if (_settings.NoiseTexture != null)
            {
                cmd.SetGlobalVector(NoiseCoordsId, new Vector4(1f / _settings.NoiseTexture.width, 1f / _settings.NoiseTexture.height, 0, 0));
                cmd.SetGlobalTexture(NoiseTexId, _settings.NoiseTexture);
            }

            SetKeyword(cmd, "USE_FALLOFF", _settings.SoftnessFalloff > Mathf.Epsilon);
            SetKeyword(cmd, "USE_CASCADE_BLENDING", _settings.CascadeBlendDistance > 0);
            SetKeyword(cmd, "USE_STATIC_BIAS", _settings.MaxStaticGradientBias > 0);
            SetKeyword(cmd, "USE_BLOCKER_BIAS", _settings.BlockerGradientBias > 0);
            SetKeyword(cmd, "USE_PCF_BIAS", _settings.PCFGradientBias > 0);
            SetKeyword(cmd, "ORTHOGRAPHIC_SUPPORTED", _settings.SupportOrthographic);

            int maxSamples = Mathf.Max(_settings.BlockerSampleCount, _settings.PCFSampleCount);
            SetKeyword(cmd, "POISSON_32", maxSamples <= 32);
            SetKeyword(cmd, "POISSON_64", maxSamples > 32);
        }

        private void SetKeyword(CommandBuffer cmd, string keyword, bool state)
        {
            if (state) cmd.EnableShaderKeyword(keyword);
            else cmd.DisableShaderKeyword(keyword);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null) throw new ArgumentNullException("cmd");
            cmd.ReleaseTemporaryRT(m_RenderTarget.id);
        }
    }

    public class MyPCSSPostPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler s_ProfilingSampler = new ProfilingSampler("PCSS Shadows Post");

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureTarget(BuiltinRenderTextureType.CurrentActive);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, s_ProfilingSampler))
            {
                ShadowData shadowData = renderingData.shadowData;
                int cascadesCount = shadowData.mainLightShadowCascadesCount;
                bool mainLightShadows = renderingData.shadowData.supportsMainLightShadows;
                bool receiveShadowsNoCascade = mainLightShadows && cascadesCount == 1;
                bool receiveShadowsCascades = mainLightShadows && cascadesCount > 1;

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowScreen, false);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, receiveShadowsNoCascade);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, receiveShadowsCascades);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
