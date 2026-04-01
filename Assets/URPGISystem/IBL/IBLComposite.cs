using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision.IBL
{
    /// <summary>
    /// IBL合成渲染特性
    /// 将基于图像的光照(Image-Based Lighting)合成到场景中
    /// 作为URP渲染管线的一个Render Feature，在渲染过程中注入自定义Pass
    /// </summary>
    public class IBLComposite : ScriptableRendererFeature
    {
        private const string PROFILER_TAG = "IBL Composite";

        /// <summary>
        /// 自定义渲染Pass
        /// 负责在渲染过程中执行IBL合成操作
        /// </summary>
        class CustomRenderPass : ScriptableRenderPass
        {
            public Material blitMaterial;
            public RenderTargetHandle tempRTHandle;
            public RenderTargetIdentifier blitSrc;
            private bool isRendererDeferred = false;
            private string profilerTag;

            public CustomRenderPass(string tag)
            {
                profilerTag = tag;
            }

            /// <summary>
            /// 初始化Pass配置
            /// </summary>
            /// <param name="deferred">是否使用延迟渲染路径</param>
            public void Setup(bool deferred)
            {
                isRendererDeferred = deferred;
            }

            /// <summary>
            /// 相机设置回调
            /// 在渲染开始前创建临时渲染目标并配置输入
            /// </summary>
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor rtDesc = renderingData.cameraData.cameraTargetDescriptor;
                cmd.GetTemporaryRT(tempRTHandle.id, rtDesc);

                blitSrc = renderingData.cameraData.renderer.cameraColorTarget;
                
                if (isRendererDeferred)
                {
                    ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color);
                }
                else
                {
                    ConfigureInput(ScriptableRenderPassInput.Depth);
                }
            }

            /// <summary>
            /// 执行渲染Pass
            /// 使用双缓冲Blit方式将IBL效果应用到场景
            /// </summary>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(profilerTag);
                RenderTargetIdentifier tempRT = tempRTHandle.Identifier();

                if (blitMaterial != null)
                {
                    cmd.Blit(blitSrc, tempRT, blitMaterial);
                    cmd.Blit(tempRT, blitSrc);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            /// <summary>
            /// 清理回调
            /// 释放临时渲染目标
            /// </summary>
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(tempRTHandle.id);
            }
        }

        public Material compositeMaterial;
        CustomRenderPass m_ScriptablePass;

        /// <summary>
        /// 创建渲染特性
        /// 初始化自定义Pass并设置渲染事件时机
        /// </summary>
        public override void Create()
        {
            m_ScriptablePass = new CustomRenderPass(PROFILER_TAG);

            m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            m_ScriptablePass.blitMaterial = compositeMaterial;
        }

        /// <summary>
        /// 添加渲染Pass到渲染队列
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            bool isDeferred = renderingData.cameraData.renderer is UniversalRenderer;
            m_ScriptablePass.Setup(isDeferred);
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}
