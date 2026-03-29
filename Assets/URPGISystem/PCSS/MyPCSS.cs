using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;


namespace NewVision.PCSS
{


    [ExecuteAlways]
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
        private Material _material;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.PCSSShader == null) return;
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(settings.PCSSShader);

            _pass.Setup(renderer, _material, settings);
            renderer.EnqueuePass(_pass);
        }

        public override void Create()
        {
            _pass = new MyPCSSPass();
            //_pass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            _pass.renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights;
        }


        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

    }


    public class MyPCSSPass : ScriptableRenderPass
    {
        private static readonly int TempRTId = Shader.PropertyToID("_TempPCSSBuffer");

        private Material _material;
        private MyPCSS.PCSSSettings _settings;
        private ScriptableRenderer _renderer;

        //bug修复？
        private int _ScreenSpaceShadowmapTextureId = Shader.PropertyToID("_ScreenSpaceShadowmapTexture");


        // Shader 属性 ID
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
        private static readonly int ShadowDistanceId = Shader.PropertyToID("_ShadowDistance");

        public void Setup(ScriptableRenderer renderer, Material material, MyPCSS.PCSSSettings settings)
        {
            _renderer = renderer;
            _material = material;
            _settings = settings;
        }
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.GetTemporaryRT(TempRTId, cameraTextureDescriptor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null || _settings == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("PCSS Shadows");

            SetMaterialParams(cmd, ref renderingData);

            //var source = _renderer.cameraColorTarget;
            //cmd.Blit(source, TempRTId, _material, 0);
            //cmd.Blit(TempRTId, source);

            //context.ExecuteCommandBuffer(cmd);
            //CommandBufferPool.Release(cmd);



            // 获取相机目标
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.colorFormat = RenderTextureFormat.R8; // 阴影单通道

            // 关键：创建 URP 内置的全局阴影纹理
            cmd.GetTemporaryRT(
                _ScreenSpaceShadowmapTextureId,
                descriptor,
                FilterMode.Bilinear
            );

            // 关键：PCSS 渲染 → 直接写入 _ScreenSpaceShadowmapTexture
            cmd.Blit(
                BuiltinRenderTextureType.None,      // 不需要输入
                _ScreenSpaceShadowmapTextureId,      // 目标：URP内置阴影图
                _material,                           // 你的PCSS Shader
                0                                    // Pass
            );
        }

        private void SetMaterialParams(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 采样数
            cmd.SetGlobalInt(BlockerSamplesId, _settings.BlockerSampleCount);
            cmd.SetGlobalInt(PCFSamplesId, _settings.PCFSampleCount);

            // 柔和度 (根据阴影距离缩放)
            float shadowDist = renderingData.cameraData.maxShadowDistance;
            cmd.SetGlobalFloat(ShadowDistanceId, shadowDist);
            cmd.SetGlobalFloat(SoftnessId, _settings.Softness / 64f / Mathf.Sqrt(shadowDist));
            cmd.SetGlobalFloat(SoftnessFalloffId, Mathf.Exp(_settings.SoftnessFalloff));

            // 偏移
            cmd.SetGlobalFloat(MaxStaticBiasId, _settings.MaxStaticGradientBias);
            cmd.SetGlobalFloat(BlockerBiasId, _settings.BlockerGradientBias);
            cmd.SetGlobalFloat(PCFBiasId, _settings.PCFGradientBias);

            // 级联混合
            cmd.SetGlobalFloat(CascadeBlendId, _settings.CascadeBlendDistance);


            // 噪声纹理
            if (_settings.NoiseTexture != null)
            {
                cmd.SetGlobalVector(NoiseCoordsId, new Vector4(1f / _settings.NoiseTexture.width, 1f / _settings.NoiseTexture.height, 0, 0));
                cmd.SetGlobalTexture(NoiseTexId, _settings.NoiseTexture);
            }


            // 关键字 (Keywords)
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

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(TempRTId);
        }
    }

}


