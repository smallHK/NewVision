using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision.VXGI
{
    [DisallowMultipleRendererFeature]
    public class MyVXGI : ScriptableRendererFeature
    {
        private VXGIVoxelizationPass m_VoxelizationPass;
        private VXGIMipmapPass m_VoxelMipmapPass;
        private VXGILightingPass m_VXGILightingPass;

        [Header("Shader & Compute References")]
        public Shader voxelizationShader;
        public Shader lightingShader;
        public ComputeShader voxelMipmapCS;

        [Header("Voxel Volume Settings")]
        public float voxelBound = 20f;
        public int voxelResolution = 64;
        public bool followCamera = true;

        [Header("VXGI Rendering Settings")]
        [Range(0.1f, 3f)] public float indirectDiffuseIntensity = 1f;
        [Range(0.1f, 3f)] public float indirectSpecularIntensity = 1f;
        [Range(1, 8)] public int coneTraceSteps = 4;
        [Range(0.1f, 1f)] public float coneAperture = 0.5f;

        public override void Create()
        {
            // 1. 体素化 Pass
            m_VoxelizationPass = new VXGIVoxelizationPass(voxelizationShader, voxelBound, voxelResolution, followCamera);
            m_VoxelizationPass.renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights;

            // 2. 体素 Mipmap Pass
            m_VoxelMipmapPass = new VXGIMipmapPass(voxelMipmapCS, voxelResolution);
            m_VoxelMipmapPass.renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights + 1;

            // 3. 光照替换 Pass
            m_VXGILightingPass = new VXGILightingPass(lightingShader, indirectDiffuseIntensity, indirectSpecularIntensity, coneTraceSteps, coneAperture);
            m_VXGILightingPass.renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights + 2;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview)
                return;

            if (m_VoxelizationPass == null || m_VoxelMipmapPass == null || m_VXGILightingPass == null)
                return;

            if (voxelizationShader == null || lightingShader == null || voxelMipmapCS == null)
                return;

            m_VoxelizationPass.SetCamera(renderingData.cameraData.camera);
            m_VoxelizationPass.SetParams(voxelBound, voxelResolution, followCamera);

            m_VoxelMipmapPass.SetVoxelTexture(m_VoxelizationPass.GetVoxelRadianceTexture());

            m_VXGILightingPass.SetVoxelTextures(m_VoxelizationPass.GetVoxelRadianceTexture(), m_VoxelMipmapPass.GetMipmapTextures());
            m_VXGILightingPass.SetVoxelParams(m_VoxelizationPass.GetWorldToVoxelMatrix(), voxelBound, voxelResolution);
            m_VXGILightingPass.SetIntensity(indirectDiffuseIntensity, indirectSpecularIntensity, coneTraceSteps, coneAperture);

            renderer.EnqueuePass(m_VoxelizationPass);
            renderer.EnqueuePass(m_VoxelMipmapPass);
            renderer.EnqueuePass(m_VXGILightingPass);
        }

        protected override void Dispose(bool disposing)
        {
            m_VoxelizationPass?.Dispose();
            m_VoxelMipmapPass?.Dispose();
            m_VXGILightingPass?.Dispose();
        }

        public class VXGIVoxelizationPass : ScriptableRenderPass
        {
            private Material m_VoxelMat;
            private float m_Bound;
            private int m_Resolution;
            private bool m_FollowCamera;
            private Camera m_Camera;
            private Matrix4x4 m_WorldToVoxel;
            private RenderTexture m_VoxelRadiance3D;

            public void SetCamera(Camera cam) => m_Camera = cam;
            public void SetParams(float bound, int resolution, bool followCamera)
            {
                m_Bound = bound;
                m_FollowCamera = followCamera;
                if (m_Resolution != resolution)
                {
                    m_Resolution = resolution;
                    ReleaseResources();
                }
            }
            public RenderTexture GetVoxelRadianceTexture() => m_VoxelRadiance3D;
            public Matrix4x4 GetWorldToVoxelMatrix() => m_WorldToVoxel;
            private ShaderTagId m_ShaderTagId = new ShaderTagId("Voxelization");

            private static readonly int s_WorldToVoxel = Shader.PropertyToID("_WorldToVoxel");
            private static readonly int s_VoxelResolution = Shader.PropertyToID("_VoxelResolution");
            private static readonly int s_VoxelRadiance = Shader.PropertyToID("_VoxelRadiance");

            public VXGIVoxelizationPass(Shader shader, float bound, int resolution, bool followCamera)
            {
                m_Bound = bound;
                m_Resolution = resolution;
                m_FollowCamera = followCamera;
                if (shader != null) m_VoxelMat = CoreUtils.CreateEngineMaterial(shader);
            }

            private void ReleaseResources()
            {
                if (m_VoxelRadiance3D != null)
                {
                    m_VoxelRadiance3D.Release();
                    m_VoxelRadiance3D = null;
                }
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (m_VoxelRadiance3D == null || m_VoxelRadiance3D.width != m_Resolution)
                {
                    ReleaseResources();
                    var desc = new RenderTextureDescriptor(m_Resolution, m_Resolution, RenderTextureFormat.ARGBHalf, 0)
                    {
                        dimension = TextureDimension.Tex3D,
                        volumeDepth = m_Resolution,
                        enableRandomWrite = true,
                        sRGB = false,
                        useMipMap = true,
                        autoGenerateMips = false
                    };
                    m_VoxelRadiance3D = new RenderTexture(desc);
                    m_VoxelRadiance3D.Create();
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_VoxelMat == null || m_Camera == null) return;
                CommandBuffer cmd = CommandBufferPool.Get("VXGI Voxelization");

                // 1. 计算体素空间变换矩阵
                Vector3 center = m_FollowCamera ? m_Camera.transform.position : Vector3.zero;
                Vector3 origin = center - Vector3.one * m_Bound / 2f;
                m_WorldToVoxel = Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one * m_Bound / m_Resolution).inverse;

                // 2. 设置全局变量
                cmd.SetGlobalMatrix(s_WorldToVoxel, m_WorldToVoxel);
                cmd.SetGlobalInt(s_VoxelResolution, m_Resolution);
                cmd.SetRenderTarget(m_VoxelRadiance3D);
                cmd.ClearRenderTarget(false, true, Color.clear);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 3. 遍历所有可见 Renderer，强制用我们的材质画
                var drawingSettings = CreateDrawingSettings(
                    m_ShaderTagId,
                    ref renderingData,
                    SortingCriteria.CommonOpaque
                );
                drawingSettings.overrideMaterial = m_VoxelMat;
                drawingSettings.overrideMaterialPassIndex = 0;

                var filteringSettings = new FilteringSettings(
                    RenderQueueRange.opaque
                );

                context.DrawRenderers(
                    renderingData.cullResults,
                    ref drawingSettings,
                    ref filteringSettings
                );

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                m_VoxelRadiance3D?.Release();
                CoreUtils.Destroy(m_VoxelMat);
            }
        }

        public class VXGIMipmapPass : ScriptableRenderPass
        {
            private ComputeShader m_MipmapCS;
            private int m_Resolution;
            private int m_Kernel;
            private RenderTexture m_VoxelTex;
            private RenderTexture[] m_MipmapTexs;

            public void SetVoxelTexture(RenderTexture tex) => m_VoxelTex = tex;
            public RenderTexture[] GetMipmapTextures() => m_MipmapTexs;

            private static readonly int s_InputTex = Shader.PropertyToID("_InputTex");
            private static readonly int s_OutputTex = Shader.PropertyToID("_OutputTex");
            private static readonly int s_InputMip = Shader.PropertyToID("_InputMip");
            private static readonly int s_OutputMip = Shader.PropertyToID("_OutputMip");
            private static readonly int s_InputTextureSize = Shader.PropertyToID("_InputTextureSize");

            public VXGIMipmapPass(ComputeShader cs, int resolution)
            {
                m_MipmapCS = cs;
                m_Resolution = resolution;
                if (cs != null) m_Kernel = cs.FindKernel("CSMain");
            }

            private void ReleaseMipmapTextures()
            {
                if (m_MipmapTexs != null)
                {
                    for (int i = 1; i < m_MipmapTexs.Length; i++)
                    {
                        if (m_MipmapTexs[i] != null)
                        {
                            m_MipmapTexs[i].Release();
                            m_MipmapTexs[i] = null;
                        }
                    }
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_MipmapCS == null || m_VoxelTex == null) return;

                CommandBuffer cmd = CommandBufferPool.Get("VXGI Mipmap");

                int mipCount = (int)Mathf.Log(m_Resolution, 2) + 1;
                
                if (m_MipmapTexs == null || m_MipmapTexs.Length != mipCount)
                {
                    ReleaseMipmapTextures();
                    m_MipmapTexs = new RenderTexture[mipCount];
                }
                m_MipmapTexs[0] = m_VoxelTex;

                cmd.SetComputeIntParams(m_MipmapCS, s_InputTextureSize, new int[] { m_Resolution, m_Resolution, m_Resolution });

                for (int i = 1; i < mipCount; i++)
                {
                    int res = m_Resolution >> i;
                    
                    if (m_MipmapTexs[i] == null || m_MipmapTexs[i].width != res)
                    {
                        if (m_MipmapTexs[i] != null) m_MipmapTexs[i].Release();
                        
                        var desc = m_VoxelTex.descriptor;
                        desc.width = desc.height = desc.volumeDepth = res;
                        m_MipmapTexs[i] = new RenderTexture(desc);
                        m_MipmapTexs[i].Create();
                    }

                    cmd.SetComputeTextureParam(m_MipmapCS, m_Kernel, s_InputTex, m_MipmapTexs[i - 1]);
                    cmd.SetComputeTextureParam(m_MipmapCS, m_Kernel, s_OutputTex, m_MipmapTexs[i]);
                    cmd.SetComputeIntParam(m_MipmapCS, s_InputMip, i - 1);
                    cmd.SetComputeIntParam(m_MipmapCS, s_OutputMip, i);
                    cmd.DispatchCompute(m_MipmapCS, m_Kernel, Mathf.CeilToInt(res / 8f), Mathf.CeilToInt(res / 8f), Mathf.CeilToInt(res / 8f));
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                ReleaseMipmapTextures();
            }
        }

        public class VXGILightingPass : ScriptableRenderPass
        {
            private Material m_LightingMat;
            private float m_DiffuseIntensity;
            private float m_SpecularIntensity;
            private int m_ConeSteps;
            private float m_ConeAperture;
            private RenderTexture m_VoxelTex;
            private RenderTexture[] m_MipmapTexs;
            private Matrix4x4 m_WorldToVoxel;
            private float m_VoxelBound;
            private int m_VoxelResolution;

            private static readonly int s_GBuffer0 = Shader.PropertyToID("_GBuffer0");
            private static readonly int s_GBuffer1 = Shader.PropertyToID("_GBuffer1");
            private static readonly int s_GBuffer2 = Shader.PropertyToID("_GBuffer2");
            private static readonly int s_CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");

            public void SetVoxelTextures(RenderTexture tex, RenderTexture[] mips) { m_VoxelTex = tex; m_MipmapTexs = mips; }
            public void SetVoxelParams(Matrix4x4 w2v, float bound, int res) { m_WorldToVoxel = w2v; m_VoxelBound = bound; m_VoxelResolution = res; }
            public void SetIntensity(float diffuse, float specular, int steps, float aperture)
            {
                m_DiffuseIntensity = diffuse;
                m_SpecularIntensity = specular;
                m_ConeSteps = steps;
                m_ConeAperture = aperture;
            }

            private static readonly int s_IndirectDiffuseIntensity = Shader.PropertyToID("_IndirectDiffuseIntensity");
            private static readonly int s_IndirectSpecularIntensity = Shader.PropertyToID("_IndirectSpecularIntensity");
            private static readonly int s_ConeTraceSteps = Shader.PropertyToID("_ConeTraceSteps");
            private static readonly int s_ConeAperture = Shader.PropertyToID("_ConeAperture");
            private static readonly int s_WorldToVoxel = Shader.PropertyToID("_WorldToVoxel");
            private static readonly int s_VoxelBound = Shader.PropertyToID("_VoxelBound");
            private static readonly int s_VoxelResolution = Shader.PropertyToID("_VoxelResolution");
            private static readonly int s_VoxelRadiance = Shader.PropertyToID("_VoxelRadiance");

            public VXGILightingPass(Shader shader, float diffuse, float specular, int steps, float aperture)
            {
                m_DiffuseIntensity = diffuse;
                m_SpecularIntensity = specular;
                m_ConeSteps = steps;
                m_ConeAperture = aperture;
                if (shader != null) m_LightingMat = CoreUtils.CreateEngineMaterial(shader);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_LightingMat == null || m_VoxelTex == null || m_MipmapTexs == null) return;

                CommandBuffer cmd = CommandBufferPool.Get("VXGI Lighting");
                
                m_LightingMat.SetFloat(s_IndirectDiffuseIntensity, m_DiffuseIntensity);
                m_LightingMat.SetFloat(s_IndirectSpecularIntensity, m_SpecularIntensity);
                m_LightingMat.SetInt(s_ConeTraceSteps, m_ConeSteps);
                m_LightingMat.SetFloat(s_ConeAperture, m_ConeAperture);
                m_LightingMat.SetMatrix(s_WorldToVoxel, m_WorldToVoxel);
                m_LightingMat.SetFloat(s_VoxelBound, m_VoxelBound);
                m_LightingMat.SetInt(s_VoxelResolution, m_VoxelResolution);
                m_LightingMat.SetTexture(s_VoxelRadiance, m_VoxelTex);

                cmd.SetGlobalTexture(s_GBuffer0, s_GBuffer0);
                cmd.SetGlobalTexture(s_GBuffer1, s_GBuffer1);
                cmd.SetGlobalTexture(s_GBuffer2, s_GBuffer2);
                cmd.SetGlobalTexture(s_CameraDepthTexture, s_CameraDepthTexture);

                var cameraTarget = renderingData.cameraData.renderer.cameraColorTarget;
                cmd.SetRenderTarget(cameraTarget);
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, m_LightingMat, 0, 0);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                CoreUtils.Destroy(m_LightingMat);
            }
        }
    }
}


