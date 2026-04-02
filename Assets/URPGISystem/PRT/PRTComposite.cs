using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision.PRT
{
    /// <summary>
    /// PRT合成渲染Feature
    /// 负责将预计算的间接光照合成到最终渲染结果中
    /// 
    /// 渲染流程：
    /// 1. Create() - 创建渲染通道实例
    /// 2. AddRenderPasses() - 将渲染通道添加到渲染队列
    /// 3. OnCameraSetup() - 设置相机相关参数和临时纹理
    /// 4. Execute() - 执行间接光照合成
    /// 5. OnCameraCleanup() - 清理临时纹理
    /// </summary>
    public class PRTComposite : ScriptableRendererFeature
    {
        /// <summary>
        /// Feature名称（用于Frame Debugger显示）
        /// </summary>
        private const string kFeatureName = "PRT Composite Feature";
        
        /// <summary>
        /// 渲染通道名称（用于Frame Debugger显示）
        /// </summary>
        private const string kPassName = "PRT Composite";
        
        /// <summary>
        /// PRT合成渲染通道
        /// 负责执行间接光照合成逻辑
        /// </summary>
        class CustomRenderPass : ScriptableRenderPass
        {
            /// <summary>
            /// 合成材质
            /// </summary>
            public Material blitMaterial;
            
            /// <summary>
            /// 临时渲染纹理句柄
            /// </summary>
            public RenderTargetHandle tempRTHandle;
            
            /// <summary>
            /// 源渲染目标
            /// </summary>
            public RenderTargetIdentifier blitSrc;
            
            /// <summary>
            /// 性能分析采样器（用于Frame Debugger显示）
            /// </summary>
            private ProfilingSampler mProfilingSampler;
            
            /// <summary>
            /// 构造函数
            /// 步骤1: 创建ProfilingSampler用于Frame Debugger显示
            /// </summary>
            public CustomRenderPass(string passName)
            {
                mProfilingSampler = new ProfilingSampler(passName);
            }
            
            /// <summary>
            /// 相机设置回调
            /// 步骤1: 获取渲染纹理描述符
            /// 步骤2: 创建临时渲染纹理
            /// 步骤3: 获取相机颜色目标作为源
            /// </summary>
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor rtDesc = renderingData.cameraData.cameraTargetDescriptor;
                cmd.GetTemporaryRT(tempRTHandle.id, rtDesc);

                blitSrc = renderingData.cameraData.renderer.cameraColorTarget;
            }

            /// <summary>
            /// 执行渲染
            /// 步骤1: 获取命令缓冲区（使用kPassName命名，用于Frame Debugger显示）
            /// 步骤2: 开启性能分析采样范围（用于Frame Debugger显示）
            /// 步骤3: 查找场景中的ProbeVolume
            /// 步骤4: 如果存在ProbeVolume，执行间接光照合成
            /// 步骤5: 结束性能分析采样范围
            /// 步骤6: 执行命令缓冲区并释放
            /// </summary>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(kPassName);
                
                using (new ProfilingScope(cmd, mProfilingSampler))
                {
                    RenderTargetIdentifier tempRT = tempRTHandle.Identifier();

                    ProbeVolume[] volumes = GameObject.FindObjectsOfType(typeof(ProbeVolume)) as ProbeVolume[];
                    ProbeVolume volume = volumes.Length == 0 ? null : volumes[0];
                    
                    if (volume != null)
                    {
                        cmd.Blit(blitSrc, tempRT, blitMaterial);
                        cmd.Blit(tempRT, blitSrc);
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            /// <summary>
            /// 相机清理回调
            /// 步骤1: 释放临时渲染纹理
            /// </summary>
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(tempRTHandle.id);
            }
        }

        /// <summary>
        /// 合成材质（使用Composite.shader）
        /// </summary>
        public Material compositeMaterial;
        
        /// <summary>
        /// 渲染通道实例
        /// </summary>
        CustomRenderPass m_ScriptablePass;

        /// <summary>
        /// 创建渲染通道
        /// 步骤1: 创建渲染通道实例
        /// 步骤2: 设置渲染通道执行时机（在后处理之前）
        /// 步骤3: 设置合成材质
        /// </summary>
        public override void Create()
        {
            m_ScriptablePass = new CustomRenderPass(kPassName);

            m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            m_ScriptablePass.blitMaterial = compositeMaterial;
        }

        /// <summary>
        /// 向渲染器添加渲染通道
        /// 步骤1: 检查材质是否存在
        /// 步骤2: 将渲染通道加入渲染队列
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (compositeMaterial == null)
            {
                Debug.LogWarning("PRT Composite: Missing composite material");
                return;
            }
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}
