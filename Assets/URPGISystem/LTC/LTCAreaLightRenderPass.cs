using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision.LTC
{
    /// <summary>
    /// LTC区域光渲染通道
    /// 负责执行LTC区域光的渲染逻辑
    /// 
    /// 渲染流程：
    /// 1. OnCameraSetup - 设置相机相关参数，准备临时纹理
    /// 2. Execute - 执行区域光渲染，包括：
    ///    - 收集场景中所有区域光
    ///    - 设置全局Shader参数
    ///    - 执行Blit渲染操作
    /// 3. Dispose - 释放渲染材质资源
    /// </summary>
    public class LTCAreaLightRenderPass : ScriptableRenderPass
    {
        /// <summary>
        /// 构造函数
        /// 步骤1: 创建ProfilingSampler用于Frame Debugger显示
        /// 步骤2: 设置渲染通道事件
        /// 步骤3: 创建渲染材质
        /// 步骤4: 初始化区域光数据数组
        /// 步骤5: 加载LTC查找表数据
        /// </summary>
        /// <param name="featureName">Feature名称，用于ProfilingSampler</param>
        /// <param name="settings">Feature设置，包含Shader和渲染参数</param>
        public LTCAreaLightRenderPass(string featureName, LTCAreaLightFeatureSettings settings)
        {
            // 步骤1: 创建ProfilingSampler，用于在Frame Debugger中显示此渲染通道
            // featureName将作为Frame Debugger中显示的名称
            mProfilingSampler = new ProfilingSampler(featureName);
            
            // 步骤2: 设置渲染通道的执行时机
            renderPassEvent = settings.m_RenderPassEvent;
            
            // 步骤3: 从Shader创建渲染材质
            mPassMaterial = CoreUtils.CreateEngineMaterial(settings.m_LTCPassShader);
            
            // 步骤4: 获取最大区域光数量限制
            mMaxAreaLightCount = settings.m_MaxAreaLightCount;
            
            // 步骤5: 准备LTC查找表数据（漫反射、高光、菲涅尔）
            PrepareLTCData();
            
            // 步骤6: 初始化区域光数据数组
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
        /// 相机设置回调
        /// 步骤1: 获取相机颜色目标
        /// 步骤2: 创建临时渲染纹理
        /// 步骤3: 计算相机视图相关参数（用于世界坐标重建）
        /// </summary>
        /// <param name="cmd">命令缓冲区</param>
        /// <param name="renderingData">渲染数据</param>
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 步骤1: 获取相机颜色渲染目标
            CameraData cameraData = renderingData.cameraData;
            mSource = cameraData.renderer.cameraColorTarget;
            
            // 步骤2: 创建临时颜色纹理用于Blit操作
            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0; // 不需要深度缓冲
            int tempTextureID = Shader.PropertyToID(kTemporaryColorTextureName);
            cmd.GetTemporaryRT(tempTextureID, descriptor, FilterMode.Bilinear);
            mTemporaryColorTexture = new RenderTargetIdentifier(tempTextureID);
            
            // 步骤3: 计算相机参数，用于在Shader中从深度重建世界坐标
            // 获取投影矩阵和视图矩阵（去除平移）
            Matrix4x4 proj = cameraData.GetProjectionMatrix();
            Matrix4x4 viewNoTrans = cameraData.GetViewMatrix();
            viewNoTrans.SetColumn(3, new Vector4(0, 0, 0, 1));
            
            // 计算逆视图投影矩阵
            Matrix4x4 invViewProj = (proj * viewNoTrans).inverse;
            
            // 计算相机视锥体的三个角点（用于重建世界坐标）
            Vector4 topLeftCorner = invViewProj.MultiplyPoint(new Vector3(-1, 1, -1));
            Vector4 topRightCorner = invViewProj.MultiplyPoint(new Vector3(1, 1, -1));
            Vector4 bottomLeftCorner = invViewProj.MultiplyPoint(new Vector3(-1, -1, -1));
            
            // 将相机参数传递给Shader
            mPassMaterial.SetVector(CameraTopLeftCornerID, topLeftCorner);
            mPassMaterial.SetVector(CameraXExtentID, topRightCorner - topLeftCorner);
            mPassMaterial.SetVector(CameraYExtentID, bottomLeftCorner - topLeftCorner);
        }

        /// <summary>
        /// 执行渲染
        /// 步骤1: 获取命令缓冲区（使用kPassName命名，用于Frame Debugger显示）
        /// 步骤2: 开启性能分析采样范围（用于Frame Debugger显示）
        /// 步骤3: 收集所有区域光数据并设置到全局变量
        /// 步骤4: 执行Blit渲染操作
        /// 步骤5: 结束性能分析采样范围
        /// 步骤6: 执行命令缓冲区并释放
        /// </summary>
        /// <param name="context">渲染上下文</param>
        /// <param name="renderingData">渲染数据</param>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 步骤1: 从命令缓冲区池获取命令缓冲区
            // kPassName会在Frame Debugger中显示为此命令缓冲区的名称
            CommandBuffer cmd = CommandBufferPool.Get(kPassName);
            
            // 步骤2: 开启ProfilingScope，这会在Frame Debugger中创建一个层级
            // mProfilingSampler包含的名称会显示在Frame Debugger中
            using (new ProfilingScope(cmd, mProfilingSampler))
            {
                // 步骤3: 获取场景中所有区域光
                HashSet<LTCAreaLight> areaLights = LTCAreaLightManager.Get();
                
                // 如果没有区域光，直接返回
                if (areaLights.Count <= 0)
                {
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                    return;
                }
                
                // 步骤4: 遍历所有区域光，收集数据
                int index = 0;
                foreach (LTCAreaLight light in areaLights)
                {
                    if (index >= mMaxAreaLightCount) break;
                    
                    // 收集光源颜色和强度
                    mAreaLightColors[index] = light.GetLightColor();
                    // 收集光源顶点位置（用于计算光照范围）
                    mAreaLightVertices[index] = light.GetLightVertices();
                    // 收集纹理索引
                    mAreaLightTextureIndices[index] = light.TextureIndex;
                    
                    // 处理阴影相关数据
                    if (!light.m_RenderShadow)
                    {
                        // 不渲染阴影时，设置默认值
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
                        // 渲染阴影时，收集阴影相关参数
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
                
                // 步骤5: 将收集的区域光数据设置到全局Shader变量
                // 设置区域光数量
                cmd.SetGlobalInteger(AreaLightCountID, index);
                // 设置阴影渲染标志数组
                cmd.SetGlobalFloatArray(AreaLightRenderShadowID, mAreaLightRenderShadow);
                // 设置纹理索引数组
                cmd.SetGlobalFloatArray(AreaLightTextureIndicesID, mAreaLightTextureIndices);
                // 设置颜色数组
                cmd.SetGlobalVectorArray(AreaLightColorsID, mAreaLightColors);
                // 设置顶点位置数组
                cmd.SetGlobalMatrixArray(AreaLightVerticesID, mAreaLightVertices);
                // 设置阴影参数数组
                cmd.SetGlobalVectorArray(AreaLightShadowParamsID, mAreaLightShadowParams);
                // 设置阴影裁剪面数组
                cmd.SetGlobalFloatArray(AreaLightShadowNearClipID, mAreaLightShadowNearClips);
                cmd.SetGlobalFloatArray(AreaLightShadowFarClipID, mAreaLightShadowFarClips);
                // 设置阴影投影矩阵数组
                cmd.SetGlobalMatrixArray(AreaLightShadowProjMatrixID, mAreaLightShadowProjMatrices);
                
                // 设置阴影贴图纹理
                mPassMaterial.SetTexture(AreaLightShadowMapID, mAreaLightShadowMaps[0]);
                mPassMaterial.SetTexture(AreaLightShadowMapDummyID, mAreaLightShadowMapDummies[0]);
                
                // 步骤6: 执行Blit渲染
                // 第一次Blit: 从源纹理渲染到临时纹理，应用区域光计算
                cmd.Blit(mSource, mTemporaryColorTexture, mPassMaterial, 0);
                // 第二次Blit: 从临时纹理渲染回源纹理
                cmd.Blit(mTemporaryColorTexture, mSource);
            }
            
            // 步骤7: 执行命令缓冲区中的所有命令
            context.ExecuteCommandBuffer(cmd);
            // 步骤8: 将命令缓冲区返回到池中
            CommandBufferPool.Release(cmd);
        }

        /// <summary>
        /// 释放资源
        /// 销毁渲染材质
        /// </summary>
        public void Dispose()
        {
            CoreUtils.Destroy(mPassMaterial);
            mPassMaterial = null;
        }

        /// <summary>
        /// 准备LTC数据
        /// 步骤1: 加载漫反射变换逆矩阵LUT
        /// 步骤2: 加载高光变换逆矩阵LUT
        /// 步骤3: 加载菲涅尔LUT
        /// 步骤4: 将LUT纹理传递给材质
        /// </summary>
        private void PrepareLTCData()
        {
            // 步骤1: 加载漫反射LTC变换逆矩阵查找表
            mTransformInvDiffuseTexture = LTCAreaLightLUT.LoadLUT(LTCAreaLightLUT.LUTType.TransformInvDisneyDiffuse);
            // 步骤2: 加载高光LTC变换逆矩阵查找表
            mTransformInvSpecularTexture = LTCAreaLightLUT.LoadLUT(LTCAreaLightLUT.LUTType.TransformInvDisneyGGX);
            // 步骤3: 加载菲涅尔查找表
            mFresnelTexture = LTCAreaLightLUT.LoadLUT(LTCAreaLightLUT.LUTType.AmpDiffAmpSpecFresnel);
            
            // 步骤4: 将查找表纹理传递给渲染材质
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
        /// 渲染通道名称（用于Frame Debugger显示）
        /// </summary>
        private const string kPassName = "LTC Area Light";
        
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