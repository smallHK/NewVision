using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision.LTC
{
    /// <summary>
    /// LTC区域光渲染通道
    /// 负责执行LTC区域光的渲染逻辑
    /// </summary>
    public class LTCAreaLightRenderPass : ScriptableRenderPass
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="featureName">Feature名称</param>
        /// <param name="settings">Feature设置</param>
        public LTCAreaLightRenderPass(string featureName, LTCAreaLightFeatureSettings settings)
        {
            mProfilingSampler = new ProfilingSampler(featureName);
            renderPassEvent = settings.m_RenderPassEvent;
            mPassMaterial = CoreUtils.CreateEngineMaterial(settings.m_LTCPassShader);
            mMaxAreaLightCount = settings.m_MaxAreaLightCount;
            PrepareLTCData();
            mAreaLightColors = new Vector4[mMaxAreaLightCount];
            mAreaLightVertices = new Matrix4x4[mMaxAreaLightCount];
            mAreaLightRenderShadow = new float[mMaxAreaLightCount];
            mAreaLightTextureIndices = new float[mMaxAreaLightCount];
            mAreaLightShadowMaps = new RenderTexture[mMaxAreaLightCount];
            mAreaLightShadowMapDummies = new Texture2D[mMaxAreaLightCount];
            mAreaLightShadowParams = new Vector4[mMaxAreaLightCount];
            mAreaLightShadowProjMatrices = new Matrix4x4[mMaxAreaLightCount];
            mAreaLightShadowNearClips = new float[mMaxAreaLightCount];
            mAreaLightShadowFarClips = new float[mMaxAreaLightCount];
        }

        /// <summary>
        /// 相机设置
        /// </summary>
        /// <param name="cmd">命令缓冲区</param>
        /// <param name="renderingData">渲染数据</param>
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            CameraData cameraData = renderingData.cameraData;
            mSource = cameraData.renderer.cameraColorTarget;
            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            int tempTextureID = Shader.PropertyToID(kTemporaryColorTextureName);
            cmd.GetTemporaryRT(tempTextureID, descriptor, FilterMode.Bilinear);
            mTemporaryColorTexture = new RenderTargetIdentifier(tempTextureID);
            Matrix4x4 proj = cameraData.GetProjectionMatrix();
            Matrix4x4 viewNoTrans = cameraData.GetViewMatrix();
            viewNoTrans.SetColumn(3, new Vector4(0, 0, 0, 1));
            Matrix4x4 invViewProj = (proj * viewNoTrans).inverse;
            Vector4 topLeftCorner = invViewProj.MultiplyPoint(new Vector3(-1, 1, -1));
            Vector4 topRightCorner = invViewProj.MultiplyPoint(new Vector3(1, 1, -1));
            Vector4 bottomLeftCorner = invViewProj.MultiplyPoint(new Vector3(-1, -1, -1));
            mPassMaterial.SetVector(CameraTopLeftCornerID, topLeftCorner);
            mPassMaterial.SetVector(CameraXExtentID, topRightCorner - topLeftCorner);
            mPassMaterial.SetVector(CameraYExtentID, bottomLeftCorner - topLeftCorner);
        }

        /// <summary>
        /// 执行渲染
        /// </summary>
        /// <param name="context">渲染上下文</param>
        /// <param name="renderingData">渲染数据</param>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, mProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                HashSet<LTCAreaLight> areaLights = LTCAreaLightManager.Get();
                if (areaLights.Count <= 0) return;
                int index = 0;
                foreach (LTCAreaLight light in areaLights)
                {
                    if (index >= mMaxAreaLightCount) break;
                    mAreaLightColors[index] = light.GetLightColor();
                    mAreaLightVertices[index] = light.GetLightVertices();
                    mAreaLightTextureIndices[index] = light.TextureIndex;
                    if (!light.m_RenderShadow)
                    {
                        mAreaLightRenderShadow[index] = 0;
                        mAreaLightShadowMaps[index] = null;
                        mAreaLightShadowMapDummies[index] = null;
                        mAreaLightShadowParams[index] = Vector4.zero;
                        mAreaLightShadowProjMatrices[index] = Matrix4x4.identity;
                        mAreaLightShadowNearClips[index] = 0;
                        mAreaLightShadowFarClips[index] = 0;
                    }
                    else
                    {
                        mAreaLightRenderShadow[index] = 1;
                        mAreaLightShadowMapDummies[index] = light.mShadowMapDummy;
                        mAreaLightShadowMaps[index] = light.m_ShadowMap;
                        mAreaLightShadowParams[index] = light.GetShadowParams();
                        mAreaLightShadowProjMatrices[index] = light.GetProjMatrix();
                        mAreaLightShadowNearClips[index] = light.GetShadowNearClip();
                        mAreaLightShadowFarClips[index] = light.GetShadowFarClip();
                    }
                    index++;
                }
                cmd.SetGlobalInteger(AreaLightCountID, index);
                cmd.SetGlobalFloatArray(AreaLightRenderShadowID, mAreaLightRenderShadow);
                cmd.SetGlobalFloatArray(AreaLightTextureIndicesID, mAreaLightTextureIndices);
                cmd.SetGlobalVectorArray(AreaLightColorsID, mAreaLightColors);
                cmd.SetGlobalMatrixArray(AreaLightVerticesID, mAreaLightVertices);
                cmd.SetGlobalVectorArray(AreaLightShadowParamsID, mAreaLightShadowParams);
                cmd.SetGlobalFloatArray(AreaLightShadowNearClipID, mAreaLightShadowNearClips);
                cmd.SetGlobalFloatArray(AreaLightShadowFarClipID, mAreaLightShadowFarClips);
                cmd.SetGlobalMatrixArray(AreaLightShadowProjMatrixID, mAreaLightShadowProjMatrices);
                
                mPassMaterial.SetTexture(AreaLightShadowMapID, mAreaLightShadowMaps[0]);
                mPassMaterial.SetTexture(AreaLightShadowMapDummyID, mAreaLightShadowMapDummies[0]);
                
                cmd.Blit(mSource, mTemporaryColorTexture, mPassMaterial, 0);
                cmd.Blit(mTemporaryColorTexture, mSource);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            CoreUtils.Destroy(mPassMaterial);
            mPassMaterial = null;
        }

        /// <summary>
        /// 准备LTC数据
        /// 加载LUT纹理并传递给shader
        /// </summary>
        private void PrepareLTCData()
        {
            mTransformInvDiffuseTexture = LTCAreaLightLUT.LoadLUT(LTCAreaLightLUT.LUTType.TransformInvDisneyDiffuse);
            mTransformInvSpecularTexture = LTCAreaLightLUT.LoadLUT(LTCAreaLightLUT.LUTType.TransformInvDisneyGGX);
            mFresnelTexture = LTCAreaLightLUT.LoadLUT(LTCAreaLightLUT.LUTType.AmpDiffAmpSpecFresnel);
            mPassMaterial.SetTexture(TransformInvDiffuseID, mTransformInvDiffuseTexture);
            mPassMaterial.SetTexture(TransformInvSpecularID, mTransformInvSpecularTexture);
            mPassMaterial.SetTexture(AmpDiffAmpSpecFresnelID, mFresnelTexture);
        }
        
        /// <summary>
        /// 性能分析采样器
        /// </summary>
        private readonly ProfilingSampler mProfilingSampler;
        
        /// <summary>
        /// 渲染材质
        /// </summary>
        private Material mPassMaterial;
        
        /// <summary>
        /// 源渲染目标
        /// </summary>
        private RenderTargetIdentifier mSource;
        
        /// <summary>
        /// 临时颜色纹理
        /// </summary>
        private RenderTargetIdentifier mTemporaryColorTexture;
        
        /// <summary>
        /// 临时颜色纹理名称
        /// </summary>
        private const string kTemporaryColorTextureName = "_TemporaryColorTexture";
        
        /// <summary>
        /// 漫反射变换逆矩阵纹理
        /// </summary>
        private Texture2D mTransformInvDiffuseTexture;
        
        /// <summary>
        /// 高光变换逆矩阵纹理
        /// </summary>
        private Texture2D mTransformInvSpecularTexture;
        
        /// <summary>
        /// 菲涅尔纹理
        /// </summary>
        private Texture2D mFresnelTexture;
        
        /// <summary>
        /// 最大区域光数量
        /// </summary>
        private readonly int mMaxAreaLightCount;
        
        /// <summary>
        /// 区域光阴影渲染标志
        /// </summary>
        private readonly float[] mAreaLightRenderShadow;
        
        /// <summary>
        /// 区域光纹理索引
        /// </summary>
        private readonly float[] mAreaLightTextureIndices;
        
        /// <summary>
        /// 区域光颜色
        /// </summary>
        private readonly Vector4[] mAreaLightColors;
        
        /// <summary>
        /// 区域光顶点
        /// </summary>
        private readonly Matrix4x4[] mAreaLightVertices;
        
        /// <summary>
        /// 区域光阴影近裁剪面
        /// </summary>
        private readonly float[] mAreaLightShadowNearClips;
        
        /// <summary>
        /// 区域光阴影远裁剪面
        /// </summary>
        private readonly float[] mAreaLightShadowFarClips;
        
        /// <summary>
        /// 区域光阴影参数
        /// </summary>
        private readonly Vector4[] mAreaLightShadowParams;
        
        /// <summary>
        /// 区域光阴影投影矩阵
        /// </summary>
        private readonly Matrix4x4[] mAreaLightShadowProjMatrices;
        
        /// <summary>
        /// 区域光阴影贴图占位符
        /// </summary>
        private readonly Texture2D[] mAreaLightShadowMapDummies;
        
        /// <summary>
        /// 区域光阴影贴图
        /// </summary>
        private readonly RenderTexture[] mAreaLightShadowMaps;
        
        /// <summary>
        /// 漫反射变换逆矩阵纹理ID
        /// </summary>
        private static readonly int TransformInvDiffuseID = Shader.PropertyToID("_TransformInv_Diffuse");
        
        /// <summary>
        /// 高光变换逆矩阵纹理ID
        /// </summary>
        private static readonly int TransformInvSpecularID = Shader.PropertyToID("_TransformInv_Specular");
        
        /// <summary>
        /// 菲涅尔纹理ID
        /// </summary>
        private static readonly int AmpDiffAmpSpecFresnelID = Shader.PropertyToID("_AmpDiffAmpSpecFresnel");
        
        /// <summary>
        /// 区域光数量ID
        /// </summary>
        private static readonly int AreaLightCountID = Shader.PropertyToID("_AreaLightCount");
        
        /// <summary>
        /// 区域光颜色ID
        /// </summary>
        private static readonly int AreaLightColorsID = Shader.PropertyToID("_AreaLightColors");
        
        /// <summary>
        /// 区域光顶点ID
        /// </summary>
        private static readonly int AreaLightVerticesID = Shader.PropertyToID("_AreaLightVertices");
        
        /// <summary>
        /// 区域光阴影渲染标志ID
        /// </summary>
        private static readonly int AreaLightRenderShadowID = Shader.PropertyToID("_AreaLightRenderShadow");
        
        /// <summary>
        /// 区域光阴影参数ID
        /// </summary>
        private static readonly int AreaLightShadowParamsID = Shader.PropertyToID("_AreaLightShadowParams");
        
        /// <summary>
        /// 区域光阴影近裁剪面ID
        /// </summary>
        private static readonly int AreaLightShadowNearClipID = Shader.PropertyToID("_AreaLightShadowNearClip");
        
        /// <summary>
        /// 区域光阴影远裁剪面ID
        /// </summary>
        private static readonly int AreaLightShadowFarClipID = Shader.PropertyToID("_AreaLightShadowFarClip");
        
        /// <summary>
        /// 区域光阴影投影矩阵ID
        /// </summary>
        private static readonly int AreaLightShadowProjMatrixID = Shader.PropertyToID("_AreaLightShadowProjMatrix");
        
        /// <summary>
        /// 区域光阴影贴图ID
        /// </summary>
        private static readonly int AreaLightShadowMapID = Shader.PropertyToID("_AreaLightShadowMap");
        
        /// <summary>
        /// 区域光阴影贴图占位符ID
        /// </summary>
        private static readonly int AreaLightShadowMapDummyID = Shader.PropertyToID("_AreaLightShadowMapDummy");
        
        /// <summary>
        /// 相机左上角ID
        /// </summary>
        private static readonly int CameraTopLeftCornerID = Shader.PropertyToID("_CameraTopLeftCorner");
        
        /// <summary>
        /// 相机X方向范围ID
        /// </summary>
        private static readonly int CameraXExtentID = Shader.PropertyToID("_CameraXExtent");
        
        /// <summary>
        /// 相机Y方向范围ID
        /// </summary>
        private static readonly int CameraYExtentID = Shader.PropertyToID("_CameraYExtent");
        
        /// <summary>
        /// 区域光纹理索引ID
        /// </summary>
        private static readonly int AreaLightTextureIndicesID = Shader.PropertyToID("_AreaLightTextureIndices");
    }
}