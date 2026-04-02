using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NewVision.IBL
{
    /// <summary>
    /// IBL合成渲染特性
    /// 将基于图像的光照(Image-Based Lighting)合成到场景中
    /// 作为URP渲染管线的一个Render Feature，在渲染过程中注入自定义Pass
    /// 
    /// 功能：
    /// - 在延迟渲染路径下，从GBuffer获取场景几何和材质信息
    /// - 使用预计算的IBL贴图计算间接光照
    /// - 将IBL结果合成到场景颜色缓冲
    /// 
    /// 使用方式：
    /// 1. 在URP Renderer Data中添加此Render Feature
    /// 2. 设置compositeMaterial为使用IBLComposite.shader的材质
    /// 3. 在材质中配置IrradianceMap、PrefilterMap和BRDFLut
    /// </summary>
    public class IBLComposite : ScriptableRendererFeature
    {
        /// <summary>
        /// Frame Debugger中显示的标签名称
        /// 用于在Frame Debugger中标识此渲染Pass
        /// </summary>
        private const string PROFILER_TAG = "IBL Composite";

        /// <summary>
        /// 自定义渲染Pass
        /// 负责在渲染过程中执行IBL合成操作
        /// 
        /// 工作流程：
        /// 1. OnCameraSetup - 创建临时渲染目标，配置GBuffer输入
        /// 2. Execute - 执行双缓冲Blit，应用IBL材质
        /// 3. OnCameraCleanup - 释放临时渲染目标
        /// </summary>
        class CustomRenderPass : ScriptableRenderPass
        {
            /// <summary>用于Blit的IBL材质</summary>
            public Material blitMaterial;
            
            /// <summary>临时渲染目标句柄</summary>
            public RenderTargetHandle tempRTHandle;
            
            /// <summary>源颜色缓冲（相机渲染目标）</summary>
            public RenderTargetIdentifier blitSrc;
            
            /// <summary>是否使用延迟渲染路径</summary>
            private bool isRendererDeferred = false;
            
            /// <summary>Profiler标签，用于Frame Debugger</summary>
            private string profilerTag;

            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="tag">Profiler标签名称</param>
            public CustomRenderPass(string tag)
            {
                profilerTag = tag;
            }

            /// <summary>
            /// 初始化Pass配置
            /// 根据渲染路径设置不同的输入需求
            /// </summary>
            /// <param name="deferred">是否使用延迟渲染路径</param>
            public void Setup(bool deferred)
            {
                isRendererDeferred = deferred;
            }

            /// <summary>
            /// 相机设置回调
            /// 在渲染开始前创建临时渲染目标并配置输入
            /// 
            /// 处理流程：
            /// 1. 根据相机描述符创建临时RT
            /// 2. 获取相机颜色目标作为Blit源
            /// 3. 配置GBuffer输入（延迟渲染需要Depth+Normal+Color，前向渲染只需Depth）
            /// </summary>
            /// <param name="cmd">命令缓冲区</param>
            /// <param name="renderingData">渲染数据</param>
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // 获取相机目标描述符，用于创建兼容的临时RT
                RenderTextureDescriptor rtDesc = renderingData.cameraData.cameraTargetDescriptor;
                cmd.GetTemporaryRT(tempRTHandle.id, rtDesc);

                // 获取相机颜色目标作为Blit源
                blitSrc = renderingData.cameraData.renderer.cameraColorTarget;
                
                // 根据渲染路径配置输入
                if (isRendererDeferred)
                {
                    // 延迟渲染：需要深度、法线和颜色缓冲
                    ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color);
                }
                else
                {
                    // 前向渲染：只需要深度缓冲
                    ConfigureInput(ScriptableRenderPassInput.Depth);
                }
            }

            /// <summary>
            /// 执行渲染Pass
            /// 使用双缓冲Blit方式将IBL效果应用到场景
            /// 
            /// 双缓冲流程：
            /// 1. Blit从源RT到临时RT（应用IBL材质）
            /// 2. Blit从临时RT回源RT
            /// 
            /// 这种方式避免了直接读写同一纹理的问题
            /// </summary>
            /// <param name="context">渲染上下文</param>
            /// <param name="renderingData">渲染数据</param>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // 从对象池获取CommandBuffer，使用profilerTag标记
                CommandBuffer cmd = CommandBufferPool.Get(profilerTag);
                RenderTargetIdentifier tempRT = tempRTHandle.Identifier();

                if (blitMaterial != null)
                {
                    // Step 1: 从源RT Blit到临时RT，应用IBL材质
                    cmd.Blit(blitSrc, tempRT, blitMaterial);
                    
                    // Step 2: 从临时RT Blit回源RT
                    cmd.Blit(tempRT, blitSrc);
                }

                // 执行命令缓冲区
                context.ExecuteCommandBuffer(cmd);
                
                // 清空命令缓冲区并归还到对象池
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            /// <summary>
            /// 清理回调
            /// 释放临时渲染目标
            /// </summary>
            /// <param name="cmd">命令缓冲区</param>
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                // 释放临时RT
                cmd.ReleaseTemporaryRT(tempRTHandle.id);
            }
        }

        /// <summary>
        /// IBL合成材质
        /// 需要使用IBLComposite.shader，并配置以下属性：
        /// - _IrradianceMap: 漫反射辐照度贴图
        /// - _PrefilterMap: 镜面反射预过滤贴图
        /// - _BRDFLut: BRDF查找表
        /// </summary>
        public Material compositeMaterial;
        
        /// <summary>自定义渲染Pass实例</summary>
        CustomRenderPass m_ScriptablePass;

        /// <summary>
        /// 创建渲染特性
        /// 初始化自定义Pass并设置渲染事件时机
        /// 
        /// 此方法在以下情况被调用：
        /// - Render Feature首次创建时
        /// - Inspector中属性修改时
        /// </summary>
        public override void Create()
        {
            // 创建自定义Pass，传入Profiler标签
            m_ScriptablePass = new CustomRenderPass(PROFILER_TAG);

            // 设置渲染事件：在后处理之前执行
            // 这样IBL效果会受到后处理的影响
            m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            
            // 设置Blit材质
            m_ScriptablePass.blitMaterial = compositeMaterial;
        }

        /// <summary>
        /// 添加渲染Pass到渲染队列
        /// 每帧每相机调用一次
        /// 
        /// 此方法负责：
        /// 1. 检测当前渲染路径
        /// 2. 配置Pass参数
        /// 3. 将Pass加入渲染队列
        /// </summary>
        /// <param name="renderer">渲染器</param>
        /// <param name="renderingData">渲染数据</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // 检测是否为UniversalRenderer（支持延迟渲染）
            bool isDeferred = renderingData.cameraData.renderer is UniversalRenderer;
            
            // 配置Pass
            m_ScriptablePass.Setup(isDeferred);
            
            // 将Pass加入渲染队列
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}
